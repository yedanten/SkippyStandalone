using System.Runtime.InteropServices;
using Skippy.Core.Interop;

namespace Skippy.Core.Tests;

public class SharedStateLayoutTests
{
    [Fact]
    public void SharedState_HasExpectedSize()
    {
        // 该数值同时由 C++ 侧 static_assert 校验；两侧任何一边改动字段都必须同步。
        Assert.Equal(17152, Marshal.SizeOf<SharedState>());
    }

    [Fact]
    public void SharedState_KeyFieldsHaveExpectedOffsets()
    {
        Assert.Equal(0, (int)Marshal.OffsetOf<SharedState>(nameof(SharedState.Magic)));
        Assert.Equal(12, (int)Marshal.OffsetOf<SharedState>(nameof(SharedState.GameSequence)));
        Assert.Equal(16, (int)Marshal.OffsetOf<SharedState>(nameof(SharedState.HeartbeatMs)));
        Assert.Equal(48, (int)Marshal.OffsetOf<SharedState>(nameof(SharedState.ContentId)));
        Assert.Equal(56, (int)Marshal.OffsetOf<SharedState>(nameof(SharedState.PartyMembers)));
        Assert.Equal(120, (int)Marshal.OffsetOf<SharedState>(nameof(SharedState.PartySize)));
        Assert.Equal(164, (int)Marshal.OffsetOf<SharedState>(nameof(SharedState.Enabled)));
        Assert.Equal(256, (int)Marshal.OffsetOf<SharedState>(nameof(SharedState.Logs)));
        Assert.Equal(256 + SharedStateLayout.LogBufferSize, Marshal.SizeOf<SharedState>());
    }

    [Fact]
    public unsafe void LogEntry_HasExpectedLayout()
    {
        Assert.Equal(SharedStateLayout.LogEntrySize, Marshal.SizeOf<SharedLogEntry>());

        var entry = default(SharedLogEntry);
        var pointer = &entry;
        var baseAddress = (byte*)pointer;

        Assert.Equal(0, (int)((byte*)&pointer->TimestampMs - baseAddress));
        Assert.Equal(8, (int)((byte*)&pointer->Level - baseAddress));
    }

    [Fact]
    public void LogEntry_MessageRoundTripsUtf8()
    {
        var entry = new SharedLogEntry { Level = 3, HookId = 1, TerritoryId = 1044 };
        entry.SetMessage("天幕魔导城 过场已跳过");

        Assert.Equal("天幕魔导城 过场已跳过", entry.GetMessage());
    }
}
