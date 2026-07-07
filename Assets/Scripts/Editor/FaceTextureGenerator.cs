using System.IO;
using UnityEditor;
using UnityEngine;

namespace TalkOut.EditorTools
{
    /// Generates placeholder 128x128 pixel-art face textures for every emotion.
    /// Point-filtered for the blocky look. Replace the PNGs with real art any time —
    /// the FaceSet assets keep pointing at the same files.
    public static class FaceTextureGenerator
    {
        public static readonly string[] Emotions =
        {
            "neutral", "suspicious", "angry", "amused", "confused",
            "panicked", "warm", "defeated", "blink"
        };

        private const int Size = 128;

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
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        // ---- drawing ----------------------------------------------------------

        private static Color ink = new Color(0.10f, 0.08f, 0.08f);
        private static Color white = new Color(0.96f, 0.96f, 0.94f);

        private static Texture2D Draw(string emotion, Color skin)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color[Size * Size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = skin;
            tex.SetPixels(pixels);

            // layout (top-origin): eyes at y=52 (x=44 / x=84), brows y~34, mouth y~92
            switch (emotion)
            {
                case "neutral":
                    Eyes(tex, 9, 10, 4, 0);
                    Brow(tex, 34, 36, 54, 36); Brow(tex, 74, 36, 94, 36);
                    ThickLine(tex, 50, 90, 78, 90, 4, ink);
                    break;

                case "blink":
                    ThickLine(tex, 36, 52, 52, 52, 3, ink);
                    ThickLine(tex, 76, 52, 92, 52, 3, ink);
                    Brow(tex, 34, 36, 54, 36); Brow(tex, 74, 36, 94, 36);
                    ThickLine(tex, 50, 90, 78, 90, 4, ink);
                    break;

                case "suspicious":
                    Eyes(tex, 9, 4, 3, 0);
                    Brow(tex, 34, 38, 54, 33); Brow(tex, 74, 33, 94, 38);
                    ThickLine(tex, 56, 92, 76, 92, 4, ink);
                    break;

                case "angry":
                    Eyes(tex, 8, 8, 4, 0);
                    Brow(tex, 32, 28, 54, 40); Brow(tex, 74, 40, 96, 28);
                    ThickLine(tex, 48, 96, 64, 89, 4, ink);
                    ThickLine(tex, 64, 89, 80, 96, 4, ink);
                    break;

                case "amused":
                    // happy closed eyes: ^ ^
                    ThickLine(tex, 36, 54, 44, 47, 3, ink); ThickLine(tex, 44, 47, 52, 54, 3, ink);
                    ThickLine(tex, 76, 54, 84, 47, 3, ink); ThickLine(tex, 84, 47, 92, 54, 3, ink);
                    Brow(tex, 34, 32, 54, 32); Brow(tex, 74, 32, 94, 32);
                    // open smile: dark half-ellipse with teeth
                    FillEllipse(tex, 64, 94, 15, 9, ink);
                    FillRect(tex, 49, 82, 79, 93, skin);
                    FillRect(tex, 53, 93, 75, 96, white);
                    break;

                case "confused":
                    FillEllipse(tex, 44, 52, 9, 10, white); FillEllipse(tex, 44, 52, 4, 4, ink);
                    FillEllipse(tex, 84, 53, 6, 7, white); FillEllipse(tex, 84, 53, 3, 3, ink);
                    Brow(tex, 34, 27, 54, 33); Brow(tex, 74, 39, 94, 39);
                    ThickLine(tex, 50, 92, 58, 87, 3, ink);
                    ThickLine(tex, 58, 87, 66, 94, 3, ink);
                    ThickLine(tex, 66, 94, 74, 89, 3, ink);
                    break;

                case "panicked":
                    Eyes(tex, 12, 13, 3, 0);
                    Brow(tex, 32, 26, 52, 22); Brow(tex, 76, 22, 96, 26);
                    FillEllipse(tex, 64, 96, 8, 12, ink);
                    break;

                case "warm":
                    Eyes(tex, 9, 9, 4, 0);
                    Brow(tex, 34, 33, 54, 33); Brow(tex, 74, 33, 94, 33);
                    ThickLine(tex, 50, 92, 64, 97, 4, ink);
                    ThickLine(tex, 64, 97, 78, 92, 4, ink);
                    break;

                case "defeated":
                    Eyes(tex, 9, 4, 3, 2);
                    Brow(tex, 34, 34, 54, 39); Brow(tex, 74, 39, 94, 34);
                    ThickLine(tex, 52, 90, 64, 95, 4, ink);
                    ThickLine(tex, 64, 95, 76, 90, 4, ink);
                    break;
            }

            tex.Apply();
            return tex;
        }

        private static void Eyes(Texture2D tex, int rx, int ry, int pupil, int pupilDrop)
        {
            FillEllipse(tex, 44, 52, rx, ry, white);
            FillEllipse(tex, 84, 52, rx, ry, white);
            FillEllipse(tex, 44, 52 + pupilDrop, pupil, pupil, ink);
            FillEllipse(tex, 84, 52 + pupilDrop, pupil, pupil, ink);
        }

        private static void Brow(Texture2D tex, int x0, int y0, int x1, int y1)
        {
            ThickLine(tex, x0, y0, x1, y1, 4, ink);
        }

        // top-origin pixel set
        private static void Px(Texture2D tex, int x, int yTop, Color c)
        {
            if (x < 0 || x >= Size || yTop < 0 || yTop >= Size) return;
            tex.SetPixel(x, Size - 1 - yTop, c);
        }

        private static void FillRect(Texture2D tex, int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Px(tex, x, y, c);
        }

        private static void FillEllipse(Texture2D tex, int cx, int cy, int rx, int ry, Color c)
        {
            for (int y = -ry; y <= ry; y++)
                for (int x = -rx; x <= rx; x++)
                {
                    float nx = rx > 0 ? (float)x / rx : 0f;
                    float ny = ry > 0 ? (float)y / ry : 0f;
                    if (nx * nx + ny * ny <= 1f) Px(tex, cx + x, cy + y, c);
                }
        }

        private static void ThickLine(Texture2D tex, int x0, int y0, int x1, int y1, int thickness, Color c)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) + 1;
            int r = Mathf.Max(1, thickness / 2);
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        Px(tex, x + dx, y + dy, c);
            }
        }
    }
}
