using System;
using Xunit;
using GraviSharp.Core;

namespace GraviSharp.Tests.Phase2;

/// <summary>
/// Negative control tests for --verify-physics harness sensitivity (QG-PH2-001).
/// Each test injects a controlled fault into an inline replica of the verification
/// harness and asserts the fault is detected. PhysicsVerifier.RunVerifyPhysics does
/// not expose injection hooks (it would couple production to testing), so tests that
/// require active per-tick fault injection replicate the harness loop locally.
/// </summary>
public unsafe class VerifyPhysicsNegativeTests
{
    private static PhysicsStep NewVerifyStep() => PhysicsStep.Create(1f / 60f, softening: 1f, damping: 1f, g: 1f);

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "NegativeControl")]
    public void VerifyPhysics_DetectsEnergyInjection_PerTickVelocityKick()
    {
        var config = new SimulationConfig(2, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            PhysicsStep step = NewVerifyStep();
            PhysicsVerifier.SeedTwoBodySymmetricForVerification(&store, 1280, 720, step.G);

            float e0 = PhysicsDiagnostics.TotalEnergy(&store, step.G, step.Softening);

            // Inject energy EVERY tick: small +0.5 on Vy[0] continuously adds kinetic energy.
            // Symplectic Euler conserves energy structurally, so passive setup changes don't
            // break conservation — only active per-tick energy injection creates real drift.
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
                        $"Energy drift {drift * 100f:F3}% should exceed 0.5% threshold after 600 per-tick kicks");
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "NegativeControl")]
    public void VerifyPhysics_DetectsGcAllocation_ManagedHeapAllocPerTick()
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

            for (int tick = 1; tick <= 100; tick++)
            {
                SimulationStore.ClearForces(&store);
                SimulationStore.ComputeForcesBrute(&store, in step);
                SimulationStore.IntegrateSymplecticEuler(&store, in step);

                HeapVerifier.BeginFrame(tick);
                // Real managed heap allocation: GC.GetTotalAllocatedBytes will increase.
                byte[] junk = new byte[1024];
                HeapVerifier.EndFrame(tick);
            }

            Assert.True(HeapVerifier.MaxDelta > 0,
                        $"HeapVerifier.MaxDelta={HeapVerifier.MaxDelta} should be >0 after 100 managed heap allocations");
        }
        finally
        {
            HeapVerifier.Reset();
            SimulationStore.Dispose(&store);
        }
    }
}
