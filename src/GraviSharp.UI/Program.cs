using Raylib_cs;
using GraviSharp.Core;

namespace GraviSharp.UI;

internal static unsafe class Program
{
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;
    private const int TargetFps = 60;

    [System.STAThread]
    private static void Main()
    {
        SimulationConfig cfg = new(10000, WindowWidth, WindowHeight, 64);
        NativeStore store = SimulationStore.Allocate(in cfg);
        SimulationStore.SeedLinear(&store, WindowWidth, WindowHeight, speed: 50f, seed: 1337);

        Raylib.InitWindow(WindowWidth, WindowHeight, "GraviSharp");
        Raylib.SetTargetFPS(TargetFps);

        RenderBuffer.Allocate(WindowWidth, WindowHeight, out Texture2D texture, out byte* pixels);

        while (!Raylib.WindowShouldClose())
        {
            float dt = Raylib.GetFrameTime();
            SimulationStore.UpdateLinear(&store, WindowWidth, WindowHeight, dt);
            RenderBuffer.DrawAll(in store, ref texture, pixels, WindowWidth, WindowHeight);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            Raylib.DrawTexture(texture, 0, 0, Color.White);
            Raylib.DrawFPS(10, 10);
            Raylib.EndDrawing();
        }

        RenderBuffer.DisposeTexture(ref texture, ref pixels);
        SimulationStore.Dispose(&store);
        Raylib.CloseWindow();
    }
}
