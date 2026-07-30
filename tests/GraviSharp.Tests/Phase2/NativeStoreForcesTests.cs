using System;
using System.Diagnostics;
using Xunit;
using GraviSharp.Core;

namespace GraviSharp.Tests.Phase2;

public class NativeStoreForcesTests
{
    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "MemoryUnit")]
    public unsafe void Allocate_Six_Pointers_NonNull_And_Count_Matches()
    {
        var config = new SimulationConfig(10000, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            Assert.Equal(10000, store.Count);
            Assert.True(store.BaseAddress != null);
            Assert.True(store.X != null);
            Assert.True(store.Y != null);
            Assert.True(store.Vx != null);
            Assert.True(store.Vy != null);
            Assert.True(store.Fx != null);
            Assert.True(store.Fy != null);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "MemoryUnit")]
    public unsafe void Allocate_Fx_Fy_64Byte_Aligned()
    {
        var config = new SimulationConfig(10000, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            Assert.Equal(0, (long)store.BaseAddress & 63);
            Assert.Equal(0, (long)store.Fx & 63);
            Assert.Equal(0, (long)store.Fy & 63);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "MemoryUnit")]
    public unsafe void ClearForces_Zeroes_Fx_Fy()
    {
        var config = new SimulationConfig(100, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            // Populate with non-zero values
            for (int i = 0; i < store.Count; i++)
            {
                store.Fx[i] = 123.45f;
                store.Fy[i] = -678.90f;
            }

            SimulationStore.ClearForces(&store);

            var viewFx = SimulationStore.ViewFx(store);
            var viewFy = SimulationStore.ViewFy(store);

            foreach (var v in viewFx) Assert.Equal(0f, v);
            foreach (var v in viewFy) Assert.Equal(0f, v);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "MemoryUnit")]
    public unsafe void Dispose_Nullifies_Six_Pointers()
    {
        var config = new SimulationConfig(1000, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        SimulationStore.Dispose(&store);

        Assert.True(store.BaseAddress == null);
        Assert.True(store.X == null);
        Assert.True(store.Y == null);
        Assert.True(store.Vx == null);
        Assert.True(store.Vy == null);
        Assert.True(store.Fx == null);
        Assert.True(store.Fy == null);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "MemoryUnit")]
    public unsafe void ClearForces_Under_50us_For_10000()
    {
        var config = new SimulationConfig(10000, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            // Warm up
            SimulationStore.ClearForces(&store);

            long t0 = Stopwatch.GetTimestamp();
            SimulationStore.ClearForces(&store);
            long t1 = Stopwatch.GetTimestamp();

            double elapsedUs = (t1 - t0) * 1_000_000.0 / Stopwatch.Frequency;
            Assert.True(elapsedUs < 50.0, $"ClearForces took {elapsedUs} us, expected < 50 us.");
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }
}
