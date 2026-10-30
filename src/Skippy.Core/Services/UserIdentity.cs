using System.Security.Cryptography;
using System.Text;

namespace Skippy.Core.Services;

/// <summary>
/// 匿名标识算法，与哨月插件完全一致：
/// UserID = SHA-256(ContentId 的 8 字节小端) 的前 32 个十六进制字符；
/// PartyID = SHA-256(队伍成员 ContentId 排序后拼接) 的前 32 个十六进制字符。
/// </summary>
public static class UserIdentity
{
    public static readonly string NoUserId = Hash(BitConverter.GetBytes(0UL));

    public static string GetUserId(ulong contentId) => Hash(BitConverter.GetBytes(contentId));

    public static string? BuildPartyId(IEnumerable<ulong> contentIds, ulong selfContentId)
    {
        var ids = contentIds.Where(id => id != 0).Distinct().ToList();

        if (selfContentId != 0 && !ids.Contains(selfContentId))
            ids.Add(selfContentId);

        if (ids.Count == 0)
            return null;

        ids.Sort();

        var buffer = new byte[ids.Count * 8];
        for (var i = 0; i < ids.Count; i++)
            BitConverter.TryWriteBytes(buffer.AsSpan(i * 8, 8), ids[i]);

        return Hash(buffer);
    }

    private static string Hash(byte[] buffer)
    {
        var hash = SHA256.HashData(buffer);
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }
}
