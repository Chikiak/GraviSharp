using BenchmarkDotNet.Attributes;
using GraviSharp.Core;
using System;

namespace GraviSharp.Benchmarks.Phase2;

[MemoryDiagnoser]
public class SeedRandomCloudBench
{
    [Params(10000)] public int N { get; set; }
    private NativeStore _store;

    [GlobalSetup]
    public unsafe void Setup()
    {
        var cfg = new SimulationConfig(N, 1280, 720, 64);
        _store = SimulationStore.Allocate(in cfg);
    }

    [Benchmark]
    public unsafe void SeedRandomCloud()
    {
        fixed (NativeStore* p = &_store)
        {
            // NOTE: per call allocates `new Random(seed)` (~304 B on net11 preview) as the only startup-jet
            // managed allocation. The inner O(N) seed loop is zero-GC.
            SimulationStore.SeedRandomCloud(p, 1280, 720, spread: 200f, thermalSpeed: 10f, seed: 1337);
        }
    }

    [GlobalCleanup]
    public unsafe void Cleanup()
    {
        fixed (NativeStore* p = &_store)
        {
            SimulationStore.Dispose(p);
        }
    }
}
