using AorusControl.Core.Features.PowerProfiles;
using AorusControl.Core.Models;
using AorusControl.Core.Services;

internal static class LaptopProfileTests
{
    public static void Run()
    {
        FanCurvePoint[] points = Enumerable.Range(0, 15).Select(i => new FanCurvePoint((byte)i, (byte)(30 + i * 4), (byte)(i == 14 ? 229 : 57 + i * 10))).ToArray();
        var profile = Make(ProfileCoolingMode.CustomCurve, curve: points);
        // Mutating the caller's array afterwards must not reach the stored profile.
        points[0] = new(0, 30, 1);
        if (profile.Curve![0].Value != 57) throw new Exception("Profile curve aliases input");

        // Below 60 °C the fans may stand still - measured on this device, and what the vendor's
        // own Quiet profile does - so a low value there is no longer invalid. Above it the
        // verified floor still applies, and that is what a profile must refuse.
        FanCurvePoint[] silentWhileHot = points.ToArray();
        silentWhileHot[8] = new(8, 62, 1);
        Reject(() => Make(ProfileCoolingMode.CustomCurve, curve: silentWhileHot));
        _ = Make(ProfileCoolingMode.CustomCurve, curve: points);
        Reject(() => Make(ProfileCoolingMode.CustomCurve));
        Reject(() => Make(ProfileCoolingMode.Normal, curve: profile.Curve));
        Reject(() => Make(ProfileCoolingMode.Fixed));
        // Fans off is a real setting on this device, so only values the firmware cannot take
        // are refused.
        _ = Make(ProfileCoolingMode.Fixed, 0);
        Reject(() => Make(ProfileCoolingMode.Fixed, 230));
        Reject(() => Make(ProfileCoolingMode.Normal, 100));
        Reject(() => Make((ProfileCoolingMode)99));
        Reject(() => new LaptopProfile(Guid.Empty, "Test", WindowsPowerOverlayMode.Balanced, ProfileCoolingMode.Normal));
        Reject(() => new LaptopProfile(Guid.NewGuid(), "\n", WindowsPowerOverlayMode.Balanced, ProfileCoolingMode.Normal));
        foreach (ProfileCoolingMode mode in Enum.GetValues<ProfileCoolingMode>())
            _ = Make(mode, mode == ProfileCoolingMode.Fixed ? (byte)114 : null, mode == ProfileCoolingMode.CustomCurve ? profile.Curve : null);
        Console.WriteLine("PASS: laptop profile modes, fixed limits, curve snapshot and contradictory settings rejected");
    }

    private static LaptopProfile Make(ProfileCoolingMode mode, byte? fixedValue = null, IReadOnlyList<FanCurvePoint>? curve = null)
        => new(Guid.NewGuid(), "Test", WindowsPowerOverlayMode.Balanced, mode, fixedValue, curve);

    private static void Reject(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new Exception("Invalid profile accepted");
    }
}
