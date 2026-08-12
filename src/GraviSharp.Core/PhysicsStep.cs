using System.Runtime.InteropServices;

namespace GraviSharp.Core;

[StructLayout(LayoutKind.Sequential)]
public readonly struct PhysicsStep
{
    public readonly float Dt;
    public readonly float Softening;
    public readonly float Damping;
    public readonly float G;

    public PhysicsStep(float dt, float softening, float damping, float g)
    {
        Dt = dt;
        Softening = softening;
        Damping = damping;
        G = g;
    }

    public static PhysicsStep Create(
        float dt,
        float? softening = null,
        float? damping = null,
        float? g = null)
        => new PhysicsStep(
            dt,
            softening ?? PhysicsConstants.DefaultSoftening,
            damping ?? PhysicsConstants.DefaultDamping,
            g ?? PhysicsConstants.G);
}
