using System.Collections.Generic;
using System.Linq;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;

namespace TimePlanning.Pn.Test.Helpers;

/// <summary>
/// In-memory <see cref="PayRuleSet"/> fixtures for the two "Udenlandske praktikanter
/// Landbrug" presets (GLS-A / 3F, § 50), used by
/// <see cref="TimePlanning.Pn.Test.PraktikantPayLineRoutingTests"/>.
///
/// THREE-WAY SYNC OBLIGATION — these two presets are <c>locked: true</c>, so the same
/// tiers/bands exist in THREE places and must be changed together, byte-for-byte:
///   1. the frontend catalogue
///      eform-client/src/app/plugins/modules/time-planning-pn/models/pay-rule-sets/pay-rule-set-presets.ts
///      (keys 'glsa-jordbrug-praktikant-udl-staldarbejde' and 'glsa-jordbrug-praktikant-udl-andet');
///   2. the base fixtures OverenskomstFixtureHelper / GlsAFixtureHelper in
///      eform-timeplanning-base (Microting.TimePlanningBase.Tests);
///   3. this file.
/// This helper is a local copy ONLY because the plugin test project does not reference
/// the base test project, so the base fixture helpers are not visible here. If you edit
/// the preset in the TS catalogue, edit the base fixture AND this file in the same change.
/// Values below were transcribed from the TS catalogue on 2026-08-07.
///
/// THE <c>Name</c> IS LOAD-BEARING, NOT DECORATION. The engine opts these two presets —
/// and only these two — into the sequential normal-time/overtime split and the
/// Grundlovsdag noon split by matching the name through
/// <c>PayRuleSetLock.IsNormalTimeSplitPresetName</c>. Change a Name here and the fixture
/// silently drops back to the historical bands-only / pure-tier routing.
/// </summary>
internal static class PraktikantFixtures
{
    /// <summary>Daily normal-time boundary: 7 h 24 m. Documented system default (37 h ÷ 5).</summary>
    internal const int NormSeconds = 26640;

    /// <summary>Cumulative end of the 50 % overtime step: 9 h 24 m.</summary>
    internal const int Overtime50UpToSeconds = 33840;

    /// <summary>
    /// Preset 39: "GLS-A / 3F - Udenlandske praktikanter Landbrug Staldarbejde 2026-2029".
    /// Stald supplements are payable per § 50 stk. 4 d only "for arbejde i normal
    /// arbejdstid", hence bands for the normal-time portion plus overtime tiers beyond it.
    /// On Grundlovsdag the plugin splits the normal-time portion at 12:00: before noon
    /// the GRUNDLOVSDAG tier-1 code (NORMAL), from noon the Sunday day-type band
    /// (ANIMAL_SUN_HOLIDAY).
    /// </summary>
    internal static PayRuleSet Staldarbejde() => new()
    {
        Id = 3901,
        Name = "GLS-A / 3F - Udenlandske praktikanter Landbrug Staldarbejde 2026-2029",
        DayRules = new List<PayDayRule>
        {
            new()
            {
                DayCode = "WEEKDAY",
                Tiers = new List<PayTierRule>
                {
                    new() { Order = 1, UpToSeconds = 26640, PayCode = "NORMAL" },
                    new() { Order = 2, UpToSeconds = 33840, PayCode = "OVERTIME_50" },
                    new() { Order = 3, UpToSeconds = null, PayCode = "OVERTIME_80" },
                }
            },
            new()
            {
                DayCode = "SATURDAY",
                Tiers = new List<PayTierRule>
                {
                    new() { Order = 1, UpToSeconds = 26640, PayCode = "SAT_NORMAL" },
                    new() { Order = 2, UpToSeconds = 33840, PayCode = "OVERTIME_50" },
                    new() { Order = 3, UpToSeconds = null, PayCode = "OVERTIME_80" },
                }
            },
            new()
            {
                DayCode = "SUNDAY",
                Tiers = new List<PayTierRule>
                {
                    new() { Order = 1, UpToSeconds = 26640, PayCode = "ANIMAL_SUN_HOLIDAY" },
                    new() { Order = 2, UpToSeconds = 33840, PayCode = "OVERTIME_50" },
                    new() { Order = 3, UpToSeconds = null, PayCode = "OVERTIME_80" },
                }
            },
            new()
            {
                DayCode = "HOLIDAY",
                Tiers = new List<PayTierRule>
                {
                    new() { Order = 1, UpToSeconds = 26640, PayCode = "ANIMAL_SUN_HOLIDAY" },
                    new() { Order = 2, UpToSeconds = 33840, PayCode = "OVERTIME_50" },
                    new() { Order = 3, UpToSeconds = null, PayCode = "OVERTIME_80" },
                }
            },
            new()
            {
                DayCode = "GRUNDLOVSDAG",
                Tiers = new List<PayTierRule>
                {
                    new() { Order = 1, UpToSeconds = 26640, PayCode = "NORMAL" },
                    new() { Order = 2, UpToSeconds = 33840, PayCode = "OVERTIME_50" },
                    new() { Order = 3, UpToSeconds = null, PayCode = "OVERTIME_80" },
                }
            },
        },
        DayTypeRules = new List<PayDayTypeRule>
        {
            new()
            {
                DayType = DayType.Saturday,
                DefaultPayCode = "SAT_NORMAL",
                Priority = 1,
                TimeBandRules = new List<PayTimeBandRule>
                {
                    new() { StartSecondOfDay = 0, EndSecondOfDay = 43200, PayCode = "SAT_NORMAL", Priority = 1 },
                    new() { StartSecondOfDay = 43200, EndSecondOfDay = 86400, PayCode = "SAT_ANIMAL_AFTERNOON", Priority = 1 },
                }
            },
            new()
            {
                DayType = DayType.Sunday,
                DefaultPayCode = "ANIMAL_SUN_HOLIDAY",
                Priority = 1,
                TimeBandRules = new List<PayTimeBandRule>
                {
                    new() { StartSecondOfDay = 0, EndSecondOfDay = 86400, PayCode = "ANIMAL_SUN_HOLIDAY", Priority = 1 },
                }
            },
            new()
            {
                DayType = DayType.Holiday,
                DefaultPayCode = "ANIMAL_SUN_HOLIDAY",
                Priority = 1,
                TimeBandRules = new List<PayTimeBandRule>
                {
                    new() { StartSecondOfDay = 0, EndSecondOfDay = 86400, PayCode = "ANIMAL_SUN_HOLIDAY", Priority = 1 },
                }
            },
        },
    };

