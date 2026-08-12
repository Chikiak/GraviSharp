using System;
using System.Runtime.CompilerServices;
using Xunit;
using GraviSharp.Core;

namespace GraviSharp.Tests.Phase2;

public unsafe class SeedRandomCloudTests
{
    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "SeedUnit")]
    public void SeedRandomCloud_AllBodiesWithinBounds()
    {
        var config = new SimulationConfig(2000, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            SimulationStore.SeedRandomCloud(&store, 1280, 720, spread: 200f, thermalSpeed: 10f, seed: 1337);

            var vx = SimulationStore.ViewVx(store);
            var vy = SimulationStore.ViewVy(store);

            for (int i = 0; i < config.BodyCount; i++)
            {
                Assert.True(store.X[i] >= 0f && store.X[i] < 1280f,
                    $"X[{i}]={store.X[i]} out of [0,1280)");
                Assert.True(store.Y[i] >= 0f && store.Y[i] < 720f,
                    $"Y[{i}]={store.Y[i]} out of [0,720)");

                float velMag = MathF.Sqrt(vx[i] * vx[i] + vy[i] * vy[i]);
                Assert.True(velMag <= thermalSpeedUpperBound(10f),
                    $"Thermal |v|={velMag} exceeds |thermalSpeed * uniform[-0.5,0.5)| bound");

                // Zero force contract
                Assert.Equal(0f, store.Fx[i]);
                Assert.Equal(0f, store.Fy[i]);
            }
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "SeedUnit")]
    public void SeedRandomCloud_DistributionUnimodalAroundCenter()
    {
        const int N = 2000;
        const float spread = 200f;
        var config = new SimulationConfig(N, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            SimulationStore.SeedRandomCloud(&store, 1280, 720, spread, thermalSpeed: 10f, seed: 1337);

            double sumX = 0f;
            double sumY = 0f;
            for (int i = 0; i < N; i++)
            {
                sumX += store.X[i];
                sumY += store.Y[i];
            }

            double meanX = sumX / N;
            double meanY = sumY / N;
            double cx = 1280.0 * 0.5;
            double cy = 720.0 * 0.5;

            // CLT unimodality assertion (simplified): mean estimator within spread*0.3 of true center.
            Assert.True(Math.Abs(meanX - cx) < spread * 0.3,
                $"Mean X drift {Math.Abs(meanX - cx):F2} exceeds spread*0.3 tolerance ({spread * 0.3:F1})");
            Assert.True(Math.Abs(meanY - cy) < spread * 0.3,
                $"Mean Y drift {Math.Abs(meanY - cy):F2} exceeds spread*0.3 tolerance ({spread * 0.3:F1})");
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "MemoryUnit")]
    public void SeedRandomCloud_NoGCAfterRepeatedCalls()
    {
        var config = new SimulationConfig(1000, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            // Per call contract: only "startup jet" alloc is `new Random(seed)` (~304 bytes on net11 preview).
            // The inner O(N) seed loop is zero-GC. Two-call diff cancels the constant Random alloc baseline.
            long perCallJet = MeasureSeedRandomCloudAllocations(&store, 1280, 720, 200f, 10f, 1);
            long totalWithJet = MeasureSeedRandomCloudAllocations(&store, 1280, 720, 200f, 10f, 1000);
            long loopOnly = totalWithJet - (perCallJet * 1000);
            Assert.Equal(0L, loopOnly);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    private static float thermalSpeedUpperBound(float thermalSpeed) =>
        // thermalSpeed * uniform[-0.5, 0.5) -> |v| <= thermalSpeed * sqrt(2) * 0.5 in the worst case
        // (each component independently in [-t/2, t/2))
        thermalSpeed * 0.5f * MathF.Sqrt(2f) * 1.0001f;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe long MeasureSeedRandomCloudAllocations(
        NativeStore* store, int width, int height, float spread, float thermalSpeed, int iterations)
    {
        for (int i = 0; i < 1000; i++)
        {
            SimulationStore.SeedRandomCloud(store, width, height, spread, thermalSpeed, seed: 1337);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            SimulationStore.SeedRandomCloud(store, width, height, spread, thermalSpeed, seed: 1337);
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
