using System.Text.RegularExpressions;

namespace MacWidgets.Core.Stocks;

/// <summary>内部统一代码格式：sh000001 / sz399006 / hkHSI / hk00700 / usAAPL / us.DJI。</summary>
public static partial class Symbols
{
    public static readonly string[] Defaults = ["sh000001", "sz399006", "hkHSI"];

    private static readonly Dictionary<string, string> FullNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sh000001"] = "上海证券交易所综合指数",
        ["sz399006"] = "深圳证券交易所创业板指数",
        ["sz399001"] = "深圳证券交易所成份股价指数",
        ["sh000300"] = "沪深300指数",
        ["sh000016"] = "上证50指数",
        ["sh000688"] = "上证科创板50成份指数",
        ["sh000905"] = "中证500指数",
        ["hkHSI"] = "Hang Seng Index",
        ["hkHSTECH"] = "Hang Seng TECH Index",
        ["hkHSCEI"] = "Hang Seng China Enterprises Index",
        ["us.DJI"] = "Dow Jones Industrial Average",
        ["us.IXIC"] = "NASDAQ Composite",
        ["us.INX"] = "Standard and Poor's 500",
    };

    [GeneratedRegex("^[0-9]+$")]
    private static partial Regex DigitsRx();

    /// <summary>把用户输入规范化为内部代码；无法识别返回 null。</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim().Replace(" ", "");
        if (s.Length < 2) return null;
        var lower = s.ToLowerInvariant();

        if (lower.StartsWith("sh", StringComparison.Ordinal) || lower.StartsWith("sz", StringComparison.Ordinal))
        {
            var code = s[2..];
            return DigitsRx().IsMatch(code) && code.Length == 6 ? lower[..2] + code : null;
        }
        if (lower.StartsWith("hk", StringComparison.Ordinal))
        {
            var code = s[2..];
            if (code.Length == 0) return null;
            if (DigitsRx().IsMatch(code)) return code.Length <= 5 ? "hk" + code.PadLeft(5, '0') : null;
            return "hk" + code.ToUpperInvariant();
        }
        if (lower.StartsWith("us", StringComparison.Ordinal))
        {
            var code = s[2..];
            if (code.Length == 0) return null;
            return "us" + code.ToUpperInvariant();
        }
        if (DigitsRx().IsMatch(s))
        {
            if (s.Length == 6)
            {
                char c = s[0];
                return (c is '5' or '6' or '9' ? "sh" : "sz") + s;
            }
            if (s.Length <= 5) return "hk" + s.PadLeft(5, '0');
            return null;
        }
        // 纯字母当作美股
        if (s.All(ch => char.IsLetter(ch) || ch == '.')) return "us" + s.ToUpperInvariant();
        return null;
    }

    public static Market MarketOf(string sym)
    {
        if (sym.StartsWith("sh", StringComparison.Ordinal) || sym.StartsWith("sz", StringComparison.Ordinal)) return Market.CN;
        if (sym.StartsWith("hk", StringComparison.Ordinal)) return Market.HK;
        if (sym.StartsWith("us", StringComparison.Ordinal)) return Market.US;
        return Market.Unknown;
    }

    public static string Code(string sym) => sym.Length > 2 ? sym[2..].TrimStart('.') : sym;

    public static string FullName(string sym, string? name)
    {
        if (FullNames.TryGetValue(sym, out var f)) return f;
        return MarketOf(sym) switch
        {
            Market.CN => (sym.StartsWith("sh", StringComparison.Ordinal) ? "上证 " : "深证 ") + Code(sym),
            Market.HK => "港股 " + Code(sym),
            Market.US => "美股 " + Code(sym),
            _ => name ?? sym,
        };
    }

    public static int ExpectedPoints(string sym) => MarketOf(sym) switch
    {
        Market.CN => 242,
        Market.HK => 332,
        Market.US => 391,
        _ => 242,
    };

    public static string? EastmoneySecid(string sym)
    {
        var code = Code(sym);
        return MarketOf(sym) switch
        {
            Market.CN => (sym.StartsWith("sh", StringComparison.Ordinal) ? "1." : "0.") + code,
            Market.HK => DigitsRx().IsMatch(code) ? "116." + code : "100." + code,
            Market.US => sym switch
            {
                "us.DJI" => "100.DJIA",
                "us.IXIC" => "100.NDX",
                "us.INX" => "100.SPX",
                _ => null,
            },
            _ => null,
        };
    }

    public static string? SinaCode(string sym) => MarketOf(sym) switch
    {
        Market.CN => sym,
        Market.HK => "rt_hk" + Code(sym),
        _ => null,
    };
}
