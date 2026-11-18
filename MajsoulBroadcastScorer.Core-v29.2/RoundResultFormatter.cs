using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MajsoulBroadcastScorer.Core.V27;

public sealed record RoundReportRow(string Header, string ResultText, string HandText, string FanText, string ScoreText);

public static class RoundResultFormatter
{
    public static RoundReportRow CreateRow(JsonElement result, IReadOnlyList<RoundReportPlayer> players)
    {
        var wins = Array(result, "winners");
        var outcome = Outcome(result);
        var header = $"{RoundLabel(result)} · {Value(result, "ben")} 本场 · {outcome}";
        var resultLines = new List<string>();
        var handLines = new List<string>();
        var fanLines = new List<string>();
        foreach (var win in wins)
        {
            var name = PlayerName(win, "seat", players);
            var tsumo = Boolean(win, "isTsumo");
            var method = tsumo == true ? "自摸" : tsumo == false
                ? $"荣和；放铳：{PlayerName(win, "fromSeat", players)}" : "和牌方式未提供";
            resultLines.Add($"和牌：{name}（{method}）　{PointText(win)}");
            handLines.Add($"{name}　手牌：{Tiles(win, "tiles")}；副露：{Tiles(win, "melds")}；和牌牌：{Value(win, "winTile")}");
            var han = Boolean(win, "isYakuman") == true
                ? $"役满倍数：{Value(win, "yakumanCount")}" : $"{Value(win, "han")} 番";
            fanLines.Add($"{name}　{han} · {Value(win, "fu")} 符；役种：{Fans(win)}");
        }
        if (wins.Count == 0)
        {
            resultLines.Add(outcome == "和牌" ? "和牌者与和牌方式：未提供" : outcome);
            handLines.Add("和牌手牌：未提供");
            fanLines.Add("役种、番符：未提供");
        }
        return new(header, string.Join(Environment.NewLine, resultLines), string.Join(Environment.NewLine, handLines),
            string.Join(Environment.NewLine, fanLines),
            "四家点数变化：" + Scores(result, "deltaScores", players, true) + Environment.NewLine +
            "结算后点数：" + Scores(result, "scores", players, false));
    }

