using System;
using Xunit;
using GraviSharp.Core;

namespace GraviSharp.Tests;

public class AllocateDisposeTests
{
    [Fact]
    [Trait("Category", "Phase1")]
    [Trait("Category", "MemoryUnit")]
    public unsafe void Allocate_Returns_Valid_Store_With_64Byte_Alignment()
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

            Assert.Equal(0, (long)store.BaseAddress & 63);
            Assert.Equal(0, (long)store.X & 63);
            Assert.Equal(0, (long)store.Y & 63);
            Assert.Equal(0, (long)store.Vx & 63);
            Assert.Equal(0, (long)store.Vy & 63);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase1")]
    [Trait("Category", "MemoryUnit")]
    public unsafe void Dispose_Is_Idempotent_And_Nullifies_Pointers()
    {
        var config = new SimulationConfig(1000, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        SimulationStore.Dispose(&store);

        Assert.True(store.BaseAddress == null);
        Assert.True(store.X == null);
        Assert.True(store.Y == null);
        Assert.True(store.Vx == null);
        Assert.True(store.Vy == null);
        Assert.Equal(0, store.Count);

        // Second dispose should not throw or double free
        SimulationStore.Dispose(&store);
        Assert.True(store.BaseAddress == null);
    }
}
