using System;
using System.Runtime.CompilerServices;
using Xunit;
using GraviSharp.Core;

namespace GraviSharp.Tests.Phase2;

public unsafe class SeedOrbitalDiskTests
{
    private const int Width = 1280;
    private const int Height = 720;
    private const float Cx = Width * 0.5f;
    private const float Cy = Height * 0.5f;

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "SeedUnit")]
    public void SeedOrbitalDisk_AllRadiiWithinRange()
    {
        const int N = 5000;
        const float inner = 50f;
        const float outer = 300f;
        var config = new SimulationConfig(N, Width, Height, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            SimulationStore.SeedOrbitalDisk(&store, Width, Height, inner, outer, centralMass: 10000f, seed: 1337);

            for (int i = 0; i < N; i++)
            {
                float dx = store.X[i] - Cx;
                float dy = store.Y[i] - Cy;
                float r = MathF.Sqrt(dx * dx + dy * dy);

                Assert.InRange(r, inner * 0.999f, outer * 1.001f);

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
    public void SeedOrbitalDisk_TangentialVelocityMatchesKepler()
    {
        const int N = 5000;
        const float inner = 50f;
        const float outer = 300f;
        const float centralMass = 10000f;
        const float g = PhysicsConstants.G;

        var config = new SimulationConfig(N, Width, Height, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            SimulationStore.SeedOrbitalDisk(&store, Width, Height, inner, outer, centralMass, seed: 1337);

            int sampleStep = 5; // sample 1000 of 5000
            int samples = 0;
            for (int i = 0; i < N; i += sampleStep)
            {
                float dx = store.X[i] - Cx;
                float dy = store.Y[i] - Cy;
                float r = MathF.Sqrt(dx * dx + dy * dy);
                float velMag = MathF.Sqrt(store.Vx[i] * store.Vx[i] + store.Vy[i] * store.Vy[i]);
                float expected = MathF.Sqrt(g * centralMass / r);

                Assert.Equal(expected, velMag, 1e-2f);

                Assert.Equal(0f, store.Fx[i]);
                Assert.Equal(0f, store.Fy[i]);
                samples++;
            }
            Assert.True(samples == 1000, $"Expected exactly 1000 sampled bodies, got {samples}");
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "SeedUnit")]
    public void SeedOrbitalDisk_RejectsInnerRadiusBelowOne()
    {
        var config = new SimulationConfig(64, Width, Height, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            ArgumentOutOfRangeException? thrown = null;
            try
            {
                SimulationStore.SeedOrbitalDisk(&store, Width, Height, innerRadius: 0.5f, outerRadius: 300f, centralMass: 10000f);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                thrown = ex;
            }
            Assert.NotNull(thrown);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "MemoryUnit")]
    public void SeedOrbitalDisk_NoGCAfterRepeatedCalls()
    {
        const int N = 1000;
        var config = new SimulationConfig(N, Width, Height, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            // Per call contract: only "startup jet" alloc is `new Random(seed)` (~304 bytes on net11 preview).
            // The inner O(N) seed loop is zero-GC. Two-call diff cancels the constant Random alloc baseline.
            long perCallJet = MeasureSeedOrbitalDiskAllocations(&store, 1280, 720, 50f, 300f, 10000f, 1);
            long totalWithJet = MeasureSeedOrbitalDiskAllocations(&store, 1280, 720, 50f, 300f, 10000f, 1000);
            long loopOnly = totalWithJet - (perCallJet * 1000);
            Assert.Equal(0L, loopOnly);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe long MeasureSeedOrbitalDiskAllocations(
        NativeStore* store, int width, int height, float innerRadius, float outerRadius, float centralMass, int iterations)
    {
        for (int i = 0; i < 1000; i++)
        {
            SimulationStore.SeedOrbitalDisk(store, width, height, innerRadius, outerRadius, centralMass, seed: 1337);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            SimulationStore.SeedOrbitalDisk(store, width, height, innerRadius, outerRadius, centralMass, seed: 1337);
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
