/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TimePlanning.Pn.Infrastructure.Helpers;

/// <summary>
/// Single source of truth for "is this pay rule set a locked GLS-A preset".
///
/// The lock used to live as private statics on
/// <c>PayRuleSetService</c>, which meant only that service could enforce it.
/// The nested rule endpoints (pay tier rules, pay day type rules, pay time band
/// rules) hang off the very same rule set and had no guard at all, so a locked
/// preset could be rewritten one child row at a time — the lock was fully
/// bypassable. Hoisting the logic here lets every service that can mutate a
/// locked preset (directly or through a child row) share the exact same
/// definition.
/// </summary>
internal static class PayRuleSetLock
{
    /// <summary>
    /// The GLS-A/3F overenskomst presets shipped by the platform. These rows are
    /// read-only for customers: they are maintained centrally and re-seeded when
    /// the agreement is renegotiated.
    /// </summary>
    internal static readonly HashSet<string> LockedPresetNames = new HashSet<string>
    {
        "GLS-A / 3F - Jordbrug Standard 2026-2029",
        "GLS-A / 3F - Jordbrug Dyrehold 2026-2029",
        "GLS-A / 3F - Jordbrug Elev u18 2026-2029",
        "GLS-A / 3F - Jordbrug Elev o18 2026-2029",
        "GLS-A / 3F - Jordbrug Elev u18 Dyrehold 2026-2029",
        "GLS-A / 3F - Gartneri Standard 2026-2029",
        "GLS-A / 3F - Gartneri Elev u18 2026-2029",
        "GLS-A / 3F - Gartneri Elev o18 2026-2029",
        "GLS-A / 3F - Skovbrug Standard 2026-2029",
        "GLS-A / 3F - Skovbrug Elev u18 2026-2029",
        "GLS-A / 3F - Skovbrug Elev o18 2026-2029",
        "GLS-A / 3F - Golf Standard 2026-2029",
        "GLS-A / 3F - Golf Elev 2026-2029",
        "GLS-A / 3F - Agroindustri Fjerkrae Standard 2026-2029",
        "GLS-A / 3F - Agroindustri Fjerkrae Elev 2026-2029",
        "GLS-A / 3F - Agroindustri Grovvare Standard 2026-2029",
        "GLS-A / 3F - Agroindustri Grovvare Elev 2026-2029",
        "GLS-A / 3F - Agroindustri Gulerod Standard 2026-2029",
        "GLS-A / 3F - Agroindustri Gulerod Elev 2026-2029",
        "GLS-A / 3F - Agroindustri Kartoffelmel Standard 2026-2029",
        "GLS-A / 3F - Agroindustri Kartoffelmel Elev 2026-2029",
        "GLS-A / 3F - Agroindustri Kartoffelsorter Standard 2026-2029",
        "GLS-A / 3F - Agroindustri Kartoffelsorter Elev 2026-2029",
        "GLS-A / 3F - Agroindustri Lucerne Standard 2026-2029",
        "GLS-A / 3F - Agroindustri Lucerne Elev 2026-2029",
        "GLS-A / 3F - Agroindustri Minkfoder Standard 2026-2029",
        "GLS-A / 3F - Agroindustri Minkfoder Elev 2026-2029",
        "GLS-A / 3F - Agroindustri Ovrige Standard 2026-2029",
        "GLS-A / 3F - Agroindustri Ovrige Elev 2026-2029",
        "GLS-A / 3F - Udenlandske praktikanter Landbrug Andet arbejde 2026-2029",
        "GLS-A / 3F - Udenlandske praktikanter Landbrug Staldarbejde 2026-2029"
    };

    /// <summary>
    /// Trailing agreement validity period, e.g. " 2024-2026" or " 2026–2029".
    /// Hyphen, en-dash and em-dash are all accepted so a stored name written
    /// with a typographic dash normalizes to the same value.
    /// </summary>
    private static readonly Regex ValidityPeriodSuffixRegex =
        new Regex(@"\s+\d{4}\s*[-–—]\s*\d{4}$", RegexOptions.Compiled);

    /// <summary>
    /// <see cref="LockedPresetNames"/> with the validity period stripped.
    /// Declared after the source set: static field initializers run in
    /// textual order.
    /// </summary>
    private static readonly HashSet<string> NormalizedLockedPresetNames =
        new HashSet<string>(LockedPresetNames.Select(NormalizePresetName));

    /// <summary>
    /// Strips the trailing validity period so names that differ only by
    /// agreement period compare equal — "… Jordbrug Dyrehold 2024-2026" and
    /// "… Jordbrug Dyrehold 2026-2029" both become "… Jordbrug Dyrehold".
    /// </summary>
    internal static string NormalizePresetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        return ValidityPeriodSuffixRegex.Replace(name.Trim(), string.Empty).Trim();
    }

    /// <summary>
    /// True when the name matches a locked preset once the validity period is
    /// normalized away. Rule sets created before a catalogue rename therefore
    /// stay locked.
    /// </summary>
    internal static bool IsLockedPresetName(string name)
    {
        var normalized = NormalizePresetName(name);
        return normalized.Length > 0 && NormalizedLockedPresetNames.Contains(normalized);
    }
}
