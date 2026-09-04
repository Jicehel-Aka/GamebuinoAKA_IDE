using System;
using Newtonsoft.Json;

namespace GamebuinoAKA.IDE.Models
{
    /// <summary>
    /// Sprite AKA : données pixel « truecolor » 16 bits, une valeur par pixel.
    ///
    /// IMPORTANT — format réel de la lib : graphics_draw_bitmap565() consomme un
    /// tableau plat de uint16 (const uint16_t data[]) ; il n'y a PAS de sprite
    /// indexé/palette dans la coquille. L'ancien champ « palette 16 couleurs /
    /// 4 bpp » était mort et a été retiré : on exporte du 16 bpp qui correspond
    /// à ce que la console dessine. La palette ci-dessous n'est qu'une aide UI
    /// (nuancier de l'éditeur), pas un format d'export.
    /// </summary>
    public class SpriteAsset
    {
        public string Name { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public int FrameCount { get; set; } = 1;
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }

        /// <summary>
        /// Format d'empaquetage des couleurs (défaut = ordre lib AKA, BGR565).
        /// </summary>
        public ColorFormat ColorFormat { get; set; } = ColorFormat.Bgr565Aka;

        /// <summary>Active la couleur-clé de transparence à l'export/preview.</summary>
        public bool UseTransparency { get; set; } = true;

        /// <summary>
        /// Couleur-clé de transparence (valeur 16 bits dans le format ColorFormat).
        /// Défaut : magenta 0xF81F, comme TRANSPARENT_KEY de core/graphics.h.
        /// (0xF81F = magenta dans les DEUX ordres, R et B étant tous deux au max.)
        /// </summary>
        public ushort TransparentKey { get; set; } = 0xF81F;

        /// <summary>Données pixel 16 bits — row-major, dans le format ColorFormat.</summary>
        public ushort[]? PixelData { get; set; }

        // ── Dérivés (non sérialisés) ────────────────────────────────────────────
        [JsonIgnore] public int TotalPixels => Width * Height;
        [JsonIgnore] public int Columns => FrameWidth > 0 ? Width / FrameWidth : 1;
        [JsonIgnore] public int Rows => FrameHeight > 0 ? Height / FrameHeight : 1;

        public double FrameDurationMs { get; set; } = 100;
    }
}
