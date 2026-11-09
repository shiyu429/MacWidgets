using System.Windows;
using MacWidgets.Data;

namespace MacWidgets.Widgets;

public static class Catalog
{
    public static readonly WidgetKind[] Kinds =
        [WidgetKind.Clock, WidgetKind.Weather, WidgetKind.Reminders, WidgetKind.Stocks, WidgetKind.Photo, WidgetKind.PhotoWide, WidgetKind.Plan, WidgetKind.Notes];

    public static string Name(WidgetKind k) => k switch
    {
        WidgetKind.Clock => "时钟",
        WidgetKind.Weather => "天气",
        WidgetKind.Reminders => "提醒事项",
        WidgetKind.Stocks => "股票",
        WidgetKind.Photo => "照片",
        WidgetKind.PhotoWide => "照片 · 宽幅",
        WidgetKind.Plan => "提醒事项 · 计划",
        WidgetKind.Notes => "备忘录",
        _ => k.ToString(),
    };

    public static string Description(WidgetKind k) => k switch
    {
        WidgetKind.Clock => "数字时钟，边缘刻度环随秒流动",
        WidgetKind.Weather => "当前温度与天气提示（Open-Meteo）",
        WidgetKind.Reminders => "今天的提醒事项，可勾选、新建",
        WidgetKind.Stocks => "指数/个股行情与分时走势",
        WidgetKind.Photo => "展示自定义图片，可裁剪缩放",
        WidgetKind.PhotoWide => "宽幅照片组件，沉浸铺满",
        WidgetKind.Plan => "已安排日期的提醒事项",
        WidgetKind.Notes => "随手记录文字",
        _ => "",
    };

    public static WidgetSize[] Sizes(WidgetKind k) => k switch
    {
        WidgetKind.Clock => [WidgetSize.S, WidgetSize.L],
        WidgetKind.Weather => [WidgetSize.S, WidgetSize.M],
        WidgetKind.Stocks => [WidgetSize.M, WidgetSize.L],
        WidgetKind.PhotoWide => [WidgetSize.M],
        WidgetKind.Plan => [WidgetSize.M, WidgetSize.L],
        _ => [WidgetSize.S, WidgetSize.M, WidgetSize.L],
    };

    public static WidgetSize DefaultSize(WidgetKind k) => Sizes(k)[0];

    public static string SizeName(WidgetSize s) => s switch
    {
        WidgetSize.S => "小",
        WidgetSize.M => "中",
        WidgetSize.L => "大",
        _ => s.ToString(),
    };

    public static Size DesignSize(WidgetSize s) => s switch
    {
        WidgetSize.S => new Size(170, 170),
        WidgetSize.M => new Size(356, 170),
        _ => new Size(356, 356),
    };

    /// <summary>参考图布局（间距 16）：首行 时钟/天气/提醒，次行 股票(中)/照片，末行 计划(中)/备忘录。</summary>
    public static (double X, double Y) ReferenceSlot(WidgetKind k) => k switch
    {
        WidgetKind.Clock => (0, 0),
        WidgetKind.Weather => (186, 0),
        WidgetKind.Reminders => (372, 0),
        WidgetKind.Stocks => (0, 186),
        WidgetKind.Photo => (372, 186),
        WidgetKind.PhotoWide => (0, 558),
        WidgetKind.Plan => (0, 372),
        WidgetKind.Notes => (372, 372),
        _ => (0, 0),
    };
}
