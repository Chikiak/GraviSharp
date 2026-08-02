using System;
using Raylib_cs;
using GraviSharp.Core;

namespace GraviSharp.UI;

internal static unsafe class Program
{
    private const int WindowWidth = 1920;
    private const int WindowHeight = 980;
    private const int TargetFps = 120;
    private const float BhMass = 50000f;
    private const float BhX = WindowWidth * 0.5f;
    private const float BhY = WindowHeight * 0.5f;
    // G elevada para el escenario UniformField: como los cuerpos están repartidos por TODA la
    // pantalla (densidad muy baja, distancia vecino-vecino ~19px con N=5000), la fuerza
    // newtoniana entre cuerpos con G=1500 es ~100.000× más débil que en el disco orbital
    // (donde están concentrados a radios 5-300px). Con G=1500 la gravedad es imperceptible
    // y los cuerpos escapan en línea recta antes de clusterizar. G=15000 (10× del default)
    // compensa la baja densidad y hace la agregación gravitatoria visible en segundos.
    // No afecta al resto de escenarios: estos usan `step` (con PhysicsConstants.G = 1500).
    private const float UniformFieldG = 4000f;

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
        // Step con G elevada solo para UniformField (ver comentario en UniformFieldG arriba).
        // Dt, Softening y Damping idénticos al step estándar — sólo difiere G.
        PhysicsStep uniformStep = PhysicsStep.Create(1f / 60f, g: UniformFieldG);
        bool paused = false;
        int frameIndex = 0;

