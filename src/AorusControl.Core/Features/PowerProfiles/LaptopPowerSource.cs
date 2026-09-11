namespace AorusControl.Core.Features.PowerProfiles;

public enum LaptopPowerSource { Unknown, Battery, Ac }

public static class LaptopPowerSources
{
    public static LaptopPowerSource FromWindowsStatus(byte acLineStatus) => acLineStatus switch
    {
        0 => LaptopPowerSource.Battery,
        1 => LaptopPowerSource.Ac,
        _ => LaptopPowerSource.Unknown
    };
}
