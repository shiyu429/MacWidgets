using MacWidgets.Core;

namespace MacWidgets.Core.Tests;

public class SnapEngineTests
{
    private static readonly RectD Work = new(0, 0, 1920, 1040);

    [Fact]
    public void SnapsToScreenMargin()
    {
        var r = new RectD(21, 300, 170, 170);
        var res = SnapEngine.Snap(r, Work, [], new SnapOptions { Grid = false });
        Assert.Equal(16, res.Rect.X, 3);
        Assert.True(res.SnappedX);
        Assert.Contains(res.Guides, g => g.Vertical && Math.Abs(g.Position - 16) < 0.01);
    }

    [Fact]
    public void SnapsWithGapNextToOtherWidget()
    {
        var other = new RectD(100, 100, 170, 170);
        // 放在右侧，期望 x = 270 + 16 = 286
        var r = new RectD(290, 104, 170, 170);
        var res = SnapEngine.Snap(r, Work, [other], new SnapOptions { Grid = false });
        Assert.Equal(286, res.Rect.X, 3);
        Assert.Equal(100, res.Rect.Y, 3); // 顶边对齐
    }

    [Fact]
    public void FarAwayWidgetsAreIgnored()
    {
        var other = new RectD(100, 100, 170, 170);
        var r = new RectD(103, 800, 170, 170);
        var res = SnapEngine.Snap(r, Work, [other], new SnapOptions { Grid = false, NeighborRange = 200 });
        Assert.Equal(103, res.Rect.X, 3);
    }

    [Fact]
    public void FallsBackToGrid()
    {
        var r = new RectD(405, 507, 170, 170);
        var res = SnapEngine.Snap(r, Work, [], new SnapOptions { Grid = true, GridSize = 16, Margin = 16 });
        Assert.Equal(0, (res.Rect.X - 16) % 16, 3);
        Assert.Equal(0, (res.Rect.Y - 16) % 16, 3);
        Assert.False(res.SnappedX);
    }

    [Fact]
    public void DisabledOnlyClamps()
    {
        var r = new RectD(-1000, -50, 170, 170);
        var res = SnapEngine.Snap(r, Work, [], new SnapOptions { Enabled = false });
        Assert.Equal(-170 + 24, res.Rect.X, 3);
        Assert.Equal(0, res.Rect.Y, 3);
    }

    [Fact]
    public void ResolveOverlapFindsFreeSpot()
    {
        var other = new RectD(16, 16, 170, 170);
        var r = new RectD(20, 20, 170, 170);
        var res = SnapEngine.ResolveOverlap(r, Work, [other], new SnapOptions());
        Assert.Equal(0, res.IntersectArea(other));
    }
}
