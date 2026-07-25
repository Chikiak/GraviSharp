namespace GraviSharp.Core;

public unsafe struct NativeStore
{
    public void* BaseAddress;
    public float* X;
    public float* Y;
    public float* Vx;
    public float* Vy;
    public int Count;
    public nuint ByteAlignment;
}
