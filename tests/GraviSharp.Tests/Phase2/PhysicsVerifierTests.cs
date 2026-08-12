using System;
using Xunit;
using GraviSharp.Core;

namespace GraviSharp.Tests.Phase2;

/// <summary>
/// Regression tests for PhysicsVerifier harness (PH2-ISSUE-009 Phase 6).
/// VerifyPhysics_TwoBody_PassesAllThresholds: pure PASS test on clean two-body.
/// Heap sensitivity tests verify HeapVerifier detects managed allocations in isolation
/// (PhysicsVerifier.RunVerifyPhysics has no injection hook; the negative control tests
/// in VerifyPhysicsNegativeTests cover the fault-detection flows).
/// </summary>
public unsafe class PhysicsVerifierTests
{
    private static PhysicsStep NewVerifyStep() => PhysicsStep.Create(1f / 60f, softening: 1f, damping: 1f, g: 1f);

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "Verifier")]
    public void VerifyPhysics_TwoBody_PassesAllThresholds()
    {
        HeapVerifier.Enable();
        HeapVerifier.Reset();

        var config = new SimulationConfig(2, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            PhysicsStep step = NewVerifyStep();
            PhysicsVerifier.SeedTwoBodySymmetricForVerification(&store, 1280, 720, step.G);

            HeapVerifier.Reset(); // clear any allocations from Allocation/seeding

            PhysicsVerificationResult result = PhysicsVerifier.RunVerifyPhysics(&store, in step);

            Assert.True(result.Pass, $"Clean two-body should PASS all thresholds. {result}");
            Assert.InRange(result.MaxEnergyDrift, 0f, 0.005f);
            Assert.InRange(result.MaxMomentumDriftX, 0f, 0.01f);
            Assert.InRange(result.MaxMomentumDriftY, 0f, 0.01f);
            Assert.InRange(result.MaxCoMDrift, 0f, 5f);
            Assert.Equal(0, result.MaxHeapDelta);
        }
        finally
        {
            HeapVerifier.Reset();
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "Verifier")]
    public void VerifyPhysics_RejectsEnergyDrift_PerTickVelocityKick()
    {
        var config = new SimulationConfig(2, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            PhysicsStep step = NewVerifyStep();
            PhysicsVerifier.SeedTwoBodySymmetricForVerification(&store, 1280, 720, step.G);

            float e0 = PhysicsDiagnostics.TotalEnergy(&store, step.G, step.Softening);

            for (int tick = 1; tick <= 600; tick++)
            {
                SimulationStore.ClearForces(&store);
                SimulationStore.ComputeForcesBrute(&store, in step);
                SimulationStore.IntegrateSymplecticEuler(&store, in step);
                store.Vy[0] += 0.5f; // active energy injection
            }

            float eFinal = PhysicsDiagnostics.TotalEnergy(&store, step.G, step.Softening);
            float drift = MathF.Abs(eFinal - e0) / (MathF.Abs(e0) + 1e-6f);

            Assert.True(drift >= 0.005f,
                        $"Energy drift {drift * 100f:F3}% should exceed 0.5% after per-tick kicks");
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "Verifier")]
    public void VerifyPhysics_RejectsGcDelta_ManagedHeapAllocPerTick()
    {
        HeapVerifier.Enable();
        HeapVerifier.Reset();

        var config = new SimulationConfig(2, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            PhysicsStep step = NewVerifyStep();
            PhysicsVerifier.SeedTwoBodySymmetricForVerification(&store, 1280, 720, step.G);

            HeapVerifier.Reset();

            for (int tick = 1; tick <= 100; tick++)
            {
                SimulationStore.ClearForces(&store);
                SimulationStore.ComputeForcesBrute(&store, in step);
                SimulationStore.IntegrateSymplecticEuler(&store, in step);

                HeapVerifier.BeginFrame(tick);
                byte[] junk = new byte[1024];
                HeapVerifier.EndFrame(tick);
            }

            Assert.True(HeapVerifier.MaxDelta > 0,
                        $"HeapVerifier.MaxDelta={HeapVerifier.MaxDelta} should be >0 under managed heap allocation");
        }
        finally
        {
            HeapVerifier.Reset();
            SimulationStore.Dispose(&store);
        }
    }
}
