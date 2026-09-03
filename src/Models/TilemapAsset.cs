using System;

namespace GamebuinoAKA.IDE.Models
{
    public class TilemapAsset
    {
        public string Name { get; set; } = string.Empty;
        public string TilesetPath { get; set; } = string.Empty;

        public int TileWidth { get; set; } = 16;
        public int TileHeight { get; set; } = 16;

        /// <summary>Number of columns in the map grid.</summary>
        public int MapColumns { get; set; } = 20;

        /// <summary>Number of rows in the map grid.</summary>
        public int MapRows { get; set; } = 15;

        /// <summary>Background layer tile indices (0 = empty).</summary>
        public byte[] BackgroundLayer { get; set; } = Array.Empty<byte>();

        /// <summary>Foreground layer tile indices (0 = empty).</summary>
        public byte[] ForegroundLayer { get; set; } = Array.Empty<byte>();

        /// <summary>RGB565 pixel data of the full tileset image.</summary>
        public ushort[]? TilesetPixels { get; set; }

        public int TilesetColumns { get; set; }
        public int TilesetRows { get; set; }

        // Derived
        public int TotalTiles => MapColumns * MapRows;
        public int MapPixelWidth => MapColumns * TileWidth;
        public int MapPixelHeight => MapRows * TileHeight;

        public void InitializeLayers()
        {
            BackgroundLayer = new byte[TotalTiles];
            ForegroundLayer = new byte[TotalTiles];
        }
    }
}
