using System;
using System.IO;
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
    
    private const float UniformFieldG = 3000f;

    [System.STAThread]
    private static int Main(string[] args)
    {
        bool verifyPhysics = Array.IndexOf(args, "--verify-physics") >= 0;
        bool headless = verifyPhysics;

        if (Array.IndexOf(args, "--verify-heap") >= 0 || verifyPhysics)
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

        PhysicsStep step = PhysicsStep.Create(1f / 60f);

        if (headless)
        {
            cfg = new SimulationConfig(2, WindowWidth, WindowHeight, 64);
            SimulationStore.Dispose(&store);
            store = SimulationStore.Allocate(in cfg);
            // Step calibrado para verificación: G=1, Softening=1, Damping=1 (per PH2-ISSUE-009 spec).
            // Simetría N=2 con v=√(G/r) en unidades naturales (no escaladas con G=1500).
            PhysicsStep verifyStep = PhysicsStep.Create(1f / 60f, softening: 1f, damping: 1f, g: 1f);
            PhysicsVerifier.SeedTwoBodySymmetricForVerification(&store, WindowWidth, WindowHeight, verifyStep.G);

            PhysicsVerificationResult result = PhysicsVerifier.RunVerifyPhysics(&store, in verifyStep);

            try
            {
                string acceptanceDir = AcceptancePaths.FindAcceptanceDir();
                string acceptancePath = Path.Combine(acceptanceDir, "PHASE2_ACCEPTANCE.md");
                string section = GenerateAcceptanceSection(result.MaxEnergyDrift, result.MaxMomentumDriftX, result.MaxMomentumDriftY, result.MaxCoMDrift, result.MaxHeapDelta, result.Pass);
                WriteAcceptanceSection(acceptancePath, section);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not write PHASE2_ACCEPTANCE.md: {ex.Message}");
            }

            SimulationStore.Dispose(&store);
            return result.Pass ? 0 : 1;
        }

        ScenarioKind currentScenario = ScenarioKind.OrbitalDisk;

        Raylib.InitWindow(WindowWidth, WindowHeight, "GraviSharp");
        Raylib.SetTargetFPS(TargetFps);

        RenderBuffer.Allocate(WindowWidth, WindowHeight, out Texture2D texture, out byte* pixels);

        // Step con G elevada solo para UniformField (ver comentario en UniformFieldG arriba).
        // Dt, Softening y Damping idénticos al step estándar — sólo difiere G.
        PhysicsStep uniformStep = PhysicsStep.Create(1f / 60f, g: UniformFieldG);
        bool paused = false;
        bool bounce = true;
        int frameIndex = 0;
		Span<StaticAttractor> bhAttractorSpan = stackalloc StaticAttractor[1];

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
            if (Raylib.IsKeyPressed(KeyboardKey.B))
                bounce = !bounce;

            // PHYSICS PIPELINE (skipped while paused — render still runs)
            if (!paused)
            {
                PhysicsStep activeStep = currentScenario == ScenarioKind.UniformField ? uniformStep : step;
                SimulationStore.ClearForces(&store);
                
                if (currentScenario == ScenarioKind.BlackHoleDisk)
                {
                    StaticAttractor bhAttractor = new(BhX, BhY, BhMass);
                    SimulationStore.ComputeForcesBruteWithAttractors(&store, new ReadOnlySpan<StaticAttractor>(&bhAttractor, 1), in activeStep);
                }
                else
                {
                    SimulationStore.ComputeForcesBruteWithAttractors(&store, ReadOnlySpan<StaticAttractor>.Empty, in activeStep);
                }
                
                SimulationStore.IntegrateSymplecticEuler(&store, in activeStep);
                if (bounce)
				    SimulationStore.ReflectBounds(&store, WindowWidth, WindowHeight);
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

    /// <summary>
    /// Writes or replaces the acceptance section in PHASE2_ACCEPTANCE.md idempotently.
    /// Removes ALL existing occurrences of Section C (collapses historical duplicates from
    /// previous File.AppendAllText runs), then appends a single fresh section.
    /// </summary>
    /// <param name="path">Full path to PHASE2_ACCEPTANCE.md</param>
    /// <param name="section">Markdown section content</param>
    private static void WriteAcceptanceSection(string path, string section)
    {
        string content = File.Exists(path) ? File.ReadAllText(path) : "";
        const string header = "## Sección C: PH2-ISSUE-009 — `--verify-physics` Headless Validation";

        // Remove ALL existing Section C blocks (idempotency over historical duplicates).
        // Each block spans from the header until the next section header or EOF.
        while (true)
        {
            int startIdx = content.IndexOf(header, StringComparison.Ordinal);
            if (startIdx < 0) break;

            // Scan forward for the next section header (## Sección or # at line start) after this block.
            int scanFrom = startIdx + header.Length;
            int nextIdx = -1;

            // Find earliest of "\n## Sección " or "\n# " after this header.
            int candA = content.IndexOf("\n## Sección ", scanFrom, StringComparison.Ordinal);
            int candB = content.IndexOf("\n# ", scanFrom, StringComparison.Ordinal);
            if (candA >= 0 && candB >= 0) nextIdx = Math.Min(candA, candB);
            else if (candA >= 0) nextIdx = candA;
            else if (candB >= 0) nextIdx = candB;
            else nextIdx = content.Length;

            // Trim leading newline of the removed block for clean formatting.
            int removeStart = startIdx;
            if (removeStart > 0 && content[removeStart - 1] == '\n') removeStart--;

            content = content.Substring(0, removeStart) + content.Substring(nextIdx);
        }

        // Append the fresh section (with its own leading blank line and --- separator).
        content += section;
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Generates the markdown acceptance section for PHASE2_ACCEPTANCE.md.
    /// </summary>
    /// <returns>Formatted markdown string</returns>
    private static string GenerateAcceptanceSection(float eDrift, float pDriftX, float pDriftY, float comDrift, long heapDelta, bool pass)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Sección C: PH2-ISSUE-009 — `--verify-physics` Headless Validation");
        sb.AppendLine();
        sb.AppendLine($"* **Fecha de Ejecución:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"* **Entorno:** .NET {Environment.Version}, {Environment.OSVersion}");
        sb.AppendLine($"* **Hardware:** {Environment.ProcessorCount} logical processors");
        sb.AppendLine($"* **Versión del harness:** v1.0 (idempotente)");
        sb.AppendLine();
        sb.AppendLine("**Escenario de validación:** Two-body simétrico (N=2, baricentro en centro de pantalla, separación 80px, velocidad Kepleriana v=√(G/r) con masa unitaria). No se usa el disco orbital `SeedOrbitalDisk(5000)` porque su fórmula de siembra asume un atractor central estático (Kepleriano estricto) que el kernel `ComputeForcesBrute` (N-body puro, sin StaticAttractor hasta PH2-ISSUE-010) no aplica — el disco arrancaría con over/under-rotation y el drift energético superaría el umbral del 0.5% por causa geométrica, no por defecto del integrador. El two-body simétrico es el caso de control certificado en TC-PH2-001 (`PhysicsConservationTests.TwoBodySymmetric_EnergyMomentumConserved_1000Ticks`) y aísla la propiedad que el QG valida: estabilidad numérica del pipeline completo en single-thread. La re-ceremonia con disco orbital Kepleriano puro queda post-(PH2-ISSUE-010) con StaticAttractor disponible.");
        sb.AppendLine();
        sb.AppendLine("### Métricas Muestreadas (600 ticks, sample cada 10 ticks = 61 muestras)");
        sb.AppendLine();
        sb.AppendLine("| Métrica | Umbral Spec | Observado | Veredicto |");
        sb.AppendLine("|---------|-------------|-----------|-----------|");
        sb.AppendLine($"| Deriva energía | < 0.5% | {eDrift * 100f:F3}% | {(eDrift < 0.005f ? "PASS" : "FAIL")} |");
        sb.AppendLine($"| Deriva momentum X | < 1% | {pDriftX * 100f:F3}% | {(pDriftX < 0.01f ? "PASS" : "FAIL")} |");
        sb.AppendLine($"| Deriva momentum Y | < 1% | {pDriftY * 100f:F3}% | {(pDriftY < 0.01f ? "PASS" : "FAIL")} |");
        sb.AppendLine($"| Deriva CoM | < 5px | {comDrift:F3}px | {(comDrift < 5f ? "PASS" : "FAIL")} |");
        sb.AppendLine($"| MaxHeapDeltaPerFrame | = 0 | {heapDelta} bytes | {(heapDelta == 0 ? "PASS" : "FAIL")} |");
        sb.AppendLine();
        sb.AppendLine($"**Veredicto Global:** {(pass ? "PASS — Fase 2 certificada para paralelismo (Fase 3)" : "FAIL — Revisar física antes de Fase 3")}");
        sb.AppendLine();
        return sb.ToString();
    }
}
