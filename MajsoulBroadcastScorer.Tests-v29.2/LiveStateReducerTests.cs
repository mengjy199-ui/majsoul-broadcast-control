using MajsoulBroadcastScorer.Core.V27;

namespace MajsoulBroadcastScorer.Tests.V27;

public sealed class LiveStateReducerTests
{
    [Fact]
    public void EmptyNickname_DoesNotErasePreviousName_ButUpdatesScoreAndWind()
    {
        var settings = new V27Settings();
        var reducer = new LiveStateReducer(settings);
        reducer.Apply(State(Player(0, "確定名", 25000, 0), Player(1), Player(2), Player(3)));
        reducer.Apply(State(Player(0, "", 27000, 2), Player(1, score: 23000), Player(2), Player(3)));

        var player = reducer.Current.Players[0];
        Assert.Equal("確定名", player.Nickname);
        Assert.Equal(27000, player.Score);
        Assert.Equal(2, player.Wind);
    }

    [Fact]
    public void ObserveNonNull_IsObservingEvenWhenModeIdle()
    {
        var reducer = new LiveStateReducer(new V27Settings());

        reducer.Apply(State(Player(0), Player(1), Player(2), Player(3), mode: "idle"));

        Assert.True(reducer.Current.IsObserving);
    }

    [Fact]
    public void ManualLocksRemainUntilRestored()
    {
        var settings = new V27Settings();
        var player = settings.Players[0];
        player.NameLocked = true;
        player.ManualName = "直播名";
        player.ScoreLocked = true;
        player.ManualScore = 32100;
        player.WindLocked = true;
        player.ManualWind = 3;
        var reducer = new LiveStateReducer(settings);
        reducer.Apply(State(Player(0, "API名", 25000, 0), Player(1), Player(2), Player(3)));

        Assert.Equal(("直播名", 32100, 3),
            (reducer.Current.Players[0].Nickname, reducer.Current.Players[0].Score, reducer.Current.Players[0].Wind));

        player.NameLocked = false;
        player.ScoreLocked = false;
        player.WindLocked = false;
        reducer.UpdateSettings(settings);

        Assert.Equal(("API名", 25000, 0),
            (reducer.Current.Players[0].Nickname, reducer.Current.Players[0].Score, reducer.Current.Players[0].Wind));
    }

    [Theory]
    [InlineData(false, RiichiDisplayMode.Auto, false)]
    [InlineData(true, RiichiDisplayMode.Auto, true)]
    [InlineData(false, RiichiDisplayMode.ForceOn, true)]
    [InlineData(true, RiichiDisplayMode.ForceOff, false)]
    public void RiichiPriority_IsDeterministic(bool api, RiichiDisplayMode mode, bool expected) =>
        Assert.Equal(expected, LiveStateReducer.ResolveRiichi(api, mode));

    [Fact]
    public void NullObserve_FreezesLastValidPlayers()
    {
        var reducer = new LiveStateReducer(new V27Settings());
        reducer.Apply(State(Player(0, "A", 26000), Player(1, score: 24000), Player(2), Player(3)));
        var before = reducer.Current.Players.Select(player => (player.Nickname, player.Score)).ToArray();

        reducer.Apply(new BroadcastApiState { Mode = "idle", Observe = null });

        Assert.False(reducer.Current.IsObserving);
        Assert.Equal(before, reducer.Current.Players.Select(player => (player.Nickname, player.Score)).ToArray());
    }

    [Fact]
    public void RiichiScoreChange_AppliesBeforeRiichiStickCatchesUp()
    {
        var reducer = new LiveStateReducer(new V27Settings());
        reducer.Apply(State(Player(0), Player(1), Player(2), Player(3)));

        var changed = State(
            Player(0, score: 24000), Player(1), Player(2), Player(3),
            riichiSticks: 0,
            serverTime: 1234567890);

        Assert.True(reducer.Apply(changed));
        Assert.Equal(24000, reducer.Current.Players[0].Score);
        Assert.True(reducer.Current.Players[0].Riichi);
        Assert.Equal(1234567890, reducer.Current.LastScoreUpdateUnixMs);
    }

