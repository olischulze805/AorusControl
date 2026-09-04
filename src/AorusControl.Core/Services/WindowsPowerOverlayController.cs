using System.Runtime.InteropServices;
using Microsoft.Win32;
using AorusControl.Core.Features.PowerProfiles;

namespace AorusControl.Core.Services;

public enum WindowsPowerOverlayMode
{
    Balanced,
    BestEfficiency,
    BestPerformance
}

public sealed class WindowsPowerOverlayController
{
    public static readonly Guid BalancedGuid = Guid.Empty;
    public static readonly Guid BestEfficiencyGuid = new("961cc777-2547-4f9d-8174-7d86181b8a7a");
    public static readonly Guid BestPerformanceGuid = new("ded574b5-45a0-4f42-8737-46345c09c238");

    private const string RegistryPath = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes";

    public bool IsOnAcPower()
    {
        if (!GetSystemPowerStatus(out SystemPowerStatus status))
        {
            throw new InvalidOperationException("Windows konnte den Netzteilzustand nicht lesen.");
        }

        return LaptopPowerSources.FromWindowsStatus(status.AcLineStatus) switch
        {
            LaptopPowerSource.Ac => true,
            LaptopPowerSource.Battery => false,
            _ => throw new InvalidOperationException("Windows meldet eine unbekannte Stromversorgung; kein Netz-/Akkuprofil auswählen.")
        };
    }

    public Guid ReadActiveForCurrentPowerSource()
    {
        string name = IsOnAcPower() ? "ActiveOverlayAcPowerScheme" : "ActiveOverlayDcPowerScheme";
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(RegistryPath, writable: false);
        object? value = key?.GetValue(name);
        return value switch
        {
            string text when Guid.TryParse(text, out Guid parsed) => parsed,
            Guid guid => guid,
            _ => throw new InvalidOperationException($"Windows-Overlaywert {name} ist nicht lesbar.")
        };
    }

    public void Set(WindowsPowerOverlayMode mode)
    {
        Guid guid = mode switch
        {
            WindowsPowerOverlayMode.Balanced => BalancedGuid,
            WindowsPowerOverlayMode.BestEfficiency => BestEfficiencyGuid,
            WindowsPowerOverlayMode.BestPerformance => BestPerformanceGuid,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        Set(guid);
    }

    public void Set(Guid guid)
    {
        if (guid != BalancedGuid && guid != BestEfficiencyGuid && guid != BestPerformanceGuid)
        {
            throw new ArgumentOutOfRangeException(nameof(guid), "Nicht freigegebener Windows-Overlay-GUID.");
        }

        IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
        try
        {
            Marshal.StructureToPtr(guid, pointer, fDeleteOld: false);
            uint result = PowerSetActiveOverlayScheme(pointer);
            if (result != 0)
            {
                throw new InvalidOperationException($"PowerSetActiveOverlayScheme meldete Windows-Fehler {result}.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveOverlayScheme(IntPtr overlaySchemeGuid);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus systemPowerStatus);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }
}
