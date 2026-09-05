using AorusControl.App.Controls;

internal static class KeyboardLayoutTests
{
    public static void Run()
    {
        IReadOnlyList<IReadOnlyList<KeyboardLayout.Key>> rows = KeyboardLayout.Rows;

        // Every row must span the same width, or the right-hand edge steps in and out and
        // the numeric pad stops lining up between rows. A row under one of the tall
        // numeric-pad keys carries that much less of its own - the tall key occupies those
        // columns already.
        const double RowUnits = 19;
        double carriedFromAbove = 0;
        for (int row = 0; row < rows.Count; row++)
        {
            double own = rows[row].Sum(key => key.Units);
            Check(Math.Abs(own + carriedFromAbove - RowUnits) < 0.001,
                $"row {row + 1} spans {own}u plus {carriedFromAbove}u reached into it, expected {RowUnits}u");
            carriedFromAbove = rows[row].Where(key => key.RowSpan > 1).Sum(key => key.Units);
        }

        Check(rows[2].Any(key => key.RowSpan == 2), "the numeric-pad plus spans two rows");
        Check(rows[4].Any(key => key.RowSpan == 2), "the numeric-pad enter spans two rows");

        // The arrow cluster is an inverted T: up sits directly above down. This is the
        // regression the file exists for - the up arrow used to sit above the RIGHT arrow,
        // which nothing but a screenshot would have revealed.
        double up = StartOf(rows[4], "▲");
        double down = StartOf(rows[5], "▼");
        double left = StartOf(rows[5], "◄");
        double right = StartOf(rows[5], "►");
        Check(Math.Abs(up - down) < 0.001,
            $"up arrow starts at {up}u but down arrow at {down}u - they must share a column");
        Check(left < down && down < right, "the bottom row reads left, down, right in that order");

        Check(rows.SelectMany(row => row).Any(key => key.IsGap),
            "the slot right of the up arrow is reserved, not taken by the next key");
        Check(rows.SelectMany(row => row).Where(key => key.IsGap).All(key => key.Legend.Length == 0),
            "a gap carries no legend");

        // Zones are contiguous bands across the keyboard, never interleaved.
        foreach (IReadOnlyList<KeyboardLayout.Key> row in rows)
        {
            int[] zones = row.Where(key => !key.IsGap).Select(key => key.Zone).ToArray();
            Check(zones.SequenceEqual(zones.OrderBy(zone => zone)),
                "zones must run 1 -> 2 -> 3 across a row, never jump back");
            Check(zones.All(zone => zone is >= 1 and <= 3), "every key belongs to one of the three zones");
        }

        Console.WriteLine("PASS: keyboard rows align, arrow cluster forms an inverted T, zones run left to right");
    }

    private static double StartOf(IReadOnlyList<KeyboardLayout.Key> row, string legend)
    {
        double units = 0;
        foreach (KeyboardLayout.Key key in row)
        {
            if (key.Legend == legend) return units;
            units += key.Units;
        }

        throw new InvalidOperationException($"Taste '{legend}' nicht in der Reihe gefunden.");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
