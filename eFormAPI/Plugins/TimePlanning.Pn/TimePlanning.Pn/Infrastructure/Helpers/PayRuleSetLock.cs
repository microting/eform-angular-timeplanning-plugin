/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;

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
///
/// It is also the home for the other preset-IDENTITY questions the engine has to ask,
/// because they all need the same validity-period normalization — see
/// <see cref="IsNormalTimeSplitPresetName"/>.
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

    // ------------------------------------------------------------------
    // Normal-time / overtime split — OPT-IN BY RULE-SET IDENTITY
    // ------------------------------------------------------------------

    /// <summary>
    /// The presets whose day rules encode tier 1 as a NORMAL-TIME BOUNDARY, and which
    /// therefore opt in to the sequential normal-time/overtime split in
    /// <c>TimePlanningWorkingHoursService.CalculatePayLinesForDay</c> (and to the
    /// Grundlovsdag noon split that rides on the same interpretation).
    ///
    /// WHY THIS IS GATED ON IDENTITY AND NOT ON RULE-SET SHAPE
    /// ------------------------------------------------------
    /// The split reinterprets the first tier of a banded day as "normal time ends here;
    /// everything past it is overtime and carries no clock-time supplement". That reading
    /// is only true for these two § 50 praktikant presets, where § 50 stk. 4 d pays the
    /// supplements exclusively "for arbejde i normal arbejdstid".
    ///
    /// The shape the split used to key off — "the day has time bands AND the day rule has
    /// more than one tier AND tier 1 has a non-null UpToSeconds" — is NOT unique to them.
    /// Thirteen other preset/day combinations match it (Jordbrug Standard and Jordbrug
    /// Dyrehold WEEKDAY+SATURDAY, Gartneri Standard WEEKDAY+SATURDAY, Skovbrug Standard
    /// WEEKDAY+SATURDAY, KA Svine/Plante/Maskin WEEKDAY, KA Gron WEEKDAY+SATURDAY). In
    /// those presets tier 1 is a deliberate MIRROR of the clock split, not a normal-time
    /// boundary, so applying the split there would hand a morning-only Saturday an
    /// afternoon supplement it never earned. Those presets are explicitly out of scope in
    /// the 2026-08-07 spec, so the opt-in is by name.
    ///
    /// A per-rule-set database column would be the better home for this flag, but the
    /// entity lives in the eform-timeplanning-base NuGet package and cannot be extended
    /// from the plugin. Name matching mirrors the locked-preset mechanism above, and goes
    /// through <see cref="NormalizePresetName"/> so a customer row still carrying the
    /// "… 2024-2026" validity period matches the "… 2026-2029" catalogue entry.
    /// </summary>
    private static readonly HashSet<string> NormalizedNormalTimeSplitPresetNames =
        new HashSet<string>(new[]
        {
            "GLS-A / 3F - Udenlandske praktikanter Landbrug Staldarbejde",
            "GLS-A / 3F - Udenlandske praktikanter Landbrug Andet arbejde"
        }.Select(NormalizePresetName));

    /// <summary>
    /// True when the rule set opts in to the sequential normal-time/overtime split.
    /// See <see cref="NormalizedNormalTimeSplitPresetNames"/> for why this is an
    /// identity check and not a shape check.
    /// </summary>
    internal static bool IsNormalTimeSplitPresetName(string name)
    {
        var normalized = NormalizePresetName(name);
        return normalized.Length > 0 && NormalizedNormalTimeSplitPresetNames.Contains(normalized);
    }

    // ------------------------------------------------------------------
    // Normal-time / overtime split — SHAPE GUARD FOR STALE PRESET SNAPSHOTS
    // ------------------------------------------------------------------

    /// <summary>
    /// Number of tiers in a corrected praktikant day rule: the normal-time boundary
    /// plus the two overtime steps.
    /// </summary>
    private const int NormalTimeBoundaryTierCount = 3;

    /// <summary>
    /// Tier 1's cutoff in the corrected presets: the daily normal-time boundary,
    /// 7 h 24 m (37 h ÷ 5). This is the value the split reads as "normal time ends here".
    /// </summary>
    internal const int NormalTimeBoundarySeconds = 26640;

    /// <summary>
    /// Tier 2's cumulative cutoff in the corrected presets: the top of the 50 % overtime
    /// step, 9 h 24 m (the boundary plus 2 h).
    /// </summary>
    internal const int Overtime50BoundarySeconds = 33840;

    /// <summary>Tier 2's pay code in the corrected presets — the 50 % overtime step.</summary>
    private const string Overtime50PayCode = "OVERTIME_50";

    /// <summary>Tier 3's pay code in the corrected presets — the open-ended 80 % overtime step.</summary>
    private const string Overtime80PayCode = "OVERTIME_80";

    /// <summary>
    /// True when a day rule's ordered tiers actually ENCODE a normal-time boundary
    /// followed by the two overtime steps, i.e. they match the corrected praktikant
    /// preset shape exactly:
    ///
    ///   tier 1: UpToSeconds == 26640 (the normal-time boundary; its pay code varies
    ///           legitimately per day — SAT_NORMAL, ANIMAL_SUN_HOLIDAY, NORMAL — so it
    ///           is deliberately NOT constrained here)
    ///   tier 2: UpToSeconds == 33840, PayCode == "OVERTIME_50"
    ///   tier 3: UpToSeconds == null,  PayCode == "OVERTIME_80"
    ///
    /// WHY THIS EXISTS — NAME MATCHING ALONE IS NOT ENOUGH
    /// ---------------------------------------------------
    /// <see cref="IsNormalTimeSplitPresetName"/> answers "which AGREEMENT is this rule
    /// set", and that is a stable question. But preset definitions are COPY-AT-CREATE-TIME
    /// SNAPSHOTS: a customer who created the praktikant rule set before the tiers were
    /// corrected still holds the OLD rows in their database, under the very same name.
    /// Those pre-correction rows encode something completely different — e.g. the old
    /// Staldarbejde SATURDAY rule was [21600 SAT_NORMAL, null SAT_ANIMAL_AFTERNOON], a
    /// MIRROR of the clock bands, not a normal-time boundary. Reinterpreting it as a
    /// boundary attributes 21600 s to the bands and dumps the overflow on tier 2's
    /// SAT_ANIMAL_AFTERNOON — a fixed kr/dag afternoon supplement — even for a shift that
    /// ended at noon.
    ///
    /// So the name identifies the agreement, and this predicate confirms the DATA really
    /// speaks the new dialect. When it does not, the caller must leave the row on the
    /// historical path rather than reinterpret it: when the data is not what the new
    /// interpretation assumes, do not reinterpret it.
    ///
    /// This is a guard, not the cure. The durable fix is a DATA MIGRATION that rewrites
    /// the stale praktikant day rules to the corrected tiers; once every customer row has
    /// been migrated this predicate becomes a no-op and can be retired. That migration
    /// shipped 2026-08-07 as CorrectPraktikantSection50Tiers in eform-timeplanning-base
    /// v10.0.57 — it runs when a customer backend upgrades past that version, so the
    /// guard stays until no pre-migration database remains.
    /// </summary>
    internal static bool HasNormalTimeBoundaryShape(IReadOnlyList<PayTierRule>? orderedTiers)
    {
        if (orderedTiers is not { Count: NormalTimeBoundaryTierCount })
        {
            return false;
        }

        return orderedTiers[0].UpToSeconds == NormalTimeBoundarySeconds
               && orderedTiers[1].UpToSeconds == Overtime50BoundarySeconds
               && orderedTiers[1].PayCode == Overtime50PayCode
               && orderedTiers[2].UpToSeconds == null
               && orderedTiers[2].PayCode == Overtime80PayCode;
    }
}
