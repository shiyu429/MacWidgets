using MacWidgets.Core.Genshin;

namespace MacWidgets.Core.Tests;

public class GenshinCalendarClientTests
{
    [Fact]
    public void ParseLandingUrlFromOfficialHomeHtml()
    {
        const string html = """
            <html><script src="https://act.mihoyo.com/ys/event/hk4e-7-2-landing/index_abc.js"></script>
            <a href="https://act.mihoyo.com/ys/event/hk4e-7-2-landing/">活动</a></html>
            """;
        Assert.Equal("https://act.mihoyo.com/ys/event/hk4e-7-2-landing/", GenshinCalendarClient.ParseLandingUrl(html));
    }

    [Fact]
    public void ParseCalendarPanelLargeFromWebpackBundle()
    {
        const string js = """
            A.exports=e.p+"images/calendar-panel-large.c2e8ade8..png";
            A.exports=e.p+"images/calendar-panel.1325a98e..png";
            """;
        Assert.Equal("images/calendar-panel-large.c2e8ade8..png", GenshinCalendarClient.ParseCalendarImageRel(js));
    }
}
