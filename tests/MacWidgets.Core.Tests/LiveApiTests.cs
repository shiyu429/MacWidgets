using MacWidgets.Core.Stocks;
using MacWidgets.Core.Weather;

namespace MacWidgets.Core.Tests;

/// <summary>真实网络接口冒烟测试：仅在环境变量 MACWIDGETS_LIVE=1 时执行。</summary>
public class LiveApiTests
{
    private static bool Live => Environment.GetEnvironmentVariable("MACWIDGETS_LIVE") == "1";

    [Fact]
    public async Task AllQuoteProviders()
    {
        if (!Live) return;
        var syms = Symbols.Defaults;
        Assert.Equal(3, (await QuoteClient.TencentAsync(syms)).Count);
        Assert.Equal(3, (await QuoteClient.SinaAsync(syms)).Count);
        Assert.Equal(3, (await QuoteClient.EastmoneyAsync(syms)).Count);
        var c = new QuoteClient();
        foreach (var s in syms)
        {
            var i = await c.GetIntradayAsync(s, 0);
            Assert.NotNull(i);
            Assert.True(i!.Points.Length > 10);
        }
    }

    [Fact]
    public async Task PreviousSessionIntraday()
    {
        if (!Live) return;
        foreach (var s in new[] { "usAAPL", "us.IXIC", "sh000001", "hkHSI" })
        {
            var days = await QuoteClient.TencentRecentSessionsAsync(s);
            Assert.True(days.Count >= 2, s);
            // 除当日外，至少有一个更早的完整交易日
            Assert.Contains(days.Skip(1), d => d.Points.Length > 100);
            Assert.NotNull(IntradaySessions.LatestNonEmpty(days));
        }
    }

    [Fact]
    public async Task Weather()
    {
        if (!Live) return;
        var w = await WeatherClient.ForecastAsync(27.83, 113.15);
        Assert.True(w.Daily.Count >= 2);

        // 本地库即可命中（不依赖 Open-Meteo Geocoding）
        foreach (var q in new[] { "宜春", "宜春市", "长沙", "长沙市", "株洲", "株洲市" })
        {
            var places = await WeatherClient.SearchAsync(q);
            Assert.True(places.Count > 0, q + " should hit");
        }
        var yichun = await WeatherClient.SearchAsync("宜春");
        Assert.Contains("宜春", yichun[0].Name);
        Assert.Equal("江西", yichun[0].Admin1);

        var name = await WeatherClient.ReverseAsync(27.83, 113.15);
        Assert.False(string.IsNullOrEmpty(name));
        var ip = await WeatherClient.IpLocateAsync();
        Assert.NotEqual(0, ip.Lat);
    }
}
