using MacWidgets.Core;

namespace MacWidgets.Core.Tests;

public class ClockGeometryTests
{
    [Fact]
    public void SixtyTicksFollowRoundedRectStartingNearTopCenter()
    {
        var ticks = ClockGeometry.ComputeTicks(170, 170, 9, 7, cornerRadius: 30);
        Assert.Equal(60, ticks.Count);

        // 12 点附近：顶边中点
        Assert.Equal(85, ticks[0].Outer.X, 1.5);
        Assert.Equal(9, ticks[0].Outer.Y, 0.5);

        // 刻度长度
        foreach (var t in ticks)
        {
            double len = Math.Sqrt(Math.Pow(t.Inner.X - t.Outer.X, 2) + Math.Pow(t.Inner.Y - t.Outer.Y, 2));
            Assert.Equal(7, len, 3);
        }

        // 相邻外端弧长近似等距（周长/60）
        double peri = ApproximatePerimeter(ticks);
        double step = peri / 60;
        for (int i = 0; i < 60; i++)
        {
            var a = ticks[i].Outer;
            var b = ticks[(i + 1) % 60].Outer;
            double d = Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
            Assert.InRange(d, step * 0.85, step * 1.15);
        }

        // 不在正圆上：四角附近外端半径应大于边中点外端半径（圆角矩形特征）
        double cx = 85, cy = 85;
        double rTop = Dist(ticks[0].Outer, cx, cy);
        double rCorner = Dist(ticks[7].Outer, cx, cy); // ~42° 附近，更靠角
        Assert.True(rCorner > rTop + 1.0);
    }

    [Fact]
    public void DiscreteHeadIsDarkestAndNextIsLightest()
    {
        double s = 25.0;
        Assert.True(ClockGeometry.TickAlpha(25, s, 60, smooth: false) > 0.98);
        Assert.True(ClockGeometry.TickAlpha(26, s, 60, smooth: false) < 0.17);
        Assert.True(ClockGeometry.TickAlpha(24, s, 60, smooth: false) > ClockGeometry.TickAlpha(20, s, 60, smooth: false));
    }

    [Fact]
    public void SmoothHeadInterpolatesBetweenAdjacentTicks()
    {
        double mid = 25.5;
        double a25 = ClockGeometry.TickAlpha(25, mid, 60, smooth: true);
        double a26 = ClockGeometry.TickAlpha(26, mid, 60, smooth: true);
        Assert.InRange(Math.Abs(a25 - a26), 0, 0.08);
        Assert.True(a25 > 0.55);
        Assert.True(a26 > 0.55);
        Assert.True(a25 > ClockGeometry.TickAlpha(20, mid, 60, smooth: true));

        double early = ClockGeometry.TickAlpha(26, 25.1, 60, smooth: true);
        double late = ClockGeometry.TickAlpha(26, 25.9, 60, smooth: true);
        Assert.True(late > early + 0.15);
    }

    private static double Dist(PointD p, double cx, double cy) =>
        Math.Sqrt(Math.Pow(p.X - cx, 2) + Math.Pow(p.Y - cy, 2));

    private static double ApproximatePerimeter(IReadOnlyList<Tick> ticks)
    {
        double sum = 0;
        for (int i = 0; i < ticks.Count; i++)
        {
            var a = ticks[i].Outer;
            var b = ticks[(i + 1) % ticks.Count].Outer;
            sum += Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        }
        return sum;
    }
}
