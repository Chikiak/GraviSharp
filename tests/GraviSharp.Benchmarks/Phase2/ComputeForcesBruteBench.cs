using BenchmarkDotNet.Attributes;
using GraviSharp.Core;
using System;

namespace GraviSharp.Benchmarks.Phase2;

[MemoryDiagnoser]
public class ComputeForcesBruteBench
{
    [Params(10000)] public int N { get; set; }
    private NativeStore _store;
    private PhysicsStep _step;

    [GlobalSetup]
    public unsafe void Setup()
    {
        var cfg = new SimulationConfig(N, 1280, 720, 64);
        _store = SimulationStore.Allocate(in cfg);
        _step = PhysicsStep.Create(1f / 60f);

        fixed (NativeStore* p = &_store)
        {
            SimulationStore.SeedLinear(p, 1280, 720, 50f, 1337);
        }
    }

    [Benchmark]
    public unsafe void ComputeForcesBrute()
    {
        fixed (NativeStore* p = &_store)
        {
            SimulationStore.ClearForces(p);
            SimulationStore.ComputeForcesBrute(p, in _step);
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