    [Fact]
    public void EndedObserve_UsesFinalScoresBySeatInsteadOfRankingOrder()
    {
        var reducer = new LiveStateReducer(new V27Settings());
        reducer.Apply(State(Player(0), Player(1), Player(2), Player(3)));
        var ended = State(Player(0), Player(1), Player(2), Player(3));
        ended = new BroadcastApiState
        {
            Mode = ended.Mode,
            Observe = new BroadcastObserveState
            {
                GameUuid = ended.Observe!.GameUuid,
                Round = ended.Observe.Round,
                Players = ended.Observe.Players,
                Ended = true,
                FinalRanking =
                [
                    Final(1, 2, 40000),
                    Final(2, 0, 30000),
                    Final(3, 3, 20000),
                    Final(4, 1, 10000)
                ]
            }
        };

        Assert.True(reducer.Apply(ended));
        Assert.Equal([30000, 10000, 40000, 20000], reducer.Current.Players.Select(player => player.Score));
    }

    [Fact]
    public void InvalidFinalRanking_FreezesPreviousScores()
    {
        var reducer = new LiveStateReducer(new V27Settings());
        reducer.Apply(State(Player(0, score: 26000), Player(1, score: 24000), Player(2), Player(3)));
        var before = reducer.Current.Players.Select(player => player.Score).ToArray();
        var ended = new BroadcastApiState
        {
            Mode = "realtime",
            Observe = new BroadcastObserveState
            {
                GameUuid = "game-1",
                Round = new BroadcastRoundInfo { Chang = 1, Ju = 3, Ben = 0 },
                Players = [Player(0), Player(1), Player(2), Player(3)],
                Ended = true,
                FinalRanking = [Final(1, 0, 50000), Final(2, 1, 30000), Final(3, 2, 20000)]
            }
        };

        Assert.False(reducer.Apply(ended));
        Assert.Equal(before, reducer.Current.Players.Select(player => player.Score));
    }

    [Fact]
    public void TotalPointsToggle_OnlyChangesVisibilityFlag()
    {
        var settings = new V27Settings { ShowTotalPoints = false };
        settings.Players[0].TotalPoints = 12.3m;
        var reducer = new LiveStateReducer(settings);

        Assert.False(reducer.Current.ShowTotalPoints);
        Assert.Equal(12.3m, reducer.Current.Players[0].TotalPoints);
    }

    [Fact]
    public void TransparentAvatarMode_DoesNotDeleteStoredPath()
    {
        var settings = new V27Settings();
        settings.Players[0].AvatarPath = "assets/avatar-0.png";
        settings.Players[0].AvatarMode = AvatarDisplayMode.Transparent;
        var loaded = V27SettingsJson.Deserialize(V27SettingsJson.Serialize(settings));

        Assert.Equal(AvatarDisplayMode.Transparent, loaded.Players[0].AvatarMode);
        Assert.Equal("assets/avatar-0.png", loaded.Players[0].AvatarPath);
    }

    [Fact]
    public void TotalPointsDraft_DoesNotChangeSettingsUntilApplied()
    {
        var settings = new V27Settings();
        settings.Players[0].TotalPoints = 1.2m;
        var draft = new TotalPointsDraft(settings);

        draft.Set(0, 18.6m);

        Assert.Equal(1.2m, settings.Players[0].TotalPoints);
        Assert.True(draft.HasPendingChanges);
        Assert.Equal(18.6m, draft.Get(0));

        draft.Apply(settings);

        Assert.Equal(18.6m, settings.Players[0].TotalPoints);
        Assert.False(draft.HasPendingChanges);
    }