        while (!Raylib.WindowShouldClose())
        {
            HeapVerifier.BeginFrame(frameIndex);

            // INPUT
            int key = Raylib.GetKeyPressed();
            if (key == (int)KeyboardKey.One) {
                LoadScenario(&store, ScenarioKind.RandomCloud, in cfg);
                currentScenario = ScenarioKind.RandomCloud;
            }
            else if (key == (int)KeyboardKey.Two) {
                LoadScenario(&store, ScenarioKind.OrbitalDisk, in cfg);
                currentScenario = ScenarioKind.OrbitalDisk;
            }
            else if (key == (int)KeyboardKey.Three) {
                LoadScenario(&store, ScenarioKind.BlackHoleDisk, in cfg);
                currentScenario = ScenarioKind.BlackHoleDisk;
            }
            else if (key == (int)KeyboardKey.Four) {
                LoadScenario(&store, ScenarioKind.UniformField, in cfg);
                currentScenario = ScenarioKind.UniformField;
            }

            if (Raylib.IsKeyPressed(KeyboardKey.Space))
                paused = !paused;

            // PHYSICS PIPELINE (skipped while paused — render still runs)
            if (!paused)
            {
                PhysicsStep activeStep = currentScenario == ScenarioKind.UniformField ? uniformStep : step;
                SimulationStore.ClearForces(&store);
                if (currentScenario == ScenarioKind.BlackHoleDisk)
                    SimulationStore.ComputeForcesBruteWithCentral(&store, BhX, BhY, BhMass, in activeStep);
                else
                    SimulationStore.ComputeForcesBrute(&store, in activeStep);
                SimulationStore.IntegrateSymplecticEuler(&store, in activeStep);
				if (currentScenario == ScenarioKind.OrbitalDisk) {
                    SimulationStore.ReflectBounds(&store, WindowWidth, WindowHeight);
                }
            }

            // RENDER
            RenderBuffer.DrawAll(in store, ref texture, pixels, WindowWidth, WindowHeight);
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            Raylib.DrawTexture(texture, 0, 0, Color.White);
            Raylib.DrawFPS(10, 10);
            PhysicsHud.Draw(&store, 10, 40);
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

        // Seed aleatoria por pulsación: aporta variedad visual entre recargas.
        // No está en el hot-path del frame (solo al pulsar tecla), por lo que el
        // `new Random()` no compromete el contrato cero-GC del bucle de render.
        var rd = new Random();
        var rseed = (int) (rd.NextDouble() * 31333333);


        switch (kind)
        {
            case ScenarioKind.RandomCloud:
			{
        // ============================================================================
        // RESTRICCIONES INVARIABLES DE SIEMBRA
        // ----------------------------------------------------------------------------
        // SeedRandomCloud:
        //   - spread     >  0  (evita nube degenerada en un único píxel central).
        //   - thermalSpeed >= 0 (negativo no tiene sentido físico térmico).
        // SeedOrbitalDisk (OrbitalDisk y BlackHoleDisk):
        //   - innerRadius >= 1f   REQUIERE phase2.md PH2-ISSUE-005:205 (evita /0 en sqrt(G*M/r)).
        //   - outerRadius >  innerRadius  INVARIANTE GEOMÉTRICA:
        //        `r = inner + rng.NextDouble() * (outer - inner)` si `outer < inner`
        //        produce radios NEGATIVOS → cuerpos siembran al otro lado del centro,
        //        corona invertida, geometría corrupta.
        //   - centralMass >  0  (atracción gravitatoria nula o repulsiva si <= 0).
        // Por ello se aplica un clamp `outer = Max(inner*2f, randomOuter)` que garantiza
        // la invariante sin sacrificar aleatoriedad (si el azar pinta outer < inner,
        // se eleva outer al doble de inner — disco más ancho, no degenerado).
        // ============================================================================

                // spread ∈ [30, 270), thermalSpeed ∈ [1, 11)
                SimulationStore.SeedRandomCloud(store, cfg.Width, cfg.Height,
                                                spread: (rseed % 380f) + 30f, thermalSpeed: (rseed % 20f) + 1f, seed: rseed);
                break;
			}

            case ScenarioKind.OrbitalDisk:
            {
                // Disco N-body puro (sin atractor central en el kernel — ver pipeline en Main).
                // innerRadius ∈ [1, 151),  outerRadius ∈ [50, 450),  centralMass ∈ [10000, 20000)
                // INCOHERENCIA MASA CENTRAL (Bug 3, phase2.md PH2-ISSUE-010):
                // Este escenario usa ComputeForcesBrute (sin atractor) en Main, pero la velocidad
                // tangencial de siembra se calibra con `centralMass` (fórmula Kepleriana v=√(G·M/r))
                // asumiendo un cuerpo central que NO se aplica en el kernel. Por tanto el disco
                // arranca con velocidad orbital calibrada para una masa central inexistente.
                // El comportamiento neto es un disco N-body puro con over/under-rotation inicial
                // según el `centralMass` elegido.
                // Esto se resuelve formalmente en PH2-ISSUE-010 con `StaticAttractor` + el
                // refactor de `ComputeForcesBruteWithAttractors`. Documentado en
                // PHASE2_ACCEPTANCE.md::Sección A1.2.
                float inner = (rseed % 30f) + 1f;
                float outer = MathF.Max(inner * 2f, (rseed % 100f) + 5f);
                SimulationStore.SeedOrbitalDisk(store, cfg.Width, cfg.Height,
                                                innerRadius: inner, outerRadius: outer,
                                                centralMass: (rseed % 3000f) + 300f, seed: rseed);
                break;
            }

            case ScenarioKind.BlackHoleDisk:
            {
                // Disco + atractor central estático (ComputeForcesBruteWithCentral en Main).
                // innerRadius ∈ [1, 16),  outerRadius ∈ [Clamp(inner*2, [5,45)),  centralMass ≈ BhMass
                //
                // FIX (opción 1): centralMass ahora centrado en BhMass = 50000f para que la
                // velocidad Kepleriana de siembra (v = √(G·M/r)) coincida con el atractor
                // que aplica el kernel. Resultado: órbitas estables visuales, sin slingshots.
                // Rango ±10% alrededor de 50000 para mantener variedad: [45000, 55000).
                float inner = (rseed % 15f) + 1f;
                float outer = MathF.Max(inner * 2f, (rseed % 500f) + 20f);
                float centralMass = 25000f + (rseed % 10000f);
                SimulationStore.SeedOrbitalDisk(store, cfg.Width, cfg.Height,
                                                innerRadius: inner, outerRadius: outer,
                                                centralMass: centralMass, seed: rseed);
                break;
            }

            case ScenarioKind.UniformField:
            {
                // Campo uniforme: cuerpos repartidos por TODA la pantalla, dirección y módulo
                // de velocidad aleatorios (isotrópico). N-body puro → ComputeForcesBrute.
                //
                // RESTRICCIÓN: maxSpeed > 0  (speed = 0 → cuerpos estáticos, sin variedad direccional,
                //   la física solo los atrae lentamente hacia el primer cluster que se forme).
                // Rango visual: maxSpeed ∈ [40, 160) px/s — ni tan lento que tarda en agregarse,
                //   ni tan rápido que sale de pantalla antes de que la gravedad actúe.
                // El módulo por cuerpo es uniforme en [0, maxSpeed) (con sqrt para densidad
                // uniforme sobre el disco de velocidades, ver SimulationStore.SeedUniformField).
                float maxSpeed = (rseed % 120f) + 40f;
                SimulationStore.SeedUniformField(store, cfg.Width, cfg.Height,
                                                 maxSpeed: maxSpeed, seed: rseed);
                break;
            }
        }
    }
}
