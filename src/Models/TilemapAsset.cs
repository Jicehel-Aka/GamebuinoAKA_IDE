using System;
using Newtonsoft.Json;

namespace GamebuinoAKA.IDE.Models
{
    public class TilemapAsset
    {
        public string Name { get; set; } = string.Empty;
        public string TilesetPath { get; set; } = string.Empty;

        public int TileWidth { get; set; } = 16;
        public int TileHeight { get; set; } = 16;

        public int MapColumns { get; set; } = 20;
        public int MapRows { get; set; } = 15;

        /// <summary>Couche fond — indices de tuile (0 = vide).</summary>
        public byte[] BackgroundLayer { get; set; } = Array.Empty<byte>();

        /// <summary>Couche premier plan — indices de tuile (0 = vide).</summary>
        public byte[] ForegroundLayer { get; set; } = Array.Empty<byte>();

        /// <summary>Pixels 16 bits du tileset complet (format ColorFormat).</summary>
        public ushort[]? TilesetPixels { get; set; }

        public int TilesetColumns { get; set; }
        public int TilesetRows { get; set; }

        /// <summary>Format d'empaquetage des couleurs (défaut = BGR565 AKA).</summary>
        public ColorFormat ColorFormat { get; set; } = ColorFormat.Bgr565Aka;

        public bool UseTransparency { get; set; } = true;
        public ushort TransparentKey { get; set; } = 0xF81F;

        // ── Dérivés (non sérialisés) ────────────────────────────────────────────
        [JsonIgnore] public int TotalTiles => MapColumns * MapRows;
        [JsonIgnore] public int MapPixelWidth => MapColumns * TileWidth;
        [JsonIgnore] public int MapPixelHeight => MapRows * TileHeight;

        public void InitializeLayers()
        {
            BackgroundLayer = new byte[TotalTiles];
            ForegroundLayer = new byte[TotalTiles];
        }
    }
}
