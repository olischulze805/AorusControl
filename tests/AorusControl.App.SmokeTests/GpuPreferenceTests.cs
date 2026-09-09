using Microsoft.Win32;
using AorusControl.App.Features.GpuPreferences;
using AorusControl.Core.Features.GpuPreferences;
using AorusControl.Core.Features.PowerProfiles;

internal static class GpuPreferenceTests
{
    public static async Task RunAsync()
    {
        Text();
        Store();
        Plan();
        Suggest();
        await Module();
        Console.WriteLine("PASS: GPU preference parsing, foreign settings kept, registry round trip, AC/battery plan, suggestions and the module");
    }

    private static async Task Module()
    {
        const string game = @"C:\App\game.exe";
        var registry = new FakeGpuPreferenceStore();
        registry.Seed(game, "AutoHDREnable=1;GpuPreference=0;");
        LaptopPowerSource source = LaptopPowerSource.Ac;

        using var module = new GpuPreferenceViewModel(registry, new FakeGpuPreferenceSettings(), () => source);
        await module.StartAsync();
        Check(registry.Writes == 0, "an empty list writes nothing at startup");

        await module.AddAsync(game);
        Check(registry.Find(game)!.Preference == GpuPreference.Nvidia, "a program added on mains goes to the RTX");
        Check(registry.Find(game)!.Raw.StartsWith("AutoHDREnable=1;", StringComparison.Ordinal),
            "and its other Windows settings survive");
        Check(module.Programs.Count == 1 && module.Programs[0].State.Contains("RTX"), "the list says where it stands");

        source = LaptopPowerSource.Battery;
        await module.ApplyNowCommand.ExecuteAsync();
        Check(registry.Find(game)!.Preference == GpuPreference.Integrated, "on battery it moves to the Intel chip");

        int before = registry.Writes;
        await module.ApplyNowCommand.ExecuteAsync();
        Check(registry.Writes == before, "applying again writes nothing when it is already right");

        // Somebody sets it back by hand in Windows. That is a decision, not drift.
        registry.Set(game, GpuPreference.Nvidia);
        before = registry.Writes;
        await module.ApplyNowCommand.ExecuteAsync();
        Check(registry.Writes == before, "a manual change is not overruled");
        Check(module.Programs[0].State.Contains("von Hand"), "and is visible as such");

        source = LaptopPowerSource.Unknown;
        before = registry.Writes;
        await module.ApplyNowCommand.ExecuteAsync();
        Check(registry.Writes == before, "an unknown power source changes nothing");

        module.IsAutomatic = false;
        source = LaptopPowerSource.Battery;
        before = registry.Writes;
        await module.ApplyNowCommand.ExecuteAsync();
        Check(registry.Writes == before, "with the automatic off nothing is written either");

        await module.RemoveCommand.ExecuteAsync(module.Programs[0]);
        Check(module.Programs.Count == 0, "removing takes it off the list");
        Check(registry.Find(game)!.Preference == GpuPreference.Nvidia,
            "and leaves the manual choice alone rather than resetting over it");
    }

