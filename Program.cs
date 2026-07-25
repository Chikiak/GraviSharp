using Raylib_cs;

namespace GraviSharp;

internal static class Program
{
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;
    private const int TargetFps = 60;

    [System.STAThread]
    private static void Main()
    {
        Raylib.InitWindow(WindowWidth, WindowHeight, "GraviSharp");
        Raylib.SetTargetFPS(TargetFps);

        while (!Raylib.WindowShouldClose())
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            Raylib.DrawFPS(10, 10);
            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }
}
