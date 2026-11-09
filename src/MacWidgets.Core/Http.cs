using System.Net;
using System.Text;

namespace MacWidgets.Core;

public static class Http
{
    private static readonly Lazy<HttpClient> _client = new(() =>
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            ConnectTimeout = TimeSpan.FromSeconds(6),
        };
        var c = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36 MacWidgets/1.0.5");
        return c;
    });

    public static HttpClient Client => _client.Value;

    private static readonly Lazy<Encoding> _gbk = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding("GB18030");
    });

    public static Encoding Gbk => _gbk.Value;

    public static async Task<string> GetStringAsync(string url, Encoding? enc = null, string? referer = null, string? userAgent = null, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (referer != null) req.Headers.Referrer = new Uri(referer);
        if (userAgent != null)
        {
            req.Headers.UserAgent.Clear();
            req.Headers.UserAgent.ParseAdd(userAgent);
        }
        using var resp = await Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        return (enc ?? Encoding.UTF8).GetString(bytes);
    }
}
