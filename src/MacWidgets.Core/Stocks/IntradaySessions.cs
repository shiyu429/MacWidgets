namespace MacWidgets.Core.Stocks;

/// <summary>一个交易日的分时数据。Date 形如 20261112；PrevClose 为该日的昨收（可能为 NaN）。</summary>
public sealed record IntradaySession(string Date, double PrevClose, double[] Points);

/// <summary>分时会话选择：当日尚未开盘/休市导致当日分时为空时，回退到最近一个有数据的交易日。</summary>
public static class IntradaySessions
{
    /// <summary>至少要有这么多个点才算“有分时”（1 个点画不出走势）。</summary>
    public const int MinPoints = 2;

    public static bool HasData(double[]? points) => points is { Length: >= MinPoints };

    /// <summary>从多日会话中选日期最新且非空的一个；全部为空返回 null。</summary>
    public static IntradaySession? LatestNonEmpty(IEnumerable<IntradaySession> sessions)
    {
        IntradaySession? best = null;
        foreach (var s in sessions)
        {
            if (!HasData(s.Points)) continue;
            if (best == null || string.CompareOrdinal(s.Date, best.Date) > 0) best = s;
        }
        return best;
    }
}
