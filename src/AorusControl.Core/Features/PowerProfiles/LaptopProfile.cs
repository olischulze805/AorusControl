using AorusControl.Core.Features.Cooling;
using AorusControl.Core.Models;
using AorusControl.Core.Services;

namespace AorusControl.Core.Features.PowerProfiles;

public enum ProfileCoolingMode { Normal, Quiet, Gaming, Maximum, Fixed, Dynamic, CustomCurve }

/// <summary>Validated user intent, never authorization to replay manual control after restart.</summary>
public sealed class LaptopProfile
{
    public Guid Id { get; }
    public string Name { get; }
    public WindowsPowerOverlayMode PowerMode { get; }
    public ProfileCoolingMode CoolingMode { get; }
    public byte? FixedRawValue { get; }
    public IReadOnlyList<FanCurvePoint>? Curve { get; }

    public LaptopProfile(Guid id, string name, WindowsPowerOverlayMode powerMode,
        ProfileCoolingMode coolingMode, byte? fixedRawValue = null, IReadOnlyList<FanCurvePoint>? curve = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Profil-ID fehlt.", nameof(id));
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 60 || name.Any(char.IsControl))
            throw new ArgumentException("Profilname muss 1–60 Zeichen ohne Steuerzeichen enthalten.", nameof(name));
        if (!Enum.IsDefined(powerMode)) throw new ArgumentOutOfRangeException(nameof(powerMode));
        if (!Enum.IsDefined(coolingMode)) throw new ArgumentOutOfRangeException(nameof(coolingMode));
        if (coolingMode == ProfileCoolingMode.Fixed)
        {
            if (fixedRawValue is null or < 57 or > 229) throw new ArgumentOutOfRangeException(nameof(fixedRawValue));
        }
        else if (fixedRawValue is not null) throw new ArgumentException("Fixed-Wert nur im Fixed-Modus erlaubt.", nameof(fixedRawValue));

        if (coolingMode == ProfileCoolingMode.CustomCurve)
        {
            ArgumentNullException.ThrowIfNull(curve);
            FanCurvePoint[] snapshot = curve.ToArray();
            FanCurveValidation.Validate(snapshot);
            Curve = Array.AsReadOnly(snapshot);
        }
        else if (curve is not null) throw new ArgumentException("Kurve nur im benutzerdefinierten Kurvenmodus erlaubt.", nameof(curve));
        Id = id;
        Name = name.Trim();
        PowerMode = powerMode;
        CoolingMode = coolingMode;
        FixedRawValue = fixedRawValue;
    }
}
