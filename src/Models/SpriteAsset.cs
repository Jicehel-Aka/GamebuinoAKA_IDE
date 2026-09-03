using System;

namespace GamebuinoAKA.IDE.Models
{
    public class SpriteAsset
    {
        public string Name { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public int FrameCount { get; set; } = 1;
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }

        /// <summary>Raw pixel data as RGB565 (uint16) — row-major.</summary>
        public ushort[]? PixelData { get; set; }

        /// <summary>16-colour palette (RGB565).</summary>
        public ushort[] Palette { get; set; } = new ushort[16];

        /// <summary>Palette-indexed pixel data (4-bit per pixel packed).</summary>
        public byte[]? IndexedData { get; set; }

        public bool IsIndexed => IndexedData is not null;

        // Derived
        public int TotalPixels => Width * Height;
        public TimeSpan FrameDuration { get; set; } = TimeSpan.FromMilliseconds(100);
    }
}
