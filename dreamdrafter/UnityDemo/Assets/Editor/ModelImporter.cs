// Package-free importer for the delivered static GLB v2 architectural models.
// Blender (x,y,z) -> Unity (x,z,y): mirror glTF Z, reverse triangle winding.
// SourceModels remains outside Assets. Resources contains native Unity prefabs.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DottedLine.Editor
{
    public static class ModelImporter
    {
        const string Version = "GLB_STATIC_5";
        const string Generated = "Assets/Models/Generated";
        const string MaterialFolder = "Assets/Models/Materials";
        const string TextureFolder = "Assets/Models/Textures";
        const string PrefabFolder = "Assets/Resources/Models";
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        [MenuItem("虚线/导入交付模型")]
        public static void ImportAll() { ImportAll(false); }

        [MenuItem("虚线/重新导入全部交付模型")]
        public static void ForceImportAll() { ImportAll(true); }

        public static void ImportAll(bool force)
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string source = Path.Combine(project, "SourceModels");
            if (!Directory.Exists(source))
                throw new DirectoryNotFoundException("Missing SourceModels. Run Tools/prepare_models.py first.");
            Directory.CreateDirectory(Path.Combine(project, Generated));
            Directory.CreateDirectory(Path.Combine(project, MaterialFolder));
            Directory.CreateDirectory(Path.Combine(project, TextureFolder));
            Directory.CreateDirectory(Path.Combine(project, PrefabFolder));
            EnsureDeliveredShaders(project);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string[] files = Directory.GetFiles(source, "*.glb", SearchOption.TopDirectoryOnly);
            System.Array.Sort(files, StringComparer.Ordinal);
            var report = new ImportReport { coordinateMapping = "Blender (x,y,z) -> Unity (x,z,y); metres", importerVersion = Version };
            string reportPath = Path.Combine(project, "Tools", "unity_import_report.json");
            var previousEntries = new Dictionary<string, ImportEntry>();
            if (File.Exists(reportPath))
            {
                var previous = JsonUtility.FromJson<ImportReport>(File.ReadAllText(reportPath));
                if (previous != null && previous.models != null)
                    foreach (ImportEntry previousEntry in previous.models) previousEntries[previousEntry.key] = previousEntry;
            }
            var entries = new List<ImportEntry>();
            try
            {
                for (int i = 0; i < files.Length; i++)
                {
                    string key = Path.GetFileNameWithoutExtension(files[i]);
                    if (!Application.isBatchMode)
                        EditorUtility.DisplayProgressBar("虚线：导入模型", key, (float)i / files.Length);
                    string stampPath = Path.Combine(project, "Tools", ".import_" + key + ".txt");
                    var info = new FileInfo(files[i]);
                    string stamp = Version + ":" + info.Length + ":" + info.LastWriteTimeUtc.Ticks;
                    string prefabPath = PrefabFolder + "/" + key + ".prefab";
                    if (!force && File.Exists(stampPath) && File.ReadAllText(stampPath) == stamp && File.Exists(Path.Combine(project, prefabPath)))
                    {
                        ImportEntry cachedEntry;
                        if (!previousEntries.TryGetValue(key, out cachedEntry)) cachedEntry = new ImportEntry { key = key };
                        cachedEntry.cached = true;
                        entries.Add(cachedEntry);
                        continue;
                    }
                    using (var glb = new Glb(files[i]))
                    {
                        ImportEntry entry = glb.Import(key, prefabPath);
                        entries.Add(entry);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(stampPath));
                    File.WriteAllText(stampPath, stamp);
                    Debug.Log("[VirtualLine] Imported " + key + " (" + entries[entries.Count - 1].nodes + " nodes)");
                }
                report.models = entries.ToArray();
                Directory.CreateDirectory(Path.Combine(project, "Tools"));
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("[VirtualLine] Model import completed: " + files.Length + " native Unity prefabs.");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        [Serializable] public class ImportReport
        {
            public string coordinateMapping;
            public string importerVersion;
            public ImportEntry[] models;
        }
        [Serializable] public class ImportEntry
        {
            public string key;
            public bool cached;
            public int nodes;
            public int meshes;
            public long triangles;
            public Vector3 boundsCenter;
            public Vector3 boundsSize;
        }

        sealed class Glb : IDisposable
        {
            readonly string path;
            readonly Dictionary<string, object> doc;
            byte[] binary;
            List<object> accessors;
            List<object> views;
            List<object> nodes;
            Mesh[] meshes;
            Material[][] meshMaterials;
            Material[] materials;
            long triangleCount;

            public Glb(string source)
            {
                path = source;
                using (var stream = File.OpenRead(source))
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadUInt32() != 0x46546c67 || reader.ReadUInt32() != 2)
                        throw new InvalidDataException("GLB version 2 required: " + source);
                    uint total = reader.ReadUInt32();
                    if (total != stream.Length) throw new InvalidDataException("Invalid GLB length: " + source);
                    while (stream.Position < stream.Length)
                    {
                        int size = checked((int)reader.ReadUInt32());
                        uint tag = reader.ReadUInt32();
                        byte[] chunk = reader.ReadBytes(size);
                        if (chunk.Length != size) throw new EndOfStreamException(source);
                        if (tag == 0x4e4f534a)
                            doc = Json.Parse(Encoding.UTF8.GetString(chunk).TrimEnd('\0', ' ', '\r', '\n')) as Dictionary<string, object>;
                        else if (tag == 0x004e4942) binary = chunk;
                    }
                }
                if (doc == null || binary == null) throw new InvalidDataException("Missing JSON/BIN chunk: " + source);
                accessors = Array(doc, "accessors");
                views = Array(doc, "bufferViews");
                nodes = Array(doc, "nodes");
                if (Array(doc, "extensionsRequired").Count > 0)
                    throw new NotSupportedException("Required GLB extensions are not supported: " + source);
            }

            public void Dispose() { binary = null; }

            public ImportEntry Import(string key, string prefabPath)
            {
                materials = ImportMaterials();
                List<object> sourceMeshes = Array(doc, "meshes");
                meshes = new Mesh[sourceMeshes.Count];
                meshMaterials = new Material[sourceMeshes.Count][];
                string meshAsset = Generated + "/" + key + "_Meshes.asset";
                if (AssetDatabase.LoadMainAssetAtPath(meshAsset) != null) AssetDatabase.DeleteAsset(meshAsset);
                for (int i = 0; i < meshes.Length; i++)
                {
                    var sourceMesh = Object(sourceMeshes[i]);
                    meshes[i] = BuildMesh(sourceMesh, out meshMaterials[i]);
                    meshes[i].name = String(sourceMesh, "name", key + "_mesh_" + i);
                    if (i == 0) AssetDatabase.CreateAsset(meshes[i], meshAsset);
                    else AssetDatabase.AddObjectToAsset(meshes[i], meshAsset);
                }
                AssetDatabase.SaveAssets();
                var root = new GameObject(key);
                var transforms = new Transform[nodes.Count];
                try
                {
                    // Create all nodes before wiring parent references. This preserves Blender pivots.
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        var node = Object(nodes[i]);
                        var obj = new GameObject(String(node, "name", "Node_" + i));
                        transforms[i] = obj.transform;
                        obj.transform.SetParent(root.transform, false);
                    }
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        var node = Object(nodes[i]);
                        foreach (object child in Array(node, "children"))
                            transforms[Convert.ToInt32(child, Invariant)].SetParent(transforms[i], false);
                    }
                    bool collisionModel = key.IndexOf("collision", StringComparison.OrdinalIgnoreCase) >= 0;
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        var node = Object(nodes[i]);
                        Transform t = transforms[i];
                        ApplyTransform(t, node);
                        if (!node.ContainsKey("mesh")) continue;
                        int index = Int(node, "mesh");
                        if (index < 0 || index >= meshes.Length) throw new InvalidDataException("Bad node mesh index");
                        t.gameObject.AddComponent<MeshFilter>().sharedMesh = meshes[index];
                        if (collisionModel)
                        {
                            // Explicit collision modules have no visible surface and remain static.
                            var collider = t.gameObject.AddComponent<MeshCollider>();
                            collider.sharedMesh = meshes[index];
                            collider.convex = false;
                        }
                        else
                        {
                            var renderer = t.gameObject.AddComponent<MeshRenderer>();
                            renderer.sharedMaterials = meshMaterials[index];
                            renderer.shadowCastingMode = ShadowCastingMode.On;
                            renderer.receiveShadows = true;
                        }
                    }
                    Bounds bounds = new Bounds();
                    bool foundBounds = false;
                    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
                    {
                        if (!foundBounds) { bounds = renderer.bounds; foundBounds = true; }
                        else bounds.Encapsulate(renderer.bounds);
                    }
                    foreach (Collider collider in root.GetComponentsInChildren<Collider>())
                    {
                        if (!foundBounds) { bounds = collider.bounds; foundBounds = true; }
                        else bounds.Encapsulate(collider.bounds);
                    }
                    GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    if (saved == null) throw new InvalidOperationException("Failed to save " + prefabPath);
                    return new ImportEntry { key = key, nodes = nodes.Count, meshes = meshes.Length,
                        triangles = triangleCount, boundsCenter = bounds.center, boundsSize = bounds.size };
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }

            static void ApplyTransform(Transform transform, Dictionary<string, object> node)
            {
                if (node.ContainsKey("matrix"))
                {
                    var source = Array(node, "matrix");
                    Matrix4x4 m = Matrix4x4.identity;
                    for (int column = 0; column < 4; column++)
                        for (int row = 0; row < 4; row++) m[row, column] = Float(source[column * 4 + row]);
                    Matrix4x4 mirror = Matrix4x4.Scale(new Vector3(1, 1, -1));
                    m = mirror * m * mirror;
                    Vector3 scale = new Vector3(((Vector3)m.GetColumn(0)).magnitude, ((Vector3)m.GetColumn(1)).magnitude, ((Vector3)m.GetColumn(2)).magnitude);
                    if (m.determinant < 0) scale.x = -scale.x;
                    transform.localPosition = m.GetColumn(3);
                    Vector3 up = (Vector3)m.GetColumn(1) / (Mathf.Abs(scale.y) < 0.00001f ? 1 : scale.y);
                    Vector3 forward = (Vector3)m.GetColumn(2) / (Mathf.Abs(scale.z) < 0.00001f ? 1 : scale.z);
                    transform.localRotation = forward.sqrMagnitude < 0.00001f ? Quaternion.identity : Quaternion.LookRotation(forward, up);
                    transform.localScale = scale;
                    return;
                }
                var p = Array(node, "translation");
                transform.localPosition = p.Count == 3 ? new Vector3(Float(p[0]), Float(p[1]), -Float(p[2])) : Vector3.zero;
                var q = Array(node, "rotation");
                transform.localRotation = q.Count == 4 ? new Quaternion(-Float(q[0]), -Float(q[1]), Float(q[2]), Float(q[3])) : Quaternion.identity;
                var s = Array(node, "scale");
                transform.localScale = s.Count == 3 ? new Vector3(Float(s[0]), Float(s[1]), Float(s[2])) : Vector3.one;
            }

            Mesh BuildMesh(Dictionary<string, object> source, out Material[] meshMats)
            {
                var positions = new List<Vector3>();
                var normals = new List<Vector3>();
                var uvs = new List<Vector2>();
                var uv1s = new List<Vector2>();
                var colors = new List<Color>();
                var submeshes = new List<int[]>();
                var selectedMaterials = new List<Material>();
                bool allNormals = true, hasUv1 = false, hasColors = false;
                foreach (object primitiveObject in Array(source, "primitives"))
                {
                    var primitive = Object(primitiveObject);
                    if (Int(primitive, "mode", 4) != 4) throw new NotSupportedException("Only triangle meshes are supported");
                    var attributes = Object(primitive["attributes"]);
                    float[][] p = ReadAccessor(Int(attributes, "POSITION"));
                    float[][] n = attributes.ContainsKey("NORMAL") ? ReadAccessor(Int(attributes, "NORMAL")) : null;
                    float[][] uv = attributes.ContainsKey("TEXCOORD_0") ? ReadAccessor(Int(attributes, "TEXCOORD_0")) : null;
                    float[][] uv1 = attributes.ContainsKey("TEXCOORD_1") ? ReadAccessor(Int(attributes, "TEXCOORD_1")) : null;
                    float[][] color = attributes.ContainsKey("COLOR_0") ? ReadAccessor(Int(attributes, "COLOR_0")) : null;
                    allNormals &= n != null;
                    hasUv1 |= uv1 != null;
                    hasColors |= color != null;
                    int vertexStart = positions.Count;
                    for (int v = 0; v < p.Length; v++)
                    {
                        positions.Add(new Vector3(p[v][0], p[v][1], -p[v][2]));
                        normals.Add(n != null ? new Vector3(n[v][0], n[v][1], -n[v][2]) : Vector3.up);
                        uvs.Add(uv != null ? new Vector2(uv[v][0], 1f - uv[v][1]) : Vector2.zero);
                        uv1s.Add(uv1 != null ? new Vector2(uv1[v][0], 1f - uv1[v][1]) : Vector2.zero);
                        colors.Add(color != null ? new Color(color[v][0], color[v][1], color[v][2], color[v].Length > 3 ? color[v][3] : 1) : Color.white);
                    }
                    int[] triangles;
                    if (primitive.ContainsKey("indices"))
                    {
                        float[][] indices = ReadAccessor(Int(primitive, "indices"));
                        triangles = new int[indices.Length];
                        for (int j = 0; j < indices.Length; j++)
                        {
                            int index = (int)indices[j][0];
                            if (index < 0 || index >= p.Length) throw new InvalidDataException("Index outside POSITION accessor");
                            triangles[j] = vertexStart + index;
                        }
                    }
                    else
                    {
                        triangles = new int[p.Length];
                        for (int j = 0; j < p.Length; j++) triangles[j] = vertexStart + j;
                    }
                    if (triangles.Length % 3 != 0) throw new InvalidDataException("Incomplete triangle primitive");
                    for (int j = 0; j < triangles.Length; j += 3)
                    {
                        int temp = triangles[j + 1]; triangles[j + 1] = triangles[j + 2]; triangles[j + 2] = temp;
                    }
                    triangleCount += triangles.Length / 3;
                    submeshes.Add(triangles);
                    int material = Int(primitive, "material", -1);
                    selectedMaterials.Add(material >= 0 && material < materials.Length ? materials[material] : DefaultMaterial());
                }
                var mesh = new Mesh { indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(positions);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                if (hasUv1) mesh.SetUVs(1, uv1s);
                if (hasColors) mesh.SetColors(colors);
                mesh.subMeshCount = submeshes.Count;
                for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i, false);
                if (!allNormals) mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                meshMats = selectedMaterials.ToArray();
                return mesh;
            }

            float[][] ReadAccessor(int index)
            {
                var accessor = Object(accessors[index]);
                if (accessor.ContainsKey("sparse")) throw new NotSupportedException("Sparse accessors are not used by delivered models");
                int count = Int(accessor, "count"), componentType = Int(accessor, "componentType");
                int components;
                switch (String(accessor, "type"))
                {
                    case "SCALAR": components = 1; break;
                    case "VEC2": components = 2; break;
                    case "VEC3": components = 3; break;
                    case "VEC4": components = 4; break;
                    default: throw new NotSupportedException("Unsupported accessor type");
                }
                int componentSize = componentType == 5120 || componentType == 5121 ? 1 : componentType == 5122 || componentType == 5123 ? 2 : 4;
                var result = new float[count][];
                if (!accessor.ContainsKey("bufferView"))
                {
                    for (int i = 0; i < count; i++) result[i] = new float[components];
                    return result;
                }
                var view = Object(views[Int(accessor, "bufferView")]);
                if (Int(view, "buffer", 0) != 0) throw new NotSupportedException("Only the GLB BIN buffer is supported");
                int start = Int(view, "byteOffset", 0) + Int(accessor, "byteOffset", 0);
                int stride = Int(view, "byteStride", components * componentSize);
                bool normalized = Bool(accessor, "normalized");
                long end = (long)start + (long)Math.Max(0, count - 1) * stride + components * componentSize;
                if (start < 0 || end > binary.Length) throw new InvalidDataException("Accessor outside binary buffer: " + path);
                for (int i = 0; i < count; i++)
                {
                    result[i] = new float[components];
                    for (int j = 0; j < components; j++)
                    {
                        int offset = start + i * stride + j * componentSize;
                        float value;
                        switch (componentType)
                        {
                            case 5120: value = (sbyte)binary[offset]; if (normalized) value = Mathf.Max(-1, value / 127); break;
                            case 5121: value = binary[offset]; if (normalized) value /= 255; break;
                            case 5122: value = BitConverter.ToInt16(binary, offset); if (normalized) value = Mathf.Max(-1, value / 32767); break;
                            case 5123: value = BitConverter.ToUInt16(binary, offset); if (normalized) value /= 65535; break;
                            case 5125: value = BitConverter.ToUInt32(binary, offset); break;
                            case 5126: value = BitConverter.ToSingle(binary, offset); break;
                            default: throw new NotSupportedException("Unsupported accessor component type " + componentType);
                        }
                        result[i][j] = value;
                    }
                }
                return result;
            }

            Material[] ImportMaterials()
            {
                var sourceMaterials = Array(doc, "materials");
                var result = new Material[sourceMaterials.Count];
                for (int i = 0; i < result.Length; i++)
                {
                    var source = Object(sourceMaterials[i]);
                    var pbr = source.ContainsKey("pbrMetallicRoughness") ? Object(source["pbrMetallicRoughness"]) : new Dictionary<string, object>();
                    var factor = Array(pbr, "baseColorFactor");
                    Color tint = factor.Count == 4 ? new Color(Float(factor[0]), Float(factor[1]), Float(factor[2]), Float(factor[3])) : Color.white;
                    string name = String(source, "name", "Delivered_Material");
                    Texture2D texture = null;
                    if (pbr.ContainsKey("baseColorTexture")) texture = ImportTexture(Int(Object(pbr["baseColorTexture"]), "index"));
                    float metallic = Number(pbr, "metallicFactor", 1), roughness = Number(pbr, "roughnessFactor", 1);
                    string alpha = String(source, "alphaMode", "OPAQUE");
                    float cutoff = Number(source, "alphaCutoff", 0.5f);
                    string signature = name + ":" + tint.r.ToString("R", Invariant) + ":" + tint.g.ToString("R", Invariant) + ":" + tint.b.ToString("R", Invariant) + ":" + tint.a.ToString("R", Invariant)
                        + ":" + metallic.ToString("R", Invariant) + ":" + roughness.ToString("R", Invariant) + ":" + alpha + ":" + cutoff.ToString("R", Invariant)
                        + ":" + (texture != null ? AssetDatabase.GetAssetPath(texture) : "none") + ":doubleSided=" + Bool(source, "doubleSided") + ":" + Version;
                    string materialPath = MaterialFolder + "/" + SafeName(name) + "_" + Hash(Encoding.UTF8.GetBytes(signature)) + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null)
                    {
                        Shader shader = Shader.Find(alpha == "BLEND" ? "VirtualLine/Delivered Transparent" : "VirtualLine/Delivered Surface");
                        if (shader == null) shader = Shader.Find("Standard");
                        if (shader == null) throw new InvalidOperationException("Delivered surface shader is unavailable");
                        material = new Material(shader) { name = name, color = tint, mainTexture = texture };
                        material.SetFloat("_Cull", Bool(source, "doubleSided") ? 0 : 2);
                        material.SetFloat("_Metallic", metallic);
                        material.SetFloat("_Glossiness", 1 - roughness);
                        material.SetFloat("_Cutoff", cutoff);
                        if (alpha == "BLEND")
                        {
                            material.SetFloat("_Mode", 2);
                            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                            material.SetInt("_ZWrite", 0);
                            material.DisableKeyword("_ALPHATEST_ON");
                            material.EnableKeyword("_ALPHABLEND_ON");
                            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                            material.SetOverrideTag("RenderType", "Transparent");
                            material.renderQueue = (int)RenderQueue.Transparent;
                        }
                        else if (alpha == "MASK")
                        {
                            material.SetFloat("_Mode", 1);
                            material.SetInt("_SrcBlend", (int)BlendMode.One);
                            material.SetInt("_DstBlend", (int)BlendMode.Zero);
                            material.SetInt("_ZWrite", 1);
                            material.EnableKeyword("_ALPHATEST_ON");
                            material.SetOverrideTag("RenderType", "TransparentCutout");
                            material.renderQueue = (int)RenderQueue.AlphaTest;
                        }
                        var emission = Array(source, "emissiveFactor");
                        if (emission.Count == 3 && Float(emission[0]) + Float(emission[1]) + Float(emission[2]) > 0)
                        {
                            material.EnableKeyword("_EMISSION");
                            material.SetColor("_EmissionColor", new Color(Float(emission[0]), Float(emission[1]), Float(emission[2])));
                        }
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                    result[i] = material;
                }
                return result;
            }

            Texture2D ImportTexture(int index)
            {
                var texture = Object(Array(doc, "textures")[index]);
                int sourceId = Int(texture, "source", -1);
                if (sourceId < 0) throw new InvalidDataException("Texture has no image source");
                var image = Object(Array(doc, "images")[sourceId]);
                byte[] data;
                string extension;
                if (image.ContainsKey("uri"))
                {
                    string uri = String(image, "uri");
                    if (uri.StartsWith("data:", StringComparison.Ordinal))
                    {
                        int comma = uri.IndexOf(',');
                        data = Convert.FromBase64String(uri.Substring(comma + 1));
                        extension = uri.StartsWith("data:image/jpeg", StringComparison.Ordinal) ? ".jpg" : ".png";
                    }
                    else
                    {
                        string imagePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), Uri.UnescapeDataString(uri).Replace('/', Path.DirectorySeparatorChar)));
                        data = File.ReadAllBytes(imagePath);
                        extension = Path.GetExtension(imagePath).ToLowerInvariant();
                    }
                }
                else
                {
                    var view = Object(views[Int(image, "bufferView")]);
                    int offset = Int(view, "byteOffset", 0), length = Int(view, "byteLength");
                    data = new byte[length];
                    Buffer.BlockCopy(binary, offset, data, 0, length);
                    extension = String(image, "mimeType") == "image/jpeg" ? ".jpg" : ".png";
                }
                string target = TextureFolder + "/" + Hash(data) + extension;
                var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
                if (imported == null)
                {
                    File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, target), data);
                    AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                    var settings = AssetImporter.GetAtPath(target) as TextureImporter;
                    if (settings != null)
                    {
                        settings.textureType = TextureImporterType.Default;
                        settings.sRGBTexture = true;
                        settings.alphaSource = TextureImporterAlphaSource.FromInput;
                        settings.alphaIsTransparency = true;
                        settings.mipmapEnabled = true;
                        settings.wrapMode = TextureWrapMode.Repeat;
                        settings.maxTextureSize = 2048;
                        settings.textureCompression = TextureImporterCompression.Compressed;
                        settings.SaveAndReimport();
                    }
                    imported = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
                }
                return imported;
            }
        }

        static Material DefaultMaterial()
        {
            string path = MaterialFolder + "/Default.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = "M_HB_Default", color = new Color(0.6f, 0.59f, 0.56f) };
                material.SetFloat("_Glossiness", 0.12f);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        static void EnsureDeliveredShaders(string project)
        {
            // Blender's exports mark paper, chains, and interior surfaces double-sided.
            // A small built-in-pipeline shader preserves that authored setting.
            string common = @"
Properties {
 _Color (""Color"", Color) = (1,1,1,1)
 _MainTex (""Base Color"", 2D) = ""white"" {}
 _Metallic (""Metallic"", Range(0,1)) = 0
 _Glossiness (""Smoothness"", Range(0,1)) = 0.2
 _Cutoff (""Alpha Cutoff"", Range(0,1)) = 0.5
 _Cull (""Cull"", Float) = 0
 _EmissionColor (""Emission"", Color) = (0,0,0,0)
}
";
            string body = @"
 Cull [_Cull]
 CGPROGRAM
 #pragma target 3.0
 #pragma shader_feature_local _ALPHATEST_ON
 #pragma shader_feature_local _EMISSION
 sampler2D _MainTex;
 fixed4 _Color;
 half _Metallic;
 half _Glossiness;
 half _Cutoff;
 fixed4 _EmissionColor;
 struct Input { float2 uv_MainTex; };
 void surf(Input IN, inout SurfaceOutputStandard o) {
  fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
  #ifdef _ALPHATEST_ON
   clip(c.a - _Cutoff);
  #endif
  o.Albedo = c.rgb;
  o.Metallic = _Metallic;
  o.Smoothness = _Glossiness;
  o.Alpha = c.a;
  #ifdef _EMISSION
   o.Emission = _EmissionColor.rgb;
  #endif
 }
 ENDCG
}
Fallback ""Diffuse""
}
";
            string opaque = "Shader \"VirtualLine/Delivered Surface\" {\n" + common + "SubShader { Tags { \"RenderType\"=\"Opaque\" }\n"
                + body.Replace("#pragma target 3.0", "#pragma target 3.0\n #pragma surface surf Standard fullforwardshadows");
            string transparent = "Shader \"VirtualLine/Delivered Transparent\" {\n" + common + "SubShader { Tags { \"RenderType\"=\"Transparent\" \"Queue\"=\"Transparent\" }\n"
                + body.Replace("#pragma target 3.0", "#pragma target 3.0\n #pragma surface surf Standard fullforwardshadows alpha:fade keepalpha");
            WriteShader(project, "Assets/Models/DeliveredSurface.shader", opaque);
            WriteShader(project, "Assets/Models/DeliveredTransparent.shader", transparent);
        }

        static void WriteShader(string project, string relative, string source)
        {
            string absolute = Path.Combine(project, relative);
            if (!File.Exists(absolute) || File.ReadAllText(absolute) != source)
                File.WriteAllText(absolute, source, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(relative, ImportAssetOptions.ForceSynchronousImport);
        }

        static string SafeName(string value)
        {
            var b = new StringBuilder();
            foreach (char c in value) b.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return b.ToString();
        }
        static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(bytes);
                return BitConverter.ToString(digest).Replace("-", "").Substring(0, 24).ToLowerInvariant();
            }
        }
        static Dictionary<string, object> Object(object value) { return (Dictionary<string, object>)value; }
        static List<object> Array(Dictionary<string, object> value, string key)
        {
            object result;
            return value.TryGetValue(key, out result) ? (List<object>)result : new List<object>();
        }
        static int Int(Dictionary<string, object> value, string key, int fallback = 0)
        {
            object result; return value.TryGetValue(key, out result) ? Convert.ToInt32(result, Invariant) : fallback;
        }
        static float Float(object value) { return Convert.ToSingle(value, Invariant); }
        static float Number(Dictionary<string, object> value, string key, float fallback)
        {
            object result; return value.TryGetValue(key, out result) ? Float(result) : fallback;
        }
        static string String(Dictionary<string, object> value, string key, string fallback = "")
        {
            object result; return value.TryGetValue(key, out result) ? Convert.ToString(result, Invariant) : fallback;
        }
        static bool Bool(Dictionary<string, object> value, string key)
        {
            object result; return value.TryGetValue(key, out result) && Convert.ToBoolean(result, Invariant);
        }

        // Small strict JSON parser. It does not invoke scripts or require JSON packages.
        sealed class Json
        {
            readonly string source;
            int at;
            Json(string input) { source = input; }
            public static object Parse(string input)
            {
                var parser = new Json(input);
                object result = parser.Value();
                parser.Space();
                if (parser.at != input.Length) throw new FormatException("Trailing JSON data");
                return result;
            }
            void Space() { while (at < source.Length && char.IsWhiteSpace(source[at])) at++; }
            char Next()
            {
                if (at >= source.Length) throw new FormatException("Unexpected JSON end");
                return source[at++];
            }
            object Value()
            {
                Space();
                if (at >= source.Length) throw new FormatException("Missing JSON value");
                char c = source[at];
                if (c == '{') return Dictionary();
                if (c == '[') return List();
                if (c == '"') return Text();
                if (c == 't') { Literal("true"); return true; }
                if (c == 'f') { Literal("false"); return false; }
                if (c == 'n') { Literal("null"); return null; }
                int start = at;
                while (at < source.Length && "-+0123456789.eE".IndexOf(source[at]) >= 0) at++;
                if (start == at) throw new FormatException("Bad JSON token at " + at);
                return double.Parse(source.Substring(start, at - start), NumberStyles.Float, Invariant);
            }
            void Literal(string word)
            {
                foreach (char c in word) if (Next() != c) throw new FormatException("Invalid JSON literal");
            }
            Dictionary<string, object> Dictionary()
            {
                Next(); Space();
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                if (source[at] == '}') { at++; return result; }
                while (true)
                {
                    Space(); string key = Text(); Space();
                    if (Next() != ':') throw new FormatException("Missing JSON colon");
                    result.Add(key, Value()); Space();
                    char c = Next();
                    if (c == '}') return result;
                    if (c != ',') throw new FormatException("Missing JSON comma");
                }
            }
            List<object> List()
            {
                Next(); Space();
                var result = new List<object>();
                if (source[at] == ']') { at++; return result; }
                while (true)
                {
                    result.Add(Value()); Space();
                    char c = Next();
                    if (c == ']') return result;
                    if (c != ',') throw new FormatException("Missing JSON comma");
                }
            }
            string Text()
            {
                if (Next() != '"') throw new FormatException("JSON string expected");
                var value = new StringBuilder();
                while (true)
                {
                    char c = Next();
                    if (c == '"') return value.ToString();
                    if (c != '\\') { value.Append(c); continue; }
                    c = Next();
                    switch (c)
                    {
                        case '"': value.Append('"'); break;
                        case '\\': value.Append('\\'); break;
                        case '/': value.Append('/'); break;
                        case 'b': value.Append('\b'); break;
                        case 'f': value.Append('\f'); break;
                        case 'n': value.Append('\n'); break;
                        case 'r': value.Append('\r'); break;
                        case 't': value.Append('\t'); break;
                        case 'u':
                            if (at + 4 > source.Length) throw new FormatException("Incomplete unicode escape");
                            value.Append((char)int.Parse(source.Substring(at, 4), NumberStyles.HexNumber, Invariant));
                            at += 4; break;
                        default: throw new FormatException("Invalid JSON escape");
                    }
                }
            }
        }
    }
}
#endif