    /// <summary>
    /// Preset 38: "GLS-A / 3F - Udenlandske praktikanter Landbrug Andet arbejde 2026-2029".
    /// No day-type rules at all — every day goes down the pure tier path, EXCEPT
    /// Grundlovsdag, which the plugin splits at 12:00: before noon the GRUNDLOVSDAG
    /// tier-1 code (NORMAL), from noon this preset's søgnehelligdag treatment, which the
    /// engine reads off the SUNDAY day rule below. Sundays and holidays are outside the
    /// permitted Mon–Sat 06–18 window, so all hours there are overtime (first 2 h @ 50 %,
    /// remainder @ 80 %).
    /// </summary>
    internal static PayRuleSet AndetArbejde() => new()
    {
        Id = 3801,
        Name = "GLS-A / 3F - Udenlandske praktikanter Landbrug Andet arbejde 2026-2029",
        DayRules = new List<PayDayRule>
        {
            new()
            {
                DayCode = "WEEKDAY",
                Tiers = new List<PayTierRule>
                {
                    new() { Order = 1, UpToSeconds = 26640, PayCode = "NORMAL" },
                    new() { Order = 2, UpToSeconds = 33840, PayCode = "OVERTIME_50" },
                    new() { Order = 3, UpToSeconds = null, PayCode = "OVERTIME_80" },
                }
            },
            new()
            {
                DayCode = "SATURDAY",
                Tiers = new List<PayTierRule>
                {
                    new() { Order = 1, UpToSeconds = 26640, PayCode = "NORMAL" },
                    new() { Order = 2, UpToSeconds = 33840, PayCode = "OVERTIME_50" },
                    new() { Order = 3, UpToSeconds = null, PayCode = "OVERTIME_80" },
                }
            },
            new()
            {
                DayCode = "SUNDAY",
                Tiers = new List<PayTierRule>
                {
                    new() { Order = 1, UpToSeconds = 7200, PayCode = "OVERTIME_50" },
                    new() { Order = 2, UpToSeconds = null, PayCode = "OVERTIME_80" },
                }
            },
            new()
            {
                DayCode = "HOLIDAY",
                Tiers = new List<PayTierRule>
                {
                    new() { Order = 1, UpToSeconds = 7200, PayCode = "OVERTIME_50" },
                    new() { Order = 2, UpToSeconds = null, PayCode = "OVERTIME_80" },
                }
            },
            new()
            {
                DayCode = "GRUNDLOVSDAG",
                Tiers = new List<PayTierRule>
                {
                    new() { Order = 1, UpToSeconds = 26640, PayCode = "NORMAL" },
                    new() { Order = 2, UpToSeconds = 33840, PayCode = "OVERTIME_50" },
                    new() { Order = 3, UpToSeconds = null, PayCode = "OVERTIME_80" },
                }
            },
        },
        DayTypeRules = new List<PayDayTypeRule>(),
    };

