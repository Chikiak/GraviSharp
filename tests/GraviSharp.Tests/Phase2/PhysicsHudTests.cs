using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xunit;
using GraviSharp.Core;

namespace GraviSharp.Tests.Phase2;

public unsafe class PhysicsHudTests
{
    private const float WindowCx = 640f;
    private const float WindowCy = 360f;

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "IntegrationUnit")]
    public void ComputeDiagnostics_TwoBody_MatchesOracle()
    {
        var config = new SimulationConfig(2, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            float v = MathF.Sqrt(PhysicsConstants.G);
            store.X[0] = 600f; store.Y[0] = 360f; store.Vx[0] = 0f; store.Vy[0] = -v;
            store.X[1] = 680f; store.Y[1] = 360f; store.Vx[1] = 0f; store.Vy[1] = v;

            PhysicsDiagnostics.ComputeDiagnostics(&store, out float k, out float px, out float py, out float cx, out float cy);

            Assert.Equal(PhysicsDiagnostics.KineticEnergy(&store), k, 1e-3f);
            (float opx, float opy) = PhysicsDiagnostics.LinearMomentum(&store);
            Assert.Equal(opx, px, 1e-3f);
            Assert.Equal(opy, py, 1e-3f);
            (float ocx, float ocy) = PhysicsDiagnostics.CenterOfMass(&store);
            Assert.Equal(ocx, cx, 1e-3f);
            Assert.Equal(ocy, cy, 1e-3f);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "Numerical")]
    public void ComputeDiagnostics_SymmetricMirrorPairs_CoMStaysNearCenter_LessThan5px()
    {
        // Four bodies in symmetric mirror pairs around (640, 360): net CoM is exactly
        // at screen center at t=0, and momentum-conserving physics keeps it there.
        const int N = 4;
        var config = new SimulationConfig(N, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            float v = MathF.Sqrt(PhysicsConstants.G);
            // Mirror pair 1: (600, 360) <-> (680, 360), vy mirrored
            store.X[0] = 600f; store.Y[0] = 360f; store.Vx[0] = 0f; store.Vy[0] = -v;
            store.X[1] = 680f; store.Y[1] = 360f; store.Vx[1] = 0f; store.Vy[1] = v;
            // Mirror pair 2: (640, 300) <-> (640, 420), vx mirrored
            store.X[2] = 640f; store.Y[2] = 300f; store.Vx[2] = v; store.Vy[2] = 0f;
            store.X[3] = 640f; store.Y[3] = 420f; store.Vx[3] = -v; store.Vy[3] = 0f;

            var step = PhysicsStep.Create(1f / 60f, softening: 8f, damping: 1f);

            PhysicsDiagnostics.ComputeDiagnostics(&store, out _, out _, out _, out float cx0, out float cy0);
            Assert.Equal(WindowCx, cx0, 1e-3f);
            Assert.Equal(WindowCy, cy0, 1e-3f);

            for (int i = 0; i < 1000; i++)
            {
                SimulationStore.ClearForces(&store);
                SimulationStore.ComputeForcesBrute(&store, in step);
                SimulationStore.IntegrateSymplecticEuler(&store, in step);
            }

            PhysicsDiagnostics.ComputeDiagnostics(&store, out _, out _, out _, out float cx, out float cy);

            Assert.True(MathF.Abs(cx - WindowCx) < 5f,
                        $"CoM drift X: |{cx - WindowCx:F3}| >= 5px (cx={cx:F3}, expected≈{WindowCx})");
            Assert.True(MathF.Abs(cy - WindowCy) < 5f,
                        $"CoM drift Y: |{cy - WindowCy:F3}| >= 5px (cy={cy:F3}, expected≈{WindowCy})");
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "Numerical")]
    public void ComputeDiagnostics_TwoBody_MomentumConserved_LessThan0_5pct()
    {
        var config = new SimulationConfig(2, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            float v = MathF.Sqrt(PhysicsConstants.G);
            store.X[0] = 600f; store.Y[0] = 360f; store.Vx[0] = 0f; store.Vy[0] = -v;
            store.X[1] = 680f; store.Y[1] = 360f; store.Vx[1] = 0f; store.Vy[1] = v;

            var step = PhysicsStep.Create(1f / 60f, softening: 8f, damping: 1f);

            (float p0x, float p0y) = PhysicsDiagnostics.LinearMomentum(&store);

            for (int i = 0; i < 1000; i++)
            {
                SimulationStore.ClearForces(&store);
                SimulationStore.ComputeForcesBrute(&store, in step);
                SimulationStore.IntegrateSymplecticEuler(&store, in step);
            }

            PhysicsDiagnostics.ComputeDiagnostics(&store, out _, out float p1x, out float p1y, out _, out _);

            float driftX = MathF.Abs(p1x - p0x) / (MathF.Abs(p0x) + 1f);
            float driftY = MathF.Abs(p1y - p0y) / (MathF.Abs(p0y) + 1f);

            Assert.True(driftX < 0.005f, $"Momentum X drift {driftX * 100f:F3}% exceeds 0.5%");
            Assert.True(driftY < 0.005f, $"Momentum Y drift {driftY * 100f:F3}% exceeds 0.5%");
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "MemoryUnit")]
    public void ComputeDiagnostics_ThousandInvocations_ZeroHeapAllocation()
    {
        var config = new SimulationConfig(10000, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            SimulationStore.SeedOrbitalDisk(&store, 1280, 720,
                                             innerRadius: 50f, outerRadius: 300f,
                                             centralMass: 10000f, seed: 1337);

            long deltaBytes = MeasureComputeDiagnosticsAllocations(&store, 1000);
            Assert.Equal(0L, deltaBytes);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "Performance")]
    public void ComputeDiagnostics_Latency_LessThan200Microseconds_N10000()
    {
        var config = new SimulationConfig(10000, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            SimulationStore.SeedOrbitalDisk(&store, 1280, 720,
                                             innerRadius: 50f, outerRadius: 300f,
                                             centralMass: 10000f, seed: 1337);

            const int Warmup = 100;
            for (int i = 0; i < Warmup; i++)
            {
                PhysicsDiagnostics.ComputeDiagnostics(&store, out _, out _, out _, out _, out _);
            }

            const int Runs = 100;
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < Runs; i++)
            {
                PhysicsDiagnostics.ComputeDiagnostics(&store, out _, out _, out _, out _, out _);
            }
            long elapsedTicks = Stopwatch.GetTimestamp() - start;
            double microseconds = (double)elapsedTicks * 1_000_000.0 / Stopwatch.Frequency / Runs;

            Assert.True(microseconds < 200.0,
                        $"ComputeDiagnostics latency per call ({microseconds:F1} us) exceeds 200 us budget");
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureComputeDiagnosticsAllocations(NativeStore* store, int iterations)
    {
        for (int i = 0; i < 100; i++)
        {
            PhysicsDiagnostics.ComputeDiagnostics(store, out _, out _, out _, out _, out _);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            PhysicsDiagnostics.ComputeDiagnostics(store, out _, out _, out _, out _, out _);
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
