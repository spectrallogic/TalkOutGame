using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TalkOut.EditorTools
{
    /// Procedural surface textures — asphalt speckle, brick courses, wood
    /// planks, stone blocks, carpet weave, grass mottle, plaster. Generated
    /// once as PNGs, applied to the flat-color materials with tiling so the
    /// world stops looking like untextured previs.
    public static class TextureForge
    {
        private const string Dir = "Assets/Art/Textures";
        private const int Size = 512;

        public static void BuildAll()
        {
            Directory.CreateDirectory(Dir);
            Generate("asphalt", RenderAsphalt);
            Generate("ground", RenderGround);
            Generate("stone", RenderStone);
            Generate("wood", RenderWood);
            Generate("brick", RenderBrick);
            Generate("carpet", RenderCarpet);
            Generate("plaster", RenderPlaster);
            AssetDatabase.SaveAssets();

            // marry textures to materials (tint comes from the material color)
            Apply("Asphalt", "asphalt", new Vector2(3f, 30f));
            Apply("Ground_Night", "ground", new Vector2(14f, 14f));
            Apply("Stone_Grey", "stone", new Vector2(4f, 3f));
            Apply("Stone_Dark", "stone", new Vector2(8f, 8f));
            Apply("Wood_Floor", "wood", new Vector2(8f, 8f));
            Apply("Tree_Trunk", "wood", new Vector2(1f, 2f));
            Apply("Car_Rust", "plaster", new Vector2(2f, 2f));
            Apply("Wall_Warm", "plaster", new Vector2(6f, 2f));
            Apply("Carpet_Red", "carpet", new Vector2(2f, 10f));
            Apply("Table_Cloth", "carpet", new Vector2(2f, 2f));
            Apply("Brick_Red", "brick", new Vector2(6f, 3f));
            Debug.Log("[TalkOut] Surface textures forged and applied.");
        }

        private static void Apply(string materialName, string textureName, Vector2 tiling)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Art/Materials/{materialName}.mat");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/{textureName}.png");
            if (mat == null || tex == null) return;
            mat.mainTexture = tex;
            mat.mainTextureScale = tiling;
            EditorUtility.SetDirty(mat);
        }

        private static void Generate(string name, Func<int, int, float> height)
        {
            string path = $"{Dir}/{name}.png";
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // grayscale multiplier around 1.0 — material color supplies hue
                    float v = Mathf.Clamp(height(x, y), 0.3f, 1.6f);
                    pixels[y * Size + x] = new Color(v, v, v, 1f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        // ---- surface recipes (return brightness multiplier ~0.7-1.3) ----------

        private static float Wrap(int v) => v & (Size - 1);

        /// Tileable value noise via wrapped lattice.
        private static float Noise(int x, int y, int cell, int seed)
        {
            float fx = (x % Size) / (float)cell, fy = (y % Size) / (float)cell;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            int cells = Size / cell;
            float tx = fx - x0, ty = fy - y0;
            float Smooth(float t) => t * t * (3f - 2f * t);
            float Hash(int a, int b)
            {
                int h = (a % cells + cells) % cells * 73856093 ^ (b % cells + cells) % cells * 19349663 ^ seed * 83492791;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
            float a00 = Hash(x0, y0), a10 = Hash(x0 + 1, y0), a01 = Hash(x0, y0 + 1), a11 = Hash(x0 + 1, y0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a00, a10, Smooth(tx)), Mathf.Lerp(a01, a11, Smooth(tx)), Smooth(ty));
        }

        private static float RenderAsphalt(int x, int y)
        {
            float baseTone = 0.9f + Noise(x, y, 64, 7) * 0.2f;
            float speckle = Noise(x * 3, y * 3, 4, 13) > 0.82f ? 0.35f : 0f; // bright aggregate
            float pit = Noise(x * 2, y * 2, 8, 29) < 0.12f ? -0.2f : 0f;
            return baseTone + speckle + pit;
        }

        private static float RenderGround(int x, int y)
        {
            float patches = 0.85f + Noise(x, y, 128, 3) * 0.35f;
            float blades = Noise(x, y, 4, 17) * 0.25f;
            return patches + blades - 0.12f;
        }

        private static float RenderStone(int x, int y)
        {
            // coursed blocks with jittered mortar
            int courseH = 128, blockW = 170;
            int row = y / courseH;
            int xo = x + (row % 2) * blockW / 2;
            float mortarY = Mathf.Abs(y % courseH) < 7 ? 0.55f : 1f;
            float mortarX = Mathf.Abs(xo % blockW) < 8 ? 0.55f : 1f;
            float blockTone = 0.85f + Noise(xo - xo % blockW, y - y % courseH, 32, 5) * 0.3f;
            float grain = Noise(x, y, 16, 23) * 0.12f;
            return Mathf.Min(mortarX, mortarY) * (blockTone + grain);
        }

        private static float RenderWood(int x, int y)
        {
            int plankW = 128;
            int plank = x / plankW;
            float seam = Mathf.Abs(x % plankW) < 4 ? 0.6f : 1f;
            float plankTone = 0.85f + ((plank * 2654435761u) % 100) / 100f * 0.28f;
            float grain = Mathf.Sin(y * 0.11f + plank * 17f + Noise(x, y, 32, 11) * 6f) * 0.07f;
            return seam * (plankTone + grain);
        }

        private static float RenderBrick(int x, int y)
        {
            int brickH = 64, brickW = 128;
            int row = y / brickH;
            int xo = x + (row % 2) * brickW / 2;
            float mortar = (Mathf.Abs(y % brickH) < 6 || Mathf.Abs(xo % brickW) < 7) ? 0.55f : 1f;
            float brickTone = 0.85f + Noise(xo - xo % brickW, y - y % brickH, 16, 31) * 0.32f;
            float wear = Noise(x, y, 8, 41) * 0.1f;
            return mortar * (brickTone + wear);
        }

        private static float RenderCarpet(int x, int y)
        {
            float weave = (Mathf.Sin(x * 0.8f) + Mathf.Sin(y * 0.8f)) * 0.035f;
            float mottle = 0.92f + Noise(x, y, 32, 19) * 0.16f;
            return mottle + weave;
        }

        private static float RenderPlaster(int x, int y)
        {
            return 0.92f + Noise(x, y, 96, 37) * 0.13f + Noise(x, y, 16, 43) * 0.05f;
        }
    }
}
