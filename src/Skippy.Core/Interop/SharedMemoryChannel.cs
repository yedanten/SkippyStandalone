using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using Skippy.Core.Game;
using Skippy.Core.Interop;
using Skippy.Core.Skips;

namespace Skippy.Core.Interop;

/// <summary>原生组件回传的一条日志。</summary>
public sealed record NativeLogRecord(
    DateTimeOffset Timestamp,
    SharedStateLayout.LogLevel Level,
    SharedStateLayout.LogHookId Hook,
    ushort TerritoryId,
    byte IntendedUse,
    string Message);

/// <summary>
/// 宿主与原生组件之间的共享内存通道。宿主负责创建映射与事件，原生组件打开。
/// </summary>
public sealed unsafe class SharedMemoryChannel : IDisposable
{
    private readonly object gate = new();
    private MemoryMappedFile? mapping;
    private MemoryMappedViewAccessor? accessor;
    private byte* pointer;
    private bool pointerAcquired;
    private uint lastLogSequence;
    private bool disposed;

    public SharedState* State => (SharedState*)this.pointer;

    public bool IsValid { get; private set; }

    /// <summary>原生组件是否已写入有效状态（完成签名扫描并开始工作）。</summary>
    public bool NativeReady => this.IsValid && this.State->GameReady != 0;

    /// <summary>原生组件最后一次报告的错误码。</summary>
    public SharedStateLayout.NativeError LastError =>
        this.IsValid ? (SharedStateLayout.NativeError)this.State->LastError : SharedStateLayout.NativeError.None;

