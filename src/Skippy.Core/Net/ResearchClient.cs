using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Skippy.Core.Net;

/// <summary>
/// 原插件的网络端点。用于研究日志上报、Tester 校验、开发者模式密码与队伍互助检测。
/// 所有请求失败都被静默处理：网络不可用不应影响跳过功能。
/// </summary>
public sealed class ResearchClient : IDisposable
{
    private const string HooksUrl = "https://script.google.com/macros/s/AKfycbz2EOwTYOqxGuRfBIuYqLihhWLZFgJ4seNusgBz98nKjYXQWNX4Hg1bCCk87qfiVNB96A/exec";
    private const string MatchmakingUrl = "https://script.google.com/macros/s/AKfycbzT5KebwghkqoTo8LZt1GAg6lz8uNImu9p5slE0ZSeMl49YP9AMmHnasm_LJbm0nXxV/exec";
    private const string TestersUrl = "https://script.google.com/macros/s/AKfycbyv6HqSB9Q0y3OvojXo4my3LUH2U_ZqR89xvFKJFwQpk0DQ-7IdWUN-wJAub1nWMbvM/exec";
    private const string PasswordUrl = "https://script.google.com/macros/s/AKfycbxJ5elyO3HPmdh2d2edIrkdh3zUyfxaJg_QLtE_6ADoup7vRAlZiurDI9Q0Rjm8LyyB/exec";

    private readonly HttpClient http;

    public ResearchClient()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        this.http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
    }

    public static string Version { get; set; } = "2.2.5.0-standalone";

    /// <summary>上报一次 hook 命中（对应原插件 <c>LogToSheet</c>）。</summary>
    public async Task ReportHookAsync(
        string hook,
        ushort territory,
        string placeName,
        byte intendedUse,
        bool isCutsceneSeen,
        uint cutsceneId,
        string userId,
        bool devMode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                hook,
                territory,
                place = placeName,
                intendedUse = intendedUse.ToString(),
                time = DateTime.UtcNow.ToString("u"),
                version = Version,
                devMode,
                isCutsceneSeen,
                cutsceneId,
                userId,
            };

            var json = JsonSerializer.Serialize(payload);
            await this.PostFollowingRedirectsAsync(
                HooksUrl,
                json,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // 静默失败。
        }
    }

    /// <summary>队列弹出时登记“本队有人使用 Skippy”。</summary>
    public async Task PostMatchmakingAsync(
        string partyId,
        int partySize,
        string dutyName,
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                action = "matchmaking",
                time = DateTime.UtcNow.ToString("u"),
                userId,
                partyId,
                duty = dutyName,
                partySize,
                version = Version,
            };

            await this.PostFollowingRedirectsAsync(
                MatchmakingUrl,
                JsonSerializer.Serialize(payload),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    public async Task DeleteMatchmakingAsync(
        string partyId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                action = "deleteMatchmaking",
                userId,
                partyId,
            };

            await this.PostFollowingRedirectsAsync(
                MatchmakingUrl,
                JsonSerializer.Serialize(payload),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    /// <summary>查询是否全队都使用 Skippy。</summary>
    public async Task<bool> CheckAllPartyHasSkippyAsync(
        string partyId,
        int expectedSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{MatchmakingUrl}?action=checkParty&partyId={Uri.EscapeDataString(partyId)}&expectedSize={expectedSize}";
            var result = await this.GetFollowingRedirectsAsync(url, cancellationToken).ConfigureAwait(false);
            return result.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>查询 Tester 信息，返回 <c>(昵称, 留言)</c>。</summary>
    public async Task<(string Nickname, string Message)?> CheckTesterAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{TestersUrl}?action=checkTester&userId={Uri.EscapeDataString(userId)}";
            var result = (await this.GetFollowingRedirectsAsync(url, cancellationToken).ConfigureAwait(false)).Trim();

            if (string.IsNullOrEmpty(result) || result.Equals("false", StringComparison.OrdinalIgnoreCase))
                return null;

            var separator = result.IndexOf('|');
            if (separator < 0)
                return null;

            return (result[..separator], result[(separator + 1)..]);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>获取开发者模式密码（由作者远程控制）。</summary>
    public async Task<string> FetchPasswordAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await this.GetFollowingRedirectsAsync(
                PasswordUrl + "?action=getPassword",
                cancellationToken).ConfigureAwait(false);

            return result.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task PostFollowingRedirectsAsync(string url, string json, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            using var response = await this.http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (IsRedirect(response.StatusCode))
            {
                url = response.Headers.Location?.ToString() ?? url;
                continue;
            }

            return;
        }
    }

    private async Task<string> GetFollowingRedirectsAsync(string url, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var response = await this.http.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (IsRedirect(response.StatusCode))
            {
                url = response.Headers.Location?.ToString() ?? url;
                continue;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        return string.Empty;
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    public void Dispose() => this.http.Dispose();
}
