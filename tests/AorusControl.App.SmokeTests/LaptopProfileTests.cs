using AorusControl.Core.Features.PowerProfiles;
using AorusControl.Core.Models;
using AorusControl.Core.Services;

internal static class LaptopProfileTests
{
    public static void Run()
    {
        FanCurvePoint[] points = Enumerable.Range(0, 15).Select(i => new FanCurvePoint((byte)i, (byte)(30 + i * 4), (byte)(i == 14 ? 229 : 57 + i * 10))).ToArray();
        var profile = Make(ProfileCoolingMode.CustomCurve, curve: points);
        points[0] = new(0, 30, 1);
        if (profile.Curve![0].Value != 57) throw new Exception("Profile curve aliases input");
        Reject(() => Make(ProfileCoolingMode.CustomCurve, curve: points));
        Reject(() => Make(ProfileCoolingMode.CustomCurve));
        Reject(() => Make(ProfileCoolingMode.Normal, curve: profile.Curve));
        Reject(() => Make(ProfileCoolingMode.Fixed));
        Reject(() => Make(ProfileCoolingMode.Fixed, 56));
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
