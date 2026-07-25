using System;

namespace GraviSharp;

internal static class Starfield
{
    public static float[] GeneratePositions(int count, int maxDim, int seed = 1337)
    {
        var rng = new Random(seed);
        var positions = new float[count];
        for (int i = 0; i < count; i++)
            positions[i] = rng.NextSingle() * maxDim;
        return positions;
    }
}