    [Fact]
    public void TotalPointsDraft_SetAllStagesFourRoundedValuesAtomically()
    {
        var settings = new V27Settings();
        settings.Players[0].TotalPoints = 1.2m;
        var draft = new TotalPointsDraft(settings);

        draft.SetAll(new Dictionary<int, decimal>
        {
            [0] = 12.34m,
            [1] = -4.56m,
            [2] = 7.89m,
            [3] = 0m
        });

        Assert.Equal(1.2m, settings.Players[0].TotalPoints);
        Assert.Equal([12.3m, -4.6m, 7.9m, 0m], Enumerable.Range(0, 4).Select(draft.Get));
        Assert.All(Enumerable.Range(0, 4), seat => Assert.True(draft.IsDirty(seat)));

        draft.Apply(settings);

        Assert.Equal([12.3m, -4.6m, 7.9m, 0m], settings.Players.Select(player => player.TotalPoints));
        Assert.False(draft.HasPendingChanges);
    }

    [Fact]
    public void TotalPointsDraft_SetAllRejectsIncompleteValuesWithoutWriting()
    {
        var settings = new V27Settings();
        var draft = new TotalPointsDraft(settings);

        Assert.Throws<ArgumentException>(() => draft.SetAll(new Dictionary<int, decimal>
        {
            [0] = 10m,
            [1] = 20m,
            [2] = 30m
        }));

        Assert.False(draft.HasPendingChanges);
        Assert.Equal([0m, 0m, 0m, 0m], Enumerable.Range(0, 4).Select(draft.Get));
    }

    [Fact]
    public void TotalPointsAndAccountBindingsFollowPlayersAcrossSeatRotation()
    {
        var settings = new V27Settings();
        var reducer = new LiveStateReducer(settings);
        reducer.Apply(State(
            Player(0, accountId: 101),
            Player(1, accountId: 102),
            Player(2, accountId: 103),
            Player(3, accountId: 104)), bindPlayerTotals: true);
        settings.Players[0].TotalPoints = 12.3m;
        settings.Players[1].TotalPoints = -4.6m;
        settings.Players[2].TotalPoints = 7.9m;
        settings.Players[3].TotalPoints = 0m;

        reducer.Apply(State(
            Player(0, accountId: 103),
            Player(1, accountId: 101),
            Player(2, accountId: 104),
            Player(3, accountId: 102),
            gameUuid: "game-2"), bindPlayerTotals: true);

        Assert.Equal(
            [(103L, 7.9m), (101L, 12.3m), (104L, 0m), (102L, -4.6m)],
            settings.Players.Select(player => (player.AccountId, player.TotalPoints)));
        Assert.Equal([7.9m, 12.3m, 0m, -4.6m],
            reducer.Current.Players.Select(player => player.TotalPoints));

        var loaded = V27SettingsJson.Deserialize(V27SettingsJson.Serialize(settings));
        Assert.Equal(
            [103L, 101L, 104L, 102L],
            loaded.Players.Select(player => player.AccountId));
    }

    [Fact]
    public void UnknownAccountDoesNotInheritPreviousPlayersTotalPoints()
    {
        var settings = new V27Settings();
        var reducer = new LiveStateReducer(settings);
        reducer.Apply(State(
            Player(0, accountId: 101),
            Player(1, accountId: 102),
            Player(2, accountId: 103),
            Player(3, accountId: 104)), bindPlayerTotals: true);
        settings.Players[0].TotalPoints = 25m;

        reducer.Apply(State(
            Player(0, accountId: 205),
            Player(1, accountId: 102),
            Player(2, accountId: 103),
            Player(3, accountId: 104),
            gameUuid: "game-2"), bindPlayerTotals: true);

        Assert.Equal(205, settings.Players[0].AccountId);
        Assert.Equal(0m, settings.Players[0].TotalPoints);
    }

    [Fact]
    public void TotalPointsDraft_RebindMovesPendingValuesToNewSeats()
    {
        var draft = new TotalPointsDraft(new V27Settings());
        draft.Set(0, 12.3m);

        draft.Rebind(new Dictionary<int, int> { [0] = 1, [1] = 0 });

        Assert.Equal(12.3m, draft.Get(1));
        Assert.False(draft.IsDirty(0));
        Assert.True(draft.IsDirty(1));
    }