    public bool Open()
    {
        lock (this.gate)
        {
            if (this.IsValid)
                return true;

            try
            {
                var size = Marshal.SizeOf<SharedState>();

                try
                {
                    this.mapping = MemoryMappedFile.CreateNew(
                        SharedStateLayout.MappingName,
                        size,
                        MemoryMappedFileAccess.ReadWrite);
                }
                catch (IOException)
                {
                    // 映射已存在（通常是原生组件仍在上一次运行的进程里，或上次未正常释放）。
                    this.mapping = MemoryMappedFile.OpenExisting(
                        SharedStateLayout.MappingName,
                        MemoryMappedFileRights.ReadWrite);
                }

                this.accessor = this.mapping.CreateViewAccessor(0, size, MemoryMappedFileAccess.ReadWrite);
                this.accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref this.pointer);
                this.pointerAcquired = true;

                var state = (SharedState*)this.pointer;

                if (state->Magic != SharedStateLayout.Magic
                    || state->Version != SharedStateLayout.Version
                    || state->Size != (uint)size)
                {
                    // 首次创建（或旧版本残留）：整体清零后写入头部。
                    NativeMemory.Clear(this.pointer, (nuint)size);
                    state->Magic = SharedStateLayout.Magic;
                    state->Version = SharedStateLayout.Version;
                    state->Size = (uint)size;
                }

                state->Shutdown = 0;
                state->HeartbeatMs = NowMs();
                this.lastLogSequence = state->LogWriteSequence;
                this.IsValid = true;
                return true;
            }
            catch
            {
                this.Release();
                this.IsValid = false;
                return false;
            }
        }
    }

    public void WriteHeartbeat()
    {
        if (!this.IsValid)
            return;

        this.State->HeartbeatMs = NowMs();
    }

    /// <summary>写入宿主计算出的决策表。</summary>
    public void WriteDecision(SkipDecision decision, bool enabled, bool researchExtraLogs)
    {
        if (!this.IsValid)
            return;

        lock (this.gate)
        {
            var s = this.State;
            s->DecisionSequence++;

            s->Enabled = enabled ? 1u : 0u;
            s->MemoryPatchActive = decision.MemoryPatchActive ? 1u : 0u;

            WritePair(ref s->ContentDirectorEnabled, ref s->ContentDirectorSkip, decision.ContentDirector);
            WritePair(ref s->MassivePCEnabled, ref s->MassivePCSkip, decision.MassivePC);
            WritePair(ref s->CustomTalkEnabled, ref s->CustomTalkSkip, decision.CustomTalk);
            WritePair(ref s->NormalCutscenesEnabled, ref s->NormalCutscenesSkip, decision.NormalCutscenes);
            WritePair(ref s->InnEnabled, ref s->InnSkip, decision.Inn);
            WritePair(ref s->FeedBuddyEnabled, ref s->FeedBuddySkip, decision.FeedBuddy);
            WritePair(ref s->CutsceneSeenEnabled, ref s->CutsceneSeenSkip, decision.CutsceneSeen);
            WritePair(ref s->GrandCompanyRankUpEnabled, ref s->GrandCompanyRankUpSkip, decision.GrandCompanyRankUp);
            WritePair(ref s->HairMakeEnabled, ref s->HairMakeSkip, decision.HairMake);

            s->ResearchExtraLogs = researchExtraLogs ? 1u : 0u;
            s->DecisionSequence++;
        }
    }

    private static void WritePair(ref uint enabledField, ref uint skipField, HookState state)
    {
        enabledField = state.Enabled ? 1u : 0u;
        skipField = state.Skip ? 1u : 0u;
    }

    public GameSnapshot ReadSnapshot()
    {
        if (!this.IsValid)
            return GameSnapshot.Empty;

        var s = this.State;
        var before = s->GameSequence;
        if ((before & 1) != 0)
            return GameSnapshot.Empty;

        var members = new ulong[8];
        for (var i = 0; i < members.Length; i++)
            members[i] = s->PartyMembers[i];

        var snapshot = new GameSnapshot(
            s->TerritoryId,
            (TerritoryIntendedUse)s->IntendedUse,
            s->ContentId,
            (int)s->PartySize,
            members,
            s->IsInWorkshop != 0,
            s->IsLoggedIn != 0,
            s->GameReady != 0);

        if (s->GameSequence != before)
            return GameSnapshot.Empty;

        return snapshot;
    }

    /// <summary>读取原生组件回传的签名/挂钩状态，供诊断界面使用。</summary>
    public (SharedStateLayout.SignatureMask Signatures, SharedStateLayout.HookMask Hooks, bool PatchApplied) ReadNativeStatus()
    {
        if (!this.IsValid)
            return (SharedStateLayout.SignatureMask.None, SharedStateLayout.HookMask.None, false);

        var s = this.State;
        return ((SharedStateLayout.SignatureMask)s->SignatureMask,
                (SharedStateLayout.HookMask)s->HookMask,
                s->PatchApplied != 0);
    }

    /// <summary>取出自上次调用以来新增的日志。环形缓冲最多保留 64 条。</summary>
    public IReadOnlyList<NativeLogRecord> DrainLogs()
    {
        if (!this.IsValid)
            return [];

        lock (this.gate)
        {
            var s = this.State;
            var sequence = s->LogWriteSequence;
            if (sequence == this.lastLogSequence)
                return [];

            var result = new List<NativeLogRecord>((int)Math.Min(sequence - this.lastLogSequence, SharedStateLayout.MaxLogEntries));

            var start = Math.Max(this.lastLogSequence + 1, sequence >= SharedStateLayout.MaxLogEntries ? sequence - SharedStateLayout.MaxLogEntries + 1 : 1);
            for (var seq = start; seq <= sequence; seq++)
            {
                var index = (int)((seq - 1) % SharedStateLayout.MaxLogEntries);
                var entry = s->GetLogEntry(index);

                result.Add(new NativeLogRecord(
                    DateTimeOffset.FromUnixTimeMilliseconds((long)entry->TimestampMs),
                    (SharedStateLayout.LogLevel)entry->Level,
                    (SharedStateLayout.LogHookId)entry->HookId,
                    (ushort)entry->TerritoryId,
                    (byte)entry->IntendedUse,
                    entry->GetMessage()));
            }

            this.lastLogSequence = sequence;
            return result;
        }
    }

    public void RequestShutdown()
    {
        if (this.IsValid)
            this.State->Shutdown = 1;
    }

    /// <summary>
    /// 游戏进程退出后清除原生组件留下的状态，避免界面继续显示旧的“已连接”。
    /// </summary>
    public void ResetNativeState()
    {
        if (!this.IsValid)
            return;

        lock (this.gate)
        {
            var s = this.State;
            s->GameReady = 0;
            s->NativeVersion = 0;
            s->TerritoryId = 0;
            s->IntendedUse = 0;
            s->ContentId = 0;
            s->PartySize = 0;
            s->IsInWorkshop = 0;
            s->IsLoggedIn = 0;
            s->SignatureMask = 0;
            s->HookMask = 0;
            s->PatchApplied = 0;
            s->LastError = 0;
        }
    }

    private static ulong NowMs() => (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;
        this.Release();
        this.IsValid = false;
    }

    private void Release()
    {
        lock (this.gate)
        {
            if (this.pointerAcquired && this.accessor is not null)
            {
                this.accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                this.pointerAcquired = false;
            }

            this.pointer = null;
            this.accessor?.Dispose();
            this.accessor = null;
            this.mapping?.Dispose();
            this.mapping = null;
        }
    }
}

