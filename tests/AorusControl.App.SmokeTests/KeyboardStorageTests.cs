using System.IO;
using AorusControl.Core.Features.Keyboard;
using AorusControl.Core.Models;

internal static class KeyboardStorageTests
{
    public static void Run()
    {
        string directory = Directory.CreateTempSubdirectory("Aorus-Rgb-Tests-").FullName;
        string path = Path.Combine(directory, "keyboard.json");
        try
        {
            var store = new KeyboardSettingsStore(path);
            Check(store.Load() is null, "missing file does not fabricate settings");
            var initial = new KeyboardLightingSettings(false, KeyboardBrightnessLevel.Low, KeyboardRgbEffect.ColorCycle,
                KeyboardEffectSpeed.Slow, new(1, 2, 3), new(4, 5, 6), new(7, 8, 9));
            store.Save(initial);
            Check(store.Load() == initial, "full state roundtrip including off and saved brightness");
            var next = initial with { Enabled = true, OnBrightness = KeyboardBrightnessLevel.Medium };
            store.Save(next);
            Check(store.Load() == next, "atomic replacement");
            Check(new KeyboardSettingsStore(path + ".bak").Load() == initial, "previous version retained");
            bool rejected = false;
            try { store.Save(next with { OnBrightness = (KeyboardBrightnessLevel)25 }); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected && store.Load() == next, "invalid save cannot damage existing configuration");
            string valid = File.ReadAllText(path);
            foreach (string invalid in new[]
            {
                "{", valid.Replace("\"Version\": 1", "\"Version\": 99"),
                valid.Replace("AORUS-5-SE4-FB0F", "OTHER"),
                valid.Replace("\"OnBrightness\": 32", "\"OnBrightness\": 25"),
                new string('x', 17000)
            })
            {
                File.WriteAllText(path, invalid);
                bool invalidRead = false;
                try { store.Load(); } catch (InvalidDataException) { invalidRead = true; }
                Check(invalidRead, "invalid configuration must be rejected");
            }
            Check(Directory.GetFiles(directory, "*.tmp").Length == 0, "temporary files cleaned");
            Console.WriteLine("PASS: RGB persistence roundtrip, backup, invalid values/version/device, corrupt/oversize files");
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".bak");
            Directory.Delete(directory); // Empty directory created exclusively for this test.
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
