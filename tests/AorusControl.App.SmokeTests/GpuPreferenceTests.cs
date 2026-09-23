using System.IO;
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
        Migrate();
        Plan();
        await Restart();
        Suggest();
        Picker();
        await Module();
        await PowerWatcher();
        Console.WriteLine("PASS: GPU preference parsing, foreign settings kept, registry round trip, per-program rules, file migration, suggestions, program search, automatic power watching and the module");
    }

    private static async Task PowerWatcher()
    {
        const string game = @"C:\App\game.exe";
        var registry = new FakeGpuPreferenceStore();
        registry.Seed(game, "GpuPreference=1;");
        var settings = new FakeGpuPreferenceSettings();
        settings.Save([new ManagedProgram(game, GpuPreference.Integrated)]);
        LaptopPowerSource source = LaptopPowerSource.Unknown;

        // Keep the real background loop asleep. Its individual check is exercised directly,
        // which makes the startup race deterministic instead of adding a two-second test.
        using var module = new GpuPreferenceViewModel(registry, settings, () => source,
            new FakeGpuActivity(), new FakeProgramCloser(), confirm: _ => false,
            powerWatchWait: (_, token) => Task.Delay(Timeout.InfiniteTimeSpan, token));
        await module.StartAsync();
        Check(registry.Writes == 0, "an unknown source at early startup is left untouched");

        source = LaptopPowerSource.Ac;
        await module.CheckPowerSourceAsync();
        Check(registry.Find(game)!.Preference == GpuPreference.Nvidia,
            "the fallback watcher applies the mains rule as soon as the source becomes known");

        int before = registry.Writes;
        await module.CheckPowerSourceAsync();
        Check(registry.Writes == before, "an unchanged source causes no repeated registry writes");

        source = LaptopPowerSource.Battery;
        await module.CheckPowerSourceAsync();
        Check(registry.Find(game)!.Preference == GpuPreference.Integrated,
            "the fallback watcher applies the battery rule without opening the graphics page");
    }

    private static async Task Module()
    {
        const string game = @"C:\App\game.exe";
        var registry = new FakeGpuPreferenceStore();
        registry.Seed(game, "AutoHDREnable=1;GpuPreference=0;");
        LaptopPowerSource source = LaptopPowerSource.Ac;
        var settings = new FakeGpuPreferenceSettings();

        var running = new FakeGpuActivity(
            new GpuUser("game", game, IsNvidia: true, ProcessIds: [1000]),
            new GpuUser("editor", @"C:\App\editor.exe", IsNvidia: false, ProcessIds: [1000, 1001]));
        var closer = new FakeProgramCloser();
        bool confirmed = false;
        using var module = new GpuPreferenceViewModel(registry, settings, () => source, running, closer,
            confirm: _ => confirmed);
        await module.StartAsync();
        await module.StartAsync();
        Check(settings.Loads == 1, "a second start does not register power events or reload managed programs again");
        Check(registry.Writes == 0, "an empty list writes nothing at startup");

        // The running list is not read until the page is on screen: reading the graphics
        // counters costs half a second, and nobody is looking while the app sits in the tray.
        Check(running.Reads == 0, "the running list stays unread while the page is hidden");
        await module.RefreshRunningCommand.ExecuteAsync();
        Check(module.Running.Count == 2 && module.HasRunning, "both running programs are listed");
        Check(module.Running[0].IsNvidia && module.RunningSummary.Contains("hält die RTX wach"),
            "the summary counts the one program on the discrete card");
        Check(module.Running[0].CanAdd, "an unmanaged, non-Store program can be taken over from here");
        Check(module.Running[1].Detail.Contains("2 Prozesse"), "several processes of one program are said once");
        Check(!module.Running[1].Detail.Contains(@"C:\"), "the path stays on the tooltip - it wraps to three lines otherwise");

        await module.AddAsync(game);
        Check(registry.Find(game)!.Preference == GpuPreference.Nvidia, "a program added on mains goes to the RTX");
        Check(!module.Running[0].CanAdd && module.Running[0].Detail.Contains("verwaltet"),
            "and the running entry says so instead of offering the button again");

        // Ending somebody's program needs a yes, and the row on the Intel chip is not what is
        // keeping the card awake, so it is not offered at all.
        Check(module.Running[0].CanClose && !module.Running[1].CanClose,
            "closing is offered for the RTX user and not for the Intel one");
        await module.CloseRunningCommand.ExecuteAsync(module.Running[0]);
        Check(closer.Asked.Count == 0, "a declined confirmation closes nothing");
        confirmed = true;
        await module.CloseRunningCommand.ExecuteAsync(module.Running[1]);
        Check(closer.Asked.Count == 0, "and a program on the Intel chip is refused even with a yes");
        await module.CloseRunningCommand.ExecuteAsync(module.Running[0]);
        Check(closer.Asked.SequenceEqual(module.Running[0].ProcessIds) || closer.Asked.Count == 1,
            "a confirmed close takes every process of that program");
        confirmed = false;

        running.Unreadable = true;
        await module.RefreshRunningCommand.ExecuteAsync();
        Check(module.Running.Count == 0 && module.RunningSummary.Contains("nichts her"),
            "an unreadable counter set says so rather than claiming nothing is on the card");
        running.Unreadable = false;
        await module.RefreshRunningCommand.ExecuteAsync();
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

    /// <summary>
    /// The promise "a change you make yourself in Windows is not overruled" used to last
    /// exactly one session: what this app had written lived in a dictionary in memory, and a
    /// restart forgot it and quietly took the program back over.
    /// </summary>
    private static async Task Restart()
    {
        const string game = @"C:\App\game.exe";
        var registry = new FakeGpuPreferenceStore();
        var settings = new FakeGpuPreferenceSettings();
        LaptopPowerSource source = LaptopPowerSource.Ac;

        GpuPreferenceViewModel New() => new(registry, settings, () => source,
            new FakeGpuActivity(), new FakeProgramCloser(), confirm: _ => false);

        using (var first = New())
        {
            await first.StartAsync();
            await first.AddAsync(game);
            Check(registry.Find(game)!.Preference == GpuPreference.Nvidia, "adopted and set on mains");
        }

        // The user opens Windows' own graphics settings and puts it back.
        registry.Set(game, GpuPreference.Integrated);

        using (var afterRestart = New())
        {
            await afterRestart.StartAsync();
            Check(registry.Find(game)!.Preference == GpuPreference.Integrated,
                "a freshly started app leaves that decision alone");
            Check(afterRestart.Programs[0].State.Contains("von Hand"), "and says why it is not acting");
        }
    }

    private static void Picker()
    {
        // Both kinds side by side, as the picker really gets them: a path for a normal
        // program, an id for a Store app that has no path at all.
        InstalledProgram[] store = [new("Netflix", "4DF9E0F8.Netflix_mcm4njqhnhss8!Netflix.App")];
        InstalledProgram[] classic =
        [
            new("Google Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe"),
            new("Chrome Remote Desktop", @"C:\Program Files\Google\Chrome\Application\chrome_proxy.exe"),
            new("VLC media player", @"C:\Program Files\VideoLAN\VLC\vlc.exe"),
            // The same program twice, as two start-menu folders really do list it.
            new("Google Chrome (Kopie)", @"C:\Program Files\Google\Chrome\Application\chrome.exe"),
            new("", @"C:\Program Files\Nameless\x.exe"),
            new("Windows-Einstellung", "DirectXUserGlobalSettings")
        ];

        IReadOnlyList<InstalledProgram> all = InstalledPrograms.Merge(store, classic);
        Check(all.Count == 4, $"duplicates and non-programs are dropped, got {all.Count}");
        Check(all.Any(program => program.Identity.EndsWith("!Netflix.App", StringComparison.Ordinal)), "the Store app is in");
        Check(all.Single(program => program.Identity.EndsWith(@"\chrome.exe", StringComparison.Ordinal)).Name == "Google Chrome",
            "the first name for an identity wins");
        Check(all.First().Name == "Chrome Remote Desktop", "sorted by name");
        Check(all.Single(program => program.Identity.Contains("Netflix", StringComparison.Ordinal)).IsStoreApp,
            "an id without a path is a Store app");
        Check(!all.First().IsStoreApp, "a path is not");

        Check(InstalledPrograms.Search(all, null).Count == 4, "no query shows everything");
        Check(InstalledPrograms.Search(all, "net").Single().Name == "Netflix", "a Store app is findable by name");
        Check(InstalledPrograms.Search(all, "chrome").Count == 2, "two programs match chrome, both by name");
        // "google" is in one name and in both paths, which is the case the ranking is for.
        Check(InstalledPrograms.Search(all, "google").First().Name == "Google Chrome",
            "a hit in the name comes before one that only matches the path");
        Check(InstalledPrograms.Search(all, "videolan").Single().Name == "VLC media player", "the path is searched too");
        Check(InstalledPrograms.Search(all, "chrome remote").Single().Name == "Chrome Remote Desktop",
            "several words all have to match");
        Check(InstalledPrograms.Search(all, "gibtesnicht").Count == 0, "and no match is no match");
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

    /// <summary>The settings file gained a per-program pair in version 2. A file written by
    /// an older build still has to load, or an update would quietly empty the list.</summary>
    private static void Migrate()
    {
        string path = Path.Combine(Path.GetTempPath(), "aorus-gpu-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path,
                """{"Version":1,"Programs":[{"Name":"C:\\App\\game.exe","Original":2}]}""");
            IReadOnlyList<ManagedProgram> loaded = new GpuPreferenceSettingsStore(path).Load();
            Check(loaded.Count == 1 && loaded[0].Original == GpuPreference.Nvidia, "a version 1 file still loads");
            Check(loaded[0].OnAc == GpuPreference.Nvidia && loaded[0].OnBattery == GpuPreference.Integrated,
                "and its entries get exactly the fixed rule that version applied");

            var store = new GpuPreferenceSettingsStore(path);
            store.Save([loaded[0] with { OnAc = GpuPreference.Integrated }]);
            Check(store.Load()[0].OnAc == GpuPreference.Integrated, "a changed rule survives the round trip");
            Check(File.ReadAllText(path).Contains("\"Version\": 2", StringComparison.Ordinal),
                "and is written back in the new version");
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".bak");
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
            Current((game.Name, "GpuPreference=2;"), (editor.Name, "GpuPreference=2;")));
        Check(onBattery.Count == 2 && onBattery.All(step => step.Target == GpuPreference.Integrated),
            "on battery everything managed moves to the Intel chip");

        var onMains = GpuSwitchPlan.Steps(LaptopPowerSource.Ac, managed,
            Current((game.Name, "GpuPreference=1;"), (editor.Name, "GpuPreference=1;")));
        Check(onMains.All(step => step.Target == GpuPreference.Nvidia), "on mains they move back to the RTX");

        Check(GpuSwitchPlan.Steps(LaptopPowerSource.Unknown, managed, Current((game.Name, "GpuPreference=1;"))).Count == 0, "an unknown power source changes nothing");

        Check(GpuSwitchPlan.Steps(LaptopPowerSource.Battery, managed,
            Current((game.Name, "GpuPreference=1;"), (editor.Name, "GpuPreference=1;"))).Count == 0, "a program already there is not rewritten");

        // Somebody set the game back to the RTX by hand after we last wrote the Intel chip.
        // That is a decision, not drift, and it is left standing.
        var afterManualChange = GpuSwitchPlan.Steps(LaptopPowerSource.Battery,
            [game with { LastWritten = GpuPreference.Integrated }, editor],
            Current((game.Name, "GpuPreference=2;"), (editor.Name, "GpuPreference=2;")));
        Check(afterManualChange.Count == 1 && afterManualChange[0].Name == editor.Name,
            "a manually changed program is skipped, the others still follow");

        // A program with its own rules: a browser belongs on the Intel chip on mains too.
        var browser = new ManagedProgram(@"C:\Approwser.exe", null,
            OnAc: GpuPreference.Integrated, OnBattery: GpuPreference.Integrated);
        var withOwnRule = GpuSwitchPlan.Steps(LaptopPowerSource.Ac, [browser],
            Current((browser.Name, "GpuPreference=2;")));
        Check(withOwnRule.Count == 1 && withOwnRule[0].Target == GpuPreference.Integrated,
            "its own rule beats the default, on mains as well");
        Check(GpuSwitchPlan.Steps(LaptopPowerSource.Battery, [browser],
            Current((browser.Name, "GpuPreference=1;"))).Count == 0,
            "and a program whose two rules agree is written once, not on every change of supply");
        Check(browser.For(LaptopPowerSource.Ac) == GpuPreference.Integrated
            && game.For(LaptopPowerSource.Ac) == GpuPreference.Nvidia
            && game.For(LaptopPowerSource.Battery) == GpuPreference.Integrated,
            "the defaults are still the rule that used to be wired in");

        var pinned = GpuSwitchPlan.Steps(LaptopPowerSource.Battery, [game],
            Current((game.Name, "SpecificAdapter=10DE&249D;GpuPreference=1073741824;")));
        Check(pinned.Count == 0, "a pinned adapter is never overruled");

        ManagedProgram ours = game with { LastWritten = GpuPreference.Integrated };
        Check(GpuSwitchPlan.Release(ours, new GpuPreferenceProgram(game.Name, "GpuPreference=1;"))
            is { Target: GpuPreference.WindowsDecides }, "releasing hands the original setting back");
        Check(GpuSwitchPlan.Release(editor with { LastWritten = GpuPreference.Integrated },
            new GpuPreferenceProgram(editor.Name, "GpuPreference=1;")) is null,
            "a program that had no setting before keeps whatever it has now");
        Check(GpuSwitchPlan.Release(ours, new GpuPreferenceProgram(game.Name, "GpuPreference=2;")) is null,
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