    public static string BuildText(DetailedMatchReport report)
    {
        var text = new StringBuilder();
        text.AppendLine("雀魂逐局对局记录");
        text.AppendLine("对局 ID：" + report.GameUuid);
        text.AppendLine("记录开始：" + report.StartedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        text.AppendLine("记录范围：" + report.Coverage);
        text.AppendLine("点数字段按接口原值保存；役种名称缺失时保留编号。结算点数是否包含本场或供托，不作额外推算。");
        foreach (var result in report.RoundResults)
        {
            var row = CreateRow(result, report.Players);
            text.AppendLine();
            text.AppendLine(row.Header);
            text.AppendLine(row.ResultText);
            text.AppendLine(row.HandText);
            text.AppendLine(row.FanText);
            text.AppendLine(row.ScoreText);
        }
        return text.ToString();
    }

    public static string BuildCsv(DetailedMatchReport report)
    {
        var text = new StringBuilder();
        text.AppendLine("对局ID,结算编号,场局,本场,结果,和牌者,放铳者,方式,结算点数(API原值),荣和点数,自摸庄家支付,自摸子家支付,番数,符数,役满倍数,役种,手牌,副露,和牌牌,四家点数变化,结算后点数,事件时间");
        foreach (var result in report.RoundResults)
        {
            var winners = Array(result, "winners");
            if (winners.Count == 0) WriteRow(default);
            else foreach (var winner in winners) WriteRow(winner);

            void WriteRow(JsonElement winner)
            {
                var tsumo = Boolean(winner, "isTsumo");
                string[] fields = [
                    report.GameUuid, Value(result, "key"), RoundLabel(result), Value(result, "ben"), Outcome(result),
                    PlayerName(winner, "seat", report.Players),
                    tsumo == true ? "—" : PlayerName(winner, "fromSeat", report.Players),
                    tsumo == true ? "自摸" : tsumo == false ? "荣和" : "未提供",
                    Value(winner, "points"), Value(winner, "pointRon"), Value(winner, "pointTsumoDealer"), Value(winner, "pointTsumoNonDealer"),
                    Value(winner, "han"), Value(winner, "fu"), Value(winner, "yakumanCount"), Fans(winner),
                    Tiles(winner, "tiles"), Tiles(winner, "melds"), Value(winner, "winTile"),
                    Scores(result, "deltaScores", report.Players, true), Scores(result, "scores", report.Players, false), EventTime(result)
                ];
                text.AppendLine(string.Join(',', fields.Select(Csv)));
            }
        }
        return text.ToString();
    }

    private static string RoundLabel(JsonElement result)
    {
        var chang = Integer(result, "chang");
        var ju = Integer(result, "ju");
        if (chang is not (>= 0 and <= 3) || ju is not (>= 0 and <= 3)) return "场局未提供";
        return WindText.FromCode(chang.Value) + new[] { "一", "二", "三", "四" }[ju.Value] + "局";
    }

    private static string Outcome(JsonElement result) => Value(result, "type") switch
    {
        "hule" => "和牌",
        "noTile" => Boolean(result, "nagashiMangan") == true ? "流局满贯" : "荒牌流局",
        "liuJu" => "途中流局",
        _ => "结算类型未提供"
    };

    private static string PointText(JsonElement winner)
    {
        var items = new List<string> { "结算点数（API原值）：" + Value(winner, "points") };
        if (Boolean(winner, "isTsumo") == true)
        {
            items.Add("庄家支付：" + Value(winner, "pointTsumoDealer"));
            items.Add("子家支付：" + Value(winner, "pointTsumoNonDealer"));
        }
        else if (Boolean(winner, "isTsumo") == false)
            items.Add("荣和点数：" + Value(winner, "pointRon"));
        return string.Join("；", items);
    }

    private static string PlayerName(JsonElement value, string property, IReadOnlyList<RoundReportPlayer> players)
    {
        var seat = Integer(value, property);
        if (seat is not (>= 0 and <= 3)) return "未提供";
        var name = players.FirstOrDefault(player => player.Seat == seat.Value)?.Nickname;
        return string.IsNullOrWhiteSpace(name) ? $"位置 {seat.Value + 1}" : name;
    }

    private static string Scores(JsonElement result, string property, IReadOnlyList<RoundReportPlayer> players, bool signed)
    {
        var values = Array(result, property);
        if (values.Count is not (3 or 4)) return "未提供";
        return string.Join("；", Enumerable.Range(0, values.Count).Select(seat =>
        {
            var name = players.FirstOrDefault(player => player.Seat == seat)?.Nickname;
            if (string.IsNullOrWhiteSpace(name)) name = $"位置 {seat + 1}";
            var value = values[seat];
            var points = value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
                ? number.ToString(signed ? "+0;-0;0" : "0", CultureInfo.InvariantCulture) : "未提供";
            return name + " " + points;
        }));
    }

    private static string Tiles(JsonElement element, string property)
    {
        var values = Array(element, property);
        if (values.Count == 0) return property == "melds" ? "无" : "未提供";
        return string.Join(" ", values.Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText()));
    }

    private static string Fans(JsonElement winner)
    {
        var values = Array(winner, "fans");
        if (values.Count == 0) return "未提供";
        return string.Join("、", values.Select(fan =>
        {
            var name = Value(fan, "name");
            if (name == "未提供") name = "役种编号 " + Value(fan, "id");
            var count = Value(fan, "value");
            return count == "未提供" ? name : name + "×" + count;
        }));
    }

    private static string EventTime(JsonElement result)
    {
        if (TryGet(result, "timestamp", out var timestamp) && timestamp.ValueKind == JsonValueKind.Number && timestamp.TryGetInt64(out var milliseconds))
        {
            try { return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture); }
            catch (ArgumentOutOfRangeException) { }
        }
        return "未提供";
    }

    private static string Value(JsonElement element, string property)
    {
        if (!TryGet(element, property, out var value)) return "未提供";
        return value.ValueKind switch
        {
            JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()) ? "未提供" : value.GetString()!,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "是",
            JsonValueKind.False => "否",
            _ => "未提供"
        };
    }

    private static int? Integer(JsonElement element, string property) =>
        TryGet(element, property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

    private static bool? Boolean(JsonElement element, string property) =>
        TryGet(element, property, out var value) ? value.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null } : null;

    private static IReadOnlyList<JsonElement> Array(JsonElement element, string property) =>
        TryGet(element, property, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToArray() : [];

    private static bool TryGet(JsonElement element, string property, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out value) &&
               value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
    }

    private static string Csv(string value) => CsvText.Escape(value);
}
