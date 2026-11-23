using Brows.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Brows.Support;

/// <summary>
/// Reads the texts of the feature from its culture resource, the way the application reads them.
/// </summary>
/// <remarks>
/// The application builds its translation from the culture resource of every component, so a test
/// that shows the user interface of the feature needs a translation of its own to see the texts
/// that the user sees.
/// </remarks>
internal sealed class MBoxTranslation : ITranslationExport {
    private static readonly Regex HelpPattern = new(@"(?<=.+\.Resource\.Culture\._\.s\.)(.+)");

    private readonly Dictionary<string, string> Values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string[]> Aliases = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxTranslation"/> class.
    /// </summary>
    public MBoxTranslation() {
        var assembly = typeof(MBoxProvider).Assembly;
        foreach (var pair in Read(assembly, extension: "")) {
            Values[pair.Key] = pair.Value;
        }
        foreach (var pair in Read(assembly, extension: ".alias")) {
            if (Aliases.TryGetValue(pair.Key, out var aliases)) {
                Aliases[pair.Key] = [.. aliases, pair.Value];
            }
            else {
                Aliases[pair.Key] = [pair.Value];
            }
        }
        foreach (var name in assembly.GetManifestResourceNames()) {
            var match = HelpPattern.Match(name);
            if (match.Success) {
                Values[match.Groups[1].Value] = ReadText(assembly, name);
            }
        }
    }

    /// <summary>
    /// Returns the text of the specified key, or <see langword="null"/> when the key has no text.
    /// </summary>
    /// <param name="key">
    /// The key of the text.
    /// </param>
    /// <returns>
    /// The text of the key.
    /// </returns>
    public string Value(string key) {
        return Values.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Returns the aliases of the specified key.
    /// </summary>
    /// <param name="key">
    /// The key of the text.
    /// </param>
    /// <returns>
    /// The aliases of the key.
    /// </returns>
    public string[] Alias(string key) {
        return Aliases.TryGetValue(key, out var value) ? value : [];
    }

    private static IEnumerable<KeyValuePair<string, string>> Read(Assembly assembly, string extension) {
        var text = ReadText(assembly, extension);
        if (text == null) {
            return [];
        }
        return text
            .Split('\r', '\n')
            .Select(line => line.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .Select(pair => KeyValuePair.Create(pair[0].Trim(), pair[1].Trim()));
    }

    private static string ReadText(Assembly assembly, string extension) {
        var name = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(resourceName => resourceName.EndsWith($".Resource.Culture._{extension}",
                                                                   StringComparison.Ordinal));
        if (name == null) {
            return null;
        }
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream == null) {
            return null;
        }
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
