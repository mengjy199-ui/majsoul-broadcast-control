namespace MajsoulBroadcastScorer.Core.V27;

public sealed record VerticalCardContent(VerticalNameContent Name, VerticalNumberContent Number)
{
    public string NameKey => Name == VerticalNameContent.TeamName ? "teamName" : "nickname";
    public string NumberKey => Number == VerticalNumberContent.CurrentScore ? "score" : "totalPoints";
}

public sealed class VerticalCardPublication
{
    public VerticalCardPublication(V27Settings settings)
    {
        Published = Preview = new(settings.VerticalName, settings.VerticalNumber);
    }

    public VerticalCardContent Published { get; private set; }
    public VerticalCardContent Preview { get; private set; }
    public bool HasPendingChanges => Published != Preview;

    public void Select(VerticalNameContent name, VerticalNumberContent number) => Preview = new(name, number);

    public void Confirm(V27Settings settings)
    {
        Published = Preview;
        settings.VerticalName = Published.Name;
        settings.VerticalNumber = Published.Number;
    }
}
