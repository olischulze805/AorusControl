using AorusControl.Core.Features.GpuPreferences;

internal static class GpuActivityTests
{
    public static void Run()
    {
        Parse();
        Summarise();
        Console.WriteLine("PASS: GPU engine instance names, LUID keys, and the running list's grouping and filtering");
    }

    private static void Parse()
    {
        // A real instance name from this machine, 2026-09-18.
        GpuEngineInstance engine = GpuEngineInstance.Parse("pid_1692_luid_0x00000000_0x0001232e_phys_0_eng_0_engtype_3d")
            ?? throw new InvalidOperationException("the machine's own instance name must parse");
        Check(engine.ProcessId == 1692, $"the process id, got {engine.ProcessId}");
        Check(engine.Luid == 0x1232e, $"the two LUID halves as one key, got 0x{engine.Luid:x}");

        // A non-zero high part is the half that would silently go missing if only the low one
        // were read - two adapters would then share a key.
        GpuEngineInstance high = GpuEngineInstance.Parse("pid_4_luid_0x00000007_0x0001232e_phys_0_eng_9_engtype_copy")!;
        Check(high.Luid == GraphicsAdapters.Key(7, 0x1232e) && high.Luid != engine.Luid,
            "the high part belongs to the key");

        Check(GpuEngineInstance.Parse(null) is null, "no name, no instance");
        Check(GpuEngineInstance.Parse("") is null, "an empty name is not an instance");
        Check(GpuEngineInstance.Parse("engtype_3d") is null, "a name without the pid prefix is skipped");
        Check(GpuEngineInstance.Parse("pid_x_luid_0x0_0x1_phys_0") is null, "a non-numeric pid is skipped, not guessed");
        Check(GpuEngineInstance.Parse("pid_12_luid_0x00000000_0x0001232e") is null,
            "a name that ends after the LUID has no trailing separator and is skipped");
        Check(GpuEngineInstance.Parse("pid_12_luid_zzz_0x1_phys_0") is null, "a malformed LUID is skipped");
    }

    private static void Summarise()
    {
        const string windows = @"C:\WINDOWS";
        GpuContext[] raw =
        [
            // Two processes of one program, one of them on each chip: a browser or an
            // Electron app. It counts as being on the RTX, which is the actionable state.
            new(20344, "claude", @"C:\Program Files\WindowsApps\Claude\Claude.exe", IsNvidia: true),
            new(22768, "claude", @"C:\Program Files\WindowsApps\Claude\Claude.exe", IsNvidia: false),
            new(19388, "UniGetUI", @"C:\Program Files\UniGetUI\UniGetUI.exe", IsNvidia: false),
            // Windows' own, on both chips: 21 of the 29 raw rows on this machine looked
            // like this, and none of them can be assigned a graphics chip.
            new(1692, "dwm", @"C:\WINDOWS\system32\dwm.exe", IsNvidia: true),
            new(11072, "explorer", @"C:\WINDOWS\Explorer.EXE", IsNvidia: true),
            // Protected processes have a name but no readable module.
            new(4, "System", null, IsNvidia: true),
            new(1576, "csrss", "", IsNvidia: true)
        ];

        IReadOnlyList<GpuUser> users = GpuActivitySummary.From(raw, windows);
        Check(users.Count == 2, $"only the two real programs survive, got {users.Count}");

        GpuUser claude = users[0];
        Check(claude.IsNvidia, "the RTX users come first");
        Check(claude.Processes == 2, $"both of its processes count as one program, got {claude.Processes}");
        Check(claude.IsStoreApp, "a WindowsApps path is a Store app, which is addressed by package id");
        Check(!users[1].IsNvidia && !users[1].IsStoreApp, "the Intel entry follows and is an ordinary program");

        Check(GpuActivitySummary.From([], windows).Count == 0, "nothing in, nothing out");
        // Case matters on neither side: Windows spells its own directory both ways in these
        // very rows ("C:\WINDOWS\system32" and "C:\Windows\System32").
        Check(GpuActivitySummary.From([new(1, "x", @"c:\windows\system32\x.exe", false)], windows).Count == 0,
            "the Windows directory is matched without regard to case");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
