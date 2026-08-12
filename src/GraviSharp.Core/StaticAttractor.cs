using System.Runtime.InteropServices;

namespace GraviSharp.Core;

[StructLayout(LayoutKind.Sequential)]
public readonly struct StaticAttractor(float x, float y, float mass)
{
    public readonly float X = x;
    public readonly float Y = y;
    public readonly float Mass = mass;
}