    private static void Suggest()
    {
        // The real entries of this machine, including the Store app that has no path at all
        // and so could never be picked from a file dialog.
        GpuPreferenceProgram[] registry =
        [
            new(@"C:\Games\theHunterCotW_F.exe", "GpuPreference=2;"),
            new("4DF9E0F8.Netflix_mcm4njqhnhss8!Netflix.App", "GpuPreference=2;"),
            new(@"C:\Program Files\VideoLAN\VLC\vlc.exe", "SpecificAdapter=10DE&249D;GpuPreference=1073741824;"),
            new(@"C:\Windows\System32\ShellHost.exe", "GpuPreference=1;"),
            new(@"C:\Program Files\Google\Chrome\chrome.exe", "GpuPreference=0;"),
            new(@"C:\Gone\removed.exe", "GpuPreference=2;")
        ];
        bool Exists(string name) => !name.Contains(@"\Gone\", StringComparison.Ordinal);

        GpuSuggestionResult result = GpuSuggestions.From(registry, [], Exists);
        Check(result.Suggestions.Count == 2, $"only the two high-performance programs are offered, got {result.Suggestions.Count}");
        Check(result.Suggestions.Any(suggestion => suggestion.Name.Contains("Netflix", StringComparison.Ordinal)),
            "a Store app is offered by its id - nothing else can add it");
        Check(result.Suggestions.All(suggestion => !suggestion.Name.Contains("vlc", StringComparison.OrdinalIgnoreCase)),
            "a pinned adapter is not a candidate");
        Check(result.Suggestions.All(suggestion => !suggestion.Name.Contains("ShellHost", StringComparison.Ordinal)),
            "programs already on the Intel chip are left alone");
        Check(result.MissingPrograms == 1, "an uninstalled program is counted, not offered");

        ManagedProgram[] managed = [new(@"C:\Games\theHunterCotW_F.exe", GpuPreference.Nvidia)];
        Check(GpuSuggestions.From(registry, managed, Exists).Suggestions.Count == 1,
            "what is already managed is not suggested again");

        Check(GpuSuggestions.StillInstalled("4DF9E0F8.Netflix_mcm4njqhnhss8!Netflix.App"),
            "a Store app id is not a path and cannot be checked on disk");
    }

    private static void Text()
    {
        // The entries this machine really has, including the two that are not programs and
        // the one where Windows has pinned an adapter.
        Check(!GpuPreferenceText.IsProgramEntry("DirectXUserGlobalSettings"), "global settings are not a program");
        Check(!GpuPreferenceText.IsProgramEntry("GraphicsFeaturesNotificationConfig"), "neither is the notification config");
        Check(GpuPreferenceText.IsProgramEntry(@"C:\App\game.exe"), "a path is a program");
        Check(GpuPreferenceText.IsProgramEntry("4DF9E0F8.Netflix_mcm4njqhnhss8!Netflix.App"), "so is a Store app id");

        Check(GpuPreferenceText.Read("GpuPreference=2;") == GpuPreference.Nvidia, "2 is the RTX");
        Check(GpuPreferenceText.Read("GpuPreference=1;") == GpuPreference.Integrated, "1 is the Intel chip");
        Check(GpuPreferenceText.Read("GpuPreference=0;") == GpuPreference.WindowsDecides, "0 leaves it to Windows");
        Check(GpuPreferenceText.Read("") is null, "an empty entry says nothing");
        Check(GpuPreferenceText.Read("SwapEffectUpgradeEnable=1;") is null, "and neither does an unrelated one");
        Check(GpuPreferenceText.Read("GpuPreference=1073741824;") is null,
            "the adapter-pinning flag is not one of the three settings");

        // The whole reason this is parsed rather than overwritten: everything else in the
        // entry has to survive, in place.
        const string vlc = "SwapEffectUpgradeEnable=1;AutoHDREnable=4147;SpecificAdapter=10DE&249D&15461458;GpuPreference=1073741824;";
        Check(GpuPreferenceText.PinsOneAdapter(vlc), "VLC pins one adapter");
        Check(!GpuPreferenceText.PinsOneAdapter("GpuPreference=2;"), "a plain preference does not");
        string switched = GpuPreferenceText.With(vlc, GpuPreference.Integrated);
        Check(switched.StartsWith("SwapEffectUpgradeEnable=1;AutoHDREnable=4147;SpecificAdapter=10DE&249D&15461458;", StringComparison.Ordinal),
            "HDR, swap chain and adapter are kept, in their original order");
        Check(switched.EndsWith("GpuPreference=1;", StringComparison.Ordinal), "and only the preference changes");

        Check(GpuPreferenceText.With("AutoHDREnable=1;", GpuPreference.Nvidia) == "AutoHDREnable=1;GpuPreference=2;",
            "a missing preference is appended");
        Check(GpuPreferenceText.With(null, GpuPreference.Nvidia) == "GpuPreference=2;", "an absent entry becomes a new one");
        Check(GpuPreferenceText.With("Broken;GpuPreference=0;", GpuPreference.Nvidia) == "Broken;GpuPreference=2;",
            "a token that is not name=value is carried through unchanged");
    }

    private static void Store()
    {
        // A key of our own under HKCU: the real graphics preferences of this machine are the
        // user's, and a test must not write into them.
        string keyPath = @"Software\AorusControl\Tests\Gpu-" + Guid.NewGuid().ToString("N");
        try
        {
            var store = new WindowsGpuPreferenceStore(keyPath);
            Check(store.List().Count == 0, "a missing key is an empty list, not a crash");
            Check(store.Find(@"C:\App\game.exe") is null, "and holds no programs");

            store.Set(@"C:\App\game.exe", GpuPreference.Nvidia);
            Check(store.Find(@"C:\App\game.exe")!.Preference == GpuPreference.Nvidia, "written and read back");
            store.Set(@"C:\App\game.exe", GpuPreference.Integrated);
            Check(store.Find(@"C:\App\game.exe")!.Preference == GpuPreference.Integrated, "and switched again");

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true))
            {
                key.SetValue("DirectXUserGlobalSettings", "AutoHDREnable=0;");
                key.SetValue(@"C:\App\pinned.exe", "SpecificAdapter=10DE&249D&15461458;GpuPreference=1073741824;");
                key.SetValue(@"C:\App\hdr.exe", "AutoHDREnable=4147;GpuPreference=1;");
            }

            IReadOnlyList<GpuPreferenceProgram> programs = store.List();
            Check(programs.Count == 3, $"Windows' own settings are not listed as programs, got {programs.Count}");
            Check(programs.Single(program => program.Name.EndsWith("pinned.exe", StringComparison.Ordinal)).IsManageable == false,
                "a pinned adapter marks the program as not ours to switch");
            Reject(() => store.Set(@"C:\App\pinned.exe", GpuPreference.Integrated), "and refuses the write");
            Reject(() => store.Set("DirectXUserGlobalSettings", GpuPreference.Integrated), "as does Windows' own value");

            store.Set(@"C:\App\hdr.exe", GpuPreference.Nvidia);
            Check(store.Find(@"C:\App\hdr.exe")!.Raw == "AutoHDREnable=4147;GpuPreference=2;",
                "the HDR setting beside it survives the switch");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\AorusControl\Tests", throwOnMissingSubKey: false);
        }
    }

    private static void Plan()
    {
        var game = new ManagedProgram(@"C:\App\game.exe", GpuPreference.WindowsDecides);
        var editor = new ManagedProgram(@"C:\App\editor.exe", null);
        ManagedProgram[] managed = [game, editor];

        Dictionary<string, GpuPreferenceProgram> Current(params (string Name, string Raw)[] entries) =>
            entries.ToDictionary(entry => entry.Name, entry => new GpuPreferenceProgram(entry.Name, entry.Raw));

        var onBattery = GpuSwitchPlan.Steps(LaptopPowerSource.Battery, managed,
            Current((game.Name, "GpuPreference=2;"), (editor.Name, "GpuPreference=2;")), new Dictionary<string, GpuPreference>());
        Check(onBattery.Count == 2 && onBattery.All(step => step.Target == GpuPreference.Integrated),
            "on battery everything managed moves to the Intel chip");

        var onMains = GpuSwitchPlan.Steps(LaptopPowerSource.Ac, managed,
            Current((game.Name, "GpuPreference=1;"), (editor.Name, "GpuPreference=1;")), new Dictionary<string, GpuPreference>());
        Check(onMains.All(step => step.Target == GpuPreference.Nvidia), "on mains they move back to the RTX");

        Check(GpuSwitchPlan.Steps(LaptopPowerSource.Unknown, managed, Current((game.Name, "GpuPreference=1;")),
            new Dictionary<string, GpuPreference>()).Count == 0, "an unknown power source changes nothing");

        Check(GpuSwitchPlan.Steps(LaptopPowerSource.Battery, managed,
            Current((game.Name, "GpuPreference=1;"), (editor.Name, "GpuPreference=1;")),
            new Dictionary<string, GpuPreference>()).Count == 0, "a program already there is not rewritten");

        // Somebody set the game back to the RTX by hand after we last wrote the Intel chip.
        // That is a decision, not drift, and it is left standing.
        var afterManualChange = GpuSwitchPlan.Steps(LaptopPowerSource.Battery, managed,
            Current((game.Name, "GpuPreference=2;"), (editor.Name, "GpuPreference=2;")),
            new Dictionary<string, GpuPreference> { [game.Name] = GpuPreference.Integrated });
        Check(afterManualChange.Count == 1 && afterManualChange[0].Name == editor.Name,
            "a manually changed program is skipped, the others still follow");

        var pinned = GpuSwitchPlan.Steps(LaptopPowerSource.Battery, [game],
            Current((game.Name, "SpecificAdapter=10DE&249D;GpuPreference=1073741824;")), new Dictionary<string, GpuPreference>());
        Check(pinned.Count == 0, "a pinned adapter is never overruled");

        Check(GpuSwitchPlan.Release(game, new GpuPreferenceProgram(game.Name, "GpuPreference=1;"), GpuPreference.Integrated)
            is { Target: GpuPreference.WindowsDecides }, "releasing hands the original setting back");
        Check(GpuSwitchPlan.Release(editor, new GpuPreferenceProgram(editor.Name, "GpuPreference=1;"), GpuPreference.Integrated) is null,
            "a program that had no setting before keeps whatever it has now");
        Check(GpuSwitchPlan.Release(game, new GpuPreferenceProgram(game.Name, "GpuPreference=2;"), GpuPreference.Integrated) is null,
            "and one changed by hand is not reset either");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (Exception) { return; }
        throw new Exception(message);
    }
}
