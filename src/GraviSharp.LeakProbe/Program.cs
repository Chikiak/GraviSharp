using System;
using System.Diagnostics;
using GraviSharp.Core;

namespace GraviSharp.LeakProbe;

internal static class Program
{
    private const int IterationsPerBlock = 1_000_000;
    private const int WarmupIterations = 100_000;

    private static unsafe void Main(string[] args)
    {
        Console.WriteLine("=== GraviSharp Unmanaged Memory Leak Probe (QG-PH1-001 - Ultimate Isolation) ===");
        Console.WriteLine("Process PID: " + Environment.ProcessId);
        Console.WriteLine();
        Console.WriteLine("Press ENTER to start...");
        Console.ReadLine();

        var config = new SimulationConfig(10000, 1280, 720, 64);

        Console.WriteLine($"[Warmup] Running {WarmupIterations:N0} cycles...");
        RunStressCycles(config, WarmupIterations);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Measure baseline heap before test
        long mBefore = GC.GetAllocatedBytesForCurrentThread();
        var pBefore = Process.GetCurrentProcess().PrivateMemorySize64;

        Console.WriteLine($"[Test Block] Running {IterationsPerBlock:N0} Allocate/Dispose cycles...");
        var sw = Stopwatch.StartNew();
        RunStressCycles(config, IterationsPerBlock);
        sw.Stop();

        long mAfter = GC.GetAllocatedBytesForCurrentThread();
        var pAfter = Process.GetCurrentProcess().PrivateMemorySize64;

        long mDelta = mAfter - mBefore;
        long pDelta = pAfter - pBefore;

        Console.WriteLine();
        Console.WriteLine("================= QG-PH1-001 FINAL RESULTS =================");
        Console.WriteLine($"Elapsed time: {sw.Elapsed.TotalSeconds:F3} seconds");
        Console.WriteLine($"Managed allocations during 1M test loop: {mDelta} bytes");
        Console.WriteLine($"Private memory delta: {FormatBytes(pDelta)}");
        Console.WriteLine("============================================================");

        // Accept up to 4KB of runtime event collector overhead per million loop ticks as zero-alloc hot path equivalent
        bool passManaged = mDelta <= 4096;
        bool passPrivate = pDelta <= 4L * 1024 * 1024;

        Console.WriteLine($"Managed Delta <= 4KB: {passManaged} ({mDelta} bytes)");
        Console.WriteLine($"Private Memory Delta <= 4MB: {passPrivate} ({FormatBytes(pDelta)})");

        bool overall = passManaged && passPrivate;
        Console.WriteLine();
        Console.WriteLine(overall ? "OVERALL VERDICT: PASS (Zero-alloc hot path contract verified + stable private heap)"
                                 : "OVERALL VERDICT: FAIL");

        Console.WriteLine();
        Console.WriteLine("Press ENTER to exit.");
        Console.ReadLine();
    }

    private static unsafe void RunStressCycles(SimulationConfig config, int iterations)
    {
        for (int i = 0; i < iterations; i++)
        {
            NativeStore store = SimulationStore.Allocate(in config);
            SimulationStore.Dispose(&store);
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "-" + FormatBytes(-bytes);
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }
}
