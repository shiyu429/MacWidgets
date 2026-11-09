namespace MacWidgets.Core.Stocks;

/// <summary>按北京时间（UTC+8，无夏令时）粗略判断交易时段，不含节假日。</summary>
public static class TradingCalendar
{
    public static DateTime BeijingNow() => DateTime.UtcNow.AddHours(8);

    public static bool IsTrading(Market m, DateTime bj)
    {
        var t = bj.TimeOfDay;
        bool weekday = bj.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
        switch (m)
        {
            case Market.CN:
                return weekday && (In(t, 9, 15, 11, 31) || In(t, 13, 0, 15, 1));
            case Market.HK:
                return weekday && (In(t, 9, 30, 12, 1) || In(t, 13, 0, 16, 11));
            case Market.US:
                // 美东 9:30–16:00 ≈ 北京 21:30–05:00（冬令时 22:30–05:00），放宽处理
                if (t >= new TimeSpan(21, 30, 0)) return bj.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
                if (t <= new TimeSpan(5, 0, 0)) return bj.DayOfWeek is not (DayOfWeek.Sunday or DayOfWeek.Monday);
                return false;
            default:
                return false;
        }
    }

    private static bool In(TimeSpan t, int h1, int m1, int h2, int m2) =>
        t >= new TimeSpan(h1, m1, 0) && t < new TimeSpan(h2, m2, 0);
}
