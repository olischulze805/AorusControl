namespace AorusControl.Core.Models;

public sealed record DeviceCompatibility(
    bool IsSupported,
    string Manufacturer,
    string Model,
    string BiosVersion,
    string Message);
