using System.IO;
using AorusControl.Core.Features.Cooling;
using AorusControl.Core.Models;

internal static class FanCurveStoreTests
{
    private static FanCurvePoint[] SampleCurve() =>
        Enumerable.Range(0, 15)
            .Select(index => new FanCurvePoint((byte)index, (byte)(40 + index * 3), (byte)(57 + index * 12)))
            .ToArray()
            .With(last => last[^1] = new FanCurvePoint(14, 90, 229));

    public static void Run()
    {
        string directory = Directory.CreateTempSubdirectory("Aorus-Curve-Tests-").FullName;
        string path = Path.Combine(directory, "curve.json");
        try
        {
            var store = new FanCurveStore(path);
            Check(store.Load() is null, "missing file does not fabricate a curve");

            FanCurvePoint[] initial = SampleCurve();
            store.Save(initial);
            Check(store.Load()!.SequenceEqual(initial), "curve roundtrip");

            FanCurvePoint[] next = (FanCurvePoint[])initial.Clone();
            next[5] = next[5] with { Value = (byte)(next[5].Value + 1) };
            // Keep the result monotonic so the save itself stays valid.
            for (int i = 6; i < next.Length; i++)
                if (next[i].Value < next[i - 1].Value) next[i] = next[i] with { Value = next[i - 1].Value };
            store.Save(next);
            Check(store.Load()!.SequenceEqual(next), "atomic replacement");
            Check(new FanCurveStore(path + ".bak").Load()!.SequenceEqual(initial), "previous version retained");

            bool rejected = false;
            var invalidCurve = (FanCurvePoint[])initial.Clone();
            invalidCurve[^1] = invalidCurve[^1] with { Value = 200 }; // last point must force 229
            try { store.Save(invalidCurve); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected && store.Load()!.SequenceEqual(next), "invalid save cannot damage existing configuration");

            string valid = File.ReadAllText(path);
            foreach (string invalid in new[]
            {
                "{", valid.Replace("\"Version\": 1", "\"Version\": 99"),
                valid.Replace("AORUS-5-SE4-FB0F", "OTHER"),
                valid.Replace("\"Value\": 229", "\"Value\": 5"),
                new string('x', 17000)
            })
            {
                File.WriteAllText(path, invalid);
                bool invalidRead = false;
                try { store.Load(); } catch (InvalidDataException) { invalidRead = true; }
                Check(invalidRead, "invalid curve file must be rejected");
            }
            Check(Directory.GetFiles(directory, "*.tmp").Length == 0, "temporary files cleaned");
            Console.WriteLine("PASS: fan curve persistence roundtrip, backup, invalid curve/version/device, corrupt/oversize files");
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".bak");
            Directory.Delete(directory);
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}

internal static class ArrayExtensions
{
    public static T[] With<T>(this T[] array, Action<T[]> mutate)
    {
        mutate(array);
        return array;
    }
}
