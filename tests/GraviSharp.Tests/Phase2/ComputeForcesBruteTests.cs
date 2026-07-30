using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;
using GraviSharp.Core;

namespace GraviSharp.Tests.Phase2;

public class ComputeForcesBruteTests
{
    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "ForcesUnit")]
    public unsafe void ComputeForcesBrute_EquilateralTriangle_RotationalSymmetry()
    {
        var config = new SimulationConfig(3, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            float cx = 640f;
            float cy = 360f;
            float r = 200f;

            store.X[0] = cx + r;
            store.Y[0] = cy;
            store.Vx[0] = 0f;
            store.Vy[0] = 0f;

            store.X[1] = cx + r * MathF.Cos(2f * MathF.PI / 3f);
            store.Y[1] = cy + r * MathF.Sin(2f * MathF.PI / 3f);
            store.Vx[1] = 0f;
            store.Vy[1] = 0f;

            store.X[2] = cx + r * MathF.Cos(4f * MathF.PI / 3f);
            store.Y[2] = cy + r * MathF.Sin(4f * MathF.PI / 3f);
            store.Vx[2] = 0f;
            store.Vy[2] = 0f;

            SimulationStore.ClearForces(&store);
            var step = PhysicsStep.Create(1f / 60f);
            SimulationStore.ComputeForcesBrute(&store, in step);

            var fx = SimulationStore.ViewFx(store);
            var fy = SimulationStore.ViewFy(store);

            float totalFx = 0f;
            float totalFy = 0f;
            for (int i = 0; i < 3; i++)
            {
                totalFx += fx[i];
                totalFy += fy[i];
            }
            Assert.Equal(0f, totalFx, 1e-3f);
            Assert.Equal(0f, totalFy, 1e-3f);

            float f0 = MathF.Sqrt(fx[0] * fx[0] + fy[0] * fy[0]);
            float f1 = MathF.Sqrt(fx[1] * fx[1] + fy[1] * fy[1]);
            float f2 = MathF.Sqrt(fx[2] * fx[2] + fy[2] * fy[2]);

            Assert.Equal(f0, f1, 1e-3f);
            Assert.Equal(f0, f2, 1e-3f);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "ForcesUnit")]
    public unsafe void ComputeForcesBrute_ScaleG_DoublesForce()
    {
        var config = new SimulationConfig(2, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            store.X[0] = 640f; store.Y[0] = 360f;
            store.X[1] = 740f; store.Y[1] = 360f;

            var step1 = PhysicsStep.Create(1f / 60f, g: 1500f);
            var step2 = PhysicsStep.Create(1f / 60f, g: 3000f);

            SimulationStore.ClearForces(&store);
            SimulationStore.ComputeForcesBrute(&store, in step1);
            float f1 = MathF.Abs(store.Fx[0]);

            SimulationStore.ClearForces(&store);
            SimulationStore.ComputeForcesBrute(&store, in step2);
            float f2 = MathF.Abs(store.Fx[0]);

            Assert.Equal(2f, f2 / f1, 1e-2f);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "ForcesUnit")]
    public unsafe void ComputeForcesBrute_LargeSoftening_ReducesForce()
    {
        var config = new SimulationConfig(2, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            store.X[0] = 640f; store.Y[0] = 360f;
            store.X[1] = 740f; store.Y[1] = 360f;

            var stepSoft = PhysicsStep.Create(1f / 60f, softening: 8f);
            var stepStiff = PhysicsStep.Create(1f / 60f, softening: 8000f);

            SimulationStore.ClearForces(&store);
            SimulationStore.ComputeForcesBrute(&store, in stepSoft);
            float fSoft = MathF.Abs(store.Fx[0]);

            SimulationStore.ClearForces(&store);
            SimulationStore.ComputeForcesBrute(&store, in stepStiff);
            float fStiff = MathF.Abs(store.Fx[0]);

            Assert.True(fStiff < fSoft * 1e-3f);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "MemoryUnit")]
    public unsafe void ComputeForcesBrute_ZeroAlloc_HotPath()
    {
        var config = new SimulationConfig(1000, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            SimulationStore.SeedLinear(&store, 1280, 720, 50f, 1337);
            var step = PhysicsStep.Create(1f / 60f);

            long deltaBytes = MeasureComputeForcesAllocationsOnDedicatedThread(&store, in step, 100);
            Assert.Equal(0L, deltaBytes);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe long MeasureComputeForcesAllocationsOnDedicatedThread(NativeStore* store, in PhysicsStep step, int iterations)
    {
        long delta = -1;
        PhysicsStep stepLocal = step;
        NativeStore storeLocal = *store;

        var thread = new Thread(() =>
        {
            NativeStore s = storeLocal;
            PhysicsStep st = stepLocal;

            for (int i = 0; i < 1000; i++)
            {
                SimulationStore.ClearForces(&s);
                SimulationStore.ComputeForcesBrute(&s, in st);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < iterations; i++)
            {
                SimulationStore.ClearForces(&s);
                SimulationStore.ComputeForcesBrute(&s, in st);
            }
            delta = GC.GetAllocatedBytesForCurrentThread() - before;
        });

        thread.IsBackground = false;
        thread.Start();
        thread.Join();
        return delta;
    }
}
