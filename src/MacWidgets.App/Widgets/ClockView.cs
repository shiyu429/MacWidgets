using MacWidgets.Data;

namespace MacWidgets.Widgets;

public sealed class ClockView : WidgetView
{
    private readonly ClockFace _face = new();

    public ClockView(WidgetInstance inst, bool preview) : base(inst, preview)
    {
        Content = _face;
        Refresh();
    }

    public override void Refresh()
    {
        _face.Hour24 = Instance.Options.Clock24;
        _face.Sweep = Instance.Options.ClockSweep;
        _face.Smooth = Store.Settings.ClockSmooth;
        _face.UpdateTimer();
    }
}
