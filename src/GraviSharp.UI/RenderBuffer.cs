using System;
using System.Runtime.InteropServices;
using Raylib_cs;
using GraviSharp.Core;

namespace GraviSharp.UI;

/// <summary>
/// Zero-allocation software-renderer bridge between a NativeStore and a Raylib GPU texture.
/// Phase 1 helper: Manages 64-byte aligned CPU pixel buffers and single-call VRAM texture streaming.
/// </summary>
public static unsafe class RenderBuffer
{
    private const nuint Alignment = 64;

    /// <summary>
    /// Allocates a 64-byte aligned unmanaged pixel buffer and initializes the GPU VRAM texture.
    /// </summary>
    public static void Allocate(int width, int height, out Texture2D texture, out byte* pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        nuint byteCount = (nuint)(width * height * 4);
        pixels = (byte*)NativeMemory.AlignedAlloc(byteCount, Alignment);
        if (pixels == null)
        {
            throw new OutOfMemoryException("AlignedAlloc failed for RenderBuffer pixel buffer.");
        }

        Image img = Raylib.GenImageColor(width, height, Color.Blank);
        texture = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        Raylib.SetTextureFilter(texture, TextureFilter.Point);
    }

    /// <summary>
    /// Clears the pixel buffer, rasterizes all active simulation bodies using zero-copy views,
    /// and flushes the buffer to GPU VRAM in a single native call.
    /// </summary>
    public static void DrawAll(in NativeStore store, ref Texture2D texture, byte* pixels, int width, int height)
    {
        if (pixels == null) return;

        // Clear CPU pixel buffer to zero (transparent black)
        NativeMemory.Clear(pixels, (nuint)(width * height * 4));

        uint* rgbaPtr = (uint*)pixels;
        ReadOnlySpan<float> xs = SimulationStore.ViewX(in store);
        ReadOnlySpan<float> ys = SimulationStore.ViewY(in store);

        int count = store.Count;
        for (int i = 0; i < count; i++)
        {
            int px = (int)xs[i];
            int py = (int)ys[i];

            // Defensive bounds check (unsigned comparison eliminates lower < 0 check)
            if ((uint)px < (uint)width && (uint)py < (uint)height)
            {
                // Write opaque white packed 32-bit RGBA (0xFFFFFFFFu)
                rgbaPtr[py * width + px] = 0xFFFFFFFFu;
            }
        }

        // Single call to update GPU VRAM texture from unmanaged memory pointer
        Raylib.UpdateTexture(texture, (void*)pixels);
    }

    /// <summary>
    /// Deterministically unloads GPU VRAM texture and frees native CPU pixel memory.
    /// </summary>
    public static void DisposeTexture(ref Texture2D texture, ref byte* pixels)
    {
        if (texture.Id != 0)
        {
            Raylib.UnloadTexture(texture);
            texture = default;
        }

        if (pixels != null)
        {
            NativeMemory.AlignedFree(pixels);
            pixels = null;
        }
    }
}
