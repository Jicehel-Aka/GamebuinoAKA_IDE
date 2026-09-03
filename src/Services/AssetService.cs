using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GamebuinoAKA.IDE.Models;

namespace GamebuinoAKA.IDE.Services
{
    public class AssetService
    {
        // ── Sprite ─────────────────────────────────────────────────────────────────

        public async Task<SpriteAsset> ImportSpriteAsync(string imagePath)
        {
            return await Task.Run(() =>
            {
                using var bmp = new Bitmap(imagePath);
                var asset = new SpriteAsset
                {
                    Name = Path.GetFileNameWithoutExtension(imagePath),
                    SourcePath = imagePath,
                    Width = bmp.Width,
                    Height = bmp.Height,
                    FrameWidth = bmp.Width,
                    FrameHeight = bmp.Height,
                    FrameCount = 1
                };
                asset.PixelData = BitmapToRgb565(bmp);
                return asset;
            });
        }

        public async Task<SpriteAsset> ImportSpritesheetAsync(string imagePath,
            int frameWidth, int frameHeight)
        {
            return await Task.Run(() =>
            {
                using var bmp = new Bitmap(imagePath);
                int cols = bmp.Width / frameWidth;
                int rows = bmp.Height / frameHeight;

                var asset = new SpriteAsset
                {
                    Name = Path.GetFileNameWithoutExtension(imagePath),
                    SourcePath = imagePath,
                    Width = bmp.Width,
                    Height = bmp.Height,
                    FrameWidth = frameWidth,
                    FrameHeight = frameHeight,
                    FrameCount = cols * rows
                };
                asset.PixelData = BitmapToRgb565(bmp);
                return asset;
            });
        }

        public string ExportSpriteToCpp(SpriteAsset asset)
        {
            if (asset.PixelData is null) return "// No pixel data";

            var sb = new StringBuilder();
            sb.AppendLine($"// Sprite: {asset.Name} ({asset.Width}x{asset.Height})");
            sb.AppendLine($"// Frames: {asset.FrameCount}  Frame size: {asset.FrameWidth}x{asset.FrameHeight}");
            sb.AppendLine($"const uint16_t {SanitizeName(asset.Name)}[] PROGMEM = {{");

            for (int i = 0; i < asset.PixelData.Length; i++)
            {
                if (i % 16 == 0) sb.Append("    ");
                sb.Append($"0x{asset.PixelData[i]:X4}");
                if (i < asset.PixelData.Length - 1) sb.Append(", ");
                if ((i + 1) % 16 == 0) sb.AppendLine();
            }

            if (asset.PixelData.Length % 16 != 0) sb.AppendLine();
            sb.AppendLine("};");
            sb.AppendLine();
            sb.AppendLine($"const uint8_t {SanitizeName(asset.Name)}_frames = {asset.FrameCount};");
            sb.AppendLine($"const uint8_t {SanitizeName(asset.Name)}_frame_width = {asset.FrameWidth};");
            sb.AppendLine($"const uint8_t {SanitizeName(asset.Name)}_frame_height = {asset.FrameHeight};");

            return sb.ToString();
        }

        // ── Tilemap ────────────────────────────────────────────────────────────────

        public async Task<TilemapAsset> ImportTilesetAsync(string imagePath,
            int tileWidth, int tileHeight)
        {
            return await Task.Run(() =>
            {
                using var bmp = new Bitmap(imagePath);
                var asset = new TilemapAsset
                {
                    Name = Path.GetFileNameWithoutExtension(imagePath),
                    TilesetPath = imagePath,
                    TileWidth = tileWidth,
                    TileHeight = tileHeight,
                    TilesetColumns = bmp.Width / tileWidth,
                    TilesetRows = bmp.Height / tileHeight,
                    TilesetPixels = BitmapToRgb565(bmp)
                };
                asset.InitializeLayers();
                return asset;
            });
        }

        public string ExportTilemapToCpp(TilemapAsset asset)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"// Tilemap: {asset.Name} ({asset.MapColumns}x{asset.MapRows} tiles)");
            sb.AppendLine($"// Tile size: {asset.TileWidth}x{asset.TileHeight}");
            sb.AppendLine();

            AppendLayerArray(sb, $"{SanitizeName(asset.Name)}_bg", asset.BackgroundLayer, asset.MapColumns);
            AppendLayerArray(sb, $"{SanitizeName(asset.Name)}_fg", asset.ForegroundLayer, asset.MapColumns);

            sb.AppendLine($"const uint8_t {SanitizeName(asset.Name)}_cols = {asset.MapColumns};");
            sb.AppendLine($"const uint8_t {SanitizeName(asset.Name)}_rows = {asset.MapRows};");

            return sb.ToString();
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private static ushort[] BitmapToRgb565(Bitmap bmp)
        {
            var data = new ushort[bmp.Width * bmp.Height];
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                    data[y * bmp.Width + x] = ColorToRgb565(bmp.GetPixel(x, y));
            return data;
        }

        public static ushort ColorToRgb565(Color c)
        {
            int r = (c.R >> 3) & 0x1F;
            int g = (c.G >> 2) & 0x3F;
            int b = (c.B >> 3) & 0x1F;
            return (ushort)((r << 11) | (g << 5) | b);
        }

        public static Color Rgb565ToColor(ushort rgb565)
        {
            int r = (rgb565 >> 11) & 0x1F;
            int g = (rgb565 >> 5) & 0x3F;
            int b = rgb565 & 0x1F;
            return Color.FromArgb(255,
                (r << 3) | (r >> 2),
                (g << 2) | (g >> 4),
                (b << 3) | (b >> 2));
        }

        private static void AppendLayerArray(StringBuilder sb, string name, byte[] data, int cols)
        {
            sb.AppendLine($"const uint8_t {name}[] PROGMEM = {{");
            for (int i = 0; i < data.Length; i++)
            {
                if (i % cols == 0) sb.Append("    ");
                sb.Append($"0x{data[i]:X2}");
                if (i < data.Length - 1) sb.Append(", ");
                if ((i + 1) % cols == 0) sb.AppendLine();
            }
            if (data.Length % cols != 0) sb.AppendLine();
            sb.AppendLine("};");
            sb.AppendLine();
        }

        private static string SanitizeName(string name) =>
            Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");
    }
}
