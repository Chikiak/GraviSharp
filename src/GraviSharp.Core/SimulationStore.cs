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
        => throw new NotImplementedException();

    public static unsafe ReadOnlySpan<float> ViewX(in NativeStore store)
        => throw new NotImplementedException();

    public static unsafe ReadOnlySpan<float> ViewY(in NativeStore store)
        => throw new NotImplementedException();

    public static unsafe ReadOnlySpan<float> ViewVx(in NativeStore store)
        => throw new NotImplementedException();

    public static unsafe ReadOnlySpan<float> ViewVy(in NativeStore store)
        => throw new NotImplementedException();
}
