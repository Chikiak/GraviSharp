using BenchmarkDotNet.Attributes;
using GraviSharp.Core;
using System;

namespace GraviSharp.Benchmarks.Phase1;

[MemoryDiagnoser]
public class ViewXBench
{
    [Params(10000)] public int N { get; set; }
    private NativeStore _store;

    [GlobalSetup]
    public unsafe void Setup()
    {
        var cfg = new SimulationConfig(N, 1280, 720, 64);
        _store = SimulationStore.Allocate(in cfg);
        // fixed is required because _store is a field of a managed class (ViewXBench);
        // the GC could move it. NativeStore is a value type holding pointers, so
        // pinning it on the stack for the duration of SeedLinear/Dispose is safe.
        // Data persists in _store after fixed scope ends because it's a by-value struct.
        fixed (NativeStore* p = &_store)
        {
            SimulationStore.SeedLinear(p, 1280, 720, 50f, 1337);
        }
    }

    [Benchmark]
    public ReadOnlySpan<float> ViewX() => SimulationStore.ViewX(in _store);

    [GlobalCleanup]
    public unsafe void Cleanup()
    {
        fixed (NativeStore* p = &_store)
        {
            SimulationStore.Dispose(p);
        }
    }
}
