using MajsoulBroadcastScorer.Core.V27;

namespace MajsoulBroadcastScorer.Tests.V27;

public sealed class FrierenStyleTests
{
    [Fact]
    public void NewStyleRoundTripsAndPublishesWithoutChangingPlayerData()
    {
        var settings = new V27Settings { OverlayStyle = OverlayLayoutStyle.FrierenVerticalAvatar };
        settings.Players[2].NameLocked = true;
        settings.Players[2].ManualName = "示例选手";
        settings.Players[2].TotalPoints = 12.3m;
        var loaded = V27SettingsJson.Deserialize(V27SettingsJson.Serialize(settings));
        Assert.Equal(OverlayLayoutStyle.FrierenVerticalAvatar, loaded.OverlayStyle);
        Assert.Equal("示例选手", loaded.Players[2].ManualName);
        Assert.Equal(12.3m, loaded.Players[2].TotalPoints);
        var publication = new OverlayStylePublication(OverlayLayoutStyle.VerticalAvatar);
        publication.Select(loaded.OverlayStyle);
        Assert.Equal(OverlayLayoutStyle.FrierenVerticalAvatar, publication.PreviewStyle);
        Assert.Equal(OverlayLayoutStyle.FrierenVerticalAvatar, publication.PublishedStyle);
    }

    [Theory]
    [InlineData(0, OverlayLayoutStyle.HorizontalPlayers)]
    [InlineData(1, OverlayLayoutStyle.VerticalAvatar)]
    [InlineData(2, OverlayLayoutStyle.GapConditions)]
    [InlineData(3, OverlayLayoutStyle.TeamMode)]
    [InlineData(4, OverlayLayoutStyle.VerticalMatch)]
    public void ExistingNumericSettingsKeepTheirLayout(int storedValue, OverlayLayoutStyle expected)
    {
        var loaded = V27SettingsJson.Deserialize("{\"overlayStyle\":" + storedValue + "}");
        Assert.Equal(expected, loaded.OverlayStyle);
    }
}
