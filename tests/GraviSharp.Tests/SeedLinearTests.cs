using System;
using Xunit;
using GraviSharp.Core;

namespace GraviSharp.Tests;

public class SeedLinearTests
{
    [Fact]
    [Trait("Category", "Phase1")]
    [Trait("Category", "PhysicsUnit")]
    public unsafe void SeedLinear_Is_Deterministic_And_Bounded()
    {
        var config = new SimulationConfig(1000, 1280, 720, 64);
        float speed = 50f;

        NativeStore store1 = SimulationStore.Allocate(in config);
        NativeStore store2 = SimulationStore.Allocate(in config);

        try
        {
            SimulationStore.SeedLinear(&store1, 1280, 720, speed, seed: 1337);
            SimulationStore.SeedLinear(&store2, 1280, 720, speed, seed: 1337);

            for (int i = 0; i < config.BodyCount; i++)
            {
                Assert.Equal(store1.X[i], store2.X[i]);
                Assert.Equal(store1.Y[i], store2.Y[i]);
                Assert.Equal(store1.Vx[i], store2.Vx[i]);
                Assert.Equal(store1.Vy[i], store2.Vy[i]);

                Assert.True(store1.X[i] >= 0f && store1.X[i] < 1280f);
                Assert.True(store1.Y[i] >= 0f && store1.Y[i] < 720f);

                float velocityMagnitude = MathF.Sqrt(store1.Vx[i] * store1.Vx[i] + store1.Vy[i] * store1.Vy[i]);
                Assert.Equal(speed, velocityMagnitude, 1e-5f);
            }
        }
        finally
        {
            SimulationStore.Dispose(&store1);
            SimulationStore.Dispose(&store2);
        }
    }
}
