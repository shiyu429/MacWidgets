using MacWidgets.Data;

namespace MacWidgets.Widgets;

public static class WidgetFactory
{
    public static WidgetView Create(WidgetInstance inst, bool preview) => inst.Kind switch
    {
        WidgetKind.Clock => new ClockView(inst, preview),
        WidgetKind.Weather => new WeatherView(inst, preview),
        WidgetKind.Reminders => new RemindersView(inst, preview),
        WidgetKind.Stocks => new StocksView(inst, preview),
        WidgetKind.Photo => new PhotoView(inst, preview),
        WidgetKind.PhotoWide => new PhotoView(inst, preview),
        WidgetKind.Plan => new PlanView(inst, preview),
        WidgetKind.Notes => new NotesView(inst, preview),
        _ => throw new ArgumentOutOfRangeException(nameof(inst)),
    };
}
