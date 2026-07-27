using System;
using Xunit;
using GraviSharp.Core;

namespace GraviSharp.NonUnsafeViewConsumerTests;

public class NonUnsafeViewConsumerTest
{
    [Fact]
    public void ViewX_CompilesAndRuns_InNonUnsafeProject()
    {
        var cfg = new SimulationConfig(8, 1280, 720, 64);
        NativeStore store = SimulationStore.AllocateSafe(in cfg);
        try
        {
            SimulationStore.SeedLinearSafe(ref store, 1280, 720, 50f, 1337);
            ReadOnlySpan<float> view = SimulationStore.ViewX(in store);
            Assert.Equal(8, view.Length);
        }
        finally
        {
            SimulationStore.DisposeSafe(ref store);
        }
    }
}
