using System;

namespace GraviSharp.UI;

public static class HeapVerifier
{
    private const int WarmupFrames = 60;

    public static bool Enabled { get; private set; }
    public static long MaxDelta { get; private set; }

    private static long _prevAllocated;

    public static void Enable()
    {
        Enabled = true;
    }

    public static void BeginFrame(int frameIndex)
    {
        if (!Enabled || frameIndex <= WarmupFrames) return;
        _prevAllocated = GC.GetTotalAllocatedBytes();
    }

    public static void EndFrame(int frameIndex)
    {
        if (!Enabled || frameIndex <= WarmupFrames) return;
        long curr = GC.GetTotalAllocatedBytes();
        long delta = curr - _prevAllocated;
        if (delta > MaxDelta)
        {
            MaxDelta = delta;
        }
    }

    public static int Report()
    {
        if (!Enabled) return 0;
        Console.WriteLine($"MaxHeapDeltaPerFrame={MaxDelta}");
        return MaxDelta > 0 ? 1 : 0;
    }
}
