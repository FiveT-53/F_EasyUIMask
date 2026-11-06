using System.IO;
using F_EasyUIMask;
using UnityEditor;
using UnityEngine;

// Deliberately separate from the production Inspector and preview workflow.
public static class FEasyUIMaskP0Tools
{
    [MenuItem("Tools/F_Easy UI Mask/P0/Configure Selected Mask")]
    private static void ConfigureSelected()
    {
        FEasyUIMask mask = Selection.activeGameObject == null ? null : Selection.activeGameObject.GetComponent<FEasyUIMask>();
        if (mask == null) { Debug.LogWarning("Select a FEasyUIMask first."); return; }
        const string folder = "Assets/FEasyUIMaskP0";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "FEasyUIMaskP0");
        Material extraction = EnsureMaterial(folder + "/Extraction.mat", "FEasyUIMask/P0/AlphaExtraction");
        Material coverage = EnsureMaterial(folder + "/Coverage.mat", "FEasyUIMask/P0/Coverage");
        Undo.RecordObject(mask, "Configure P0 precision experiment");
        mask.p0ExtractionTemplate = extraction; mask.p0CoverageTemplate = coverage;
        mask.p0PrecisionExperiment = true; mask.areaSelectMode = FEasyUIMask.AreaSelectMode.UITarget;
        EditorUtility.SetDirty(mask);
    }

    public static Material EnsureMaterial(string path, string shaderName)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = Shader.Find(shaderName);
        if (shader == null) throw new System.InvalidOperationException("Missing P0 shader: " + shaderName);
        material = new Material(shader);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    [MenuItem("Tools/F_Easy UI Mask/P0/Export Selected Texture Alpha (Offline)")]
    private static void ExportAlpha()
    {
        Texture source = Selection.activeObject as Texture;
        Sprite sprite = Selection.activeObject as Sprite;
        if (sprite != null) source = sprite.texture; // Export the actual atlas texture; retain its UV mapping separately.
        if (source == null) { Debug.LogWarning("Select a Texture or Sprite."); return; }
        string path = EditorUtility.SaveFilePanel("Offline alpha reference", "", source.name + "-alpha", "png");
        if (string.IsNullOrEmpty(path)) return;
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        RenderTexture previous = RenderTexture.active;
        Texture2D copy = null;
        try
        {
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            copy = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
            copy.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            Color32[] pixels = copy.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(pixels[i].a, pixels[i].a, pixels[i].a, 255);
            copy.SetPixels32(pixels); copy.Apply();
            File.WriteAllBytes(path, copy.EncodeToPNG());
            if (sprite != null) File.WriteAllText(path + ".uv.json", JsonUtility.ToJson(new UVReference { uv = sprite.uv, vertices = sprite.vertices, triangles = sprite.triangles }, true));
        }
        finally
        {
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
            if (copy != null) Object.DestroyImmediate(copy);
        }
    }

    [System.Serializable] private sealed class UVReference { public Vector2[] uv, vertices; public ushort[] triangles; }
}
