using System;
using Xunit;
using GraviSharp.Core;

namespace GraviSharp.Tests.Phase2;

public class ScenarioSwitchTests
{
    private const int BodyCount = 1000;
    private const int Width = 1280;
    private const int Height = 720;
    private const int Alignment = 64;
    private const float BhMass = 50000f;
    private const float BhX = Width * 0.5f;
    private const float BhY = Height * 0.5f;
    private const float Tolerance = 1e-3f;

    private static (NativeStore store, SimulationConfig cfg) Allocate()
    {
        var cfg = new SimulationConfig(BodyCount, Width, Height, Alignment);
        NativeStore store = SimulationStore.Allocate(in cfg);
        return (store, cfg);
    }

    private static void Cleanup(NativeStore* store) { SimulationStore.Dispose(store); }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "ScenarioSwitch")]
    public unsafe void ComputeForcesBruteWithAttractors_AddsStaticAttractorPull()
    {
        // Use a single-body store so only the BH contributes (no body-body pair interactions).
        var cfg = new SimulationConfig(1, Width, Height, Alignment);
        NativeStore store = SimulationStore.Allocate(in cfg);
        try
        {
            // Place body at a known offset from BH (BhX=640, BhY=360).
            store.X[0] = BhX + 100f;
            store.Y[0] = BhY + 50f;
            store.Vx[0] = 0f;
            store.Vy[0] = 0f;

            PhysicsStep step = PhysicsStep.Create(1f / 60f);
            SimulationStore.ClearForces(&store);
            Span<StaticAttractor> attr = stackalloc StaticAttractor[1];
            attr[0] = new StaticAttractor(BhX, BhY, BhMass);
            SimulationStore.ComputeForcesBruteWithAttractors(&store, attr, in step);

            ReadOnlySpan<float> x = SimulationStore.ViewX(in store);
            ReadOnlySpan<float> y = SimulationStore.ViewY(in store);
            ReadOnlySpan<float> fx = SimulationStore.ViewFx(in store);
            ReadOnlySpan<float> fy = SimulationStore.ViewFy(in store);

            float dx = BhX - x[0];
            float dy = BhY - y[0];
            float distSq = dx * dx + dy * dy + step.Softening * step.Softening;
            float invDist = MathF.ReciprocalSqrtEstimate(distSq);
            float invDist3 = invDist * invDist * invDist;
            float fbhMag = step.G * BhMass * invDist3;
            float expectedFx = fbhMag * dx;
            float expectedFy = fbhMag * dy;

            Assert.Equal(expectedFx, fx[0], Tolerance);
            Assert.Equal(expectedFy, fy[0], Tolerance);
        }
        finally { SimulationStore.Dispose(&store); }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "ScenarioSwitch")]
    public unsafe void ComputeForcesBruteWithAttractors_ThrowsOnInvalidArgs()
    {
        var (store, _) = Allocate();
        NativeStore* pStore = &store;
        try
        {
            PhysicsStep step = PhysicsStep.Create(1f / 60f);

            Assert.Throws<InvalidOperationException>(() =>
                SimulationStore.ComputeForcesBruteWithAttractors(null, ReadOnlySpan<StaticAttractor>.Empty, in step));
                
            StaticAttractor[] zeroMassAttr = [ new(BhX, BhY, 0f) ];
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SimulationStore.ComputeForcesBruteWithAttractors(pStore, zeroMassAttr, in step));
            
            StaticAttractor[] negMassAttr = [ new(BhX, BhY, -1f) ];
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SimulationStore.ComputeForcesBruteWithAttractors(pStore, negMassAttr, in step));
        }
        finally { Cleanup(pStore); }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "ScenarioSwitch")]
    public unsafe void SeedRandomCloud_OverridesPreviousOrbitalDiskScenario()
    {
        var (store, _) = Allocate();
        try
        {
            SimulationStore.SeedOrbitalDisk(&store, Width, Height,
                                            innerRadius: 50f, outerRadius: 300f,
                                            centralMass: 10000f, seed: 1337);
            ReadOnlySpan<float> vxBefore = SimulationStore.ViewVx(in store);
            ReadOnlySpan<float> vyBefore = SimulationStore.ViewVy(in store);
            float vBefore = MathF.Sqrt(vxBefore[0] * vxBefore[0] + vyBefore[0] * vyBefore[0]);

            SimulationStore.SeedRandomCloud(&store, Width, Height,
                                            spread: 120f, thermalSpeed: 5f, seed: 42);
            ReadOnlySpan<float> vxAfter = SimulationStore.ViewVx(in store);
            ReadOnlySpan<float> vyAfter = SimulationStore.ViewVy(in store);
            float vAfter = MathF.Sqrt(vxAfter[0] * vxAfter[0] + vyAfter[0] * vyAfter[0]);

            Assert.NotEqual(vBefore, vAfter);
            Assert.True(MathF.Abs(vAfter) < 10f, $"Thermal velocity {vAfter} out of expected range.");
        }
        finally { Cleanup(&store); }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "ScenarioSwitch")]
    public unsafe void ClearForces_PreservesCountAndZeroesAccumulators()
    {
        var (store, _) = Allocate();
        try
        {
            SimulationStore.SeedRandomCloud(&store, Width, Height,
                                            spread: 120f, thermalSpeed: 5f, seed: 1337);
            PhysicsStep step = PhysicsStep.Create(1f / 60f);
            SimulationStore.ComputeForcesBrute(&store, in step);
            Assert.Equal(BodyCount, store.Count);

            SimulationStore.ClearForces(&store);
            ReadOnlySpan<float> fx = SimulationStore.ViewFx(in store);
            ReadOnlySpan<float> fy = SimulationStore.ViewFy(in store);
            for (int i = 0; i < BodyCount; i++)
            {
                Assert.Equal(0f, fx[i]);
                Assert.Equal(0f, fy[i]);
            }
            Assert.Equal(BodyCount, store.Count);
        }
        finally { Cleanup(&store); }
    }
}
