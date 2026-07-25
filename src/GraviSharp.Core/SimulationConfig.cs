using System.Runtime.InteropServices;

namespace GraviSharp.Core;

[StructLayout(LayoutKind.Sequential)]
public readonly struct SimulationConfig
{
    public readonly int BodyCount;
    public readonly int Width;
    public readonly int Height;
    public readonly int Alignment;

    public SimulationConfig(int bodyCount, int width, int height, int alignment = 64)
    {
        BodyCount = bodyCount;
        Width = width;
        Height = height;
        Alignment = alignment;
    }
}
