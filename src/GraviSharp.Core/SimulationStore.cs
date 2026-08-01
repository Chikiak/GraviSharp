using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GraviSharp.Core;

public static class SimulationStore
{
    public static unsafe NativeStore Allocate(in SimulationConfig config)
    {
        if (config.BodyCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(config.BodyCount), "BodyCount must be greater than zero.");
        if (config.Width <= 0 || config.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(config), "Width and Height must be positive.");

        int alignment = config.Alignment < 64 ? 64 : config.Alignment;
        nuint stride = (nuint)(config.BodyCount * sizeof(float));
        nuint alignedStride = (stride + (nuint)alignment - 1) & ~(nuint)(alignment - 1);
        nuint totalBytes = alignedStride * 6;

        void* basePtr = NativeMemory.AlignedAlloc(totalBytes, (nuint)alignment);
        if (basePtr == null)
            throw new OutOfMemoryException("AlignedAlloc failed for consolidated simulation memory block.");

        NativeStore store = new()
        {
            BaseAddress = basePtr,
            X = (float*)basePtr,
            Y = (float*)((byte*)basePtr + alignedStride),
            Vx = (float*)((byte*)basePtr + alignedStride * 2),
            Vy = (float*)((byte*)basePtr + alignedStride * 3),
            Fx = (float*)((byte*)basePtr + alignedStride * 4),
            Fy = (float*)((byte*)basePtr + alignedStride * 5),
            Count = config.BodyCount,
            ByteAlignment = (nuint)alignment
        };

        return store;
    }

    public static unsafe void Dispose(NativeStore* store)
    {
        if (store == null) return;

        if (store->BaseAddress != null)
        {
            NativeMemory.AlignedFree(store->BaseAddress);
            store->BaseAddress = null;
        }

        store->X = null;
        store->Y = null;
        store->Vx = null;
        store->Vy = null;
        store->Fx = null;
        store->Fy = null;
        store->Count = 0;
        store->ByteAlignment = 0;
    }

    public static unsafe void SeedLinear(NativeStore* store, int width, int height, float speed, int seed = 1337)
    {
        if (store == null || store->BaseAddress == null)
            throw new InvalidOperationException("Cannot seed a null or disposed NativeStore.");

        var rng = new Random(seed);
        int count = store->Count;

        for (int i = 0; i < count; i++)
        {
            store->X[i] = (float)rng.NextDouble() * width;
            store->Y[i] = (float)rng.NextDouble() * height;
            float angle = (float)rng.NextDouble() * MathF.PI * 2f;
            store->Vx[i] = MathF.Cos(angle) * speed;
            store->Vy[i] = MathF.Sin(angle) * speed;
        }
    }

