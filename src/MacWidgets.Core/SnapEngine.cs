namespace MacWidgets.Core;

public sealed class SnapOptions
{
    public bool Enabled { get; set; } = true;
    public bool Grid { get; set; } = true;
    public double GridSize { get; set; } = 16;
    public double Threshold { get; set; } = 8;
    public double Gap { get; set; } = 16;
    public double Margin { get; set; } = 16;
    /// <summary>只考虑另一轴上距离小于该值的组件，避免远处组件干扰。</summary>
    public double NeighborRange { get; set; } = 200;
    /// <summary>组件至少露出多少逻辑像素，防止拖丢。</summary>
    public double MinVisible { get; set; } = 24;
}

/// <summary>参考线：Vertical=true 表示竖线（x = Position），范围 Start..End 为 y。</summary>
public readonly record struct SnapGuide(bool Vertical, double Position, double Start, double End);

public sealed record SnapResult(RectD Rect, IReadOnlyList<SnapGuide> Guides, bool SnappedX, bool SnappedY);

/// <summary>
/// 吸附算法（纯函数）。所有坐标均为同一显示器的逻辑坐标（DIP）。
/// X、Y 两轴独立：屏幕边/中线、其他组件的边/中线/等间距线优先，未命中时退回网格吸附。
/// </summary>
public static class SnapEngine
{
    private readonly record struct Candidate(double Line, int Point, RectD? Ref);

    public static SnapResult Snap(RectD r, RectD work, IReadOnlyList<RectD> others, SnapOptions o)
    {
        if (!o.Enabled)
            return new SnapResult(Clamp(r, work, o.MinVisible), Array.Empty<SnapGuide>(), false, false);

        var bx = SnapAxis(r, work, others, o, horizontal: true);
        var by = SnapAxis(r, work, others, o, horizontal: false);

        double x = bx.HasValue ? r.X + bx.Value.Delta : r.X;
        double y = by.HasValue ? r.Y + by.Value.Delta : r.Y;

        if (!bx.HasValue && o.Grid && o.GridSize > 0)
            x = work.Left + o.Margin + Math.Round((x - work.Left - o.Margin) / o.GridSize) * o.GridSize;
        if (!by.HasValue && o.Grid && o.GridSize > 0)
            y = work.Top + o.Margin + Math.Round((y - work.Top - o.Margin) / o.GridSize) * o.GridSize;

        var nr = Clamp(r.WithPos(x, y), work, o.MinVisible);
        var guides = new List<SnapGuide>(2);
        if (bx.HasValue && Math.Abs(nr.X - x) < 0.01)
        {
            var c = bx.Value.Cand;
            double s = c.Ref is { } rf ? Math.Min(nr.Top, rf.Top) : work.Top;
            double e = c.Ref is { } rf2 ? Math.Max(nr.Bottom, rf2.Bottom) : work.Bottom;
            guides.Add(new SnapGuide(true, c.Line, s, e));
        }
        if (by.HasValue && Math.Abs(nr.Y - y) < 0.01)
        {
            var c = by.Value.Cand;
            double s = c.Ref is { } rf ? Math.Min(nr.Left, rf.Left) : work.Left;
            double e = c.Ref is { } rf2 ? Math.Max(nr.Right, rf2.Right) : work.Right;
            guides.Add(new SnapGuide(false, c.Line, s, e));
        }
        return new SnapResult(nr, guides, bx.HasValue, by.HasValue);
    }

