namespace MajsoulBroadcastScorer.Core.V27;

public sealed class OverlayStylePublication
{
    public OverlayStylePublication(OverlayLayoutStyle initialStyle)
    {
        PublishedStyle = initialStyle;
        PreviewStyle = initialStyle;
    }

    public OverlayLayoutStyle PublishedStyle { get; private set; }
    public OverlayLayoutStyle PreviewStyle { get; private set; }
    public bool HasPendingPublication => PreviewStyle != PublishedStyle;

    public bool Select(OverlayLayoutStyle style)
    {
        PreviewStyle = style;
        if ((style is OverlayLayoutStyle.GapConditions or OverlayLayoutStyle.TeamMode or OverlayLayoutStyle.VerticalMatch) && PublishedStyle != style)
            return false;

        var changed = PublishedStyle != style;
        PublishedStyle = style;
        return changed;
    }

    public bool Confirm()
    {
        if (!HasPendingPublication)
            return false;
        PublishedStyle = PreviewStyle;
        return true;
    }
}
