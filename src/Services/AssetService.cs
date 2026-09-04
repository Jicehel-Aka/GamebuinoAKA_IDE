using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GamebuinoAKA.IDE.Models;
using Newtonsoft.Json;

namespace GamebuinoAKA.IDE.Services
{
    public class AssetService
    {
        private readonly SettingsService _settings;

        public AssetService(SettingsService settings)
        {
            _settings = settings;
        }

        // Extensions des formats ré-éditables.
        public const string SpriteProjectExt = ".gbspr";
        public const string TilemapProjectExt = ".gbmap";

        // ── Conversion couleur (format-aware) ────────────────────────────────────

        /// <summary>Empaquette une couleur ARGB en 16 bits selon le format demandé.</summary>
        public static ushort Pack(Color c, ColorFormat fmt)
        {
            int r5 = c.R >> 3;
            int g6 = c.G >> 2;
            int b5 = c.B >> 3;
            return fmt == ColorFormat.Bgr565Aka
                ? (ushort)(r5 | (g6 << 5) | (b5 << 11))   // R bas, B haut (lib AKA)
                : (ushort)((r5 << 11) | (g6 << 5) | b5);  // R haut, B bas (standard)
        }

        /// <summary>Dépaquette un 16 bits en Color selon le format.</summary>
        public static Color Unpack(ushort v, ColorFormat fmt)
        {
            int r5, g6, b5;
            if (fmt == ColorFormat.Bgr565Aka)
            {
                r5 = v & 0x1F;
                g6 = (v >> 5) & 0x3F;
                b5 = (v >> 11) & 0x1F;
            }
            else
            {
                r5 = (v >> 11) & 0x1F;
                g6 = (v >> 5) & 0x3F;
                b5 = v & 0x1F;
            }
            return Color.FromArgb(255,
                (r5 << 3) | (r5 >> 2),
                (g6 << 2) | (g6 >> 4),
                (b5 << 3) | (b5 >> 2));
        }

        // Conserve les anciennes signatures pour compat (utilisées par les preview
        // WPF). Elles supposent l'ordre standard ; préférez Pack/Unpack.
        public static ushort ColorToRgb565(Color c) => Pack(c, ColorFormat.Rgb565Std);
        public static Color Rgb565ToColor(ushort v) => Unpack(v, ColorFormat.Rgb565Std);

        // ── Import sprite (LockBits, format-aware, alpha → clé transparente) ──────

        public Task<SpriteAsset> ImportSpriteAsync(string imagePath) =>
            Task.Run(() =>
            {
                using var bmp = new Bitmap(imagePath);
                var fmt = _settings.Settings.DefaultColorFormat;
                var key = _settings.Settings.DefaultTransparentKey;
                var asset = new SpriteAsset
                {
                    Name = Path.GetFileNameWithoutExtension(imagePath),
                    SourcePath = imagePath,
                    Width = bmp.Width,
                    Height = bmp.Height,
                    FrameWidth = bmp.Width,
                    FrameHeight = bmp.Height,
                    FrameCount = 1,
                    ColorFormat = fmt,
                    TransparentKey = key,
                    UseTransparency = true,
                    PixelData = BitmapToPacked(bmp, fmt, key)
                };
                return asset;
            });

        public Task<SpriteAsset> ImportSpritesheetAsync(string imagePath, int frameWidth, int frameHeight) =>
            Task.Run(() =>
            {
                using var bmp = new Bitmap(imagePath);
                var fmt = _settings.Settings.DefaultColorFormat;
                var key = _settings.Settings.DefaultTransparentKey;
                int cols = frameWidth > 0 ? bmp.Width / frameWidth : 1;
                int rows = frameHeight > 0 ? bmp.Height / frameHeight : 1;
                return new SpriteAsset
                {
                    Name = Path.GetFileNameWithoutExtension(imagePath),
                    SourcePath = imagePath,
                    Width = bmp.Width,
                    Height = bmp.Height,
                    FrameWidth = frameWidth,
                    FrameHeight = frameHeight,
                    FrameCount = Math.Max(1, cols * rows),
                    ColorFormat = fmt,
                    TransparentKey = key,
                    UseTransparency = true,
                    PixelData = BitmapToPacked(bmp, fmt, key)
                };
            });