    [Fact]
    public void PlayerPresentationDraft_DoesNotChangeSettingsUntilApplied()
    {
        var settings = new V27Settings();
        var player = settings.Players[0];
        player.ManualName = "旧昵称";
        player.ManualScore = 25000;
        player.AvatarPath = "assets/old.png";
        player.AvatarMode = AvatarDisplayMode.Transparent;
        player.AvatarFramePath = "assets/old-frame.png";
        player.AvatarFrameMode = AvatarFrameDisplayMode.Default;
        var draft = new PlayerPresentationDraft();

        draft.StageName(0, "确认昵称");
        draft.StageScore(0, 32100);
        draft.StageAvatarPath(0, "assets/new.png");
        draft.StageAvatarMode(0, AvatarDisplayMode.Image);
        draft.StageAvatarFramePath(0, "assets/new-frame.png");
        draft.StageAvatarFrameMode(0, AvatarFrameDisplayMode.Custom);
        draft.StageTeamName(0, "星海队");

        Assert.Equal("旧昵称", player.ManualName);
        Assert.Equal(25000, player.ManualScore);
        Assert.Equal("assets/old.png", player.AvatarPath);
        Assert.Equal(AvatarDisplayMode.Transparent, player.AvatarMode);
        Assert.Equal("assets/old-frame.png", player.AvatarFramePath);
        Assert.Equal(AvatarFrameDisplayMode.Default, player.AvatarFrameMode);
        Assert.Equal(string.Empty, player.TeamName);
        Assert.True(draft.HasPendingChanges);

        draft.Apply(settings);

        Assert.True(player.NameLocked);
        Assert.Equal("确认昵称", player.ManualName);
        Assert.True(player.ScoreLocked);
        Assert.Equal(32100, player.ManualScore);
        Assert.Equal("assets/new.png", player.AvatarPath);
        Assert.Equal(AvatarDisplayMode.Image, player.AvatarMode);
        Assert.Equal("assets/new-frame.png", player.AvatarFramePath);
        Assert.Equal(AvatarFrameDisplayMode.Custom, player.AvatarFrameMode);
        Assert.Equal("星海队", player.TeamName);
        Assert.False(draft.HasPendingChanges);
    }

    [Fact]
    public void PlayerPresentationDraft_RestoreApiWaitsForApply()
    {
        var settings = new V27Settings();
        settings.Players[0].NameLocked = true;
        settings.Players[0].ScoreLocked = true;
        var draft = new PlayerPresentationDraft();

        draft.StageRestoreName(0);
        draft.StageRestoreScore(0);

        Assert.True(settings.Players[0].NameLocked);
        Assert.True(settings.Players[0].ScoreLocked);

        draft.Apply(settings);

        Assert.False(settings.Players[0].NameLocked);
        Assert.False(settings.Players[0].ScoreLocked);
    }

    private static BroadcastPlayerState Player(
        int seat,
        string? name = null,
        int score = 25000,
        int wind = -1,
        long accountId = 0) => new()
        {
            Seat = seat,
            AccountId = accountId,
            Nickname = name ?? $"P{seat}",
            Score = score,
            Wind = wind,
            Riichi = seat == 0
        };

    private static BroadcastApiState State(
        BroadcastPlayerState p0,
        BroadcastPlayerState p1,
        BroadcastPlayerState p2,
        BroadcastPlayerState p3,
        string mode = "realtime",
        int? riichiSticks = null,
        long serverTime = 0,
        string gameUuid = "game-1")
    {
        var players = new[] { p0, p1, p2, p3 };
        var missing = 100000 - players.Sum(player => player.Score);
        return new BroadcastApiState
        {
            Mode = mode,
            ServerTime = serverTime,
            Observe = new BroadcastObserveState
            {
                GameUuid = gameUuid,
                Round = new BroadcastRoundInfo { Chang = 0, Ju = 0, Ben = 0 },
                RiichiSticks = riichiSticks ?? missing / 1000,
                Players = players
            }
        };
    }

    private static BroadcastFinalRankingState Final(int rank, int seat, int score) => new()
    {
        Rank = rank,
        Seat = seat,
        AccountId = seat + 1,
        Nickname = $"P{seat}",
        TotalPoint = score
    };
}
