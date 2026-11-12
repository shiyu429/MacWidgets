using MacWidgets.Core.Stocks;

namespace MacWidgets.Core.Tests;

public class IntradaySessionTests
{
    private static IntradaySession S(string date, int n, double prec = 100) =>
        new(date, prec, Enumerable.Range(0, n).Select(i => 100.0 + i).ToArray());

    [Fact]
    public void PicksTodayWhenTodayHasData()
    {
        var pick = IntradaySessions.LatestNonEmpty([S("20261112", 54), S("20261111", 391), S("20261110", 391)]);
        Assert.Equal("20261112", pick!.Date);
    }

    [Fact]
    public void PreOpenFallsBackToPreviousTradingDay()
    {
        // 未开盘：当日 0 个点或只有 1 个点（画不出走势）
        Assert.Equal("20261111", IntradaySessions.LatestNonEmpty([S("20261112", 0), S("20261111", 391, 348.51)])!.Date);
        var p = IntradaySessions.LatestNonEmpty([S("20261112", 1), S("20261111", 391, 348.51), S("20261110", 391)]);
        Assert.Equal("20261111", p!.Date);
        Assert.Equal(348.51, p.PrevClose);
    }

    [Fact]
    public void OrderIndependentAndSkipsEmptyDays()
    {
        var p = IntradaySessions.LatestNonEmpty([S("20261106", 242), S("20261110", 0), S("20261109", 242), S("20261112", 0)]);
        Assert.Equal("20261109", p!.Date);
    }

    [Fact]
    public void AllEmptyReturnsNull()
    {
        Assert.Null(IntradaySessions.LatestNonEmpty([S("20261112", 0), S("20261111", 1)]));
        Assert.Null(IntradaySessions.LatestNonEmpty([]));
    }

    [Fact]
    public void ParsesTencentFiveDayJson()
    {
        const string json = """
        {"code":0,"msg":"","data":{"usAAPL":{"data":[
          {"date":"20261112","prec":"355.36","data":[]},
          {"date":"20261111","prec":"348.51","data":["0930 349.10 100","0931 349.80 200","0932 350.20 300"]},
          {"date":"20261110","prec":"350.69","data":["0930 350.00 100","0931 349.00 200"]}
        ]}}}
        """;
        var days = Parsers.ParseTencentDays(json, "usAAPL");
        Assert.Equal(3, days.Count);
        Assert.Empty(days[0].Points);
        Assert.Equal(new[] { 349.10, 349.80, 350.20 }, days[1].Points);
        var pick = IntradaySessions.LatestNonEmpty(days);
        Assert.Equal("20261111", pick!.Date);
        Assert.Equal(348.51, pick.PrevClose);
    }
}
