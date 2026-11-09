namespace MacWidgets.Core;

/// <summary>与 UI 框架无关的双精度矩形（逻辑像素 / DIP）。</summary>
public readonly record struct RectD(double X, double Y, double W, double H)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + W;
    public double Bottom => Y + H;
    public double CenterX => X + W / 2;
    public double CenterY => Y + H / 2;

    public RectD WithPos(double x, double y) => new(x, y, W, H);

    public double IntersectArea(RectD o)
    {
        var w = Math.Min(Right, o.Right) - Math.Max(Left, o.Left);
        var h = Math.Min(Bottom, o.Bottom) - Math.Max(Top, o.Top);
        return w > 0 && h > 0 ? w * h : 0;
    }

    public RectD Inflate(double d) => new(X - d, Y - d, W + 2 * d, H + 2 * d);
}

public readonly record struct PointD(double X, double Y);
