namespace MacWidgets.Core;

public readonly record struct Tick(PointD Outer, PointD Inner);

/// <summary>
/// 时钟刻度环：沿圆角矩形（接近卡片 squircle）路径、按弧长等距采样 60 根刻度，
/// 每根刻度沿路径法线朝内，避免正圆在圆角卡片四角显得空、也避免椭圆扭曲。
/// </summary>
public static class ClockGeometry
{
    /// <param name="w">组件宽</param>
    /// <param name="h">组件高</param>
    /// <param name="inset">刻度外端距组件边缘</param>
    /// <param name="length">刻度长度（沿内法线）</param>
    /// <param name="cornerRadius">路径圆角；默认按短边比例贴近 CardRadius=28 @170</param>
    /// <param name="count">刻度数，默认 60</param>
    public static IReadOnlyList<Tick> ComputeTicks(
        double w, double h, double inset, double length, double? cornerRadius = null, int count = 60)
    {
        double left = inset, top = inset, right = w - inset, bottom = h - inset;
        double pw = Math.Max(1, right - left), ph = Math.Max(1, bottom - top);
        double defR = Math.Min(pw, ph) * (28.0 / 170.0);
        double r = Math.Clamp(cornerRadius ?? defR, 0, Math.Min(pw, ph) / 2);

        // 圆角矩形周长（直线段 + 四分之一圆弧 ×4）
        double straightW = Math.Max(0, pw - 2 * r);
        double straightH = Math.Max(0, ph - 2 * r);
        double arcLen = Math.PI * r / 2;
        double peri = 2 * straightW + 2 * straightH + 4 * arcLen;
        if (peri <= 1e-6)
        {
            // 退化成圆
            double cx = w / 2, cy = h / 2, outerR = Math.Min(w, h) / 2 - inset;
            var fallback = new List<Tick>(count);
            for (int i = 0; i < count; i++)
            {
                double ang = -Math.PI / 2 + 2 * Math.PI * i / count;
                double cos = Math.Cos(ang), sin = Math.Sin(ang);
                fallback.Add(new Tick(
                    new PointD(cx + outerR * cos, cy + outerR * sin),
                    new PointD(cx + (outerR - length) * cos, cy + (outerR - length) * sin)));
            }
            return fallback;
        }

        var list = new List<Tick>(count);
        for (int i = 0; i < count; i++)
        {
            // 从顶边中点起顺时针（12 点）
            double s = (peri * i / count + straightW / 2) % peri;
            SampleRoundedRect(left, top, right, bottom, r, straightW, straightH, arcLen, s,
                out var ox, out var oy, out var nx, out var ny);
            // (nx,ny) 为外法线；刻度朝内
            list.Add(new Tick(
                new PointD(ox, oy),
                new PointD(ox - nx * length, oy - ny * length)));
        }
        return list;
    }

    /// <summary>
    /// 从顶边左端（圆角结束后）起顺时针累进弧长 s，返回点与单位外法线。
    /// 段序：顶边 → 右上弧 → 右边 → 右下弧 → 底边 → 左下弧 → 左边 → 左上弧。
    /// </summary>
    private static void SampleRoundedRect(
        double left, double top, double right, double bottom, double r,
        double straightW, double straightH, double arcLen, double s,
        out double x, out double y, out double nx, out double ny)
    {
        double cxR = right - r, cyT = top + r, cxL = left + r, cyB = bottom - r;

        // 1 顶边：左→右，外法线朝上
        if (s <= straightW)
        {
            x = left + r + s; y = top; nx = 0; ny = -1; return;
        }
        s -= straightW;

        // 2 右上弧：角度 -π/2 → 0
        if (s <= arcLen)
        {
            double t = arcLen > 0 ? s / arcLen : 0;
            double ang = -Math.PI / 2 + t * (Math.PI / 2);
            x = cxR + r * Math.Cos(ang); y = cyT + r * Math.Sin(ang);
            nx = Math.Cos(ang); ny = Math.Sin(ang); return;
        }
        s -= arcLen;

        // 3 右边：上→下
        if (s <= straightH)
        {
            x = right; y = top + r + s; nx = 1; ny = 0; return;
        }
        s -= straightH;

        // 4 右下弧：0 → π/2
        if (s <= arcLen)
        {
            double t = arcLen > 0 ? s / arcLen : 0;
            double ang = 0 + t * (Math.PI / 2);
            x = cxR + r * Math.Cos(ang); y = cyB + r * Math.Sin(ang);
            nx = Math.Cos(ang); ny = Math.Sin(ang); return;
        }
        s -= arcLen;

        // 5 底边：右→左
        if (s <= straightW)
        {
            x = right - r - s; y = bottom; nx = 0; ny = 1; return;
        }
        s -= straightW;

        // 6 左下弧：π/2 → π
        if (s <= arcLen)
        {
            double t = arcLen > 0 ? s / arcLen : 0;
            double ang = Math.PI / 2 + t * (Math.PI / 2);
            x = cxL + r * Math.Cos(ang); y = cyB + r * Math.Sin(ang);
            nx = Math.Cos(ang); ny = Math.Sin(ang); return;
        }
        s -= arcLen;

        // 7 左边：下→上
        if (s <= straightH)
        {
            x = left; y = bottom - r - s; nx = -1; ny = 0; return;
        }
        s -= straightH;

        // 8 左上弧：π → 3π/2
        {
            double t = arcLen > 0 ? Math.Clamp(s / arcLen, 0, 1) : 0;
            double ang = Math.PI + t * (Math.PI / 2);
            x = cxL + r * Math.Cos(ang); y = cyT + r * Math.Sin(ang);
            nx = Math.Cos(ang); ny = Math.Sin(ang);
        }
    }

    /// <summary>
    /// 彗尾透明度。smooth=false：按整秒；smooth=true：小数秒连续滑动。
    /// </summary>
    public static double TickAlpha(int index, double seconds, int count = 60, bool smooth = true)
    {
        double head = ((seconds % count) + count) % count;
        double behind = (head - index + count) % count;
        double ahead = (index - head + count) % count;
        double near = Math.Min(behind, ahead);

        if (!smooth)
        {
            return 0.15 + 0.85 * (1 - behind / count);
        }

        const double glowSigma = 0.85;
        double glow = Math.Exp(-(near * near) / (2 * glowSigma * glowSigma));

        const double tailLen = 22.0; // 1.0.18：彗尾略加长
        double tail = 0;
        if (behind <= tailLen)
        {
            double t = 1 - behind / tailLen;
            tail = t * t;
        }

        double mix = Math.Max(glow, tail * 0.72);
        return 0.10 + 0.90 * mix;
    }
}
