using System.Text.Json;
using MajsoulBroadcastScorer.Core.V27;

namespace MajsoulBroadcastScorer.Tests.V27;

public sealed class VerticalMatchTests
{
    private static BroadcastObserveState Observe(string game = "g1", string? team = "API队伍", long account = 10) => new()
    {
        GameUuid = game,
        Players = Enumerable.Range(0, 4).Select(seat => new BroadcastPlayerState
        {
            Seat = seat, Wind = seat, AccountId = account + seat, Nickname = $"P{seat}",
            TeamName = seat == 0 ? team : $"T{seat}", Score = 25000
        }).ToArray()
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Protocol_ReadsTeamNameFromSnapshotAndObserveEvent(bool asEvent)
    {
        var observe = JsonSerializer.Serialize(Observe(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var json = asEvent
            ? "{\"type\":\"event\",\"event\":\"observe\",\"data\":" + observe + "}"
            : "{\"type\":\"snapshot\",\"data\":{\"mode\":\"idle\",\"observe\":" + observe + "}}";

        var message = BroadcastApiProtocol.Parse(json);

        Assert.Equal("API队伍", message.Observe!.Players[0].TeamName);
    }

    [Fact]
    public void TeamOverride_IsStagedAndCanReturnToLatestApiValue()
    {
        var settings = new V27Settings();
        var reducer = new LiveStateReducer(settings);
        reducer.Apply(new() { Observe = Observe() });
        var draft = new PlayerPresentationDraft();
        draft.StageTeamName(0, "手动队伍");
        Assert.Equal("API队伍", reducer.Current.Players[0].TeamName);

        draft.Apply(settings);
        reducer.Apply(new() { Observe = Observe(team: "API新队伍") });
        Assert.Equal("手动队伍", reducer.Current.Players[0].TeamName);
        draft.StageRestoreTeam(0);
        Assert.True(settings.Players[0].TeamNameLocked);
        draft.Apply(settings);

        Assert.False(settings.Players[0].TeamNameLocked);
        Assert.Equal("API新队伍", reducer.Current.Players[0].TeamName);
    }

    [Fact]
    public void EmptyTeam_DoesNotFlickerButCannotLeakToAnotherGameOrPlayer()
    {
        var reducer = new LiveStateReducer(new V27Settings());
        reducer.Apply(new() { Observe = Observe() });
        reducer.Apply(new() { Observe = Observe(team: "") });
        Assert.Equal("API队伍", reducer.Current.Players[0].TeamName);
        reducer.Apply(new() { Observe = Observe(team: null, account: 90) });
        Assert.Equal(string.Empty, reducer.Current.Players[0].TeamName);
        reducer.Apply(new() { Observe = Observe(team: "新队伍", account: 90) });
        reducer.Apply(new() { Observe = Observe(game: "g2", team: "", account: 90) });
        Assert.Equal(string.Empty, reducer.Current.Players[0].TeamName);
    }

    [Fact]
    public void ContentSelection_WaitsForConfirmation_AndPersistsBothChoices()
    {
        var settings = new V27Settings();
        var publication = new VerticalCardPublication(settings);
        Assert.Equal(("teamName", "score"), (publication.Published.NameKey, publication.Published.NumberKey));
        publication.Select(VerticalNameContent.PlayerName, VerticalNumberContent.CalculationPoints);
        Assert.True(publication.HasPendingChanges);
        Assert.Equal(VerticalNumberContent.CurrentScore, settings.VerticalNumber);
        Assert.Equal("score", publication.Published.NumberKey);

        publication.Confirm(settings);
        var loaded = V27SettingsJson.Deserialize(V27SettingsJson.Serialize(settings));

        Assert.False(publication.HasPendingChanges);
        Assert.Equal(VerticalNameContent.PlayerName, loaded.VerticalName);
        Assert.Equal(VerticalNumberContent.CalculationPoints, loaded.VerticalNumber);
    }

    [Fact]
    public void NewStyle_WaitsForConfirmation()
    {
        var publication = new OverlayStylePublication(OverlayLayoutStyle.VerticalAvatar);
        Assert.False(publication.Select(OverlayLayoutStyle.VerticalMatch));
        Assert.Equal(OverlayLayoutStyle.VerticalAvatar, publication.PublishedStyle);
        Assert.True(publication.Confirm());
        Assert.Equal(OverlayLayoutStyle.VerticalMatch, publication.PublishedStyle);
    }

    [Fact]
    public void LegacyManualTeamsArePreservedAndInvalidContentModesRecover()
    {
        var settings = V27SettingsJson.Deserialize("""{"version":291,"verticalName":999,"verticalNumber":999,"players":[{"seat":0,"teamName":"旧队伍"}]}""");
        Assert.True(settings.Players[0].TeamNameLocked);
        Assert.Equal(VerticalNameContent.TeamName, settings.VerticalName);
        Assert.Equal(VerticalNumberContent.CurrentScore, settings.VerticalNumber);
    }
}
