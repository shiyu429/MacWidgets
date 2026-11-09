using System.Text.RegularExpressions;

namespace MacWidgets.Core.Tests;

/// <summary>
/// 1.0.12 回归守卫：UiKit.Squircle 返回已 Freeze 的 Geometry，若对其结果再赋值 .Transform
/// 会在 WPF 抛 InvalidOperationException（只读对象），导致所有 WidgetChrome 构造失败
/// （组件库预览全部“预览加载失败”、添加到桌面无效）。WPF 无法在 Linux CI 上运行，
/// 因此用源码扫描守住这一类错误。
/// </summary>
public class FrozenGeometryGuardTests
{
    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "MacWidgets.sln"))) d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    private static IEnumerable<string> AppSources() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src", "MacWidgets.App"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    [Fact]
    public void Squircle_IsFrozen_SoCallersMustNotMutate()
    {
        var uikit = File.ReadAllText(Path.Combine(RepoRoot(), "src", "MacWidgets.App", "Widgets", "UiKit.cs"));
        var body = uikit[uikit.IndexOf("public static Geometry Squircle(", StringComparison.Ordinal)..];
        body = body[..body.IndexOf("public static Geometry G(", StringComparison.Ordinal)];
        Assert.Contains("g.Freeze();", body);
    }

    [Fact]
    public void NoCode_SetsPropertiesOnFrozenSquircleResult()
    {
        var assign = new Regex(@"(?:var|Geometry)\s+(\w+)\s*=\s*UiKit\.Squircle\(", RegexOptions.Compiled);
        var offenders = new List<string>();
        foreach (var f in AppSources())
        {
            var src = File.ReadAllText(f);
            foreach (Match m in assign.Matches(src))
            {
                var name = m.Groups[1].Value;
                var mut = new Regex($@"\b{Regex.Escape(name)}\.(Transform|FillRule)\s*=");
                if (mut.IsMatch(src)) offenders.Add($"{Path.GetFileName(f)}: {name}");
            }
        }
        Assert.Empty(offenders);
    }

    [Fact]
    public void WidgetChrome_MaskHasFallback()
    {
        var src = File.ReadAllText(Path.Combine(RepoRoot(), "src", "MacWidgets.App", "Widgets", "WidgetView.cs"));
        Assert.Contains("UiKit.HardSquircleMask", src);
    }
}
