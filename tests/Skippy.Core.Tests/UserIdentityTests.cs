using Skippy.Core.Services;

namespace Skippy.Core.Tests;

public class UserIdentityTests
{
    [Fact]
    public void NeutralContentIdProducesKnownPlaceholder()
    {
        Assert.Equal(UserIdentity.NoUserId, UserIdentity.GetUserId(0));
        Assert.Equal(32, UserIdentity.NoUserId.Length);
        Assert.Equal(UserIdentity.NoUserId, UserIdentity.NoUserId.ToLowerInvariant());
    }

    [Fact]
    public void UserIdIsStableAndShort()
    {
        var first = UserIdentity.GetUserId(0x1122334455667788);
        var second = UserIdentity.GetUserId(0x1122334455667788);
        var other = UserIdentity.GetUserId(0x1122334455667789);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.Equal(32, first.Length);
        Assert.Matches("^[0-9a-f]{32}$", first);
    }

    [Fact]
    public void PartyIdIgnoresOrderAndDuplicates()
    {
        var a = UserIdentity.BuildPartyId([2UL, 1UL, 3UL], 1UL);
        var b = UserIdentity.BuildPartyId([3UL, 2UL, 1UL], 1UL);
        var c = UserIdentity.BuildPartyId([1UL, 2UL, 3UL, 3UL], 1UL);

        Assert.NotNull(a);
        Assert.Equal(a, b);
        Assert.Equal(a, c);
    }

    [Fact]
    public void PartyIdRequiresAtLeastOneMember()
    {
        Assert.Null(UserIdentity.BuildPartyId([], 0));
    }

    [Fact]
    public void PartyIdChangesWithComposition()
    {
        var four = UserIdentity.BuildPartyId([1UL, 2UL, 3UL, 4UL], 1UL);
        var three = UserIdentity.BuildPartyId([1UL, 2UL, 3UL], 1UL);

        Assert.NotEqual(four, three);
    }
}
