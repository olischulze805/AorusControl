using System.IO;
using AorusControl.Core.Features.Keyboard;
using AorusControl.Core.Models;

internal static class RecentColorsStoreTests
{
    public static void Run()
    {
        string directory = Directory.CreateTempSubdirectory("Aorus-Colors-Tests-").FullName;
        string path = Path.Combine(directory, "colors.json");
        try
        {
            var store = new RecentColorsStore(path, capacity: 3);
            Check(store.Load().Count == 0, "missing file yields an empty list, not an error");

            var colors = new[]
            {
                new KeyboardRgbColor(255, 0, 0),
                new KeyboardRgbColor(0, 255, 0),
                new KeyboardRgbColor(0, 0, 255),
            };
            store.Save(colors);
            Check(store.Load().SequenceEqual(colors), "roundtrip preserves order");

            store.Save(new[] { new KeyboardRgbColor(1, 2, 3) }.Concat(colors).ToArray());
            Check(store.Load().Count == 3, "capacity caps the persisted list even if more are saved");
            Check(store.Load()[0] == new KeyboardRgbColor(1, 2, 3), "most recent stays first");

            // Convenience data: any corruption must fail soft to an empty list, never throw.
            File.WriteAllText(path, "{ not valid json");
            Check(store.Load().Count == 0, "corrupt file must not throw, just yields empty");
            File.WriteAllText(path, "{\"Version\": 99, \"Colors\": [\"#FF0000\"]}");
            Check(store.Load().Count == 0, "unknown version must not throw, just yields empty");
            File.WriteAllText(path, "{\"Version\": 1, \"Colors\": [\"not-a-color\", \"#00FF00\"]}");
            Check(store.Load().SequenceEqual(new[] { new KeyboardRgbColor(0, 255, 0) }), "unparseable entries are skipped, valid ones kept");

            Check(Directory.GetFiles(directory, "*.tmp").Length == 0, "temporary files cleaned");
            Console.WriteLine("PASS: recent colors capacity, ordering, and fail-soft corrupt/version/entry handling");
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
