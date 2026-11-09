using MajsoulBroadcastScorer.Core.V27;

namespace MajsoulBroadcastScorer.Tests.V27;

public sealed class BroadcastApiProtocolTests
{
    [Fact]
    public void Snapshot_ObserveWinsOverIdleMode_AndReadsRiichiSticks()
    {
        var message = BroadcastApiProtocol.Parse(SnapshotJson("idle", 2));

        Assert.Equal(BroadcastMessageKind.Snapshot, message.Kind);
        Assert.Equal("idle", message.Mode);
        Assert.NotNull(message.Observe);
        Assert.Equal(2, message.Observe!.RiichiSticks);
        Assert.Equal("东2局", message.Observe.Round.Label);
    }

    [Fact]
    public void ObserveEvent_AllowsEmptyNickname()
    {
        var json = $$"""
        { "type":"event", "event":"observe", "time":2, "data":{{ObserveJson(0, "")}} }
        """;

        var message = BroadcastApiProtocol.Parse(json);

        Assert.Equal(string.Empty, message.Observe!.Players[0].Nickname);
    }

    [Fact]
    public void TransientTotalMismatchDuringRiichi_IsAccepted()
    {
        var json = SnapshotJson("realtime", 1).Replace("24000", "23000");

        var message = BroadcastApiProtocol.Parse(json);

        Assert.Equal(23000, message.Observe!.Players[0].Score);
        Assert.Equal(1, message.Observe.RiichiSticks);
    }

    [Fact]
    public void InvalidNonHundredScore_IsStillRejected()
    {
        var json = SnapshotJson("realtime", 1).Replace("24000", "24050");

        Assert.Throws<InvalidDataException>(() => BroadcastApiProtocol.Parse(json));
    }

    [Fact]
    public void EndedObserve_ReadsFinalRankingAndMapsScoresBySeat()
    {
        var json = $$"""
        { "type":"event", "event":"observe", "time":2, "data":{{EndedObserveJson()}} }
        """;

        var message = BroadcastApiProtocol.Parse(json);
        var valid = BroadcastApiProtocol.TryGetFinalScores(message.Observe!, out var scores);

        Assert.True(message.Observe!.Ended);
        Assert.True(valid);
        Assert.Equal([30000, 10000, 40000, 20000], scores);
    }

    [Fact]
    public void IncompleteFinalRanking_IsNotAcceptedAsFinalScores()
    {
        var incompleteObserve = RemoveFourthFinalRanking(EndedObserveJson());
        var json = $$"""
        { "type":"event", "event":"observe", "time":2, "data":{{incompleteObserve}} }
        """;

        var message = BroadcastApiProtocol.Parse(json);

        Assert.False(BroadcastApiProtocol.TryGetFinalScores(message.Observe!, out _));
    }

    private static string RemoveFourthFinalRanking(string json)
    {
        const string fourth = "{\"rank\":4,\"seat\":1,\"accountId\":2,\"nickname\":\"Bob\",\"totalPoint\":10000}";
        var itemStart = json.IndexOf(fourth, StringComparison.Ordinal);
        var commaStart = json.LastIndexOf(',', itemStart);
        Assert.True(itemStart >= 0 && commaStart >= 0);
        return json.Remove(commaStart, itemStart + fourth.Length - commaStart);
    }

    private static string SnapshotJson(string mode, int riichiSticks) => $$"""
    {
      "type":"snapshot",
      "time":1,
      "data":{
        "mode":"{{mode}}",
        "auth":{"connection":"connected","account":null},
        "observe":{{ObserveJson(riichiSticks, "Alice")}}
      }
    }
    """;

    private static string ObserveJson(int riichiSticks, string firstName)
    {
        var firstScore = 25000 - riichiSticks * 1000;
        return $$"""
        {
          "gameUuid":"game-1",
          "round":{"chang":0,"ju":1,"ben":3},
          "currentSeat":2,
          "waitingSeconds":0,
          "riichiSticks":{{riichiSticks}},
          "players":[
            {"seat":0,"wind":1,"accountId":1,"nickname":"{{firstName}}","score":{{firstScore}},"riichi":true,"tenpai":false},
            {"seat":1,"wind":2,"accountId":2,"nickname":"Bob","score":25000,"riichi":false,"tenpai":false},
            {"seat":2,"wind":3,"accountId":3,"nickname":"Carol","score":25000,"riichi":false,"tenpai":false},
            {"seat":3,"wind":0,"accountId":4,"nickname":"Dave","score":25000,"riichi":false,"tenpai":false}
          ]
        }
        """;
    }

    private static string EndedObserveJson() => """
    {
      "gameUuid":"game-1",
      "round":{"chang":1,"ju":3,"ben":0},
      "currentSeat":-1,
      "waitingSeconds":0,
      "riichiSticks":0,
      "players":[
        {"seat":0,"wind":0,"accountId":1,"nickname":"Alice","score":25000,"riichi":false,"tenpai":false},
        {"seat":1,"wind":1,"accountId":2,"nickname":"Bob","score":25000,"riichi":false,"tenpai":false},
        {"seat":2,"wind":2,"accountId":3,"nickname":"Carol","score":25000,"riichi":false,"tenpai":false},
        {"seat":3,"wind":3,"accountId":4,"nickname":"Dave","score":25000,"riichi":false,"tenpai":false}
      ],
      "ended":true,
      "finalRanking":[
        {"rank":1,"seat":2,"accountId":3,"nickname":"Carol","totalPoint":40000},
        {"rank":2,"seat":0,"accountId":1,"nickname":"Alice","totalPoint":30000},
        {"rank":3,"seat":3,"accountId":4,"nickname":"Dave","totalPoint":20000},
        {"rank":4,"seat":1,"accountId":2,"nickname":"Bob","totalPoint":10000}
      ]
    }
    """;
}
