using System;

namespace GraviSharp.Core;

public static class Starfield
{
    public static float[] GeneratePositions(int count, int maxDim, int? seed = null)
    {
        var positions = new float[count];
        if (seed.HasValue)
        {
            var rng = new Random(seed.Value);
            for (int i = 0; i < count; i++)
                positions[i] = rng.NextSingle() * maxDim;
        }
        else
        {
            for (int i = 0; i < count; i++)
                positions[i] = Random.Shared.NextSingle() * maxDim;
        }
        return positions;
    }
}
