using System;
using System.Text.Unicode;
using GraviSharp.Core;
using Raylib_cs;

namespace GraviSharp.UI;

public static unsafe class PhysicsHud
{
    public static void Draw(NativeStore* store, int x, int y)
    {
        PhysicsDiagnostics.ComputeDiagnostics(store, out float k, out float px, out float py, out float cx, out float cy);

        Span<byte> buf = stackalloc byte[256];
        if (Utf8.TryWrite(buf, $"K: {k:F1} | Px: {px:F1} | Py: {py:F1} | Cx: {cx:F1} | Cy: {cy:F1}", out int n))
        {
            // Terminador nulo C-string: garantiza que Raylib.DrawText no lea basura residual
            // del frame anterior contenida en el stackalloc (256 bytes no se resetean entre frames).
            buf[n] = 0;
            fixed (byte* p = buf)
            {
                Raylib.DrawText((sbyte*)p, x, y, 20, Color.White);
            }
        }
        // else: HUD truncado por exceder 256 bytes — descartar silenciosamente este frame.
        // No escribir buf[n]=0 fuera de rango (n >= buf.Length). En la práctica los 5 campos
        // float:F1 + separadores caben en ~60 bytes, pero se mantiene la guarda por robustez
        // ante futuras extensiones del HUD (ver phase2.md:367 — "descartar o etiquetar").
    }
}
