using System;
using Raylib_cs;
using GraviSharp.Core;

namespace GraviSharp.UI;

internal static unsafe class Program
{
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;
    private const int TargetFps = 60;
    private const float BhMass = 50000f;
    private const float BhX = WindowWidth * 0.5f;
    private const float BhY = WindowHeight * 0.5f;

    [System.STAThread]
    private static int Main(string[] args)
    {
        if (Array.IndexOf(args, "--verify-heap") >= 0)
        {
            HeapVerifier.Enable();
        }

        int bodyCount = 5000;
        int idx = Array.IndexOf(args, "--body-count");
        if (idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out int parsed))
        {
            bodyCount = parsed;
        }

        SimulationConfig cfg = new(bodyCount, WindowWidth, WindowHeight, 64);
        NativeStore store = SimulationStore.Allocate(in cfg);
        SimulationStore.SeedOrbitalDisk(&store, WindowWidth, WindowHeight,
                                         innerRadius: 50f, outerRadius: 300f,
                                         centralMass: 10000f, seed: 1337);
        ScenarioKind currentScenario = ScenarioKind.OrbitalDisk;

        Raylib.InitWindow(WindowWidth, WindowHeight, "GraviSharp");
        Raylib.SetTargetFPS(TargetFps);

        RenderBuffer.Allocate(WindowWidth, WindowHeight, out Texture2D texture, out byte* pixels);

        PhysicsStep step = PhysicsStep.Create(1f / 60f);
        bool paused = false;
        int frameIndex = 0;

        while (!Raylib.WindowShouldClose())
        {
            HeapVerifier.BeginFrame(frameIndex);

            // INPUT
            int key = Raylib.GetKeyPressed();
            if (key == (int)KeyboardKey.One)
                LoadScenario(&store, ScenarioKind.RandomCloud, in cfg);
            else if (key == (int)KeyboardKey.Two)
                LoadScenario(&store, ScenarioKind.OrbitalDisk, in cfg);
            else if (key == (int)KeyboardKey.Three)
                LoadScenario(&store, ScenarioKind.BlackHoleDisk, in cfg);

            if (Raylib.IsKeyPressed(KeyboardKey.Space))
                paused = !paused;

            // PHYSICS PIPELINE (skipped while paused — render still runs)
            if (!paused)
            {
                SimulationStore.ClearForces(&store);
                if (currentScenario == ScenarioKind.BlackHoleDisk)
                    SimulationStore.ComputeForcesBruteWithCentral(&store, BhX, BhY, BhMass, in step);
                else
                    SimulationStore.ComputeForcesBrute(&store, in step);
                SimulationStore.IntegrateSymplecticEuler(&store, in step);
            }

            // RENDER
            RenderBuffer.DrawAll(in store, ref texture, pixels, WindowWidth, WindowHeight);
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            Raylib.DrawTexture(texture, 0, 0, Color.White);
            Raylib.DrawFPS(10, 10);
            Raylib.EndDrawing();

            HeapVerifier.EndFrame(frameIndex);
            frameIndex++;
        }

        RenderBuffer.DisposeTexture(ref texture, ref pixels);
        SimulationStore.Dispose(&store);
        Raylib.CloseWindow();

        return HeapVerifier.Report();
    }

    private static unsafe void LoadScenario(NativeStore* store, ScenarioKind kind, in SimulationConfig cfg)
    {
        if (store == null || store->BaseAddress == null)
            throw new InvalidOperationException("LoadScenario: store is null or disposed.");

        if (store->Count != cfg.BodyCount)
        {
            SimulationStore.Dispose(store);
            *store = SimulationStore.Allocate(in cfg);
        }
        else
        {
            SimulationStore.ClearForces(store);
        }

        switch (kind)
        {
            case ScenarioKind.RandomCloud:
                SimulationStore.SeedRandomCloud(store, cfg.Width, cfg.Height,
                                                spread: 120f, thermalSpeed: 5f, seed: 1337);
                break;
            case ScenarioKind.OrbitalDisk:
                SimulationStore.SeedOrbitalDisk(store, cfg.Width, cfg.Height,
                                                innerRadius: 50f, outerRadius: 300f,
                                                centralMass: 10000f, seed: 1337);
                break;
            case ScenarioKind.BlackHoleDisk:
                SimulationStore.SeedOrbitalDisk(store, cfg.Width, cfg.Height,
                                                innerRadius: 50f, outerRadius: 300f,
                                                centralMass: 10000f, seed: 1337);
                break;
        }
    }
}
