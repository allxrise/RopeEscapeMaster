using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RopeEscape.Utils
{
    /// <summary>
    /// Loads the extracted original 3D meshes from Resources/Meshes/{name} (.obj or .txt)
    /// and caches them as runtime UnityEngine.Mesh instances.
    /// </summary>
    public static class ObjMeshLoader
    {
        private static readonly Dictionary<string, Mesh> _meshCache = new Dictionary<string, Mesh>();
        private static Material _sharedLitMaterial;

        public static Mesh LoadMesh(string meshName)
        {
            if (string.IsNullOrEmpty(meshName))
                return null;

            if (_meshCache.TryGetValue(meshName, out Mesh cached) && cached != null)
                return cached;

            // 1. Try direct Unity Mesh resource (if imported by Unity Editor from .obj)
            Mesh imported = Resources.Load<Mesh>($"Meshes/{meshName}");
            if (imported != null)
            {
                _meshCache[meshName] = imported;
                return imported;
            }

            // 2. Parse from extracted .txt Wavefront OBJ in Resources/Meshes/{meshName}
            TextAsset txt = Resources.Load<TextAsset>($"Meshes/{meshName}");
            if (txt != null && !string.IsNullOrWhiteSpace(txt.text))
            {
                Mesh parsed = ParseObj(txt.text, meshName);
                if (parsed != null)
                {
                    _meshCache[meshName] = parsed;
                    return parsed;
                }
            }

            return null;
        }

        public static Material CreateColoredMaterial(Color color, float smoothness = 0.55f, float metallic = 0.05f)
        {
            if (_sharedLitMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                _sharedLitMaterial = new Material(shader);
            }

            Material mat = new Material(_sharedLitMaterial);
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic);
            return mat;
        }

        private static Mesh ParseObj(string objText, string meshName)
        {
            List<Vector3> rawVerts = new List<Vector3>(512);
            List<Vector2> rawUvs = new List<Vector2>(512);
            List<Vector3> rawNorms = new List<Vector3>(512);

            List<Vector3> outVerts = new List<Vector3>(512);
            List<Vector2> outUvs = new List<Vector2>(512);
            List<Vector3> outNorms = new List<Vector3>(512);
            List<int> outTris = new List<int>(1536);

            string[] lines = objText.Split('\n');
            CultureInfo inv = CultureInfo.InvariantCulture;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length < 2 || line[0] == '#')
                    continue;

                if (line.StartsWith("v "))
                {
                    string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        float x = float.Parse(parts[1], inv);
                        float y = float.Parse(parts[2], inv);
                        float z = float.Parse(parts[3], inv);
                        rawVerts.Add(new Vector3(x, y, z));
                    }
                }
                else if (line.StartsWith("vt "))
                {
                    string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        float u = float.Parse(parts[1], inv);
                        float v = float.Parse(parts[2], inv);
                        rawUvs.Add(new Vector2(u, v));
                    }
                }
                else if (line.StartsWith("vn "))
                {
                    string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        float nx = float.Parse(parts[1], inv);
                        float ny = float.Parse(parts[2], inv);
                        float nz = float.Parse(parts[3], inv);
                        rawNorms.Add(new Vector3(nx, ny, nz));
                    }
                }
                else if (line.StartsWith("f "))
                {
                    string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        int firstIdx = AppendFaceVertex(parts[1], rawVerts, rawUvs, rawNorms, outVerts, outUvs, outNorms);
                        int prevIdx = AppendFaceVertex(parts[2], rawVerts, rawUvs, rawNorms, outVerts, outUvs, outNorms);
                        for (int k = 3; k < parts.Length; k++)
                        {
                            int curIdx = AppendFaceVertex(parts[k], rawVerts, rawUvs, rawNorms, outVerts, outUvs, outNorms);
                            outTris.Add(firstIdx);
                            outTris.Add(prevIdx);
                            outTris.Add(curIdx);
                            prevIdx = curIdx;
                        }
                    }
                }
            }

            if (outVerts.Count == 0 || outTris.Count == 0)
                return null;

            Mesh mesh = new Mesh { name = meshName };
            mesh.SetVertices(outVerts);
            if (outUvs.Count == outVerts.Count)
                mesh.SetUVs(0, outUvs);
            if (outNorms.Count == outVerts.Count)
                mesh.SetNormals(outNorms);
            mesh.SetTriangles(outTris, 0);
            if (outNorms.Count != outVerts.Count)
                mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int AppendFaceVertex(
            string token,
            List<Vector3> rawVerts,
            List<Vector2> rawUvs,
            List<Vector3> rawNorms,
            List<Vector3> outVerts,
            List<Vector2> outUvs,
            List<Vector3> outNorms)
        {
            string[] sub = token.Split('/');
            int vIdx = int.Parse(sub[0], CultureInfo.InvariantCulture) - 1;
            int vtIdx = (sub.Length > 1 && sub[1].Length > 0) ? int.Parse(sub[1], CultureInfo.InvariantCulture) - 1 : vIdx;
            int vnIdx = (sub.Length > 2 && sub[2].Length > 0) ? int.Parse(sub[2], CultureInfo.InvariantCulture) - 1 : vIdx;

            int outIndex = outVerts.Count;
            outVerts.Add(vIdx >= 0 && vIdx < rawVerts.Count ? rawVerts[vIdx] : Vector3.zero);
            if (rawUvs.Count > 0)
                outUvs.Add(vtIdx >= 0 && vtIdx < rawUvs.Count ? rawUvs[vtIdx] : Vector2.zero);
            if (rawNorms.Count > 0)
                outNorms.Add(vnIdx >= 0 && vnIdx < rawNorms.Count ? rawNorms[vnIdx] : Vector3.back);
            return outIndex;
        }
    }
}
