namespace MacWidgets.Core;

public enum HintKind { Warmer, Cooler, Rain, Normal }

public static class WeatherText
{
    /// <summary>描述当前天气；降雨时补充带伞提示。</summary>
    public static (HintKind Kind, string Text) Current(int wmo)
    {
        bool rain = wmo is >= 51 and <= 67 or >= 80 and <= 82 or >= 95 and <= 99;
        if (rain) return (HintKind.Rain, $"现在有{Describe(wmo)}，注意带伞");
        return (HintKind.Normal, $"当前{Describe(wmo)}");
    }

    public static string Describe(int wmo) => wmo switch
    {
        0 => "晴",
        1 => "大部晴朗",
        2 => "局部多云",
        3 => "阴",
        45 or 48 => "雾",
        51 or 53 or 55 => "毛毛雨",
        56 or 57 => "冻毛毛雨",
        61 => "小雨",
        63 => "中雨",
        65 => "大雨",
        66 or 67 => "冻雨",
        71 => "小雪",
        73 => "中雪",
        75 => "大雪",
        77 => "雪粒",
        80 or 81 or 82 => "阵雨",
        85 or 86 => "阵雪",
        95 => "雷暴",
        96 or 99 => "雷暴伴冰雹",
        _ => "—",
    };

    public static double ToUnit(double celsius, bool fahrenheit) => fahrenheit ? celsius * 9 / 5 + 32 : celsius;
}
