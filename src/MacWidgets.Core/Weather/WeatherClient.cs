using System.Globalization;
using System.Text.Json;

namespace MacWidgets.Core.Weather;

public sealed record DailyForecast(DateTime Date, double Max, double Min, int Code, double? RainProb);

public sealed record WeatherData(double CurrentTemp, int CurrentCode, bool IsDay, IReadOnlyList<DailyForecast> Daily, DateTime FetchedAt);

public sealed record GeoPlace(
    string Name,
    string? Admin1,
    string? Admin2,
    string? Country,
    double Lat,
    double Lon,
    string? FeatureCode = null,
    int? Population = null)
{
    public string Display => string.Join(" · ", new[] { Name, Admin2 != Name ? Admin2 : null, Admin1, Country }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public sealed record IpLocation(double Lat, double Lon, string? City, string? Region);

public static class WeatherClient
{
    private static string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

    public static async Task<WeatherData> ForecastAsync(double lat, double lon, CancellationToken ct = default)
    {
        var url = $"https://api.open-meteo.com/v1/forecast?latitude={F(lat)}&longitude={F(lon)}" +
                  "&current=temperature_2m,weather_code,is_day" +
                  "&daily=temperature_2m_max,temperature_2m_min,weather_code,precipitation_probability_max" +
                  "&timezone=auto&forecast_days=4";
        var json = await Http.GetStringAsync(url, ct: ct).ConfigureAwait(false);
        return ParseForecast(json);
    }

    public static WeatherData ParseForecast(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var cur = root.GetProperty("current");
        double temp = cur.GetProperty("temperature_2m").GetDouble();
        int code = cur.TryGetProperty("weather_code", out var wc) && wc.ValueKind == JsonValueKind.Number ? wc.GetInt32() : 0;
        bool isDay = !cur.TryGetProperty("is_day", out var d) || d.ValueKind != JsonValueKind.Number || d.GetInt32() == 1;

        var daily = new List<DailyForecast>();
        if (root.TryGetProperty("daily", out var dl))
        {
            var times = dl.GetProperty("time").EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
            var max = Arr(dl, "temperature_2m_max");
            var min = Arr(dl, "temperature_2m_min");
            var codes = Arr(dl, "weather_code");
            var rain = Arr(dl, "precipitation_probability_max");
            for (int i = 0; i < times.Length; i++)
            {
                if (!DateTime.TryParse(times[i], CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) continue;
                double mx = At(max, i) ?? temp, mn = At(min, i) ?? temp;
                daily.Add(new DailyForecast(dt, mx, mn, (int)(At(codes, i) ?? 0), At(rain, i)));
            }
        }
        return new WeatherData(temp, code, isDay, daily, DateTime.Now);
    }

    private static double?[] Arr(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var a) || a.ValueKind != JsonValueKind.Array) return [];
        return a.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Number ? e.GetDouble() : (double?)null).ToArray();
    }

    private static double? At(double?[] a, int i) => i < a.Length ? a[i] : null;

    /// <summary>
    /// 城市搜索（1.0.17）：内置中国地级市库优先，再补 OpenStreetMap Nominatim（countrycodes=cn）。
    /// 预报仍用 Open-Meteo；不再依赖 Open-Meteo Geocoding（对宜春等缺条目、同名乡镇乱序）。
    /// </summary>
    public static async Task<List<GeoPlace>> SearchAsync(string name, CancellationToken ct = default)
    {
        var raw = (name ?? "").Trim();
        if (raw.Length == 0) return [];

        var merged = new List<GeoPlace>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AddRange(IEnumerable<GeoPlace> places)
        {
            foreach (var p in places)
            {
                // 约 1.1km 网格去重，避免本地库与 Nominatim 重复
                string key = $"{Math.Round(p.Lat, 2)},{Math.Round(p.Lon, 2)}|{NormalizeCityQuery(p.Name)}";
                if (seen.Add(key)) merged.Add(p);
            }
        }

        AddRange(ChinaCities.SearchLocal(raw));

        // Nominatim：覆盖库外区县/乡镇。本地已有精确地级市命中时跳过网络，保证「宜春/长沙」等秒回且稳。
        bool localExact = merged.Any(p =>
        {
            string n = NormalizeCityQuery(p.Name);
            string q = NormalizeCityQuery(raw);
            return n.Equals(q, StringComparison.OrdinalIgnoreCase);
        });
        if (!localExact)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                // 单次查询即可；市/非市别名已由本地库与 RankPlaces 处理
                var url = "https://nominatim.openstreetmap.org/search?format=json&addressdetails=1&limit=8&countrycodes=cn&accept-language=zh&q="
                          + Uri.EscapeDataString(raw);
                var json = await Http.GetStringAsync(url, userAgent: "MacWidgets/1.0 (personal desktop widget app)", ct: ct).ConfigureAwait(false);
                AddRange(ParseNominatimSearch(json));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                // 离线或 Nominatim 不可用：仅返回本地库
            }
        }

        return RankPlaces(merged, raw);
    }

    /// <summary>解析 Nominatim search JSON（format=json&amp;addressdetails=1）。</summary>
    public static List<GeoPlace> ParseNominatimSearch(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<GeoPlace>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return list;
        foreach (var r in doc.RootElement.EnumerateArray())
        {
            string name = S(r, "name") ?? "";
            if (name.Length == 0) continue;
            string? cls = S(r, "class");
            string? typ = S(r, "type");
            // 跳过铁路站等非行政区噪声（如「株洲」火车站）
            if (string.Equals(cls, "railway", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cls, "highway", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cls, "amenity", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!double.TryParse(S(r, "lat"), System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) continue;
            if (!double.TryParse(S(r, "lon"), System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) continue;

            string? admin1 = null, admin2 = null, country = "中国";
            if (r.TryGetProperty("address", out var addr) && addr.ValueKind == JsonValueKind.Object)
            {
                admin1 = S(addr, "state") ?? S(addr, "province");
                admin2 = S(addr, "city") ?? S(addr, "town") ?? S(addr, "county") ?? S(addr, "region");
                country = S(addr, "country") ?? country;
            }
            string feature = NominatimFeatureCode(cls, typ, S(r, "addresstype"));
            list.Add(new GeoPlace(name, admin1, admin2, country, lat, lon, feature, null));
        }
        return list;
    }

    public static string NominatimFeatureCode(string? cls, string? typ, string? addresstype)
    {
        string t = (addresstype ?? typ ?? "").ToLowerInvariant();
        return t switch
        {
            "state" or "province" or "region" => "ADM1",
            "city" => "PPLA",
            "town" => "PPLA3",
            "county" or "district" or "city_district" => "ADM3",
            "suburb" or "village" or "neighbourhood" or "neighborhood" => "PPLA4",
            _ when string.Equals(cls, "boundary", StringComparison.OrdinalIgnoreCase) => "ADM2",
            _ => "PPL",
        };
    }

    /// <summary>去掉市/县/区等行政后缀；「株洲市」→「株洲」，「长沙」保持「长沙」。</summary>
    public static string NormalizeCityQuery(string q)
    {
        q = (q ?? "").Trim();
        if (q.Length == 0) return q;
        // Open-Meteo 偶发「株洲 市」中间空格；先压成无空白再剥后缀
        q = string.Concat(q.Where(c => !char.IsWhiteSpace(c)));
        // 长后缀优先
        // 注意：不要剥单独的「州」——「徐州/广州/杭州」剥完会变成一字，导致「徐州市」匹配失败。
        string[] suffixes =
        [
            "特别行政区", "自治州", "自治县", "自治区", "地区", "盟",
            "市", "县", "区", "旗",
        ];
        foreach (var s in suffixes)
        {
            if (q.Length > s.Length && q.EndsWith(s, StringComparison.Ordinal))
                return q[..^s.Length];
        }
        return q;
    }

    /// <summary>生成搜索变体：原文、去后缀、必要时补「市」。</summary>
    public static IReadOnlyList<string> SearchQueryVariants(string raw)
    {
        raw = (raw ?? "").Trim();
        if (raw.Length == 0) return [];
        var list = new List<string>();
        void Add(string s)
        {
            s = s.Trim();
            if (s.Length == 0) return;
            if (!list.Contains(s, StringComparer.OrdinalIgnoreCase)) list.Add(s);
        }
        Add(raw);
        var norm = NormalizeCityQuery(raw);
        Add(norm);
        // 简称常需要带「市」才能命中地级市（如「长沙」→「长沙市」）；已有后缀则不再叠
        if (norm.Length > 0 && norm == raw && !raw.EndsWith("市", StringComparison.Ordinal))
            Add(norm + "市");
        return list;
    }

    public static List<GeoPlace> ParseSearchResults(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<GeoPlace>();
        if (!doc.RootElement.TryGetProperty("results", out var res) || res.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var r in res.EnumerateArray())
        {
            int? pop = null;
            if (r.TryGetProperty("population", out var pe) && pe.ValueKind == JsonValueKind.Number)
                pop = pe.TryGetInt32(out var pi) ? pi : (int?)pe.GetDouble();
            list.Add(new GeoPlace(
                r.GetProperty("name").GetString() ?? "",
                S(r, "admin1"), S(r, "admin2"), S(r, "country"),
                r.GetProperty("latitude").GetDouble(), r.GetProperty("longitude").GetDouble(),
                S(r, "feature_code"), pop));
        }
        return list;
    }

    /// <summary>按与查询的匹配度 / GeoNames feature_code / 人口排序（高分在前）。</summary>
    public static List<GeoPlace> RankPlaces(IEnumerable<GeoPlace> places, string rawQuery)
    {
        string raw = (rawQuery ?? "").Trim();
        string norm = NormalizeCityQuery(raw);
        return places
            .Select(p => (p, score: ScorePlace(p, raw, norm)))
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.p.Name, StringComparer.Ordinal)
            .Select(x => x.p)
            .ToList();
    }

    public static double ScorePlace(GeoPlace p, string rawQuery, string? normalizedQuery = null)
    {
        string raw = (rawQuery ?? "").Trim();
        string norm = normalizedQuery ?? NormalizeCityQuery(raw);
        string name = p.Name ?? "";
        string nameNorm = NormalizeCityQuery(name);
        string admin2 = p.Admin2 ?? "";
        string admin2Norm = NormalizeCityQuery(admin2);

        double score = 0;
        // 名称匹配（最高优先级）
        if (name.Equals(raw, StringComparison.OrdinalIgnoreCase) ||
            nameNorm.Equals(norm, StringComparison.OrdinalIgnoreCase))
            score += 1000;
        else if (name.StartsWith(raw, StringComparison.OrdinalIgnoreCase) ||
                 nameNorm.StartsWith(norm, StringComparison.OrdinalIgnoreCase))
            score += 700;
        else if (name.Contains(raw, StringComparison.OrdinalIgnoreCase) ||
                 (norm.Length > 0 && nameNorm.Contains(norm, StringComparison.OrdinalIgnoreCase)))
            score += 300;

        // admin2 与查询一致（如结果名「长沙」但 admin2 是「长沙市」）——少见，但仍加分
        if (norm.Length > 0 && (admin2Norm.Equals(norm, StringComparison.OrdinalIgnoreCase) ||
                                admin2.Equals(raw, StringComparison.OrdinalIgnoreCase)))
            score += 400;

        // GeoNames feature_code：地级/省级驻地远高于乡镇 PPLA4
        score += FeatureCodeScore(p.FeatureCode);

        if (p.Population is int pop && pop > 0)
            score += Math.Log10(pop + 1) * 80;

        // 中国结果略优先（本应用面向中文城市搜索）
        if (string.Equals(p.Country, "中国", StringComparison.Ordinal) ||
            string.Equals(p.Country, "China", StringComparison.OrdinalIgnoreCase))
            score += 25;

        return score;
    }

    public static double FeatureCodeScore(string? code) => code switch
    {
        "PPLC" => 500,   // 国家首都
        "PPLA" => 420,   // 一级行政区驻地（省会/地级市常标此）
        "PPLA2" => 320,
        "PPLA3" => 220,
        "PPLA4" => 40,   // 乡镇级——「长沙」同名小镇多为此类
        "PPL" => 30,
        "ADM1" => 280,
        "ADM2" => 240,
        "ADM3" => 160,
        _ => 10,
    };

    private static string? S(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>IP 定位：ipwho.is（免 key）。</summary>
    public static async Task<IpLocation> IpLocateAsync(CancellationToken ct = default)
    {
        var json = await Http.GetStringAsync("https://ipwho.is/?lang=zh-CN", ct: ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        if (r.TryGetProperty("success", out var ok) && ok.ValueKind == JsonValueKind.False)
            throw new InvalidOperationException("IP 定位失败");
        return new IpLocation(r.GetProperty("latitude").GetDouble(), r.GetProperty("longitude").GetDouble(), S(r, "city"), S(r, "region"));
    }

    /// <summary>Nominatim 反查区县名（必须带自定义 UA，≤1 次/秒，调用方负责缓存）。</summary>
    public static async Task<string?> ReverseAsync(double lat, double lon, CancellationToken ct = default)
    {
        var url = $"https://nominatim.openstreetmap.org/reverse?lat={F(lat)}&lon={F(lon)}&format=json&accept-language=zh&zoom=12";
        var json = await Http.GetStringAsync(url, userAgent: "MacWidgets/1.0 (personal desktop widget app)", ct: ct).ConfigureAwait(false);
        return ParseReverse(json);
    }

    public static string? ParseReverse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("address", out var a)) return null;
        foreach (var k in new[] { "district", "city_district", "county", "city", "town", "village", "suburb", "state" })
        {
            var v = S(a, k);
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
        return null;
    }
}