    public static unsafe void UpdateLinear(NativeStore* store, int width, int height, float dt)
    {
        if (store == null || store->BaseAddress == null)
            throw new InvalidOperationException("Cannot update a null or disposed NativeStore.");

        int count = store->Count;
        float* xPtr = store->X;
        float* yPtr = store->Y;
        float* vxPtr = store->Vx;
        float* vyPtr = store->Vy;

        for (int i = 0; i < count; i++)
        {
            xPtr[i] += vxPtr[i] * dt;
            yPtr[i] += vyPtr[i] * dt;
        }

        ReflectBounds(store, width, height);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ReflectBounds(NativeStore* store, int width, int height)
    {
        if (store == null || store->BaseAddress == null)
            throw new InvalidOperationException("Cannot reflect bounds on a null or disposed NativeStore.");

        int count = store->Count;
        float* xPtr = store->X;
        float* yPtr = store->Y;
        float* vxPtr = store->Vx;
        float* vyPtr = store->Vy;

        for (int i = 0; i < count; i++)
        {
            float x = xPtr[i];
            float y = yPtr[i];
            float vx = vxPtr[i];
            float vy = vyPtr[i];

            // X bounds reflection
            if (x < 0f)
            {
                x = -x;
                vx = -vx;
            }
            else if (x >= width)
            {
                x = 2f * width - x;
                vx = -vx;
            }

            // Y bounds reflection
            if (y < 0f)
            {
                y = -y;
                vy = -vy;
            }
            else if (y >= height)
            {
                y = 2f * height - y;
                vy = -vy;
            }

            xPtr[i] = x;
            yPtr[i] = y;
            vxPtr[i] = vx;
            vyPtr[i] = vy;
        }
    }

    /// <summary>Applies Symplectic Euler (kick-drift) integration. Assumes m=1 implicit mass.
    /// Does NOT recalculate forces and does NOT reflect bounds (caller invokes <see cref="ReflectBounds"/> separately if needed).</summary>
    public static unsafe void IntegrateSymplecticEuler(NativeStore* store, in PhysicsStep step)
    {
        if (store == null || store->BaseAddress == null)
            throw new InvalidOperationException("Cannot integrate a null or disposed NativeStore.");

        float dt = step.Dt;
        float damping = step.Damping;
        int count = store->Count;
        float* xPtr = store->X;
        float* yPtr = store->Y;
        float* vxPtr = store->Vx;
        float* vyPtr = store->Vy;
        float* fxPtr = store->Fx;
        float* fyPtr = store->Fy;

        for (int i = 0; i < count; i++)
        {
            vxPtr[i] = (vxPtr[i] + fxPtr[i] * dt) * damping;
            vyPtr[i] = (vyPtr[i] + fyPtr[i] * dt) * damping;
            xPtr[i] += vxPtr[i] * dt;
            yPtr[i] += vyPtr[i] * dt;
        }
    }

    /// <summary>Exposes the X coordinates as a zero-copy <see cref="ReadOnlySpan{float}"/>.</summary>
    /// <remarks>LIFETIME CONTRACT: The returned <see cref="ReadOnlySpan{T}"/> aliases the native
    /// memory of <paramref name="store"/>. Callers MUST NOT retain it after
    /// <see cref="SimulationStore.Dispose(NativeStore*)"/> runs; doing so reads freed memory.
    /// After Dispose, returns <see cref="ReadOnlySpan{float}.Empty"/> safely.</remarks>
    public static unsafe ReadOnlySpan<float> ViewX(in NativeStore store)
    {
        if (store.X is null) return ReadOnlySpan<float>.Empty;
        return new ReadOnlySpan<float>(store.X, store.Count);
    }

    /// <summary>Exposes the Y coordinates as a zero-copy <see cref="ReadOnlySpan{float}"/>.</summary>
    /// <remarks>LIFETIME CONTRACT: Aliases store native memory. Returns empty if disposed.</remarks>
    public static unsafe ReadOnlySpan<float> ViewY(in NativeStore store)
    {
        if (store.Y is null) return ReadOnlySpan<float>.Empty;
        return new ReadOnlySpan<float>(store.Y, store.Count);
    }

    /// <summary>Exposes the X velocities as a zero-copy <see cref="ReadOnlySpan{float}"/>.</summary>
    /// <remarks>LIFETIME CONTRACT: Aliases store native memory. Returns empty if disposed.</remarks>
    public static unsafe ReadOnlySpan<float> ViewVx(in NativeStore store)
    {
        if (store.Vx is null) return ReadOnlySpan<float>.Empty;
        return new ReadOnlySpan<float>(store.Vx, store.Count);
    }

    /// <summary>Exposes the Y velocities as a zero-copy <see cref="ReadOnlySpan{float}"/>.</summary>
    /// <remarks>LIFETIME CONTRACT: Aliases store native memory. Returns empty if disposed.</remarks>
    public static unsafe ReadOnlySpan<float> ViewVy(in NativeStore store)
    {
        if (store.Vy is null) return ReadOnlySpan<float>.Empty;
        return new ReadOnlySpan<float>(store.Vy, store.Count);
    }

    /// <summary>Resets the Fx and Fy force accumulators to zero in O(N) via native memset.</summary>
    public static unsafe void ClearForces(NativeStore* store)
    {
        if (store == null || store->BaseAddress == null) return;
        nuint bytes = (nuint)store->Count * sizeof(float);
        NativeMemory.Clear(store->Fx, bytes);
        NativeMemory.Clear(store->Fy, bytes);
    }

    /// <summary>Calculates the Newtonian gravitational forces accumulated in Fx/Fy via O(N^2) brute-force.
    /// Implicit unit mass (m=1) for all integrated bodies in set N. Static attractors with explicit mass
    /// are handled outside this kernel (see ComputeForcesBruteWith...).</summary>
    /// <remarks>F = G * dx / (dx*dx + dy*dy + softening^2)^(3/2). Action-reaction symmetry: single computation per pair.</remarks>
    public static unsafe void ComputeForcesBrute(NativeStore* store, in PhysicsStep step)
    {
        if (store == null || store->BaseAddress == null)
            throw new InvalidOperationException("Cannot compute forces on a null or disposed NativeStore.");

        int count = store->Count;
        if (count < 2) return;

        float g = step.G;
        float softening = step.Softening;
        float softeningSq = softening * softening;

        float* xPtr = store->X;
        float* yPtr = store->Y;
        float* fxPtr = store->Fx;
        float* fyPtr = store->Fy;

        for (int i = 0; i < count - 1; i++)
        {
            float xi = xPtr[i];
            float yi = yPtr[i];

            for (int j = i + 1; j < count; j++)
            {
                float dx = xPtr[j] - xi;
                float dy = yPtr[j] - yi;
                float distSq = dx * dx + dy * dy + softeningSq;
                float invDist = MathF.ReciprocalSqrtEstimate(distSq);
                float invDist3 = invDist * invDist * invDist;
                float f = g * invDist3;
                float fx = f * dx;
                float fy = f * dy;

                fxPtr[i] += fx;
                fyPtr[i] += fy;
                fxPtr[j] -= fx;
                fyPtr[j] -= fy;
            }
        }
    }

    /// <summary>O(N^2) brute-force with static central attractor (BlackHoleDisk). 
    /// Attractor contributes to Fx[i]/Fy[i] only (static, not integrated). 
    /// Will be refactored to ComputeForcesBruteWithAttractors in PH2-ISSUE-010.</summary>
    public static unsafe void ComputeForcesBruteWithCentral(
        NativeStore* store, float bhX, float bhY, float bhMass, in PhysicsStep step)
    {
        if (store == null || store->BaseAddress == null)
            throw new InvalidOperationException("Cannot compute forces on a null or disposed NativeStore.");
        if (bhMass <= 0f)
            throw new ArgumentOutOfRangeException(nameof(bhMass), "bhMass must be > 0f.");

        int count = store->Count;
        float g = step.G;
        float softening = step.Softening;
        float softeningSq = softening * softening;
        float* xPtr = store->X;
        float* yPtr = store->Y;
        float* fxPtr = store->Fx;
        float* fyPtr = store->Fy;

        for (int i = 0; i < count - 1; i++)
        {
            float xi = xPtr[i];
            float yi = yPtr[i];

            // Body-body pair (action-reaction, identical to ComputeForcesBrute)
            for (int j = i + 1; j < count; j++)
            {
                float dx = xPtr[j] - xi;
                float dy = yPtr[j] - yi;
                float distSq = dx * dx + dy * dy + softeningSq;
                float invDist = MathF.ReciprocalSqrtEstimate(distSq);
                float invDist3 = invDist * invDist * invDist;
                float f = g * invDist3;
                float fx = f * dx;
                float fy = f * dy;
                fxPtr[i] += fx; fyPtr[i] += fy;
                fxPtr[j] -= fx; fyPtr[j] -= fy;
            }

            // Static central attractor (BH contributes only to body i)
            float bdx = bhX - xi;
            float bdy = bhY - yi;
            float bDistSq = bdx * bdx + bdy * bdy + softeningSq;
            float bInvDist = MathF.ReciprocalSqrtEstimate(bDistSq);
            float bInvDist3 = bInvDist * bInvDist * bInvDist;
            float fbh = g * bhMass * bInvDist3;
            fxPtr[i] += fbh * bdx;
            fyPtr[i] += fbh * bdy;
        }

        // i == count - 1: skip body-body (no j > i), but still pull by BH
        if (count > 0)
        {
            int i = count - 1;
            float bdx = bhX - xPtr[i];
            float bdy = bhY - yPtr[i];
            float bDistSq = bdx * bdx + bdy * bdy + softeningSq;
            float bInvDist = MathF.ReciprocalSqrtEstimate(bDistSq);
            float bInvDist3 = bInvDist * bInvDist * bInvDist;
            float fbh = g * bhMass * bInvDist3;
            fxPtr[i] += fbh * bdx;
            fyPtr[i] += fbh * bdy;
        }
    }

    /// <summary>Exposes the Fx force accumulator as a zero-copy <see cref="ReadOnlySpan{float}"/>.</summary>
    /// <remarks>LIFETIME CONTRACT: Aliases store native memory. Returns empty if disposed.</remarks>
    public static unsafe ReadOnlySpan<float> ViewFx(in NativeStore store)
    {
        if (store.Fx is null) return ReadOnlySpan<float>.Empty;
        return new ReadOnlySpan<float>(store.Fx, store.Count);
    }

    /// <summary>Exposes the Fy force accumulator as a zero-copy <see cref="ReadOnlySpan{float}"/>.</summary>
    /// <remarks>LIFETIME CONTRACT: Aliases store native memory. Returns empty if disposed.</remarks>
    public static unsafe ReadOnlySpan<float> ViewFy(in NativeStore store)
    {
        if (store.Fy is null) return ReadOnlySpan<float>.Empty;
        return new ReadOnlySpan<float>(store.Fy, store.Count);
    }

    /// <summary>Seeds a Gaussian-centered random cloud via CLT approximation.
    /// m=1 implicit. Thermal velocities. Zero forces. Only startup jet allocation: new Random(seed).</summary>
    public static unsafe void SeedRandomCloud(
        NativeStore* store, int width, int height, float spread, float thermalSpeed, int seed = 1337)
    {
        if (store == null || store->BaseAddress == null)
            throw new InvalidOperationException("Cannot seed a null or disposed NativeStore.");

        var rng = new Random(seed);
        int count = store->Count;
        float cx = width * 0.5f;
        float cy = height * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float normal = (float)(rng.NextDouble() + rng.NextDouble() + rng.NextDouble() - 1.5);
            float r = normal * spread;
            float theta = (float)rng.NextDouble() * MathF.PI * 2f;
            float x = cx + r * MathF.Cos(theta);
            float y = cy + r * MathF.Sin(theta);
            if (x < 0f) x = 0f; else if (x >= width) x = width - 1f;
            if (y < 0f) y = 0f; else if (y >= height) y = height - 1f;
            store->X[i] = x;
            store->Y[i] = y;
            store->Vx[i] = thermalSpeed * ((float)rng.NextDouble() - 0.5f);
            store->Vy[i] = thermalSpeed * ((float)rng.NextDouble() - 0.5f);
        }

        ClearForces(store);
    }

