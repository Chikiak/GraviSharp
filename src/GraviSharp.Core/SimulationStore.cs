using System;

namespace GraviSharp.Core;

public static class SimulationStore
{
    public static unsafe NativeStore Allocate(in SimulationConfig config)
        => throw new NotImplementedException();

    public static unsafe void Dispose(NativeStore* store)
        => throw new NotImplementedException();

    public static unsafe void SeedLinear(NativeStore* store, int width, int height, float speed, int seed = 1337)
        => throw new NotImplementedException();

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
