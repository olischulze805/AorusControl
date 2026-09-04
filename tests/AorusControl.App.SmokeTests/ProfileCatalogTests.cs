using System.IO;
using AorusControl.Core.Features.PowerProfiles;
using AorusControl.Core.Models;
using AorusControl.Core.Services;

internal static class ProfileCatalogTests
{
    public static void Run()
    {
        var curve = Enumerable.Range(0, 15).Select(i => new FanCurvePoint((byte)i, (byte)(30 + i * 4), (byte)(i == 14 ? 229 : 57 + i * 10))).ToArray();
        var profile = new LaptopProfile(Guid.NewGuid(), "Eigene Kurve", WindowsPowerOverlayMode.Balanced, ProfileCoolingMode.CustomCurve, curve: curve);
        var catalog = new ProfileCatalog([profile], new(profile.Id, profile.Id));
        var removed = catalog.Remove(profile.Id);
        Check(removed.Profiles.Count == 0 && removed.Assignments == new PowerProfileAssignments(null, null), "delete clears both assignments");
        Reject(() => new ProfileCatalog([profile, profile], new(null, null)));
        Reject(() => new ProfileCatalog([], new(profile.Id, null)));
        var renamed = new LaptopProfile(profile.Id, "Umbenannt", profile.PowerMode, ProfileCoolingMode.Normal);
        Check(catalog.Upsert(renamed).Profiles.Single().Name == "Umbenannt", "update existing ID");
        string directory = Directory.CreateTempSubdirectory("Aorus-Profiles-").FullName;
        string path = Path.Combine(directory, "profiles.json");
        try
        {
            var store = new ProfileCatalogStore(path);
            Check(store.Load() is null, "missing file");
            store.Save(catalog);
            var loaded = store.Load()!;
            Check(loaded.Assignments == catalog.Assignments && loaded.Profiles.Single().Curve!.SequenceEqual(curve), "full curve roundtrip");
            store.Save(catalog.Upsert(renamed));
            Check(store.Load()!.Profiles.Single().Name == "Umbenannt", "replacement");
            Check(new ProfileCatalogStore(path + ".bak").Load()!.Profiles.Single().Name == profile.Name, "backup");
            string valid = File.ReadAllText(path);
            foreach (string invalid in new[] { "{", "{}", valid.Replace("\"Version\": 1", "\"Version\": 2"), valid.Replace("AORUS-5-SE4-FB0F", "OTHER"), valid.Replace("\"PowerMode\": 0", "\"PowerMode\": 999"), new string('x', 262145) })
            {
                File.WriteAllText(path, invalid);
                try { store.Load(); throw new Exception("Invalid file accepted"); }
                catch (InvalidDataException) { }
            }
            Check(Directory.GetFiles(directory, "*.tmp").Length == 0, "temporary cleanup");
        }
        finally { File.Delete(path); File.Delete(path + ".bak"); Directory.Delete(directory); }
        Console.WriteLine("PASS: profile catalog references, deletion, update, persistence, curves, backup and invalid files");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new Exception("Invalid catalog accepted");
    }
}
