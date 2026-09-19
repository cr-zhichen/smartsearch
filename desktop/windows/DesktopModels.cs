using System.Diagnostics;

namespace SmartSearch.Desktop;

internal sealed record BackendEvent(string Name, System.Text.Json.JsonElement Data);

internal sealed class BackendRpcException(string code, string? message) : Exception(
    string.IsNullOrWhiteSpace(message) ? $"后端拒绝了请求（{code}）。" : message)
{
    public string Code { get; } = code;
}

internal sealed class BackendDisconnectedException(string reason) : Exception(reason);

internal sealed record CommandArgument(
    string Name,
    IReadOnlyList<string> Flags,
    bool IsBoolean,
    bool Multiple,
    bool Required);

internal sealed record CommandValue(string? Text, bool IsChecked = false);

internal static class ControlValueComparer
{
    public static bool Equal(CommandValue first, CommandValue second) =>
        first.IsChecked == second.IsChecked && string.Equals(first.Text?.Trim(), second.Text?.Trim(), StringComparison.Ordinal);
}

internal static class ProtocolArguments
{
    public static IReadOnlyList<string> Build(
        IEnumerable<CommandArgument> fields,
        Func<string, CommandValue> valueFor)
    {
        var result = new List<string>();
        foreach (var field in fields)
        {
            var value = valueFor(field.Name);
            if (field.IsBoolean)
            {
                if (value.IsChecked && field.Flags.Count > 0)
                    result.Add(field.Flags[0]);
                continue;
            }

            var values = (value.Text ?? string.Empty)
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (values.Length == 0)
                continue;

            if (field.Flags.Count == 0)
            {
                result.AddRange(field.Multiple ? values : values.Take(1));
                continue;
            }

            foreach (var item in field.Multiple ? values : values.Take(1))
            {
                result.Add(field.Flags[0]);
                result.Add(item);
            }
        }
        return result;
    }
}

internal static class ProtocolSelfTest
{
    [Conditional("DEBUG")]
    public static void Run()
    {
        var arguments = ProtocolArguments.Build(
            [
                new("query", [], false, false, true),
                new("limit", ["--limit"], false, false, false),
                new("verbose", ["--verbose"], true, false, false),
                new("tag", ["--tag"], false, true, false)
            ],
            name => name switch
            {
                "query" => new("Smart Search"),
                "limit" => new("5"),
                "verbose" => new(null, true),
                "tag" => new("docs\nweb"),
                _ => new(null)
            });
        Debug.Assert(arguments.SequenceEqual(["Smart Search", "--limit", "5", "--verbose", "--tag", "docs", "--tag", "web"]));
        Debug.Assert(ControlValueComparer.Equal(new CommandValue(null, false), new CommandValue(null, false)));
        Debug.Assert(!ControlValueComparer.Equal(new CommandValue(null, false), new CommandValue(null, true)));
    }
}
