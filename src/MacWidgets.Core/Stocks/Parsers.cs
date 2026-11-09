using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MacWidgets.Core.Stocks;

public static partial class Parsers
{
    private static double D(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN;

    [GeneratedRegex("v_([A-Za-z0-9._]+)=\"([^\"]*)\"")]
    private static partial Regex TencentRx();

    [GeneratedRegex("hq_str_([A-Za-z0-9._]+)=\"([^\"]*)\"")]
    private static partial Regex SinaRx();

    /// <summary>腾讯 qt.gtimg.cn：[1] 名称 [3] 现价 [4] 昨收 [30] 时间 [31] 涨跌额 [32] 涨跌幅%。</summary>
    public static List<Quote> ParseTencent(string body)
    {
        var list = new List<Quote>();
        foreach (Match m in TencentRx().Matches(body))
        {
            var sym = m.Groups[1].Value;
            var f = m.Groups[2].Value.Split('~');
            if (f.Length < 33) continue;
            double price = D(f[3]), prev = D(f[4]), chg = D(f[31]), pct = D(f[32]);
            if (double.IsNaN(price) || price <= 0) continue;
            if (double.IsNaN(chg) && !double.IsNaN(prev)) chg = price - prev;
            if (double.IsNaN(pct) && prev > 0) pct = (price - prev) / prev * 100;
            list.Add(new Quote(sym, f[1], price, prev, chg, pct, f[30], "腾讯"));
        }
        return list;
    }

    /// <summary>新浪 hq.sinajs.cn。A 股：[0]名称 [2]昨收 [3]现价 [30]日期 [31]时间；港股 rt_：[1]名称 [3]昨收 [6]现价 [7]涨跌 [8]涨跌幅 [17]日期 [18]时间。</summary>
    public static List<Quote> ParseSina(string body)
    {
        var list = new List<Quote>();
        foreach (Match m in SinaRx().Matches(body))
        {
            var key = m.Groups[1].Value;
            var raw = m.Groups[2].Value;
            if (string.IsNullOrEmpty(raw)) continue;
            var f = raw.Split(',');
            if (key.StartsWith("rt_hk", StringComparison.Ordinal))
            {
                if (f.Length < 19) continue;
                double price = D(f[6]), prev = D(f[3]), chg = D(f[7]), pct = D(f[8]);
                if (double.IsNaN(price) || price <= 0) continue;
                list.Add(new Quote("hk" + key[5..], f[1], price, prev, chg, pct, f[17] + " " + f[18], "新浪"));
            }
            else
            {
                if (f.Length < 32) continue;
                double price = D(f[3]), prev = D(f[2]);
                if (double.IsNaN(price) || price <= 0) continue;
                double chg = price - prev;
                double pct = prev > 0 ? chg / prev * 100 : 0;
                list.Add(new Quote(key, f[0], price, prev, chg, pct, f[30] + " " + f[31], "新浪"));
            }
        }
        return list;
    }

    /// <summary>东方财富 ulist.np：f2 现价 f3 涨跌幅 f4 涨跌额 f12 代码 f13 市场 f14 名称 f18 昨收。</summary>
    public static List<Quote> ParseEastmoney(string json, IReadOnlyDictionary<string, string> secidToSymbol)
    {
        var list = new List<Quote>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) return list;
        if (!data.TryGetProperty("diff", out var diff)) return list;
        IEnumerable<JsonElement> items = diff.ValueKind switch
        {
            JsonValueKind.Array => diff.EnumerateArray(),
            JsonValueKind.Object => diff.EnumerateObject().Select(p => p.Value),
            _ => [],
        };
        foreach (var it in items)
        {
            string code = Str(it, "f12");
            string mkt = it.TryGetProperty("f13", out var f13) ? f13.ToString() : "";
            var secid = mkt + "." + code;
            if (!secidToSymbol.TryGetValue(secid, out var sym)) continue;
            double price = Num(it, "f2"), pct = Num(it, "f3"), chg = Num(it, "f4"), prev = Num(it, "f18");
            if (double.IsNaN(price) || price <= 0) continue;
            list.Add(new Quote(sym, Str(it, "f14"), price, prev, chg, pct, "", "东方财富"));
        }
        return list;
    }

    /// <summary>腾讯分时：data.{sym}.data.data = ["HHMM 价格 量 额", ...]。</summary>
    public static double[] ParseTencentMinute(string json, string sym)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) return [];
        JsonElement node = default;
        bool found = false;
        foreach (var p in data.EnumerateObject())
        {
            if (string.Equals(p.Name, sym, StringComparison.OrdinalIgnoreCase)) { node = p.Value; found = true; break; }
        }
        if (!found && data.EnumerateObject().Any()) { node = data.EnumerateObject().First().Value; found = true; }
        if (!found || node.ValueKind != JsonValueKind.Object) return [];
        if (!node.TryGetProperty("data", out var d1) || d1.ValueKind != JsonValueKind.Object) return [];
        if (!d1.TryGetProperty("data", out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
        var pts = new List<double>();
        foreach (var e in arr.EnumerateArray())
        {
            var s = e.GetString();
            if (s is null) continue;
            var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            var v = D(parts[1]);
            if (!double.IsNaN(v) && v > 0) pts.Add(v);
        }
        return pts.ToArray();
    }

    /// <summary>东方财富 trends2：data.preClose，data.trends = ["时间,开,收,高,低,...", ...]。</summary>
    public static (double PrevClose, double[] Points) ParseEastmoneyTrends(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) return (double.NaN, []);
        double prev = data.TryGetProperty("preClose", out var pc) && pc.ValueKind == JsonValueKind.Number ? pc.GetDouble() : double.NaN;
        var pts = new List<double>();
        if (data.TryGetProperty("trends", out var tr) && tr.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in tr.EnumerateArray())
            {
                var s = e.GetString();
                if (s is null) continue;
                var parts = s.Split(',');
                if (parts.Length < 3) continue;
                var v = D(parts[2]);
                if (!double.IsNaN(v) && v > 0) pts.Add(v);
            }
        }
        return (prev, pts.ToArray());
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? (v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString()) : "";

    private static double Num(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return double.NaN;
        if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
        return v.ValueKind == JsonValueKind.String ? D(v.GetString() ?? "") : double.NaN;
    }
}
