using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;
using GraviSharp.Core;

namespace GraviSharp.Tests.Phase2;

public class PhysicsStepTests
{
    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "PhysicsStep")]
    public void MarshalSize_Is16Bytes()
    {
        int size = Marshal.SizeOf<PhysicsStep>();
        Assert.Equal(16, size);
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "PhysicsStep")]
    public unsafe void UnsafeSizeOf_Is16Bytes()
    {
        int size = sizeof(PhysicsStep);
        Assert.Equal(16, size);
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "PhysicsStep")]
    public void Create_WithDefaults_ReturnsExpected()
    {
        var step = PhysicsStep.Create(1f / 60f);
        Assert.Equal(1f / 60f, step.Dt, 5);
        Assert.Equal(8f, step.Softening);
        Assert.Equal(1.0f, step.Damping);
        Assert.Equal(1500f, step.G);
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "PhysicsStep")]
    public void Create_WithExplicitValues_OverridesDefaults()
    {
        var step = PhysicsStep.Create(0.01f, softening: 12f, damping: 0.99f, g: 2000f);
        Assert.Equal(0.01f, step.Dt, 5);
        Assert.Equal(12f, step.Softening);
        Assert.Equal(0.99f, step.Damping);
        Assert.Equal(2000f, step.G);
    }

    [Fact]
    [Trait("Category", "Phase2")]
    [Trait("Category", "PhysicsStep")]
    public void PassByIn_TenInvocations_NoDefensiveCopy()
    {
        var step = PhysicsStep.Create(1f / 60f);

        long delta = MeasureDefensiveCopyDelta(in step);
        Assert.Equal(0L, delta);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureDefensiveCopyDelta(in PhysicsStep step)
    {
        // Warmup JIT
        ConsumeStep(in step);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long startBytes = GC.GetTotalAllocatedBytes();

        for (int i = 0; i < 10; i++)
        {
            ConsumeStep(in step);
        }

        long endBytes = GC.GetTotalAllocatedBytes();
        return endBytes - startBytes;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ConsumeStep(in PhysicsStep step)
    {
        // Reference step fields to ensure no optimization elides the parameter pass
        _ = step.Dt + step.Softening + step.Damping + step.G;
    }
}
