using System;
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
        nuint totalBytes = alignedStride * 4;

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
            float x = xPtr[i] + vxPtr[i] * dt;
            float y = yPtr[i] + vyPtr[i] * dt;
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
