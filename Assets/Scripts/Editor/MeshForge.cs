using UnityEditor;
using UnityEngine;

namespace TalkOut.EditorTools
{
    /// Procedural character meshes: rounded (beveled) boxes with smooth
    /// normals, plus a shoulder-tapered torso. Unit-sized, scaled by
    /// transforms — one mesh serves every body part. Saved as assets so
    /// scene references stay stable across rebuilds.
    public static class MeshForge
    {
        private const string Dir = "Assets/Art/Meshes";

        public static void BuildAll()
        {
            Save("RoundedCube", RoundedBox(Vector3.one, 0.12f, 5, 0f));
            Save("RoundedHead", RoundedBox(Vector3.one, 0.22f, 6, 0f));
            Save("TaperedTorso", RoundedBox(Vector3.one, 0.14f, 6, 0.28f));
            Save("RoundedLimb", RoundedBox(Vector3.one, 0.30f, 5, 0f));
            AssetDatabase.SaveAssets();
            Debug.Log("[TalkOut] Character meshes forged.");
        }

        public static Mesh Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/{name}.asset");

        private static void Save(string name, Mesh mesh)
        {
            System.IO.Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                mesh.name = name;
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                // mutate in place so scene GUID references survive
                existing.Clear();
                existing.vertices = mesh.vertices;
                existing.normals = mesh.normals;
                existing.uv = mesh.uv;
                existing.triangles = mesh.triangles;
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
            }
        }

        /// Classic rounded box: grid-subdivided cube faces, vertices pulled
        /// onto a radius-r shell around the shrunken core box. taper narrows
        /// the bottom (0 = none) for shoulders-wider-than-hips torsos.
        private static Mesh RoundedBox(Vector3 size, float radius, int segments, float taper)
        {
            var half = size * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.95f);
            var inner = half - Vector3.one * radius;

            var vertices = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();

            // six faces, each a (segments+1)^2 grid
            foreach (var (normal, up) in new (Vector3, Vector3)[]
            {
                (Vector3.forward, Vector3.up), (Vector3.back, Vector3.up),
                (Vector3.left, Vector3.up), (Vector3.right, Vector3.up),
                (Vector3.up, Vector3.forward), (Vector3.down, Vector3.forward),
            })
            {
                Vector3 axisRight = Vector3.Cross(up, normal);
                int baseIndex = vertices.Count;
                for (int y = 0; y <= segments; y++)
                {
                    for (int x = 0; x <= segments; x++)
                    {
                        float u = x / (float)segments - 0.5f;
                        float v = y / (float)segments - 0.5f;
                        Vector3 p = Vector3.Scale(normal, half)
                                    + axisRight * (u * Dot(size, axisRight))
                                    + up * (v * Dot(size, up));

                        // pull onto the rounded shell
                        Vector3 core = new Vector3(
                            Mathf.Clamp(p.x, -inner.x, inner.x),
                            Mathf.Clamp(p.y, -inner.y, inner.y),
                            Mathf.Clamp(p.z, -inner.z, inner.z));
                        Vector3 dir = (p - core).normalized;
                        Vector3 pos = core + dir * radius;

                        // taper: narrow toward the bottom
                        if (taper > 0f)
                        {
                            float t = Mathf.InverseLerp(-half.y, half.y, pos.y);
                            float squeeze = Mathf.Lerp(1f - taper, 1f, t);
                            pos.x *= squeeze;
                            pos.z *= squeeze;
                        }

                        vertices.Add(pos);
                        uvs.Add(new Vector2(u + 0.5f, v + 0.5f));
                    }
                }
                for (int y = 0; y < segments; y++)
                {
                    for (int x = 0; x < segments; x++)
                    {
                        int i = baseIndex + y * (segments + 1) + x;
                        triangles.Add(i); triangles.Add(i + segments + 1); triangles.Add(i + 1);
                        triangles.Add(i + 1); triangles.Add(i + segments + 1); triangles.Add(i + segments + 2);
                    }
                }
            }

            var mesh = new Mesh
            {
                vertices = vertices.ToArray(),
                uv = uvs.ToArray(),
                triangles = triangles.ToArray(),
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float Dot(Vector3 size, Vector3 axis) =>
            Mathf.Abs(size.x * axis.x + size.y * axis.y + size.z * axis.z);
    }
}
