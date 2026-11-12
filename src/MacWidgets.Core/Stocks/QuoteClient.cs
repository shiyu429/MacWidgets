namespace MacWidgets.Core.Stocks;

/// <summary>行情客户端：腾讯 → 新浪（带 Referer）→ 东方财富，连续失败 3 次切换下一源，10 分钟后回主源。</summary>
public sealed class QuoteClient
{
    private readonly int[] _failures = new int[3];
    private int _current;
    private DateTime _switchedAt = DateTime.MinValue;

    public string? LastSource { get; private set; }
    public string? LastError { get; private set; }

    public async Task<List<Quote>> GetQuotesAsync(IReadOnlyCollection<string> symbols, CancellationToken ct = default)
    {
        if (symbols.Count == 0) return [];
        if (_current != 0 && DateTime.UtcNow - _switchedAt > TimeSpan.FromMinutes(10))
        {
            _current = 0;
            Array.Clear(_failures);
        }

        Exception? last = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            int p = (_current + attempt) % 3;
            try
            {
                var res = p switch
                {
                    0 => await TencentAsync(symbols, ct).ConfigureAwait(false),
                    1 => await SinaAsync(symbols, ct).ConfigureAwait(false),
                    _ => await EastmoneyAsync(symbols, ct).ConfigureAwait(false),
                };
                if (res.Count == 0) throw new InvalidOperationException("空数据");
                // 主源未覆盖的代码（如新浪不支持美股）尝试用腾讯补齐
                if (p != 0 && res.Count < symbols.Count)
                {
                    try
                    {
                        var missing = symbols.Where(s => res.All(q => q.Symbol != s)).ToList();
                        if (missing.Count > 0) res.AddRange(await TencentAsync(missing, ct).ConfigureAwait(false));
                    }
                    catch { /* 忽略 */ }
                }
                _failures[p] = 0;
                LastSource = res[0].Source;
                LastError = null;
                return res;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                last = ex;
                _failures[p]++;
                if (p == _current && _failures[p] >= 3)
                {
                    _current = (_current + 1) % 3;
                    _switchedAt = DateTime.UtcNow;
                }
            }
        }
        LastError = last?.Message;
        throw last ?? new InvalidOperationException("行情获取失败");
    }

    public static async Task<List<Quote>> TencentAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        var url = "https://qt.gtimg.cn/q=" + string.Join(",", symbols);
        var body = await Http.GetStringAsync(url, Http.Gbk, ct: ct).ConfigureAwait(false);
        return Parsers.ParseTencent(body);
    }

    public static async Task<List<Quote>> SinaAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        var codes = symbols.Select(Symbols.SinaCode).Where(c => c != null).ToList();
        if (codes.Count == 0) return [];
        var url = "https://hq.sinajs.cn/list=" + string.Join(",", codes);
        var body = await Http.GetStringAsync(url, Http.Gbk, referer: "https://finance.sina.com.cn", ct: ct).ConfigureAwait(false);
        return Parsers.ParseSina(body);
    }

    private static readonly string[] EastmoneyHosts = ["push2delay.eastmoney.com", "82.push2.eastmoney.com", "push2.eastmoney.com"];

    public static async Task<List<Quote>> EastmoneyAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        var map = new Dictionary<string, string>();
        foreach (var s in symbols)
        {
            var id = Symbols.EastmoneySecid(s);
            if (id != null) map[id] = s;
        }
        if (map.Count == 0) return [];
        Exception? last = null;
        foreach (var host in EastmoneyHosts)
        {
            try
            {
                var url = $"https://{host}/api/qt/ulist.np/get?fltt=2&secids={string.Join(",", map.Keys)}&fields=f2,f3,f4,f12,f13,f14,f18";
                var body = await Http.GetStringAsync(url, referer: "https://quote.eastmoney.com/", ct: ct).ConfigureAwait(false);
                var res = Parsers.ParseEastmoney(body, map);
                if (res.Count > 0) return res;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                last = ex;
            }
        }
        if (last != null) throw last;
        return [];
    }

    /// <summary>腾讯近 5 个交易日分时（A 股/港股 day，美股 dayus），新到旧。</summary>
    public static async Task<List<IntradaySession>> TencentRecentSessionsAsync(string sym, CancellationToken ct = default)
    {
        var path = Symbols.MarketOf(sym) == Market.US ? "dayus" : "day";
        var url = $"https://web.ifzq.gtimg.cn/appstock/app/{path}/query?code={sym}";
        var json = await Http.GetStringAsync(url, ct: ct).ConfigureAwait(false);
        return Parsers.ParseTencentDays(json, sym);
    }

    /// <summary>当日分时：腾讯（A 股 minute / 港股 hkMinute / 美股 minute）→ 腾讯近 5 日（取最近非空交易日）→ 东方财富 trends2。</summary>
    public async Task<Intraday?> GetIntradayAsync(string sym, double prevCloseHint, CancellationToken ct = default)
    {
        int expected = Symbols.ExpectedPoints(sym);
        try
        {
            var path = Symbols.MarketOf(sym) == Market.HK ? "hkMinute" : "minute";
            var url = $"https://web.ifzq.gtimg.cn/appstock/app/{path}/query?code={sym}";
            var json = await Http.GetStringAsync(url, ct: ct).ConfigureAwait(false);
            var pts = Parsers.ParseTencentMinute(json, sym);
            if (IntradaySessions.HasData(pts)) return new Intraday(sym, prevCloseHint, pts, Math.Max(expected, pts.Length), "腾讯");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested) { }

        // 1.0.24：当日未开盘/休市时当日分时为空，改取近 5 日分时中最近一个有数据的交易日
        try
        {
            var s = IntradaySessions.LatestNonEmpty(await TencentRecentSessionsAsync(sym, ct).ConfigureAwait(false));
            if (s != null)
            {
                double prev = double.IsNaN(s.PrevClose) || s.PrevClose <= 0 ? prevCloseHint : s.PrevClose;
                return new Intraday(sym, prev, s.Points, Math.Max(expected, s.Points.Length), "腾讯");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested) { }

        var secid = Symbols.EastmoneySecid(sym);
        if (secid == null) return null;
        try
        {
            var url = $"https://push2his.eastmoney.com/api/qt/stock/trends2/get?secid={secid}&fields1=f1,f2,f3,f4,f5,f6,f7,f8&fields2=f51,f52,f53,f54,f55,f56,f57,f58&ndays=1&iscr=0";
            var json = await Http.GetStringAsync(url, referer: "https://quote.eastmoney.com/", ct: ct).ConfigureAwait(false);
            var (prev, pts) = Parsers.ParseEastmoneyTrends(json);
            if (pts.Length > 1) return new Intraday(sym, double.IsNaN(prev) ? prevCloseHint : prev, pts, Math.Max(expected, pts.Length), "东方财富");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested) { }
        return null;
    }
}
