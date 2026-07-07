using System.IO;
using UnityEditor;
using UnityEngine;

namespace TalkOut.EditorTools
{
    /// Generates 512px placeholder face textures for every emotion the judge
    /// can rule — smooth-filtered for the cleaner look. Replace the PNGs with
    /// real art any time; FaceSet assets keep pointing at the same files.
    public static class FaceTextureGenerator
    {
        public static readonly string[] Emotions =
        {
            "neutral", "suspicious", "angry", "amused", "confused",
            "panicked", "warm", "defeated", "blink"
        };

        private const int Size = 512;
        private const int S = 4; // layout designed on a 128 grid, drawn at 4x

        public static void GenerateFor(string characterName, Color skin)
        {
            string dir = $"Assets/Art/FaceTextures/{characterName}";
            Directory.CreateDirectory(dir);

            foreach (var emotion in Emotions)
            {
                var texture = Draw(emotion, skin);
                string path = $"{dir}/{emotion}.png";
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
        }

        // ---- drawing (coordinates on a 128 grid, top-origin) -------------------

        private static Color ink = new Color(0.10f, 0.08f, 0.08f);
        private static Color white = new Color(0.97f, 0.97f, 0.95f);

        private static Texture2D Draw(string emotion, Color skin)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color[Size * Size];
            // subtle vertical shading so faces aren't flat posters
            for (int y = 0; y < Size; y++)
            {
                float shade = 1f - 0.08f * (y / (float)Size);
                var row = new Color(skin.r * shade, skin.g * shade, skin.b * shade, 1f);
                for (int x = 0; x < Size; x++) pixels[y * Size + x] = row;
            }
            tex.SetPixels(pixels);

            switch (emotion)
            {
                case "neutral":
                    Eyes(tex, 9, 10, 4, 0);
                    Brow(tex, 34, 36, 54, 36); Brow(tex, 74, 36, 94, 36);
                    Line(tex, 50, 90, 78, 90, 4);
                    break;
                case "blink":
                    Line(tex, 36, 52, 52, 52, 3); Line(tex, 76, 52, 92, 52, 3);
                    Brow(tex, 34, 36, 54, 36); Brow(tex, 74, 36, 94, 36);
                    Line(tex, 50, 90, 78, 90, 4);
                    break;
                case "suspicious":
                    Eyes(tex, 9, 4, 3, 0);
                    Brow(tex, 34, 38, 54, 33); Brow(tex, 74, 33, 94, 38);
                    Line(tex, 56, 92, 76, 92, 4);
                    break;
                case "angry":
                    Eyes(tex, 8, 8, 4, 0);
                    Brow(tex, 32, 28, 54, 40); Brow(tex, 74, 40, 96, 28);
                    Line(tex, 48, 96, 64, 89, 4); Line(tex, 64, 89, 80, 96, 4);
                    break;
                case "amused":
                    Line(tex, 36, 54, 44, 47, 3); Line(tex, 44, 47, 52, 54, 3);
                    Line(tex, 76, 54, 84, 47, 3); Line(tex, 84, 47, 92, 54, 3);
                    Brow(tex, 34, 32, 54, 32); Brow(tex, 74, 32, 94, 32);
                    Ellipse(tex, 64, 94, 15, 9, ink);
                    Rect(tex, 49, 82, 79, 93, tex.GetPixel(20 * S, Size - 1 - 88 * S));
                    Rect(tex, 53, 93, 75, 96, white);
                    break;
                case "confused":
                    Ellipse(tex, 44, 52, 9, 10, white); Ellipse(tex, 44, 52, 4, 4, ink);
                    Ellipse(tex, 84, 53, 6, 7, white); Ellipse(tex, 84, 53, 3, 3, ink);
                    Brow(tex, 34, 27, 54, 33); Brow(tex, 74, 39, 94, 39);
                    Line(tex, 50, 92, 58, 87, 3); Line(tex, 58, 87, 66, 94, 3); Line(tex, 66, 94, 74, 89, 3);
                    break;
                case "panicked":
                    Eyes(tex, 12, 13, 3, 0);
                    Brow(tex, 32, 26, 52, 22); Brow(tex, 76, 22, 96, 26);
                    Ellipse(tex, 64, 96, 8, 12, ink);
                    break;
                case "warm":
                    Eyes(tex, 9, 9, 4, 0);
                    Brow(tex, 34, 33, 54, 33); Brow(tex, 74, 33, 94, 33);
                    Line(tex, 50, 92, 64, 97, 4); Line(tex, 64, 97, 78, 92, 4);
                    break;
                case "defeated":
                    Eyes(tex, 9, 4, 3, 2);
                    Brow(tex, 34, 34, 54, 39); Brow(tex, 74, 39, 94, 34);
                    Line(tex, 52, 90, 64, 95, 4); Line(tex, 64, 95, 76, 90, 4);
                    break;
            }

            tex.Apply();
            return tex;
        }

        private static void Eyes(Texture2D tex, int rx, int ry, int pupil, int pupilDrop)
        {
            Ellipse(tex, 44, 52, rx, ry, white);
            Ellipse(tex, 84, 52, rx, ry, white);
            Ellipse(tex, 44, 52 + pupilDrop, pupil, pupil, ink);
            Ellipse(tex, 84, 52 + pupilDrop, pupil, pupil, ink);
        }

        private static void Brow(Texture2D tex, int x0, int y0, int x1, int y1) =>
            Line(tex, x0, y0, x1, y1, 4);

        private static void Px(Texture2D tex, int x, int yTop, Color c)
        {
            if (x < 0 || x >= Size || yTop < 0 || yTop >= Size) return;
            tex.SetPixel(x, Size - 1 - yTop, c);
        }

        private static void Rect(Texture2D tex, int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0 * S; y <= y1 * S; y++)
                for (int x = x0 * S; x <= x1 * S; x++)
                    Px(tex, x, y, c);
        }

        private static void Ellipse(Texture2D tex, int cx, int cy, int rx, int ry, Color c)
        {
            int px = cx * S, py = cy * S, qx = rx * S, qy = ry * S;
            for (int y = -qy; y <= qy; y++)
                for (int x = -qx; x <= qx; x++)
                {
                    float nx = qx > 0 ? (float)x / qx : 0f;
                    float ny = qy > 0 ? (float)y / qy : 0f;
                    float d = nx * nx + ny * ny;
                    if (d <= 1f) Px(tex, px + x, py + y, c);
                }
        }

        private static void Line(Texture2D tex, int x0, int y0, int x1, int y1, int thickness)
        {
            int steps = (Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) + 1) * S;
            int r = Mathf.Max(1, thickness * S / 2);
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0 * S, x1 * S, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0 * S, y1 * S, t));
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if (dx * dx + dy * dy <= r * r) Px(tex, x + dx, y + dy, ink);
            }
        }
    }
}
