using MacWidgets.Core.Weather;

namespace MacWidgets.Core.Tests;

/// <summary>城市搜索归一化、本地库与排序（纯逻辑，不访问网络）。</summary>
public class GeoSearchTests
{
    [Theory]
    [InlineData("长沙", "长沙")]
    [InlineData("长沙市", "长沙")]
    [InlineData("株洲", "株洲")]
    [InlineData("株洲市", "株洲")]
    [InlineData("宜春", "宜春")]
    [InlineData("宜春市", "宜春")]
    [InlineData("  梅州市  ", "梅州")]
    public void NormalizeCityQuery_strips_admin_suffix(string raw, string expected)
        => Assert.Equal(expected, WeatherClient.NormalizeCityQuery(raw));

    [Fact]
    public void SearchQueryVariants_includes_bare_and_shi()
    {
        var v = WeatherClient.SearchQueryVariants("长沙");
        Assert.Contains("长沙", v);
        Assert.Contains("长沙市", v);

        var z = WeatherClient.SearchQueryVariants("株洲市");
        Assert.Contains("株洲市", z);
        Assert.Contains("株洲", z);
    }

    [Theory]
    [InlineData("宜春")]
    [InlineData("宜春市")]
    public void ChinaCities_Yichun_is_Jiangxi(string q)
    {
        var list = ChinaCities.SearchLocal(q);
        Assert.NotEmpty(list);
        Assert.Contains("宜春", list[0].Name);
        Assert.Equal("江西", list[0].Admin1);
        Assert.InRange(list[0].Lat, 27.5, 28.2);
        Assert.InRange(list[0].Lon, 114.0, 114.8);
    }

    [Theory]
    [InlineData("长沙")]
    [InlineData("长沙市")]
    public void ChinaCities_Changsha_is_Hunan(string q)
    {
        var list = ChinaCities.SearchLocal(q);
        Assert.NotEmpty(list);
        Assert.Contains("长沙", list[0].Name);
        Assert.Equal("湖南", list[0].Admin1);
    }

    [Theory]
    [InlineData("株洲")]
    [InlineData("株洲市")]
    public void ChinaCities_Zhuzhou_is_Hunan(string q)
    {
        var list = ChinaCities.SearchLocal(q);
        Assert.NotEmpty(list);
        Assert.Contains("株洲", list[0].Name);
        Assert.Equal("湖南", list[0].Admin1);
    }

    [Theory]
    [InlineData("徐州")]
    [InlineData("徐州市")]
    [InlineData("广州")]
    [InlineData("广州市")]
    public void ChinaCities_zhou_suffix_cities_match_with_and_without_shi(string q)
    {
        var list = ChinaCities.SearchLocal(q);
        Assert.NotEmpty(list);
        Assert.Contains("州", list[0].Name);
    }

    [Fact]
    public void NormalizeCityQuery_does_not_strip_bare_zhou()
    {
        Assert.Equal("徐州", WeatherClient.NormalizeCityQuery("徐州"));
        Assert.Equal("徐州", WeatherClient.NormalizeCityQuery("徐州市"));
        Assert.Equal("广州", WeatherClient.NormalizeCityQuery("广州"));
        // 「自治州」仍剥；单独「…州」地名保留
        Assert.Equal("海南州", WeatherClient.NormalizeCityQuery("海南州"));
    }

    [Fact]
    public void ChinaCities_Yichun_not_confused_with_Yichun_Heilongjiang()
    {
        // 「宜春」≠「伊春」
        var yiChun = ChinaCities.SearchLocal("宜春");
        var yiChunHLJ = ChinaCities.SearchLocal("伊春");
        Assert.Equal("江西", yiChun[0].Admin1);
        Assert.Equal("黑龙江", yiChunHLJ[0].Admin1);
    }

    [Fact]
    public void RankPlaces_Changsha_prefers_prefecture_over_townships()
    {
        var places = new List<GeoPlace>
        {
            new("长沙", "重庆市", "重庆市", "中国", 30.99997, 108.30463, "PPLA4", 47170),
            new("长沙", "贵州", "遵义市", "中国", 28.68778, 105.98972, "PPLA4", 1653),
            new("长沙", "广东", "梅州市", "中国", 24.21575, 116.12126, "PPLA4", null),
            new("长沙市", "湖南", "长沙市", "中国", 28.19874, 112.97087, "PPLA", 3093980),
        };

        var ranked = WeatherClient.RankPlaces(places, "长沙");
        Assert.Equal("长沙市", ranked[0].Name);
        Assert.Equal("湖南", ranked[0].Admin1);
        Assert.DoesNotContain(ranked.Take(1), p => p.Admin1 is "重庆市" or "贵州" or "广东");

        var rankedShi = WeatherClient.RankPlaces(places, "长沙市");
        Assert.Equal("长沙市", rankedShi[0].Name);
        Assert.Equal("湖南", rankedShi[0].Admin1);
    }

    [Fact]
    public void RankPlaces_Zhuzhou_works_with_and_without_shi()
    {
        var places = new List<GeoPlace>
        {
            new("株洲", "湖南", "株洲 市", "中国", 27.83333, 113.15, "PPLA", 985470),
            new("柱洲县", "湖南", "株洲 市", "中国", 27.70292, 113.15202, "PPLA3", 12000),
            new("竹洲", "广东", null, "中国", 22.37211, 113.26409, "PPL", 800),
        };

        foreach (var q in new[] { "株洲", "株洲市" })
        {
            var ranked = WeatherClient.RankPlaces(places, q);
            Assert.Equal("株洲", ranked[0].Name);
            Assert.Equal("湖南", ranked[0].Admin1);
        }
    }

    [Fact]
    public void ParseSearchResults_reads_feature_and_population()
    {
        const string json = """
        {"results":[{"name":"长沙市","latitude":28.19874,"longitude":112.97087,
          "feature_code":"PPLA","country":"中国","admin1":"湖南","admin2":"长沙市","population":3093980}]}
        """;
        var list = WeatherClient.ParseSearchResults(json);
        Assert.Single(list);
        Assert.Equal("长沙市", list[0].Name);
        Assert.Equal("PPLA", list[0].FeatureCode);
        Assert.Equal(3093980, list[0].Population);
    }

    [Fact]
    public void ParseNominatimSearch_skips_railway_and_reads_city()
    {
        const string json = """
        [
          {"name":"株洲","lat":"27.83","lon":"113.15","class":"railway","type":"station","addresstype":"railway",
           "address":{"state":"湖南省","country":"中国"}},
          {"name":"株洲市","lat":"27.8274","lon":"113.1340","class":"boundary","type":"administrative","addresstype":"region",
           "address":{"state":"湖南省","country":"中国"}}
        ]
        """;
        var list = WeatherClient.ParseNominatimSearch(json);
        Assert.Single(list);
        Assert.Equal("株洲市", list[0].Name);
        Assert.Equal("湖南省", list[0].Admin1);
    }

    [Fact]
    public void ScorePlace_township_loses_to_prefecture()
    {
        var town = new GeoPlace("长沙", "重庆市", "重庆市", "中国", 31, 108, "PPLA4", 47170);
        var city = new GeoPlace("长沙市", "湖南", "长沙市", "中国", 28.2, 113, "PPLA", 3093980);
        double a = WeatherClient.ScorePlace(town, "长沙");
        double b = WeatherClient.ScorePlace(city, "长沙");
        Assert.True(b > a, $"city {b} should beat town {a}");
    }
}
