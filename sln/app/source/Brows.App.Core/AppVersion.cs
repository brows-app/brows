using System;
using System.Text.RegularExpressions;

namespace Brows;

/// <summary>
/// Represents a Brows release version with four numeric components and an optional pre-release stage.
/// </summary>
public sealed partial record AppVersion {
    [GeneratedRegex(@"\Av?(?<text>(?<core>[0-9]+\.[0-9]+\.[0-9]+)(?:-(?<stage>[A-Za-z0-9]+))?\.(?<revision>[0-9]+))\z")]
    private static partial Regex PatternRegex();

    private static readonly Regex Pattern = PatternRegex();

    private AppVersion(string text, string stage, Version number) {
        Text = text;
        Stage = stage;
        Number = number;
    }

    /// <summary>
    /// Gets the release identity without the optional leading v or build metadata.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the pre-release stage, or null for a stable release.
    /// </summary>
    public string Stage { get; }

    /// <summary>
    /// Gets the major, minor, build, and revision components used to order releases.
    /// </summary>
    public Version Number { get; }

    /// <summary>
    /// Compares the four numeric components, ignoring pre-release stages.
    /// </summary>
    /// <param name="other">
    /// The non-null release version to compare against.
    /// </param>
    /// <returns>
    /// True if this version has a greater numeric version, otherwise false.
    /// </returns>
    public bool IsNewerThan(AppVersion other) {
        return Number > other.Number;
    }

    /// <summary>
    /// Removes build metadata beginning at the first plus sign.
    /// </summary>
    /// <param name="version">
    /// The version text, or null.
    /// </param>
    /// <returns>
    /// The text before the first plus sign, the original text if none is present, or null for null input.
    /// </returns>
    public static string RemoveMetadata(string version) {
        var separator = version?.IndexOf('+') ?? -1;
        return separator < 0 ? version : version[..separator];
    }

    /// <summary>
    /// Parses a Brows release version, accepting an optional leading v and ignoring build metadata.
    /// </summary>
    /// <param name="text">
    /// A version such as 1.2.3.4 or 1.2.3-beta.4, with an alphanumeric pre-release stage when present.
    /// </param>
    /// <param name="version">
    /// The parsed version on success, or null when the input is null, malformed, or outside the numeric range.
    /// </param>
    /// <returns>
    /// True if all four numeric components and the optional stage can be parsed, otherwise false.
    /// </returns>
    public static bool TryParse(string text, out AppVersion version) {
        version = null;
        if (text is null) {
            return false;
        }
        var match = Pattern.Match(RemoveMetadata(text));
        if (!match.Success) {
            return false;
        }
        var groups = match.Groups;
        var identity = groups["text"].Value;
        var stage = groups["stage"];
        var numeric = stage.Success ? $"{groups["core"].Value}.{groups["revision"].Value}" : identity;
        if (!Version.TryParse(numeric, out var number)) {
            return false;
        }
        version = new(identity, stage.Success ? stage.Value : null, number);
        return true;
    }
}
