namespace MacWidgets.Core;

/// <summary>
/// 主窗口默认尺寸策略（与 UI 解耦，便于单测）。
/// 组件库用 WrapPanel + 约 364DIP 宽卡片；侧栏200 + 页边距/滚动约250DIP，两列约需 ≥1000 DIP。
/// </summary>
public static class MainWindowLayout
{
    public const double GalleryTwoColumnMinWidth = 1020;
    public const double GalleryTwoColumnWindowWidth = 1100;
    public const double GalleryTwoColumnMaxWidth = 1480;

    /// <summary>给定工作区宽度（已除以 DPI 的 DIP），返回首次打开主窗口应使用的宽度。</summary>
    public static double DefaultWindowWidthForWorkArea(double workWDip)
    {
        if (workWDip <= 0) return GalleryTwoColumnWindowWidth;
        double minW = GalleryTwoColumnMinWidth;
        double maxW = Math.Max(minW, Math.Min(GalleryTwoColumnMaxWidth, workWDip - 24));
        // 工作区本身窄于两列下限：尽量用满，不强行超出屏幕
        if (workWDip < GalleryTwoColumnMinWidth)
            return Math.Clamp(workWDip - 16, 0, Math.Max(0, workWDip - 16));
        double targetW = Math.Max(workWDip * 0.82, GalleryTwoColumnWindowWidth);
        return Math.Clamp(targetW, minW, maxW);
    }

    /// <summary>给定内容区可用宽度，估算 WrapPanel 能排几列（卡片宽 352 + 右间距 12）。</summary>
    public static int EstimateGalleryColumns(double contentWidthDip, double cardPitch = 364)
    {
        if (contentWidthDip <= 0 || cardPitch <= 0) return 0;
        // 最后一列可不依赖右侧间距：pitch=364，裸宽=352 → 近似 floor((w+12)/364)
        return Math.Max(0, (int)Math.Floor((contentWidthDip + 12) / cardPitch));
    }

    /// <summary>主窗口宽度 → 组件库内容区近似可用宽（侧栏200 + 页边距/滚动约250）。</summary>
    public static double ApproxGalleryContentWidth(double windowWidthDip) =>
        Math.Max(0, windowWidthDip - 250);
}
