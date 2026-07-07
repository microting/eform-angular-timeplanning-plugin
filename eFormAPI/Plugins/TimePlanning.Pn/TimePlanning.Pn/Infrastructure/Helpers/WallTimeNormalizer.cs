using System;
using System.Globalization;

namespace TimePlanning.Pn.Infrastructure.Helpers;

/// <summary>
/// Enforces the storage convention for the PlanRegistration exact-time columns
/// (<c>Start{N}StartedAt</c> / <c>Stop{N}StoppedAt</c> / <c>Pause*StartedAt</c> /
/// <c>Pause*StoppedAt</c>): these columns store USER-LOCAL WALL TIME with
/// <c>DateTimeKind.Unspecified</c> — the digits the worker saw on the clock —
/// matching the 5-minute interval ids (Start1Id=79 ↔ 06:30) and the id-derived
/// backfill in <c>TimePlanningPlanningService.EnsureTimestampsFromIds</c>.
/// See docs/adr/0001-plan-registration-timestamps-wall-time.md.
///
/// Incoming timestamp strings arrive in two shapes:
/// <list type="bullet">
///   <item>naive digits ("2026-07-07T06:30:00") — already wall time; kept
///   verbatim. Punch-clock flows, flag-off id-driven sites and pre-June-25
///   app builds all write this shape.</item>
///   <item>UTC or offset-carrying ("2026-07-07T04:30:00Z", "…+05:30") — sent
///   by the app edit screen's <c>.toUtc().toIso8601String()</c> path since the
///   June 25 Android release; must be converted into the user's zone so the
///   stored digits are wall time. This normalization stays even after the
///   app-side cleanup ships, so no client can ever corrupt storage again.</item>
/// </list>
/// </summary>
public static class WallTimeNormalizer
{
    /// <summary>
    /// IANA id used when no per-user timezone can be resolved (kiosk device
    /// flows have no authenticated user; personal flows fall back here when
    /// the user has no zone stored). All current customers operate in
    /// Denmark, which is also the assumption the wall-time interval ids have
    /// always encoded.
    /// </summary>
    public const string DefaultTimeZoneId = "Europe/Copenhagen";

    public static TimeZoneInfo DefaultTimeZone =>
        TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);

    /// <summary>
    /// Parses <paramref name="raw"/> and returns the user-local wall-time
    /// digits with <c>Kind=Unspecified</c>, ready to be stored:
    /// naive input is returned verbatim; input carrying Z or an explicit
    /// offset is converted through UTC into <paramref name="timeZone"/>
    /// (falling back to <see cref="DefaultTimeZone"/> when null).
    ///
    /// DST note: conversion FROM an instant is always deterministic — during
    /// the fall-back overlap (e.g. 2026-10-25 02:00–03:00 in Copenhagen) both
    /// the CEST and the CET instant map to the same wall digits; the stored
    /// value is only ambiguous in reverse, a limitation shared with the
    /// 5-minute interval ids.
    /// </summary>
    public static DateTime NormalizeToWallTime(string raw, TimeZoneInfo timeZone)
    {
        var parsed = DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (parsed.Kind == DateTimeKind.Unspecified)
        {
            // Naive digits are already wall time — the storage convention.
            return parsed;
        }

        // Kind=Utc (Z suffix / +00:00) or Kind=Local (explicit non-zero offset
        // → RoundtripKind yields server-local): normalize via the UTC instant
        // into the user's zone, then drop the kind so the wall digits
        // round-trip to MySQL without any further adjustment.
        var utc = parsed.ToUniversalTime();
        var wall = TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone ?? DefaultTimeZone);
        return DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Null-tolerant convenience overload for the ubiquitous
    /// "empty string means no stamp" call sites.
    /// </summary>
    public static DateTime? NormalizeToWallTimeOrNull(string raw, TimeZoneInfo timeZone) =>
        string.IsNullOrEmpty(raw) ? null : NormalizeToWallTime(raw, timeZone);
}