    // ==================================================================
    // PRE-CORRECTION SNAPSHOTS — what EXISTING customer databases hold
    // ==================================================================
    //
    // Preset definitions are COPIED INTO the customer's database when the rule set is
    // created; they are not a live reference to the catalogue. Every customer who created
    // a praktikant rule set BEFORE the tiers above were corrected therefore still holds
    // the OLD tier rows — under the very same, unchanged Name.
    //
    // That matters because the normal-time/overtime split and the Grundlovsdag noon split
    // are opted into BY NAME. The name still matches on these stale rows, so without a
    // shape guard the engine would reinterpret tiers that do not encode a normal-time
    // boundary at all: the old Staldarbejde SATURDAY tier 1 (21600 s) is a MIRROR of the
    // 12:00 clock band, and tier 2's SAT_ANIMAL_AFTERNOON is a fixed kr/dag afternoon
    // supplement — so the overflow would pay an afternoon supplement to a worker who went
    // home at noon.
    //
    // PayRuleSetLock.HasNormalTimeBoundaryShape keeps these rows on the historical path.
    // The fixtures below exist to prove that, and should be DELETED once a data migration
    // has rewritten the stale rows to the corrected tiers.
    //
    // Only the day rules listed in the bug report differ from the corrected fixtures; the
    // WEEKDAY rule and all time bands were never part of the correction and are inherited
    // unchanged, so the fixtures are built by mutating the corrected ones — that keeps the
    // delta visible and honours the three-way sync note above.

    /// <summary>Old Staldarbejde SATURDAY tier 1 cutoff: 06:00, mirroring the 12:00 band from a 06:00 start.</summary>
    internal const int LegacySaturdayMirrorSeconds = 21600;

    /// <summary>Old Andet arbejde GRUNDLOVSDAG tier 1 cutoff: the 2 h Sunday-ladder step.</summary>
    internal const int LegacyGrundlovsdagFirstStepSeconds = 7200;

    /// <summary>
    /// <see cref="Staldarbejde"/> as it exists in customer databases created BEFORE the
    /// tier correction. Same Name (so the name gate still matches), but:
    ///   SATURDAY            [21600 SAT_NORMAL, null SAT_ANIMAL_AFTERNOON]  (2 tiers)
    ///   SUNDAY / HOLIDAY    [null ANIMAL_SUN_HOLIDAY]                      (1 tier)
    ///   GRUNDLOVSDAG        [null ANIMAL_SUN_HOLIDAY]                      (1 tier)
    /// None of these encodes a normal-time boundary, so all of them must keep the
    /// historical bands-only / pure-tier routing.
    /// </summary>
    internal static PayRuleSet StaldarbejdeLegacyTiers()
    {
        var payRuleSet = Staldarbejde();

        payRuleSet.DayRules.Single(r => r.DayCode == "SATURDAY").Tiers = new List<PayTierRule>
        {
            new() { Order = 1, UpToSeconds = LegacySaturdayMirrorSeconds, PayCode = "SAT_NORMAL" },
            new() { Order = 2, UpToSeconds = null, PayCode = "SAT_ANIMAL_AFTERNOON" },
        };

        payRuleSet.DayRules.Single(r => r.DayCode == "SUNDAY").Tiers = new List<PayTierRule>
        {
            new() { Order = 1, UpToSeconds = null, PayCode = "ANIMAL_SUN_HOLIDAY" },
        };

        payRuleSet.DayRules.Single(r => r.DayCode == "HOLIDAY").Tiers = new List<PayTierRule>
        {
            new() { Order = 1, UpToSeconds = null, PayCode = "ANIMAL_SUN_HOLIDAY" },
        };

        payRuleSet.DayRules.Single(r => r.DayCode == "GRUNDLOVSDAG").Tiers = new List<PayTierRule>
        {
            new() { Order = 1, UpToSeconds = null, PayCode = "ANIMAL_SUN_HOLIDAY" },
        };

        return payRuleSet;
    }

    /// <summary>
    /// <see cref="AndetArbejde"/> as it exists in customer databases created BEFORE the
    /// tier correction. Same Name, but GRUNDLOVSDAG is the plain Sunday-style ladder
    /// [7200 OVERTIME_50, null OVERTIME_80] — two tiers, tier 1 non-null, yet no
    /// normal-time boundary anywhere in it. Reading 7200 s as the boundary would shrink
    /// the ordinary-working-time half of Grundlovsdag to two hours, so this row must keep
    /// its historical pure-tier treatment.
    /// </summary>
    internal static PayRuleSet AndetArbejdeLegacyTiers()
    {
        var payRuleSet = AndetArbejde();

        payRuleSet.DayRules.Single(r => r.DayCode == "GRUNDLOVSDAG").Tiers = new List<PayTierRule>
        {
            new() { Order = 1, UpToSeconds = LegacyGrundlovsdagFirstStepSeconds, PayCode = "OVERTIME_50" },
            new() { Order = 2, UpToSeconds = null, PayCode = "OVERTIME_80" },
        };

        return payRuleSet;
    }
}
