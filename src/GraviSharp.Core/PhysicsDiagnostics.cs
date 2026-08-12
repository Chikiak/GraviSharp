using System;
using System.Runtime.CompilerServices;

namespace GraviSharp.Core;

public static unsafe class PhysicsDiagnostics
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeDiagnostics(
        NativeStore* store, out float kinetic, out float px, out float py, out float cx, out float cy)
    {
        if (store == null || store->Count <= 0)
        {
            kinetic = 0f;
            px = 0f;
            py = 0f;
            cx = 0f;
            cy = 0f;
            return;
        }

        int count = store->Count;
        kinetic = 0f;
        px = 0f;
        py = 0f;
        cx = 0f;
        cy = 0f;

        for (int i = 0; i < count; i++)
        {
            float vx = store->Vx[i];
            float vy = store->Vy[i];
            kinetic += 0.5f * (vx * vx + vy * vy);
            px += vx;
            py += vy;
            cx += store->X[i];
            cy += store->Y[i];
        }

        cx /= count;
        cy /= count;
    }

    /// <summary>
    /// Computes total kinetic energy: 0.5 * Σ m * v²
    /// </summary>
    /// <param name="store">Native particle store (must be valid for Count particles)</param>
    /// <returns>Kinetic energy in simulation units</returns>
    public static float KineticEnergy(NativeStore* store)
    {
        float ke = 0f;
        for (int i = 0; i < store->Count; i++)
        {
            float vx = store->Vx[i];
            float vy = store->Vy[i];
            ke += vx * vx + vy * vy;
        }
        return 0.5f * ke;
    }

    /// <summary>
    /// Computes total potential energy: -G * Σᵢ<ⱼ (mᵢ mⱼ / rᵢⱼ) with softening
    /// </summary>
    /// <param name="store">Native particle store (must be valid for Count particles)</param>
    /// <param name="g">Gravitational constant</param>
    /// <param name="softening">Softening factor</param>
    /// <returns>Potential energy (negative)</returns>
    public static float PotentialEnergy(NativeStore* store, float g, float softening)
    {
        float pe = 0f;
        float softeningSq = softening * softening;
        for (int i = 0; i < store->Count - 1; i++)
        {
            float xi = store->X[i];
            float yi = store->Y[i];
            for (int j = i + 1; j < store->Count; j++)
            {
                float dx = store->X[j] - xi;
                float dy = store->Y[j] - yi;
                float distSq = dx * dx + dy * dy + softeningSq;
                float invDist = MathF.ReciprocalSqrtEstimate(distSq);
                pe -= g * invDist;
            }
        }
        return pe;
    }

    /// <summary>
    /// Computes total energy: Kinetic + Potential
    /// </summary>
    public static float TotalEnergy(NativeStore* store, float g, float softening)
        => KineticEnergy(store) + PotentialEnergy(store, g, softening);

    /// <summary>
    /// Computes total linear momentum: (Σ vx, Σ vy)
    /// </summary>
    public static (float px, float py) LinearMomentum(NativeStore* store)
    {
        float px = 0f, py = 0f;
        int count = store->Count;
        for (int i = 0; i < count; i++)
        {
            px += store->Vx[i];
            py += store->Vy[i];
        }
        return (px, py);
    }

    /// <summary>
    /// Computes center of mass: (Σ x / count, Σ y / count)
    /// </summary>
    public static (float cx, float cy) CenterOfMass(NativeStore* store)
    {
        float cx = 0f, cy = 0f;
        int count = store->Count;
        if (count == 0) return (0f, 0f);
        for (int i = 0; i < count; i++)
        {
            cx += store->X[i];
            cy += store->Y[i];
        }
        return (cx / count, cy / count);
    }
}
