using MacWidgets.Core;

namespace MacWidgets.Core.Tests;

public class MainWindowLayoutTests
{
    // work-area width in DIP (physical px / DPI scale)
    [Theory]
    [InlineData(1366)]      // 1366×768 @100%
    [InlineData(1092.8)]    // 1366×768 @125%
    [InlineData(1920)]      // 1920×1080 @100%
    [InlineData(1280)]      // 1920×1080 @150%
    [InlineData(2560)]      // 2560×1440 @100%
    [InlineData(1706.67)]   // 2560×1440 @150%
    public void DefaultWidth_yields_two_gallery_columns(double workW)
    {
        double win = MainWindowLayout.DefaultWindowWidthForWorkArea(workW);
        double content = MainWindowLayout.ApproxGalleryContentWidth(win);
        int cols = MainWindowLayout.EstimateGalleryColumns(content);
        Assert.True(win >= MainWindowLayout.GalleryTwoColumnMinWidth || workW < MainWindowLayout.GalleryTwoColumnMinWidth,
            $"win={win} work={workW}");
        Assert.True(cols >= 2, $"expected ≥2 cols, got {cols} (win={win:F0}, content={content:F0}, work={workW:F0})");
    }

    [Fact]
    public void Tiny_work_area_does_not_exceed_screen()
    {
        double win = MainWindowLayout.DefaultWindowWidthForWorkArea(800);
        Assert.True(win <= 800 - 16);
    }

    [Fact]
    public void Old_70pct_logic_would_be_one_column_on_125pct_1366()
    {
        // 回归：旧 FitToWorkArea(work*0.70, min760, max1200) 在 1366@125% ≈765 → 一列
        double old = Math.Clamp(1092.8 * 0.70, 760, Math.Min(1200, 1092.8));
        Assert.True(MainWindowLayout.EstimateGalleryColumns(MainWindowLayout.ApproxGalleryContentWidth(old)) < 2);
        double neu = MainWindowLayout.DefaultWindowWidthForWorkArea(1092.8);
        Assert.True(MainWindowLayout.EstimateGalleryColumns(MainWindowLayout.ApproxGalleryContentWidth(neu)) >= 2);
    }
}