    /// <summary>Seeds an orbital disk with Keplerian tangential velocity v=sqrt(G*M/r) around a static central mass.
    /// m=1 implicit for N integrated bodies. centralMass informs v only (atractor integration is PH2-ISSUE-006).
    ///REQUIRES innerRadius >= 1f. Zero forces. Only startup jet: new Random(seed).</summary>
    public static unsafe void SeedOrbitalDisk(
        NativeStore* store, int width, int height, float innerRadius, float outerRadius, float centralMass, int seed = 1337)
    {
        if (store == null || store->BaseAddress == null)
            throw new InvalidOperationException("Cannot seed a null or disposed NativeStore.");
        if (innerRadius < 1f)
            throw new ArgumentOutOfRangeException(nameof(innerRadius), "innerRadius must be >= 1f to avoid division by zero.");

        var rng = new Random(seed);
        int count = store->Count;
        float cx = width * 0.5f;
        float cy = height * 0.5f;
        float g = PhysicsConstants.G;

        for (int i = 0; i < count; i++)
        {
            float r = innerRadius + (float)rng.NextDouble() * (outerRadius - innerRadius);
            float theta = (float)rng.NextDouble() * MathF.PI * 2f;
            store->X[i] = cx + r * MathF.Cos(theta);
            store->Y[i] = cy + r * MathF.Sin(theta);
            float v = MathF.Sqrt(g * centralMass / r);
            store->Vx[i] = -v * MathF.Sin(theta);
            store->Vy[i] =  v * MathF.Cos(theta);
        }

        ClearForces(store);
    }

