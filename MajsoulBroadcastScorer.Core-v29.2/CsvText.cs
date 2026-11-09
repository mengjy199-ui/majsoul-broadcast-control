namespace MajsoulBroadcastScorer.Core.V27;

public static class CsvText
{
    public static string Escape(string value)
    {
        var first = value.AsSpan().TrimStart();
        if (!first.IsEmpty && first[0] is '=' or '+' or '-' or '@')
            value = "'" + value;
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
