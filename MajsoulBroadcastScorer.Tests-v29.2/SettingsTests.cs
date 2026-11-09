using MajsoulBroadcastScorer.Core.V27;

namespace MajsoulBroadcastScorer.Tests.V27;

public sealed class SettingsTests
{
    [Fact]
    public void SettingsRoundTrip_PreservesOverridesAndFixedSeatOrder()
    {
        var settings = new V27Settings
        {
            Department = "赛事部",
            MatchText = "决赛第2回战",
            OverlayStyle = OverlayLayoutStyle.VerticalAvatar
        };
        settings.Players.Reverse();
        settings.Players[0].NameLocked = true;
        settings.Players[0].ManualName = "手动名";
        settings.Players[0].AvatarFrameMode = AvatarFrameDisplayMode.Custom;
        settings.Players[0].AvatarFramePath = "assets/frame.png";
        settings.Players[0].TeamName = "青空队";

        var loaded = V27SettingsJson.Deserialize(V27SettingsJson.Serialize(settings));

        Assert.Equal([0, 1, 2, 3], loaded.Players.Select(player => player.Seat));
        Assert.Contains(loaded.Players, player => player.NameLocked && player.ManualName == "手动名");
        Assert.Contains(loaded.Players, player =>
            player.AvatarFrameMode == AvatarFrameDisplayMode.Custom &&
            player.AvatarFramePath == "assets/frame.png");
        Assert.Contains(loaded.Players, player => player.TeamName == "青空队");
        Assert.Equal("赛事部", loaded.Department);
        Assert.Equal(OverlayLayoutStyle.VerticalAvatar, loaded.OverlayStyle);
        Assert.Equal(292, loaded.Version);
    }

    [Fact]
    public void SettingsRoundTrip_PreservesTeamMode()
    {
        var loaded = V27SettingsJson.Deserialize(V27SettingsJson.Serialize(new V27Settings
        {
            OverlayStyle = OverlayLayoutStyle.TeamMode
        }));

        Assert.Equal(OverlayLayoutStyle.TeamMode, loaded.OverlayStyle);
    }

    [Fact]
    public void InvalidOrDuplicatePlayerEntries_AreNormalized()
    {
        var settings = V27SettingsJson.Deserialize("""
        {"version":27,"players":[{"seat":2},{"seat":2},{"seat":9}]}
        """);

        Assert.Equal([0, 1, 2, 3], settings.Players.Select(player => player.Seat));
    }

    [Fact]
    public void AssetReset_RestoresOriginalPositionsAndBottomLeftFrame()
    {
        var settings = new V27Settings();
        settings.LogoLayout.X = 900;
        settings.LogoLayout.Y = 500;
        settings.LogoLayout.Width = 420;
        settings.BottomLeftLayout.X = 600;
        settings.BottomLeftLayout.Y = 400;
        settings.BottomLeftLayout.Width = 500;
        settings.LeftBoardFocusX = 3;
        settings.LeftBoardFocusY = 91;

        settings.ResetLogoPosition();
        settings.ResetBottomLeftPosition();
        settings.ResetLeftBoardFocus();

        Assert.Equal(35, settings.LogoLayout.X);
        Assert.Equal(32, settings.LogoLayout.Y);
        Assert.Equal(420, settings.LogoLayout.Width);
        Assert.Equal(0, settings.BottomLeftLayout.X);
        Assert.Equal(856, settings.BottomLeftLayout.Y);
        Assert.Equal(370, settings.BottomLeftLayout.Width);
        Assert.Equal(224, settings.BottomLeftLayout.Height);
        Assert.False(settings.BottomLeftLayout.LockAspectRatio);
        Assert.Equal(50, settings.LeftBoardFocusX);
        Assert.Equal(50, settings.LeftBoardFocusY);
    }

    [Fact]
    public void LegacySettings_MigrateToV28AndKeepHorizontalStyle()
    {
        var settings = V27SettingsJson.Deserialize("""
        {
          "version":27,
          "bottomLeftLayout":{"x":20,"y":700,"width":500,"height":180,"lockAspectRatio":true}
        }
        """);

        Assert.Equal(292, settings.Version);
        Assert.Equal(OverlayLayoutStyle.HorizontalPlayers, settings.OverlayStyle);
        Assert.False(settings.BottomLeftLayout.LockAspectRatio);
        Assert.Equal(500, settings.BottomLeftLayout.Width);
        Assert.Equal(180, settings.BottomLeftLayout.Height);
    }

    [Fact]
    public void LeftBoardFocus_IsClampedAndRoundTrips()
    {
        var settings = new V27Settings { LeftBoardFocusX = -12, LeftBoardFocusY = 140 };

        var loaded = V27SettingsJson.Deserialize(V27SettingsJson.Serialize(settings));

        Assert.Equal(0, loaded.LeftBoardFocusX);
        Assert.Equal(100, loaded.LeftBoardFocusY);
    }

    [Fact]
    public void SettingsRoundTrip_PreservesGapConditionsStyle()
    {
        var loaded = V27SettingsJson.Deserialize(V27SettingsJson.Serialize(new V27Settings
        {
            OverlayStyle = OverlayLayoutStyle.GapConditions,
            GapSelectedSeat = 3,
            GapTargetMode = ReversalTargetMode.FirstPlace
        }));

        Assert.Equal(OverlayLayoutStyle.GapConditions, loaded.OverlayStyle);
        Assert.Equal(3, loaded.GapSelectedSeat);
        Assert.Equal(ReversalTargetMode.FirstPlace, loaded.GapTargetMode);
        Assert.Equal(292, loaded.Version);
    }

    [Fact]
    public void InvalidGapSelection_IsNormalized()
    {
        var loaded = V27SettingsJson.Deserialize("""
        {"version":283,"gapSelectedSeat":99,"gapTargetMode":99}
        """);

        Assert.Equal(3, loaded.GapSelectedSeat);
        Assert.Equal(ReversalTargetMode.PreviousRank, loaded.GapTargetMode);
    }
}
