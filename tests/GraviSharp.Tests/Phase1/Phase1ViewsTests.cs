using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;
using GraviSharp.Core;

namespace GraviSharp.Tests.Phase1;

public class Phase1ViewsTests
{
    [Fact]
    [Trait("Category", "Phase1")]
    [Trait("Category", "MemoryUnit")]
    public unsafe void ViewX_Aliases_Native_Memory_NoCopy()
    {
        var config = new SimulationConfig(16, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            store.X[7] = 123.45f;
            store.Y[7] = 456.78f;
            store.Vx[7] = 10f;
            store.Vy[7] = 20f;

            ReadOnlySpan<float> viewX = SimulationStore.ViewX(in store);
            ReadOnlySpan<float> viewY = SimulationStore.ViewY(in store);
            ReadOnlySpan<float> viewVx = SimulationStore.ViewVx(in store);
            ReadOnlySpan<float> viewVy = SimulationStore.ViewVy(in store);

            Assert.Equal(123.45f, viewX[7], 1e-6f);
            Assert.Equal(456.78f, viewY[7], 1e-6f);
            Assert.Equal(10f, viewVx[7], 1e-6f);
            Assert.Equal(20f, viewVy[7], 1e-6f);

            // Mutate native pointer directly and verify aliasing (zero-copy proof)
            store.X[7] = 999.5f;
            Assert.Equal(999.5f, viewX[7], 1e-6f);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase1")]
    [Trait("Category", "MemoryUnit")]
    public unsafe void ViewX_AfterDispose_Returns_EmptySpan()
    {
        var config = new SimulationConfig(10, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        SimulationStore.Dispose(&store);

        // Post-Dispose view should return Empty safely without AV
        ReadOnlySpan<float> viewX = SimulationStore.ViewX(in store);
        ReadOnlySpan<float> viewY = SimulationStore.ViewY(in store);

        Assert.True(viewX.IsEmpty);
        Assert.Equal(0, viewX.Length);
        Assert.True(viewY.IsEmpty);
        Assert.Equal(0, viewY.Length);
    }

    [Fact]
    [Trait("Category", "Phase1")]
    [Trait("Category", "MemoryUnit")]
    public unsafe void View_Length_Equals_StoreCount()
    {
        int count = 2048;
        var config = new SimulationConfig(count, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            Assert.Equal(count, SimulationStore.ViewX(in store).Length);
            Assert.Equal(count, SimulationStore.ViewY(in store).Length);
            Assert.Equal(count, SimulationStore.ViewVx(in store).Length);
            Assert.Equal(count, SimulationStore.ViewVy(in store).Length);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    [Fact]
    [Trait("Category", "Phase1")]
    [Trait("Category", "MemoryUnit")]
    public unsafe void ViewX_InvocationIs_ManagedHeapZeroAlloc()
    {
        var config = new SimulationConfig(1000, 1280, 720, 64);
        NativeStore store = SimulationStore.Allocate(in config);

        try
        {
            // Pin store on the dedicated thread's stack (avoids GC relocation of the
            // local during the measurement). NoInlining keeps the helper free of fusion with compiler-emitted overhead.
            long delta = MeasureViewXAllocationsOnDedicatedThread(in store, 1_000_000);

            // Hard zero — ViewX constructs a ReadOnlySpan over a raw pointer (on-stack value-type,
            // zero managed allocation). Dedicated thread isolation eliminates framework noise from
            // parallel xUnit tests sharing the process-wide GC.GetTotalAllocatedBytes() counter.
            Assert.Equal(0L, delta);
        }
        finally
        {
            SimulationStore.Dispose(&store);
        }
    }

    /// <summary>
    /// Runs the ViewX allocation probe on a dedicated foreground thread to isolate xUnit framework
    /// allocation noise. GC.GetTotalAllocatedBytes() is process-wide, so concurrent test execution
    /// pollutes the measurement unless we single-thread the probe.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe long MeasureViewXAllocationsOnDedicatedThread(in NativeStore store, int iterations)
    {
        long delta = -1;
        NativeStore storeCopy = store;
        var thread = new Thread(() =>
        {
            // Warmup JIT for ViewX
            for (int i = 0; i < 1000; i++)
            {
                _ = SimulationStore.ViewX(in storeCopy);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < iterations; i++)
            {
                ReadOnlySpan<float> v = SimulationStore.ViewX(in storeCopy);
                _ = v.Length;
            }
            delta = GC.GetAllocatedBytesForCurrentThread() - before;
        });

        thread.IsBackground = false;
        thread.Start();
        thread.Join();
        return delta;
    }
}