using System.Runtime.InteropServices;

namespace Skippy.Core.Interop;

/// <summary>
/// 与原生组件（Skippy.Native.dll）共享的内存布局。
/// 修改本文件时必须同步更新 <c>src/Skippy.Native/ipc/SharedState.h</c>，两侧都有尺寸断言。
/// </summary>
public static class SharedStateLayout
{
    /// <summary>"SKPY" 的小端表示。</summary>
    public const uint Magic = 0x534B5059;

    public const uint Version = 1;

    public const int MaxLogEntries = 64;

    public const int LogMessageBytes = 240;

    public const int LogEntrySize = 264;

    public const int LogBufferSize = MaxLogEntries * LogEntrySize;

    public const string MappingName = @"Local\SkippyStandalone.SharedState.v1";

    public const string EventName = @"Local\SkippyStandalone.SharedState.Updated.v1";

    // 状态位。
    [Flags]
    public enum SignatureMask : uint
    {
        None = 0,
        CutscenePatch1 = 1 << 0,
        CutscenePatch2 = 1 << 1,
        ContentDirector = 1 << 2,
        MassivePC = 1 << 3,
        CustomTalk = 1 << 4,
        NormalCutscenes = 1 << 5,
        Inn = 1 << 6,
        FeedBuddy = 1 << 7,
        CutsceneSeen = 1 << 8,
        GrandCompanyRankUp = 1 << 9,
        HairMake = 1 << 10,
        GameMain = 1 << 11,
        PlayerState = 1 << 12,
        GroupManager = 1 << 13,
        HousingManager = 1 << 14,
    }

    [Flags]
    public enum HookMask : uint
    {
        None = 0,
        ContentDirector = 1 << 0,
        MassivePC = 1 << 1,
        CustomTalk = 1 << 2,
        NormalCutscenes = 1 << 3,
        Inn = 1 << 4,
        FeedBuddy = 1 << 5,
        CutsceneSeen = 1 << 6,
        GrandCompanyRankUp = 1 << 7,
        HairMake = 1 << 8,
    }

    public enum LogLevel : uint
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        HookFired = 3,
    }

    public enum LogHookId : uint
    {
        None = 0,
        ContentDirector = 1,
        MassivePC = 2,
        CustomTalk = 3,
        NormalCutscenes = 4,
        Inn = 5,
        FeedBuddy = 6,
        CutsceneSeen = 7,
        GrandCompanyRankUp = 8,
        HairMake = 9,
    }

    public enum NativeError : int
    {
        None = 0,
        SharedMemoryOpenFailed = 1,
        SignatureScanFailed = 2,
        HookInstallFailed = 3,
        UnsupportedGameVersion = 4,
        Internal = 5,
    }
}

/// <summary>原生组件写入的日志条目（264 字节）。</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public unsafe struct SharedLogEntry
{
    public ulong TimestampMs;
    public uint Level;
    public uint HookId;
    public uint TerritoryId;
    public uint IntendedUse;
    public fixed byte Message[SharedStateLayout.LogMessageBytes];

    public string GetMessage()
    {
        fixed (byte* p = this.Message)
            return ReadUtf8(p, SharedStateLayout.LogMessageBytes);

        static string ReadUtf8(byte* p, int max)
        {
            var length = 0;
            while (length < max && p[length] != 0)
                length++;

            return length == 0 ? string.Empty : System.Text.Encoding.UTF8.GetString(p, length);
        }
    }

    public void SetMessage(string text)
    {
        fixed (byte* p = this.Message)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            var count = Math.Min(bytes.Length, SharedStateLayout.LogMessageBytes - 1);
            for (var i = 0; i < count; i++)
                p[i] = bytes[i];
            p[count] = 0;
        }
    }
}

/// <summary>
/// 完整共享状态。字段顺序/大小与原生侧一一对应，总大小 17088 字节。
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public unsafe struct SharedState
{
    // ── 头部 ───────────────────────────────────────────────────────────────
    public uint Magic;
    public uint Version;
    public uint Size;
    public uint GameSequence;
    public ulong HeartbeatMs;
    public uint Shutdown;
    public uint DecisionSequence;

    // ── 原生 → 宿主：游戏状态 ──────────────────────────────────────────────
    public uint NativeVersion;
    public uint GameReady;
    public uint TerritoryId;
    public uint IntendedUse;
    public ulong ContentId;
    public fixed ulong PartyMembers[8];
    public uint PartySize;
    public uint IsInWorkshop;
    public uint IsLoggedIn;
    public uint SignatureMask;
    public uint HookMask;
    public uint PatchApplied;
    public int LastError;
    public uint LogWriteSequence;
    public uint LastHookId;
    public uint LastHookTerritory;
    public uint LastHookIntendedUse;

    // ── 宿主 → 原生：决策 ──────────────────────────────────────────────────
    public uint Enabled;
    public uint MemoryPatchActive;
    public uint ContentDirectorEnabled;
    public uint ContentDirectorSkip;
    public uint MassivePCEnabled;
    public uint MassivePCSkip;
    public uint CustomTalkEnabled;
    public uint CustomTalkSkip;
    public uint NormalCutscenesEnabled;
    public uint NormalCutscenesSkip;
    public uint InnEnabled;
    public uint InnSkip;
    public uint FeedBuddyEnabled;
    public uint FeedBuddySkip;
    public uint CutsceneSeenEnabled;
    public uint CutsceneSeenSkip;
    public uint GrandCompanyRankUpEnabled;
    public uint GrandCompanyRankUpSkip;
    public uint HairMakeEnabled;
    public uint HairMakeSkip;
    public uint ResearchExtraLogs;
    public uint Reserved2;
    public uint Reserved3;

    public fixed byte Logs[SharedStateLayout.LogBufferSize];

    public SharedLogEntry* GetLogEntry(int index)
    {
        fixed (byte* p = this.Logs)
            return (SharedLogEntry*)(p + (index * SharedStateLayout.LogEntrySize));
    }
}
