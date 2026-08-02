using System.Runtime.CompilerServices;

namespace GraviSharp.Core;

public static unsafe class PhysicsDiagnostics
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeDiagnostics(
        NativeStore* store, out float kinetic, out float px, out float py, out float cx, out float cy)
    {
        if (store == null || store->Count <= 0)
        {
            kinetic = 0f;
            px = 0f;
            py = 0f;
            cx = 0f;
            cy = 0f;
            return;
        }

        int count = store->Count;
        kinetic = 0f;
        px = 0f;
        py = 0f;
        cx = 0f;
        cy = 0f;

        for (int i = 0; i < count; i++)
        {
            float vx = store->Vx[i];
            float vy = store->Vy[i];
            kinetic += 0.5f * (vx * vx + vy * vy);
            px += vx;
            py += vy;
            cx += store->X[i];
            cy += store->Y[i];
        }

        cx /= count;
        cy /= count;
    }
}