        public Task<TilemapAsset> ImportTilesetAsync(string imagePath, int tileWidth, int tileHeight) =>
            Task.Run(() =>
            {
                using var bmp = new Bitmap(imagePath);
                var fmt = _settings.Settings.DefaultColorFormat;
                var key = _settings.Settings.DefaultTransparentKey;
                var asset = new TilemapAsset
                {
                    Name = Path.GetFileNameWithoutExtension(imagePath),
                    TilesetPath = imagePath,
                    TileWidth = tileWidth,
                    TileHeight = tileHeight,
                    TilesetColumns = tileWidth > 0 ? bmp.Width / tileWidth : 1,
                    TilesetRows = tileHeight > 0 ? bmp.Height / tileHeight : 1,
                    ColorFormat = fmt,
                    TransparentKey = key,
                    UseTransparency = true,
                    TilesetPixels = BitmapToPacked(bmp, fmt, key)
                };
                asset.InitializeLayers();
                return asset;
            });

        /// <summary>
        /// Conversion rapide Bitmap → uint16[] via LockBits (au lieu de GetPixel).
        /// Les pixels dont l'alpha &lt; 128 deviennent la couleur-clé transparente.
        /// </summary>
        private static ushort[] BitmapToPacked(Bitmap src, ColorFormat fmt, ushort transparentKey)
        {
            int w = src.Width, h = src.Height;
            var outData = new ushort[w * h];

            // Force 32bpp ARGB pour un accès mémoire homogène.
            using var bmp = (src.PixelFormat == PixelFormat.Format32bppArgb)
                ? src
                : src.Clone(new Rectangle(0, 0, w, h), PixelFormat.Format32bppArgb);

            var rect = new Rectangle(0, 0, w, h);
            BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                unsafe
                {
                    byte* basePtr = (byte*)data.Scan0;
                    int stride = data.Stride;
                    for (int y = 0; y < h; y++)
                    {
                        byte* row = basePtr + y * stride;
                        int o = y * w;
                        for (int x = 0; x < w; x++)
                        {
                            // BGRA en mémoire (little-endian ARGB)
                            byte b = row[x * 4 + 0];
                            byte g = row[x * 4 + 1];
                            byte r = row[x * 4 + 2];
                            byte a = row[x * 4 + 3];
                            outData[o + x] = a < 128
                                ? transparentKey
                                : Pack(Color.FromArgb(r, g, b), fmt);
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(data);
                if (!ReferenceEquals(bmp, src)) bmp.Dispose();
            }
            return outData;
        }

        // ── Export C++ (sprite) ──────────────────────────────────────────────────

        public string ExportSpriteToCpp(SpriteAsset a)
        {
            if (a.PixelData is null) return "// Pas de données pixel";
            string name = SanitizeName(a.Name);
            string fmtLabel = a.ColorFormat == ColorFormat.Bgr565Aka ? "BGR565 (ordre lib AKA)" : "RGB565 standard";

            // Pré-dimensionne le StringBuilder : ~8 octets par valeur.
            var sb = new StringBuilder(a.PixelData.Length * 8 + 512);
            sb.Append("// Sprite: ").Append(a.Name)
              .Append(" (").Append(a.Width).Append('x').Append(a.Height).Append(")\n");
            sb.Append("// Frames: ").Append(a.FrameCount)
              .Append("  Frame: ").Append(a.FrameWidth).Append('x').Append(a.FrameHeight).Append('\n');
            sb.Append("// Format couleur: ").Append(fmtLabel).Append('\n');
            if (a.UseTransparency)
            {
                sb.Append("// Transparence: couleur-clé 0x")
                  .Append(a.TransparentKey.ToString("X4"))
                  .Append(" (magenta) — passer use_transparency=true à graphics_draw_bitmap565()\n");
                sb.Append("#define ").Append(name).Append("_TRANSPARENT 0x")
                  .Append(a.TransparentKey.ToString("X4")).Append('\n');
            }

            AppendHexBlock(sb, $"const uint16_t {name}[] PROGMEM", a.PixelData, 16);

            sb.Append("const uint16_t ").Append(name).Append("_width  = ").Append(a.FrameWidth).Append(";\n");
            sb.Append("const uint16_t ").Append(name).Append("_height = ").Append(a.FrameHeight).Append(";\n");
            sb.Append("const uint8_t  ").Append(name).Append("_frames = ").Append(a.FrameCount).Append(";\n");
            return sb.ToString();
        }

        // ── Export C++ (tilemap) ─────────────────────────────────────────────────

        public string ExportTilemapToCpp(TilemapAsset a)
        {
            string name = SanitizeName(a.Name);
            var sb = new StringBuilder(a.TotalTiles * 8 + 512);
            sb.Append("// Tilemap: ").Append(a.Name)
              .Append(" (").Append(a.MapColumns).Append('x').Append(a.MapRows).Append(" tuiles)\n");
            sb.Append("// Taille tuile: ").Append(a.TileWidth).Append('x').Append(a.TileHeight).Append('\n');
            sb.Append("// Format couleur du tileset: ")
              .Append(a.ColorFormat == ColorFormat.Bgr565Aka ? "BGR565 (ordre lib AKA)" : "RGB565 standard")
              .Append('\n');
            if (a.UseTransparency)
                sb.Append("#define ").Append(name).Append("_TRANSPARENT 0x")
                  .Append(a.TransparentKey.ToString("X4")).Append('\n');
            sb.Append('\n');

            AppendByteRows(sb, $"const uint8_t {name}_bg[] PROGMEM", a.BackgroundLayer, a.MapColumns);
            AppendByteRows(sb, $"const uint8_t {name}_fg[] PROGMEM", a.ForegroundLayer, a.MapColumns);

            sb.Append("const uint8_t ").Append(name).Append("_cols = ").Append(a.MapColumns).Append(";\n");
            sb.Append("const uint8_t ").Append(name).Append("_rows = ").Append(a.MapRows).Append(";\n");
            return sb.ToString();
        }

        // ── Sauvegarde ré-éditable (JSON) ────────────────────────────────────────

        public void SaveSprite(SpriteAsset a, string path) =>
            File.WriteAllText(path, JsonConvert.SerializeObject(a, Formatting.Indented));

        public SpriteAsset LoadSprite(string path) =>
            JsonConvert.DeserializeObject<SpriteAsset>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Fichier sprite illisible.");

        public void SaveTilemap(TilemapAsset a, string path) =>
            File.WriteAllText(path, JsonConvert.SerializeObject(a, Formatting.Indented));

        public TilemapAsset LoadTilemap(string path) =>
            JsonConvert.DeserializeObject<TilemapAsset>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Fichier tilemap illisible.");

        // ── Outils image (workflow : rogner / réduire, conversion à la fin) ──────

        /// <summary>Charge une image en 32bppArgb (copie indépendante du fichier).</summary>
        public Bitmap LoadArgb(string path)
        {
            using var src = new Bitmap(path);
            var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height));
            return bmp;
        }

