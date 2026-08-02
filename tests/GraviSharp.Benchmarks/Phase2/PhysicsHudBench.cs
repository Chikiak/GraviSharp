using System;
using System.Text.Unicode;
using BenchmarkDotNet.Attributes;
using GraviSharp.Core;

namespace GraviSharp.Benchmarks.Phase2;

[MemoryDiagnoser]
public unsafe class PhysicsHudBench
{
    [Params(1000, 10000)]
    public int N { get; set; }

    private NativeStore _store;
    private PhysicsStep _step;

    [GlobalSetup]
    public void Setup()
    {
        var cfg = new SimulationConfig(N, 1280, 720, 64);
        _store = SimulationStore.Allocate(in cfg);
        _step = PhysicsStep.Create(1f / 60f);

        fixed (NativeStore* p = &_store)
        {
            SimulationStore.SeedOrbitalDisk(p, 1280, 720,
                                             innerRadius: 50f, outerRadius: 300f,
                                             centralMass: 10000f, seed: 1337);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        fixed (NativeStore* p = &_store)
        {
            SimulationStore.Dispose(p);
        }
    }

    [Benchmark]
    public void ComputeDiagnosticsBench()
    {
        fixed (NativeStore* p = &_store)
        {
            PhysicsDiagnostics.ComputeDiagnostics(p, out _, out _, out _, out _, out _);
        }
    }

    [Benchmark]
    public void FormatUtf8Bench()
    {
        fixed (NativeStore* p = &_store)
        {
            PhysicsDiagnostics.ComputeDiagnostics(p, out float k, out float px, out float py, out float cx, out float cy);

            Span<byte> buf = stackalloc byte[256];
            if (Utf8.TryWrite(buf, $"K: {k:F1} | Px: {px:F1} | Py: {py:F1} | Cx: {cx:F1} | Cy: {cy:F1}", out int n))
            {
                buf[n] = 0;
            }
        }
    }
}
