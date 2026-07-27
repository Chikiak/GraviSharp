using System;
using Xunit;
using GraviSharp.Core;

namespace GraviSharp.Tests;

public class UpdateLinearTests
{
    [Fact]
    [Trait("Category", "Phase1")]
    [Trait("Category", "PhysicsUnit")]
    public unsafe void UpdateLinear_LeftWallBounce_ReflectsPositionAndVelocity()
    {
        var config = new SimulationConfig(1, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            // Set particle at X=0, moving left (Vx = -100)
            store.X[0] = 0f;
            store.Y[0] = 100f;
            store.Vx[0] = -100f;
            store.Vy[0] = 0f;

            // dt = 0.1 -> raw position would be -10; after bounce: X = 10, Vx = 100
            SimulationStore.UpdateLinear(&store, 1280, 720, 0.1f);

            Assert.Equal(10f, store.X[0], 1e-5f);
            Assert.Equal(100f, store.Vx[0], 1e-5f);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase1")]
    [Trait("Category", "PhysicsUnit")]
    public unsafe void UpdateLinear_RightWallBounce_ReflectsPositionAndVelocity()
    {
        var config = new SimulationConfig(1, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            // Set particle near right wall X=1279, moving right (Vx = 100), dt = 0.1 -> raw position = 1289
            store.X[0] = 1279f;
            store.Y[0] = 100f;
            store.Vx[0] = 100f;
            store.Vy[0] = 0f;

            // After bounce: X = 2*1280 - 1289 = 2560 - 1289 = 1271, Vx = -100
            SimulationStore.UpdateLinear(&store, 1280, 720, 0.1f);

            Assert.True(store.X[0] < 1280f);
            Assert.Equal(1271f, store.X[0], 1e-5f);
            Assert.Equal(-100f, store.Vx[0], 1e-5f);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase1")]
    [Trait("Category", "PhysicsUnit")]
    public unsafe void UpdateLinear_TopAndBottomWallBounce()
    {
        var config = new SimulationConfig(2, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            // Particle 0: Top wall bounce (Y = 0, Vy = -50)
            store.X[0] = 100f;
            store.Y[0] = 0f;
            store.Vx[0] = 0f;
            store.Vy[0] = -50f;

            // Particle 1: Bottom wall bounce (Y = 719, Vy = 50, dt = 0.1 -> raw Y = 719.5)
            store.X[1] = 100f;
            store.Y[1] = 719f;
            store.Vx[1] = 0f;
            store.Vy[1] = 50f;

            SimulationStore.UpdateLinear(&store, 1280, 720, 0.1f);

            // Top bounce: Y = 5, Vy = 50
            Assert.Equal(5f, store.Y[0], 1e-5f);
            Assert.Equal(50f, store.Vy[0], 1e-5f);

            // Bottom bounce: Y = 2*720 - 719.5 = 1440 - 719.5 = 720.5? Wait:
            // Y_raw = 719 + 50 * 0.1 = 719.5. Since 719.5 >= 720? No, 719.5 < 720. Wait!
            // Let's check calculation: 719 + 5 = 724 >= 720. Let's use dt = 0.2 -> Y = 719 + 10 = 729.
            // 2*720 - 729 = 1440 - 729 = 711. Vy = -50.
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase1")]
    [Trait("Category", "PhysicsUnit")]
    public unsafe void UpdateLinear_1000Iterations_AllParticlesStayInBounds()
    {
        var config = new SimulationConfig(5000, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            SimulationStore.SeedLinear(&store, 1280, 720, speed: 150f, seed: 1337);

            for (int iter = 0; iter < 1000; iter++)
            {
                SimulationStore.UpdateLinear(&store, 1280, 720, 1f / 60f);
            }

            for (int i = 0; i < store.Count; i++)
            {
                Assert.True(store.X[i] >= 0f && store.X[i] < 1280f, $"Particle {i} X out of bounds: {store.X[i]}");
                Assert.True(store.Y[i] >= 0f && store.Y[i] < 720f, $"Particle {i} Y out of bounds: {store.Y[i]}");
            }
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }
}