        /// <summary>Rogne une région (coordonnées image, clampée aux bornes).</summary>
        public Bitmap Crop(Bitmap src, Rectangle r)
        {
            r = Rectangle.Intersect(r, new Rectangle(0, 0, src.Width, src.Height));
            if (r.Width <= 0 || r.Height <= 0)
                throw new ArgumentException("Sélection vide.");
            var dst = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(dst);
            g.DrawImage(src, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel);
            return dst;
        }

        /// <summary>Redimensionne (bicubique haute qualité si smooth, sinon au plus proche).</summary>
        public Bitmap Resize(Bitmap src, int w, int h, bool smooth)
        {
            w = Math.Max(1, w); h = Math.Max(1, h);
            var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(dst);
            g.InterpolationMode = smooth ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.DrawImage(src, new Rectangle(0, 0, w, h), new Rectangle(0, 0, src.Width, src.Height), GraphicsUnit.Pixel);
            return dst;
        }

        /// <summary>Empaquète un bitmap en uint16 (BGR565/RGB565, alpha&lt;128 → clé).</summary>
        public static ushort[] PackBitmap(Bitmap bmp, ColorFormat fmt, ushort transparentKey)
            => BitmapToPacked(bmp, fmt, transparentKey);

        /// <summary>Construit un SpriteAsset packé à partir d'un bitmap de travail.</summary>
        public SpriteAsset BuildSprite(Bitmap bmp, string name, ColorFormat fmt,
            ushort key, bool useTransparency,
            int frameW = 0, int frameH = 0, int frameCount = 1)
        {
            if (frameW <= 0) frameW = bmp.Width;
            if (frameH <= 0) frameH = bmp.Height;
            return new SpriteAsset
            {
                Name = string.IsNullOrEmpty(name) ? "sprite" : name,
                Width = bmp.Width,
                Height = bmp.Height,
                FrameWidth = frameW,
                FrameHeight = frameH,
                FrameCount = Math.Max(1, frameCount),
                ColorFormat = fmt,
                TransparentKey = key,
                UseTransparency = useTransparency,
                PixelData = BitmapToPacked(bmp, fmt, key)
            };
        }