    private static (double Delta, Candidate Cand)? SnapAxis(RectD r, RectD work, IReadOnlyList<RectD> others, SnapOptions o, bool horizontal)
    {
        double start = horizontal ? r.Left : r.Top;
        double size = horizontal ? r.W : r.H;
        double wStart = horizontal ? work.Left : work.Top;
        double wEnd = horizontal ? work.Right : work.Bottom;

        var cands = new List<Candidate>
        {
            new(wStart + o.Margin, 0, null),
            new(wEnd - o.Margin, 2, null),
            new((wStart + wEnd) / 2, 1, null),
        };

        foreach (var ot in others)
        {
            // 另一轴上的距离（重叠时为负）
            double gapOther = horizontal
                ? Math.Max(ot.Top - r.Bottom, r.Top - ot.Bottom)
                : Math.Max(ot.Left - r.Right, r.Left - ot.Right);
            if (gapOther >= o.NeighborRange) continue;

            double oS = horizontal ? ot.Left : ot.Top;
            double oE = horizontal ? ot.Right : ot.Bottom;
            double oC = (oS + oE) / 2;
            cands.Add(new(oS, 0, ot));          // 起边对齐
            cands.Add(new(oE, 2, ot));          // 末边对齐
            cands.Add(new(oC, 1, ot));          // 中线对齐
            cands.Add(new(oE + o.Gap, 0, ot));  // 等间距：放在对方之后
            cands.Add(new(oS - o.Gap, 2, ot));  // 等间距：放在对方之前
        }

        (double Delta, Candidate Cand)? best = null;
        foreach (var c in cands)
        {
            double p = start + c.Point * size / 2;
            double d = c.Line - p;
            if (Math.Abs(d) > o.Threshold) continue;
            if (best is null || Math.Abs(d) < Math.Abs(best.Value.Delta))
                best = (d, c);
        }
        return best;
    }

    public static RectD Clamp(RectD r, RectD work, double minVisible)
    {
        double minX = work.Left - r.W + minVisible;
        double maxX = work.Right - minVisible;
        double minY = work.Top;
        double maxY = work.Bottom - minVisible;
        double x = Math.Min(Math.Max(r.X, minX), Math.Max(minX, maxX));
        double y = Math.Min(Math.Max(r.Y, minY), Math.Max(minY, maxY));
        return r.WithPos(x, y);
    }

    /// <summary>完全放在工作区内（用于自动摆放 / 重叠避让）。</summary>
    public static RectD ClampInside(RectD r, RectD work)
    {
        double x = Math.Min(Math.Max(r.X, work.Left), Math.Max(work.Left, work.Right - r.W));
        double y = Math.Min(Math.Max(r.Y, work.Top), Math.Max(work.Top, work.Bottom - r.H));
        return r.WithPos(x, y);
    }

    /// <summary>与其他组件重叠面积占比（相对自身面积）的最大值。</summary>
    public static double MaxOverlapRatio(RectD r, IReadOnlyList<RectD> others)
    {
        double area = r.W * r.H;
        if (area <= 0) return 0;
        double m = 0;
        foreach (var o in others) m = Math.Max(m, r.IntersectArea(o) / area);
        return m;
    }

    /// <summary>
    /// 若与其他组件重叠超过 threshold，则在网格上搜索距离最近、与所有组件保持 gap 间距的空位。
    /// 找不到则原样返回。
    /// </summary>
    public static RectD ResolveOverlap(RectD r, RectD work, IReadOnlyList<RectD> others, SnapOptions o, double threshold = 0.3)
    {
        if (MaxOverlapRatio(r, others) <= threshold) return r;
        double step = o.GridSize > 0 ? o.GridSize : 16;
        RectD? best = null;
        double bestD = double.MaxValue;
        for (double y = work.Top + o.Margin; y + r.H <= work.Bottom - o.Margin + 0.01; y += step)
        {
            for (double x = work.Left + o.Margin; x + r.W <= work.Right - o.Margin + 0.01; x += step)
            {
                var c = r.WithPos(x, y);
                var inflated = c.Inflate(o.Gap - 0.5);
                bool hit = false;
                foreach (var ot in others)
                    if (inflated.IntersectArea(ot) > 0) { hit = true; break; }
                if (hit) continue;
                double d = (x - r.X) * (x - r.X) + (y - r.Y) * (y - r.Y);
                if (d < bestD) { bestD = d; best = c; }
            }
        }
        return best ?? r;
    }
}
