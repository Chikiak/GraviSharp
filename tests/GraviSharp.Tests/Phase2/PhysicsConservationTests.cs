using System;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;
using GraviSharp.Core;

namespace GraviSharp.Tests.Phase2;

public unsafe class PhysicsConservationTests
{
    private const double EnergyTolerance = 0.005;   // 0.5%
    private const double MomentumTolerance = 0.01;  // 1.0%
    private const int Ticks1000 = 1000;

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "Numerical")]
    public void TwoBodySymmetric_EnergyMomentumConserved_1000Ticks()
    {
        var config = new SimulationConfig(2, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            // Two bodies orbiting barycenter
            // Distance = 80 px (40 px each from center 640,360)
            float v = MathF.Sqrt(PhysicsConstants.G);
            store.X[0] = 600f; store.Y[0] = 360f; store.Vx[0] = 0f; store.Vy[0] = -v; // v = sqrt(G*M/r) approx
            store.X[1] = 680f; store.Y[1] = 360f; store.Vx[1] = 0f; store.Vy[1] = v;

            var step = PhysicsStep.Create(1f / 60f, softening: 8f, damping: 1f);

            float initialEnergy = PhysicsDiagnostics.TotalEnergy(&store, step.G, step.Softening);
            (float p0x, float p0y) = PhysicsDiagnostics.LinearMomentum(&store);

            for (int i = 0; i < Ticks1000; i++)
            {
                SimulationStore.ClearForces(&store);
                SimulationStore.ComputeForcesBrute(&store, in step);
                SimulationStore.IntegrateSymplecticEuler(&store, in step);
            }

            float finalEnergy = PhysicsDiagnostics.TotalEnergy(&store, step.G, step.Softening);
            (float p1x, float p1y) = PhysicsDiagnostics.LinearMomentum(&store);

            float energyDrift = MathF.Abs(finalEnergy - initialEnergy) / (MathF.Abs(initialEnergy) + 1e-6f);
            float momentumDriftX = MathF.Abs(p1x - p0x) / (MathF.Abs(p0x) + 1f);
            float momentumDriftY = MathF.Abs(p1y - p0y) / (MathF.Abs(p0y) + 1f);

            Assert.True(energyDrift < (float)EnergyTolerance, $"Energy drift {energyDrift * 100f:F3}% exceeds tolerance {EnergyTolerance * 100f}%");
            Assert.True(momentumDriftX < (float)MomentumTolerance, $"Momentum X drift {momentumDriftX * 100f:F3}% exceeds tolerance");
            Assert.True(momentumDriftY < (float)MomentumTolerance, $"Momentum Y drift {momentumDriftY * 100f:F3}% exceeds tolerance");
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "Numerical")]
    public void ThreeBodyEquilateral_EnergyConserved_1000Ticks()
    {
        var config = new SimulationConfig(3, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            float cx = 640f, cy = 360f, r = 80f;
            // Keplerian orbital speed for equilateral triangle (side = r*sqrt(3)):
            // Net force on each body from the other two points to centroid, magnitude = sqrt(3) * G / side^2.
            // Centripetal balance: v^2 / r = sqrt(3) * G / (3 * r^2)  =>  v = sqrt(G / (sqrt(3) * r)).
            float v = MathF.Sqrt(PhysicsConstants.G / (MathF.Sqrt(3f) * r));
            for (int k = 0; k < 3; k++)
            {
                float theta = k * (2f * MathF.PI / 3f);
                store.X[k] = cx + r * MathF.Cos(theta);
                store.Y[k] = cy + r * MathF.Sin(theta);
                // Tangential velocity for stable equilateral rotation (counter-clockwise)
                store.Vx[k] = -v * MathF.Sin(theta);
                store.Vy[k] = v * MathF.Cos(theta);
            }

            var step = PhysicsStep.Create(1f / 60f, softening: 8f, damping: 1f);

            float initialEnergy = PhysicsDiagnostics.TotalEnergy(&store, step.G, step.Softening);
            (float p0x, float p0y) = PhysicsDiagnostics.LinearMomentum(&store);

            for (int i = 0; i < Ticks1000; i++)
            {
                SimulationStore.ClearForces(&store);
                SimulationStore.ComputeForcesBrute(&store, in step);
                SimulationStore.IntegrateSymplecticEuler(&store, in step);
            }

            float finalEnergy = PhysicsDiagnostics.TotalEnergy(&store, step.G, step.Softening);
            (float p1x, float p1y) = PhysicsDiagnostics.LinearMomentum(&store);

            float energyDrift = MathF.Abs(finalEnergy - initialEnergy) / (MathF.Abs(initialEnergy) + 1e-6f);
            float momentumDriftX = MathF.Abs(p1x - p0x) / (MathF.Abs(p0x) + 1f);
            float momentumDriftY = MathF.Abs(p1y - p0y) / (MathF.Abs(p0y) + 1f);

            Assert.True(energyDrift < (float)EnergyTolerance, $"Three-body energy drift {energyDrift * 100f:F3}% exceeds tolerance {EnergyTolerance * 100f}%");
            Assert.True(momentumDriftX < (float)MomentumTolerance);
            Assert.True(momentumDriftY < (float)MomentumTolerance);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "Numerical")]
    public void IsolatedBody_NoForce_AdvancesLinear_1000Ticks()
    {
        var config = new SimulationConfig(1, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            store.X[0] = 100f;
            store.Y[0] = 200f;
            store.Vx[0] = 100f;
            store.Vy[0] = 0f;
            store.Fx[0] = 0f;
            store.Fy[0] = 0f;

            var step = PhysicsStep.Create(0.1f);

            for (int i = 0; i < Ticks1000; i++)
            {
                SimulationStore.IntegrateSymplecticEuler(&store, in step);
            }

            // Spec phase2.md:155: Vx=100, Vy=0, F=0, Dt=0.1 -> X increases by exactly Vx*Dt*N = 10*N units.
            // For N=1000 ticks: delta = 100 * 0.1 * 1000 = 10000; expected X = 100 + 10000 = 10100.
            float expectedX = 100f + 100f * 0.1f * Ticks1000;
            float expectedY = 200f;

            Assert.Equal(expectedX, store.X[0], 1e-2f);
            Assert.Equal(expectedY, store.Y[0], 1e-2f);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "Numerical")]
    public void BounceStaysInBounds_100000Ticks()
    {
        var config = new SimulationConfig(1, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            store.X[0] = 640f;
            store.Y[0] = 360f;
            store.Vx[0] = 500f;
            store.Vy[0] = -500f;

            var step = PhysicsStep.Create(1f / 60f);

            for (int i = 0; i < 100000; i++)
            {
                store.Fx[0] = 0f;
                store.Fy[0] = 0f;
                SimulationStore.IntegrateSymplecticEuler(&store, in step);
                SimulationStore.ReflectBounds(&store, 1280, 720);
            }

            Assert.InRange(store.X[0], 0f, 1280f);
            Assert.InRange(store.Y[0], 0f, 720f);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "Numerical")]
    public void TwoBody_DumpsEnergyMomentumCsv_ArtifactForPlot()
    {
        var config = new SimulationConfig(2, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            float v = MathF.Sqrt(PhysicsConstants.G);
            store.X[0] = 600f; store.Y[0] = 360f; store.Vx[0] = 0f; store.Vy[0] = -v;
            store.X[1] = 680f; store.Y[1] = 360f; store.Vx[1] = 0f; store.Vy[1] = v;

            var step = PhysicsStep.Create(1f / 60f, softening: 8f, damping: 1f);

            string csvPath = Path.Combine(AcceptancePaths.FindAcceptanceDir(), "phase2_tc1_energy_momentum.csv");
            string? dir = Path.GetDirectoryName(csvPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using var writer = new StreamWriter(csvPath, false);
            writer.WriteLine("tick,E_total,E_kinetic,E_potential,Px,Py,Cx,Cy");

            for (int i = 0; i <= Ticks1000; i++)
            {
                if (i % 10 == 0)
                {
                    float k = PhysicsDiagnostics.KineticEnergy(&store);
                    float u = PhysicsDiagnostics.PotentialEnergy(&store, step.G, step.Softening);
                    float et = k + u;
                    (float px, float py) = PhysicsDiagnostics.LinearMomentum(&store);
                    (float cx, float cy) = PhysicsDiagnostics.CenterOfMass(&store);
                    writer.WriteLine($"{i},{et:F4},{k:F4},{u:F4},{px:F4},{py:F4},{cx:F2},{cy:F2}");
                }

                if (i < Ticks1000)
                {
                    SimulationStore.ClearForces(&store);
                    SimulationStore.ComputeForcesBrute(&store, in step);
                    SimulationStore.IntegrateSymplecticEuler(&store, in step);
                }
            }

            Assert.True(File.Exists(csvPath), "CSV artifact was not created.");
            var fileInfo = new FileInfo(csvPath);
            Assert.True(fileInfo.Length > 100, "CSV artifact is too small.");
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }
}
