using Xunit;
using Xunit.Abstractions;

namespace GraviSharp.Tests;

public class SmokeVisualTests
{
    private readonly ITestOutputHelper _output;

    public SmokeVisualTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Skip = "Visual smoke test requiring human inspection and Raylib window context.")]
    [Trait("Category", "SmokeVisual")]
    public void Verify_Starfield_Visual_And_Fps_Stability()
    {
        _output.WriteLine("=== SMOKE VISUAL TEST PROCEDURE (TC-PH0-001) ===");
        _output.WriteLine("1. Run: dotnet run -c Release --project GraviSharp");
        _output.WriteLine("2. Observe 10,000 static white points on black background.");
        _output.WriteLine("3. Verify DrawFPS in top-left corner reports stable >30 FPS.");
        _output.WriteLine("4. Monitor dotnet-counters --counters System.Runtime to verify gen-0-heap-count is flat.");
        _output.WriteLine("5. Take screenshot and save to plans/acceptance/phase0_starfield.png.");
        Assert.True(true);
    }
}
