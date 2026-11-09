using System.Text;
using MacWidgets.Core;
using MacWidgets.Core.Stocks;
using MacWidgets.Core.Weather;

namespace MacWidgets.Core.Tests;

public class ParserTests
{
    private static string Sample(string name, Encoding? enc = null) =>
        (enc ?? Encoding.UTF8).GetString(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", name)));

    [Fact]
    public void TencentGbk()
    {
        var q = Parsers.ParseTencent(Sample("tencent_quotes.gbk.txt", Http.Gbk));
        var sh = Assert.Single(q, x => x.Symbol == "sh000001");
        Assert.Equal("上证指数", sh.Name);
        Assert.True(sh.Price > 1000);
        var hsi = Assert.Single(q, x => x.Symbol == "hkHSI");
        Assert.Equal("恒生指数", hsi.Name);
        Assert.Contains(q, x => x.Symbol == "usAAPL");
        Assert.Contains(q, x => x.Symbol == "sz399006" && x.Name == "创业板指");
    }

    [Fact]
    public void SinaGbk()
    {
        var q = Parsers.ParseSina(Sample("sina_quotes.gbk.txt", Http.Gbk));
        Assert.Contains(q, x => x.Symbol == "sh000001" && x.Name == "上证指数" && x.Price > 1000);
        Assert.Contains(q, x => x.Symbol == "hkHSI" && x.Name == "恒生指数");
    }

    [Fact]
    public void Eastmoney()
    {
        var map = new Dictionary<string, string> { ["1.000001"] = "sh000001", ["0.399006"] = "sz399006", ["100.HSI"] = "hkHSI" };
        var q = Parsers.ParseEastmoney(Sample("eastmoney_quotes.json"), map);
        Assert.Equal(3, q.Count);
        Assert.Contains(q, x => x.Symbol == "hkHSI" && x.PrevClose > 0);
    }

    [Fact]
    public void Intraday()
    {
        var pts = Parsers.ParseTencentMinute(Sample("tencent_minute_sh000001.json"), "sh000001");
        Assert.True(pts.Length > 200);
        var (prev, pts2) = Parsers.ParseEastmoneyTrends(Sample("eastmoney_trends_hsi.json"));
        Assert.True(prev > 0);
        Assert.True(pts2.Length > 100);
    }

    [Fact]
    public void OpenMeteo()
    {
        var w = WeatherClient.ParseForecast(Sample("openmeteo.json"));
        Assert.True(w.Daily.Count >= 2);
        var (_, text) = WeatherText.Current(w.CurrentCode);
        Assert.StartsWith("当前", text);
        Assert.Equal("现在有小雨，注意带伞", WeatherText.Current(61).Text);
    }

    [Theory]
    [InlineData("SH000001", "sh000001")]
    [InlineData("399006", "sz399006")]
    [InlineData("600519", "sh600519")]
    [InlineData("hkhsi", "hkHSI")]
    [InlineData("700", "hk00700")]
    [InlineData("aapl", "usAAPL")]
    [InlineData("us.dji", "us.DJI")]
    public void Normalize(string input, string expected) => Assert.Equal(expected, Symbols.Normalize(input));
}
