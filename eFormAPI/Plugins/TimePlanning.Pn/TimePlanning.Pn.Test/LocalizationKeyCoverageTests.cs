using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Every message key the plugin hands to <c>ITimePlanningLocalizationService.GetString</c>
/// must be translated. That service reads only the embedded
/// <c>Resources/localization.json</c> (BasePn's JsonStringLocalizer), not the
/// <c>Translations*.resx</c> files, and a key missing there comes back as the raw
/// key: the web UI and the mobile app then show e.g. "UnauthorizedToCreateAbsenceRequest".
/// This fixture scans the plugin's sources for the keys and fails listing any key,
/// or any language of a key, that localization.json does not have.
/// </summary>
[TestFixture]
public class LocalizationKeyCoverageTests
{
    private const string LocalizationResourceName = "TimePlanning.Pn.Resources.localization.json";

    /// <summary>The languages the plugin ships, as named in localization.json.</summary>
    private static readonly string[] Languages =
    [
        "da", "de", "en-US", "uk-UA", "pl-PL", "no-NO", "sv-SE", "es-ES", "fr-FR", "it-IT",
        "nl-NL", "pt-BR", "pt-PT", "fi-FI", "et-EE", "lv-LV", "lt-LT", "ro-RO", "hr-HR",
        "sl-SI", "cs-CZ", "sk-SK", "hu-HU", "bg-BG", "el-GR", "is-IS"
    ];

    // GetString("Key"), GetString(x ?? "Key") and SiteScope.Failed("Key") (whose key a
    // service passes to GetString). Only literal keys are found; a key passed in a
    // variable is covered when the literal it comes from matches one of these.
    // ResourceManager.GetString reads the resx files, not localization.json.
    private static readonly Regex[] KeyPatterns =
    [
        new(@"(?<!ResourceManager\.)GetString\(\s*""((?:[^""\\]|\\.)+)""", RegexOptions.Compiled),
        new(@"(?<!ResourceManager\.)GetString\([^;""]*\?\?\s*""((?:[^""\\]|\\.)+)""", RegexOptions.Compiled),
        new(@"SiteScope\.Failed\(\s*""((?:[^""\\]|\\.)+)""", RegexOptions.Compiled)
    ];

    [Test]
    public void EveryGetStringKey_IsTranslatedInEveryLanguage_InLocalizationJson()
    {
        var keys = CollectKeysFromSources();
        Assert.That(keys, Is.Not.Empty, "no GetString keys found; the source scan is broken");
        Assert.That(keys.Keys, Does.Contain("UnauthorizedToCreateAbsenceRequest"),
            "the source scan missed a key known to be used; it is broken");

        var translations = LoadLocalizationJson();

        var problems = new List<string>();
        foreach (var (key, files) in keys.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            if (!translations.TryGetValue(key, out var values))
            {
                problems.Add($"  {key}  (missing; used in {string.Join(", ", files.Order())})");
                continue;
            }

            var missingLanguages = Languages
                .Where(l => !values.TryGetValue(l, out var v) || string.IsNullOrWhiteSpace(v))
                .ToList();
            if (missingLanguages.Count > 0)
            {
                problems.Add($"  {key}  (no text for {string.Join(", ", missingLanguages)})");
            }
        }

        if (problems.Count == 0)
        {
            return;
        }

        var message = new StringBuilder();
        message.AppendLine(
            $"{problems.Count} message key(s) would reach the user as the raw key, because " +
            "TimePlanning.Pn/Resources/localization.json has no text for them:");
        problems.ForEach(p => message.AppendLine(p));
        message.AppendLine();
        message.AppendLine("Add an entry per key to localization.json with a text for each of: " +
                           string.Join(", ", Languages));
        Assert.Fail(message.ToString());
    }

    private static Dictionary<string, HashSet<string>> CollectKeysFromSources()
    {
        var sourceDir = FindPluginSourceDirectory();
        var keys = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        void Add(string key, string file)
        {
            if (!keys.TryGetValue(key, out var files))
            {
                keys[key] = files = new HashSet<string>(StringComparer.Ordinal);
            }

            files.Add(file);
        }

        // EformTimePlanningPlugin.HeaderMenu passes menu labels, not messages.
        var sources = Directory.EnumerateFiles(sourceDir, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .Where(p => Path.GetFileName(p) != "EformTimePlanningPlugin.cs")
            .Where(p => !IsUnder(p, sourceDir, "bin") && !IsUnder(p, sourceDir, "obj"));
        foreach (var path in sources)
        {
            var text = File.ReadAllText(path);
            foreach (var pattern in KeyPatterns)
            {
                foreach (Match match in pattern.Matches(text))
                {
                    Add(Regex.Unescape(match.Groups[1].Value), Path.GetFileName(path));
                }
            }
        }

        // The day-lock refusal keys reach GetString through DayLockHelper.
        Add(DayLockHelper.LockedMessageKey(true), "DayLockHelper.cs");
        Add(DayLockHelper.LockedMessageKey(false), "DayLockHelper.cs");

        return keys;
    }

    private static bool IsUnder(string path, string root, string folder) =>
        Path.GetRelativePath(root, path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .First() == folder;

    private static Dictionary<string, Dictionary<string, string>> LoadLocalizationJson()
    {
        // The embedded copy, which is what the running plugin reads.
        using var stream = typeof(EformTimePlanningPlugin).Assembly
            .GetManifestResourceStream(LocalizationResourceName);
        Assert.That(stream, Is.Not.Null, $"embedded resource {LocalizationResourceName} not found");

        using var document = JsonDocument.Parse(stream!);
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            var key = entry.GetProperty("Key").GetString()!;
            Assert.That(result.ContainsKey(key), Is.False, $"localization.json lists {key} twice");
            result[key] = entry.GetProperty("LocalizedValue").EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.GetString() ?? "", StringComparer.Ordinal);
        }

        return result;
    }

    private static string FindPluginSourceDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "TimePlanning.Pn");
            if (File.Exists(Path.Combine(candidate, "TimePlanning.Pn.csproj")))
            {
                return candidate;
            }
        }

        Assert.Fail($"TimePlanning.Pn sources not found above {AppContext.BaseDirectory}");
        return null!;
    }
}
