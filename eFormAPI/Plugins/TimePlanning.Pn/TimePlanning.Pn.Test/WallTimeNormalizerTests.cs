using System;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Unit contract for <see cref="WallTimeNormalizer"/>, the single seam that
/// enforces wall-time-at-rest on every timestamp-string write path:
/// naive digits verbatim; Z / offset-carrying input converted through the UTC
/// instant into the user's zone; result always Kind=Unspecified so the digits
/// round-trip to MySQL untouched.
/// </summary>
[TestFixture]
public class WallTimeNormalizerTests
{
    private static readonly TimeZoneInfo Cph =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    [Test]
    public void NaiveDigits_ReturnedVerbatim_KindUnspecified()
    {
        var result = WallTimeNormalizer.NormalizeToWallTime("2026-07-07T06:30:00", Cph);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)));
            Assert.That(result.Kind, Is.EqualTo(DateTimeKind.Unspecified));
        });
    }

    [Test]
    public void ZSuffix_Summer_ConvertsToCestWall()
    {
        // 2026-07-07 is CEST (+02:00): 04:30Z == 06:30 wall.
        var result = WallTimeNormalizer.NormalizeToWallTime("2026-07-07T04:30:00Z", Cph);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)));
            Assert.That(result.Kind, Is.EqualTo(DateTimeKind.Unspecified));
        });
    }

    [Test]
    public void ZSuffix_Winter_ConvertsToCetWall()
    {
        // 2026-01-07 is CET (+01:00): 05:30Z == 06:30 wall.
        var result = WallTimeNormalizer.NormalizeToWallTime("2026-01-07T05:30:00Z", Cph);

        Assert.That(result, Is.EqualTo(new DateTime(2026, 1, 7, 6, 30, 0)));
    }

    [Test]
    public void PlusZeroOffset_TreatedAsUtc()
    {
        var result = WallTimeNormalizer.NormalizeToWallTime("2026-07-07T04:30:00+00:00", Cph);

        Assert.That(result, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)));
    }

    [Test]
    public void NonUtcOffset_ConvertsThroughInstantIntoUserZone()
    {
        // 10:00+05:30 == 04:30Z == 06:30 Copenhagen wall.
        var result = WallTimeNormalizer.NormalizeToWallTime("2026-07-07T10:00:00+05:30", Cph);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)));
            Assert.That(result.Kind, Is.EqualTo(DateTimeKind.Unspecified));
        });
    }

    /// <summary>
    /// The punch-clock flows persist Dart's <c>DateTime.toString()</c> output:
    /// space-separated, microsecond precision, no offset designator. This is
    /// the majority production shape — it must round-trip byte-verbatim
    /// (naive → wall time as-is, sub-second digits included, Kind=Unspecified).
    /// </summary>
    [Test]
    public void DartToStringFormat_SpaceSeparatedMicroseconds_RoundTripsVerbatim()
    {
        var result = WallTimeNormalizer.NormalizeToWallTime("2026-07-07 06:30:00.123456", Cph);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0).AddTicks(1234560)),
                "Dart DateTime.toString() digits (incl. .123456 = 1_234_560 ticks) must be preserved exactly");
            Assert.That(result.Kind, Is.EqualTo(DateTimeKind.Unspecified));
        });
    }

    [Test]
    public void NullTimeZone_FallsBackToCopenhagen()
    {
        var result = WallTimeNormalizer.NormalizeToWallTime("2026-07-07T04:30:00Z", null);

        Assert.That(result, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)),
            "Null zone must use the documented default (Europe/Copenhagen)");
    }

    [Test]
    public void EmptyOrNull_ReturnsNull_ViaOrNullOverload()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WallTimeNormalizer.NormalizeToWallTimeOrNull(null, Cph), Is.Null);
            Assert.That(WallTimeNormalizer.NormalizeToWallTimeOrNull("", Cph), Is.Null);
            Assert.That(WallTimeNormalizer.NormalizeToWallTimeOrNull("2026-07-07T04:30:00Z", Cph),
                Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)));
        });
    }

    /// <summary>
    /// DST fall-back boundary: Copenhagen leaves CEST on 2026-10-25 at 03:00
    /// (clocks go back to 02:00), so wall times 02:00–03:00 occur twice.
    /// Converting FROM an instant is deterministic — TimeZoneInfo maps
    /// 00:30Z (still CEST, +2) and 01:30Z (already CET, +1) each to wall
    /// 02:30. The two distinct instants therefore store the same digits;
    /// the ambiguity exists only in reverse, a limitation shared with the
    /// 5-minute interval ids (documented in the ADR).
    /// </summary>
    [Test]
    public void DstFallBackOverlap_BothInstants_MapToSameWallDigits_Deterministically()
    {
        var beforeSwitch = WallTimeNormalizer.NormalizeToWallTime("2026-10-25T00:30:00Z", Cph);
        var afterSwitch = WallTimeNormalizer.NormalizeToWallTime("2026-10-25T01:30:00Z", Cph);

        Assert.Multiple(() =>
        {
            Assert.That(beforeSwitch, Is.EqualTo(new DateTime(2026, 10, 25, 2, 30, 0)),
                "00:30Z is 02:30 CEST — first occurrence of the overlapped wall time");
            Assert.That(afterSwitch, Is.EqualTo(new DateTime(2026, 10, 25, 2, 30, 0)),
                "01:30Z is 02:30 CET — second occurrence; same wall digits, deterministic");
        });
    }

    /// <summary>
    /// DST spring-forward: 2026-03-29 02:00 CET jumps to 03:00 CEST; the wall
    /// hour 02:00–03:00 does not exist. An instant can never land in the gap:
    /// 01:30Z converts to 03:30 CEST.
    /// </summary>
    [Test]
    public void DstSpringForward_InstantConvertsPastTheGap()
    {
        var result = WallTimeNormalizer.NormalizeToWallTime("2026-03-29T01:30:00Z", Cph);

        Assert.That(result, Is.EqualTo(new DateTime(2026, 3, 29, 3, 30, 0)),
            "01:30Z is after the 02:00→03:00 jump, i.e. 03:30 CEST");
    }
}
