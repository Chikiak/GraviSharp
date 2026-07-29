using System;
using Xunit;
using GraviSharp.Core;

namespace GraviSharp.VisualTests;

[Trait("Category", "SmokeVisual")]
public class Phase1SmokeTest
{
    private const int SeedRegressionBodyCount = 10000;
    private const int SeedRegressionWidth = 1280;
    private const int SeedRegressionHeight = 720;
    private const float SeedRegressionSpeed = 50f;
    private const int SeedRegressionSeed = 1337;

    [Fact]
    public void SeedRegression_1337()
    {
        var cfg = new SimulationConfig(SeedRegressionBodyCount,
                                        SeedRegressionWidth,
                                        SeedRegressionHeight,
                                        64);
        NativeStore store = SimulationStore.Allocate(in cfg);
        try
        {
            unsafe
            {
                SimulationStore.SeedLinear(&store, SeedRegressionWidth,
                                            SeedRegressionHeight,
                                            SeedRegressionSpeed,
                                            SeedRegressionSeed);
            }
            ReadOnlySpan<float> view = SimulationStore.ViewX(in store);
            Assert.Equal(SeedRegressionBodyCount, view.Length);

            var rng = new Random(SeedRegressionSeed);
            for (int i = 0; i < SeedRegressionBodyCount; i++)
            {
                float expected = (float)rng.NextDouble() * SeedRegressionWidth;
                _ = rng.NextDouble(); // Consume Y NextDouble()
                _ = rng.NextDouble(); // Consume angle NextDouble()
                Assert.Equal(expected, view[i], 1e-6f);
            }
        }
        finally
        {
            unsafe { SimulationStore.Dispose(&store); }
        }
    }

    [Fact]
    public void BounceStaysInbounds_1000iterations()
    {
        var cfg = new SimulationConfig(SeedRegressionBodyCount,
                                        SeedRegressionWidth,
                                        SeedRegressionHeight,
                                        64);
        NativeStore store = SimulationStore.Allocate(in cfg);
        try
        {
            unsafe
            {
                SimulationStore.SeedLinear(&store, SeedRegressionWidth,
                                            SeedRegressionHeight,
                                            SeedRegressionSpeed,
                                            SeedRegressionSeed);
                const float dt = 1f / 60f;
                for (int iter = 0; iter < 1000; iter++)
                {
                    SimulationStore.UpdateLinear(&store, SeedRegressionWidth,
                                                  SeedRegressionHeight, dt);
                    ReadOnlySpan<float> viewX = SimulationStore.ViewX(in store);
                    ReadOnlySpan<float> viewY = SimulationStore.ViewY(in store);
                    for (int i = 0; i < SeedRegressionBodyCount; i++)
                    {
                        Assert.InRange(viewX[i], 0f, (float)SeedRegressionWidth);
                        Assert.InRange(viewY[i], 0f, (float)SeedRegressionHeight);
                    }
                }
            }
        }
        finally
        {
            unsafe { SimulationStore.Dispose(&store); }
        }
    }
}
