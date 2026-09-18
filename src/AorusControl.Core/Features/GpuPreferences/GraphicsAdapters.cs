using System.Runtime.InteropServices;

namespace AorusControl.Core.Features.GpuPreferences;

/// <summary>One graphics chip, as the kernel's display driver interface names it.</summary>
/// <param name="Luid">The identity Windows uses for this adapter everywhere else, including
/// in the names of its performance counter instances.</param>
public sealed record GraphicsAdapter(long Luid, string Name)
{
    public bool IsNvidia => Name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The installed graphics chips and their LUIDs.
///
/// This exists to answer one question: which of the LUIDs in a "GPU Engine" counter instance
/// is the RTX. Asked through D3DKMT rather than through DXGI or the NVIDIA driver, because
/// this is a lookup in the kernel's adapter list - no device is created, nothing is rendered,
/// and a sleeping card has no reason to wake up for it. The same calls sit behind Task
/// Manager's per-process graphics column.
///
/// Everything here fails soft. Not knowing which chip is which costs a label; throwing on a
/// page refresh would cost the page.
/// </summary>
public static class GraphicsAdapters
{
    private const int AdapterRegistryInfo = 8;
    private const int MaxPath = 260;

    public static IReadOnlyList<GraphicsAdapter> Enumerate()
    {
        try { return Ask(); }
        catch (DllNotFoundException) { return []; }
        catch (EntryPointNotFoundException) { return []; }
    }

    private static IReadOnlyList<GraphicsAdapter> Ask()
    {
        // First call with no buffer: it only fills in how many adapters there are.
        var request = new EnumAdapters2 { NumAdapters = 0, Adapters = IntPtr.Zero };
        if (D3DKMTEnumAdapters2(ref request) != 0 || request.NumAdapters == 0) return [];

        int size = Marshal.SizeOf<AdapterInfo>();
        request.Adapters = Marshal.AllocHGlobal(size * (int)request.NumAdapters);
        try
        {
            if (D3DKMTEnumAdapters2(ref request) != 0) return [];
            var adapters = new List<GraphicsAdapter>((int)request.NumAdapters);
            for (int index = 0; index < request.NumAdapters; index++)
            {
                AdapterInfo info = Marshal.PtrToStructure<AdapterInfo>(request.Adapters + index * size);
                // The handle belongs to us from here on, whether or not the name comes back.
                try
                {
                    if (Name(info.Handle) is { Length: > 0 } name)
                        adapters.Add(new GraphicsAdapter(Key(info.Luid.HighPart, info.Luid.LowPart), name));
                }
                finally
                {
                    var close = new CloseAdapter { Handle = info.Handle };
                    D3DKMTCloseAdapter(ref close);
                }
            }
            return adapters;
        }
        finally { Marshal.FreeHGlobal(request.Adapters); }
    }

    /// <summary>The two halves of a LUID as one comparable number, the same way the counter
    /// instance names spell them: high part first, low part second.</summary>
    public static long Key(int highPart, uint lowPart) => ((long)highPart << 32) | lowPart;

    /// <summary>D3DKMT_ADAPTERREGISTRYINFO is four fixed strings in a row; only the first one
    /// is the adapter's name, and the rest is allocated so the driver has somewhere to put
    /// what it insists on writing.</summary>
    private static string? Name(uint adapter)
    {
        IntPtr buffer = Marshal.AllocHGlobal(MaxPath * 4 * sizeof(char));
        try
        {
            var query = new QueryAdapterInfo
            {
                Handle = adapter,
                Type = AdapterRegistryInfo,
                Data = buffer,
                DataSize = (uint)(MaxPath * 4 * sizeof(char))
            };
            return D3DKMTQueryAdapterInfo(ref query) == 0 ? Marshal.PtrToStringUni(buffer)?.Trim() : null;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AdapterInfo
    {
        public uint Handle;
        public Luid Luid;
        public uint SourceCount;
        public int PresentMoveRegionsPreferred;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EnumAdapters2
    {
        public uint NumAdapters;
        public IntPtr Adapters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct QueryAdapterInfo
    {
        public uint Handle;
        public int Type;
        public IntPtr Data;
        public uint DataSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CloseAdapter
    {
        public uint Handle;
    }

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTEnumAdapters2(ref EnumAdapters2 request);

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTQueryAdapterInfo(ref QueryAdapterInfo request);

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTCloseAdapter(ref CloseAdapter request);
}
