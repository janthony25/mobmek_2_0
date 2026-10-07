using System.Text.RegularExpressions;

namespace MobmekApi.Services;

/// <summary>
/// Deterministic <c>{{Token}}</c> substitution for email template wording — no scripting, no
/// conditionals. A token with no matching entry (or a null value) renders as an empty string
/// rather than erroring, so a template can reference a token that happens not to apply (e.g.
/// <c>{{CarPlate}}</c> on a reminder with no linked car).
/// </summary>
public static partial class EmailTemplateRenderer
{
    public static string Render(string template, IReadOnlyDictionary<string, string?> tokens) =>
        TokenPattern().Replace(template, match => tokens.GetValueOrDefault(match.Groups[1].Value) ?? "");

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex TokenPattern();
}
