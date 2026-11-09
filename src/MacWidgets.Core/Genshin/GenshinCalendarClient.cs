using System.Text.RegularExpressions;

namespace MacWidgets.Core.Genshin;

/// <summary>
/// 从《原神》官网 https://ys.mihoyo.com/ 解析当前版本落地页，提取「活动日历」大图 CDN 地址并下载。
/// 解析链：官网 HTML → act.mihoyo.com/ys/event/hk4e-*-landing/ → index_*.js → images/calendar-panel-large*.png
/// </summary>
public static class GenshinCalendarClient
{
    public const string OfficialSite = "https://ys.mihoyo.com/";
    private static readonly Regex LandingRx = new(
        @"https://act\.mihoyo\.com/ys/event/hk4e-[^/""'\s]+/",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IndexJsRx = new(
        @"https://act\.mihoyo\.com/ys/event/hk4e-[^/""'\s]+/index_[^""'\s]+\.js",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RelIndexJsRx = new(
        @"(?:src|href)=[""'](index_[^""']+\.js)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CalendarImgRx = new(
        @"images/calendar-panel-large\.[^""'\s]+\.png",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CalendarImgAltRx = new(
        @"images/calendar-panel\.[^""'\s]+\.png",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public sealed record Result(string ImageUrl, string LandingUrl, byte[] Bytes);

    /// <summary>发现并下载当前活动日历大图。失败抛异常。</summary>
    public static async Task<Result> FetchAsync(CancellationToken ct = default)
    {
        var home = await Http.GetStringAsync(OfficialSite, referer: OfficialSite, ct: ct).ConfigureAwait(false);
        var landingMatch = LandingRx.Match(home);
        if (!landingMatch.Success)
            throw new InvalidOperationException("官网未找到版本落地页链接");
        string landing = landingMatch.Value;
        if (!landing.EndsWith('/')) landing += "/";

        var landingHtml = await Http.GetStringAsync(landing, referer: OfficialSite, ct: ct).ConfigureAwait(false);
        string? indexJs = IndexJsRx.Match(landingHtml).Value;
        if (string.IsNullOrEmpty(indexJs))
        {
            var rel = RelIndexJsRx.Match(landingHtml);
            if (rel.Success) indexJs = landing + rel.Groups[1].Value;
        }
        if (string.IsNullOrEmpty(indexJs))
            throw new InvalidOperationException("落地页未找到主脚本");

        var js = await Http.GetStringAsync(indexJs, referer: landing, ct: ct).ConfigureAwait(false);
        var imgRel = CalendarImgRx.Match(js).Value;
        if (string.IsNullOrEmpty(imgRel))
            imgRel = CalendarImgAltRx.Match(js).Value;
        if (string.IsNullOrEmpty(imgRel))
            throw new InvalidOperationException("脚本中未找到活动日历图片");

        string imgUrl = landing + imgRel.TrimStart('/');
        using var req = new HttpRequestMessage(HttpMethod.Get, imgUrl);
        req.Headers.Referrer = new Uri(landing);
        using var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        if (bytes.Length < 1024)
            throw new InvalidOperationException("活动日历图片过小，可能无效");
        return new Result(imgUrl, landing, bytes);
    }

    /// <summary>仅从 HTML/JS 文本解析图片相对路径（供单测）。</summary>
    public static string? ParseCalendarImageRel(string jsBundle) =>
        CalendarImgRx.Match(jsBundle) is { Success: true } m ? m.Value
        : CalendarImgAltRx.Match(jsBundle) is { Success: true } m2 ? m2.Value
        : null;

    public static string? ParseLandingUrl(string homeHtml) =>
        LandingRx.Match(homeHtml) is { Success: true } m ? m.Value : null;
}