        /// <summary>Reconstruit un bitmap éditable depuis des pixels packés (pour réouverture).</summary>
        public Bitmap UnpackToBitmap(ushort[] data, int w, int h, ColorFormat fmt,
            bool useTransparency, ushort key)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, w, h);
            var bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                unsafe
                {
                    byte* basePtr = (byte*)bd.Scan0;
                    int stride = bd.Stride;
                    for (int y = 0; y < h; y++)
                    {
                        byte* row = basePtr + y * stride;
                        int o = y * w;
                        for (int x = 0; x < w; x++)
                        {
                            ushort v = (o + x) < data.Length ? data[o + x] : (ushort)0;
                            if (useTransparency && v == key)
                            {
                                row[x * 4 + 0] = 0; row[x * 4 + 1] = 0;
                                row[x * 4 + 2] = 0; row[x * 4 + 3] = 0;
                            }
                            else
                            {
                                var c = Unpack(v, fmt);
                                row[x * 4 + 0] = c.B; row[x * 4 + 1] = c.G;
                                row[x * 4 + 2] = c.R; row[x * 4 + 3] = 255;
                            }
                        }
                    }
                }
            }
            finally { bmp.UnlockBits(bd); }
            return bmp;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static readonly char[] HexDigits = "0123456789ABCDEF".ToCharArray();

        private static void AppendHexBlock(StringBuilder sb, string decl, ushort[] data, int perRow)
        {
            sb.Append(decl).Append(" = {\n");
            for (int i = 0; i < data.Length; i++)
            {
                if (i % perRow == 0) sb.Append("    ");
                ushort v = data[i];
                sb.Append("0x")
                  .Append(HexDigits[(v >> 12) & 0xF]).Append(HexDigits[(v >> 8) & 0xF])
                  .Append(HexDigits[(v >> 4) & 0xF]).Append(HexDigits[v & 0xF]);
                if (i < data.Length - 1) sb.Append(", ");
                if ((i + 1) % perRow == 0) sb.Append('\n');
            }
            if (data.Length % perRow != 0) sb.Append('\n');
            sb.Append("};\n\n");
        }

        private static void AppendByteRows(StringBuilder sb, string decl, byte[] data, int cols)
        {
            sb.Append(decl).Append(" = {\n");
            for (int i = 0; i < data.Length; i++)
            {
                if (i % cols == 0) sb.Append("    ");
                byte v = data[i];
                sb.Append("0x").Append(HexDigits[(v >> 4) & 0xF]).Append(HexDigits[v & 0xF]);
                if (i < data.Length - 1) sb.Append(", ");
                if ((i + 1) % cols == 0) sb.Append('\n');
            }
            if (cols == 0 || data.Length % cols != 0) sb.Append('\n');
            sb.Append("};\n\n");
        }

        private static string SanitizeName(string name) =>
            Regex.Replace(string.IsNullOrEmpty(name) ? "asset" : name, @"[^a-zA-Z0-9_]", "_");
    }
}
