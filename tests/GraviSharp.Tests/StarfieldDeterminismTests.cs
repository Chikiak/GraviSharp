using System;
using System.IO;
using System.Security.Cryptography;
using Xunit;
using GraviSharp.Core;

namespace GraviSharp.Tests;

public class StarfieldDeterminismTests
{
    private static (float[] xs, float[] ys) GenerateSeed1337()
    {
        var xs = Starfield.GeneratePositions(10000, 1280, seed: 1337);
        var ys = Starfield.GeneratePositions(10000, 720, seed: 1337);
        return (xs, ys);
    }

    private static string ComputeSha256(float[] array)
    {
        var byteSpan = System.Runtime.InteropServices.MemoryMarshal.AsBytes(array.AsSpan());
        var hashBytes = SHA256.HashData(byteSpan);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public void GeneratePositions_Returns_Expected_Length()
    {
        var (xs, ys) = GenerateSeed1337();
        Assert.Equal(10000, xs.Length);
        Assert.Equal(10000, ys.Length);
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public void GeneratePositions_Bounded_To_Window()
    {
        var (xs, ys) = GenerateSeed1337();

        foreach (var x in xs)
        {
            Assert.True(x >= 0f && x < 1280f, $"X coordinate {x} out of bounds [0, 1280)");
        }

        foreach (var y in ys)
        {
            Assert.True(y >= 0f && y < 720f, $"Y coordinate {y} out of bounds [0, 720)");
        }
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public void GeneratePositions_IsDeterministic_With_Seed_1337()
    {
        var (xs1, ys1) = GenerateSeed1337();
        var (xs2, ys2) = GenerateSeed1337();

        Assert.Equal(xs1, xs2);
        Assert.Equal(ys1, ys2);
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public void GeneratePositions_Hash_Matches_Cached_SHA256()
    {
        var (xs, ys) = GenerateSeed1337();
        var actualXsHash = ComputeSha256(xs);
        var actualYsHash = ComputeSha256(ys);

        var fixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        Directory.CreateDirectory(fixturesDir);

        var xsPath = Path.Combine(fixturesDir, "expected_xs_1337.sha256");
        var ysPath = Path.Combine(fixturesDir, "expected_ys_1337.sha256");

        if (!File.Exists(xsPath)) File.WriteAllText(xsPath, actualXsHash);
        if (!File.Exists(ysPath)) File.WriteAllText(ysPath, actualYsHash);

        var expectedXsHash = File.ReadAllText(xsPath).Trim();
        var expectedYsHash = File.ReadAllText(ysPath).Trim();

        Assert.Equal(expectedXsHash, actualXsHash);
        Assert.Equal(expectedYsHash, actualYsHash);
    }
}
