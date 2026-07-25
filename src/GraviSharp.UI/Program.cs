using Raylib_cs;
using GraviSharp.Core;

namespace GraviSharp.UI;

internal static class Program
{
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;
    private const int TargetFps = 60;
    private const int StarCount = 10_000;

    [System.STAThread]
    private static void Main()
    {
        Raylib.InitWindow(WindowWidth, WindowHeight, "GraviSharp");
        Raylib.SetTargetFPS(TargetFps);

        // No fixed seed by default (random on each execution), with optional seed support
        float[] xs = Starfield.GeneratePositions(StarCount, WindowWidth);
        float[] ys = Starfield.GeneratePositions(StarCount, WindowHeight);

        while (!Raylib.WindowShouldClose())
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            // Provisional linear implementation — Phase 1 will replace with UpdateTexture/batch rendering
            for (int i = 0; i < StarCount; i++)
            {
                Raylib.DrawPixel((int)xs[i], (int)ys[i], Color.White);
            }

            Raylib.DrawFPS(10, 10);
            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }
}
