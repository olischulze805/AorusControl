namespace AorusControl.App.Controls;

/// <summary>
/// The physical key layout of this laptop's keyboard, transcribed from the device: six
/// rows, full numeric pad, and which of the three RGB zones each key belongs to.
///
/// The zone boundaries are where the hardware puts them, not at neat thirds - on this
/// keyboard zone 1 reaches to about F6/T/G/V, zone 2 covers roughly F7-F9/Y-O/H-L/B-M,
/// and zone 3 takes the remainder including the whole numeric pad. That is why the
/// boundary sits at a different key in every row.
/// </summary>
internal static class KeyboardLayout
{
    /// <param name="Units">Key width in standard key units (1u = a letter key).</param>
    /// <param name="Zone">RGB zone 1-3.</param>
    /// <param name="RowSpan">Grid rows covered; 2 for the tall numeric-pad keys.</param>
    internal sealed record Key(string Legend, int Zone, double Units = 1, int RowSpan = 1);

    internal static IReadOnlyList<IReadOnlyList<Key>> Rows { get; } =
    [
        // Function row
        [
            new("Esc", 1), new("F1", 1), new("F2", 1), new("F3", 1), new("F4", 1), new("F5", 1), new("F6", 1),
            new("F7", 2), new("F8", 2), new("F9", 2),
            new("F10", 3), new("F11", 3), new("F12", 3), new("Pause", 3), new("Del", 3),
            new("Home", 3), new("PgUp", 3), new("PgDn", 3), new("End", 3)
        ],
        // Number row + numeric pad top
        [
            new("~", 1), new("1", 1), new("2", 1), new("3", 1), new("4", 1), new("5", 1),
            new("6", 2), new("7", 2), new("8", 2), new("9", 2),
            new("0", 3), new("-", 3), new("=", 3), new("←", 3, 2),
            new("NumLk", 3), new("/", 3), new("*", 3), new("−", 3)
        ],
        // Upper letter row
        [
            new("Tab", 1, 1.5), new("Q", 1), new("W", 1), new("E", 1), new("R", 1), new("T", 1),
            new("Y", 2), new("U", 2), new("I", 2), new("O", 2),
            new("P", 3), new("[", 3), new("]", 3), new("\\", 3, 1.5),
            new("7", 3), new("8", 3), new("9", 3), new("+", 3, 1, RowSpan: 2)
        ],
        // Home row
        [
            new("Caps", 1, 1.75), new("A", 1), new("S", 1), new("D", 1), new("F", 1), new("G", 1),
            new("H", 2), new("J", 2), new("K", 2), new("L", 2),
            new(";", 3), new("'", 3), new("↵", 3, 2.25),
            new("4", 3), new("5", 3), new("6", 3)
        ],
        // Lower letter row
        [
            new("⇧", 1, 2.25), new("Z", 1), new("X", 1), new("C", 1), new("V", 1),
            new("B", 2), new("N", 2), new("M", 2),
            new(",", 3), new(".", 3), new("/", 3), new("⇧", 3, 1.75), new("▲", 3),
            new("1", 3), new("2", 3), new("3", 3), new("↵", 3, 1, RowSpan: 2)
        ],
        // Modifier row
        [
            new("Ctrl", 1, 1.25), new("Fn", 1), new("⊞", 1), new("Alt", 1, 1.25),
            new("", 2, 4),
            new("AltGr", 3, 1.25), new("▤", 3), new("Ctrl", 3, 1.25),
            new("◄", 3), new("▼", 3), new("►", 3),
            new("0", 3, 2), new(".", 3)
        ]
    ];
}
