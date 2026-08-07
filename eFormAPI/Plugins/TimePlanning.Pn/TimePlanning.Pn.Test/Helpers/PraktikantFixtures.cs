using System.Collections.Generic;
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
}