    /// <summary>Safe-facade for <see cref="Allocate"/>. Allows callers without <c>unsafe</c>
    /// context to obtain a <see cref="NativeStore"/>.</summary>
    /// <remarks>TRANSITORY SCOPE: This wrapper exists exclusively to satisfy TC-PH1-002 criterion
    /// (phase1.md:213 — consume ViewX from non-unsafe code). Fase 2 may delete it in favor of
    /// direct unsafe callers once NativeStore allocation is moved out of non-unsafe test scaffolds.</remarks>
    public static NativeStore AllocateSafe(in SimulationConfig config)
        => Allocate(in config);

    /// <summary>Safe-facade for <see cref="SeedLinear"/>. Takes the store by reference.</summary>
    /// <remarks>TRANSITORY SCOPE: See <see cref="AllocateSafe"/> remarks. Fase 2 may delete.</remarks>
    public static void SeedLinearSafe(ref NativeStore store, int width, int height, float speed, int seed = 1337)
    {
        unsafe { fixed (NativeStore* p = &store) SeedLinear(p, width, height, speed, seed); }
    }

    /// <summary>Safe-facade for <see cref="Dispose"/>. Takes the store by reference.</summary>
    /// <remarks>TRANSITORY SCOPE: See <see cref="AllocateSafe"/> remarks. Fase 2 may delete.</remarks>
    public static void DisposeSafe(ref NativeStore store)
    {
        unsafe { fixed (NativeStore* p = &store) Dispose(p); }
    }
}
