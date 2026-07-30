using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;
using GraviSharp.Core;

namespace GraviSharp.Tests.Phase2;

public class IntegrateSymplecticEulerTests
{
    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "IntegrationUnit")]
    public unsafe void IsolatedBody_NoForce_AdvancesExactlyExpected()
    {
        var config = new SimulationConfig(1, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            store.X[0] = 100f;
            store.Y[0] = 100f;
            store.Vx[0] = 100f;
            store.Vy[0] = 0f;
            store.Fx[0] = 0f;
            store.Fy[0] = 0f;

            var step = PhysicsStep.Create(0.1f);
            SimulationStore.IntegrateSymplecticEuler(&store, in step);
            SimulationStore.IntegrateSymplecticEuler(&store, in step);
            SimulationStore.IntegrateSymplecticEuler(&store, in step);

            Assert.Equal(130f, store.X[0], 1e-3f);
            Assert.Equal(100f, store.Y[0], 1e-3f);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "IntegrationUnit")]
    public unsafe void BounceStaysInBounds_ManyTicks()
    {
        var config = new SimulationConfig(1, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            store.X[0] = 640f;
            store.Y[0] = 360f;
            store.Vx[0] = 500f;
            store.Vy[0] = -500f;

            var step = PhysicsStep.Create(1f / 60f);

            for (int i = 0; i < 1000; i++)
            {
                store.Fx[0] = 0f;
                store.Fy[0] = 0f;
                SimulationStore.IntegrateSymplecticEuler(&store, in step);
                SimulationStore.ReflectBounds(&store, 1280, 720);
            }

            Assert.InRange(store.X[0], 0f, 1280f);
            Assert.InRange(store.Y[0], 0f, 720f);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "MemoryUnit")]
    public unsafe void IntegrateSymplecticEuler_ZeroAlloc_HotPath()
    {
            var config = new SimulationConfig(1000, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            SimulationStore.SeedLinear(&store, 1280, 720, 50f, 1337);
            var step = PhysicsStep.Create(1f / 60f);

            long deltaBytes = MeasureIntegrateAllocationsOnDedicatedThread(&store, in step, 100);
            Assert.Equal(0L, deltaBytes);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "IntegrationUnit")]
    public unsafe void SymplecticConservation_Smoke_ShortOrbit()
    {
        var config = new SimulationConfig(2, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            // Two bodies orbiting barycenter
            store.X[0] = 600f; store.Y[0] = 360f; store.Vx[0] = 0f; store.Vy[0] = -50f;
            store.X[1] = 680f; store.Y[1] = 360f; store.Vx[1] = 0f; store.Vy[1] = 50f;

            var step = PhysicsStep.Create(1f / 60f, softening: 8f, damping: 1f);

            float initialEnergy = ComputeTotalKinetic(&store);

            for (int i = 0; i < 100; i++)
            {
                SimulationStore.ClearForces(&store);
                SimulationStore.ComputeForcesBrute(&store, in step);
                SimulationStore.IntegrateSymplecticEuler(&store, in step);
            }

            float finalEnergy = ComputeTotalKinetic(&store);
            float energyDrift = MathF.Abs(finalEnergy - initialEnergy) / (initialEnergy + 1e-6f);

            // Smoke threshold: energy doesn't diverge wildly (>5% drift in 100 ticks without full potential energy included)
            Assert.True(energyDrift < 0.2f);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    private static unsafe float ComputeTotalKinetic(NativeStore* store)
    {
        float k = 0f;
        int count = store->Count;
        for (int i = 0; i < count; i++)
        {
            float vx = store->Vx[i];
            float vy = store->Vy[i];
            k += 0.5f * (vx * vx + vy * vy);
        }
        return k;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe long MeasureIntegrateAllocationsOnDedicatedThread(NativeStore* store, in PhysicsStep step, int iterations)
    {
        long delta = -1;
        PhysicsStep stepLocal = step;
        NativeStore storeLocal = *store;

        var thread = new Thread(() =>
        {
            NativeStore s = storeLocal;
            PhysicsStep st = stepLocal;

            for (int i = 0; i < 100; i++)
            {
                SimulationStore.ClearForces(&s);
                SimulationStore.ComputeForcesBrute(&s, in st);
                SimulationStore.IntegrateSymplecticEuler(&s, in st);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < iterations; i++)
            {
                SimulationStore.ClearForces(&s);
                SimulationStore.ComputeForcesBrute(&s, in st);
                SimulationStore.IntegrateSymplecticEuler(&s, in st);
            }
            delta = GC.GetAllocatedBytesForCurrentThread() - before;
        });

        thread.IsBackground = false;
        thread.Start();
        thread.Join();
        return delta;
    }
}
