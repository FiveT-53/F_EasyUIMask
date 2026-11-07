using System.Collections.Generic;
using F_EasyUIMask;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(FEasyUIMask))]
public class FEasyUIMaskEditor : Editor
{
    private bool showGraphic = true, showResources;
    internal FEasyUIMask Mask { get { return target as FEasyUIMask; } }
    private void OnEnable() { FEasyUIMaskPreviewQueue.Register(this); }
    private void OnDisable() { FEasyUIMaskPreviewQueue.Unregister(this); }

    public override void OnInspectorGUI()
    {
        if (targets.Length != 1 || serializedObject.isEditingMultipleObjects)
        { EditorGUILayout.HelpBox("Select one precision mask to edit.", MessageType.Info); return; }
        FEasyUIMask mask = Mask;
        if (mask == null) return;
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        showGraphic = EditorGUILayout.Foldout(showGraphic, "Graphic", true);
        if (showGraphic)
            DrawPropertiesExcluding(serializedObject, "m_Script", "m_Material", "targetUI", "realTimeUpdate",
                "invertMask", "alphaThreshold", "maxTextureSize", "extractionTemplate", "coverageTemplate");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Precision Mask", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetUI"), new GUIContent("Target UI"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("invertMask"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("realTimeUpdate"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("alphaThreshold"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxTextureSize"));
        EditorGUILayout.HelpBox("P1 extracts one standard Simple Image's mesh and texture Alpha. Target tint, "
            + "CanvasGroup Alpha and ancestor Mask/RectMask2D visibility are ignored. Sliced, Filled, Tiled, split Alpha "
            + "and custom materials/mesh effects are unsupported.", MessageType.Info);
        EditorGUILayout.LabelField("Quality", "Point sampling; 16–2048 texels per axis");
        EditorGUILayout.HelpBox("Precision is limited by the committed texel size. At 2048², two RGBA32 GPU/CPU "
            + "slots cost about 64 MiB, excluding meshes and driver memory. Async preparation may take several frames.", MessageType.Info);
        if (mask.IsReady)
        {
            Vector2 texel = mask.EffectiveTexelSize;
            EditorGUILayout.LabelField("Committed texel (screen pixels)", texel.x.ToString("0.###") + " × " + texel.y.ToString("0.###"));
        }
        showResources = EditorGUILayout.Foldout(showResources, "Package Resources", true);
        if (showResources)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("extractionTemplate"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("coverageTemplate"));
            if (GUILayout.Button("Restore Package Resources")) AssignPackageResources(serializedObject);
        }
        if (serializedObject.ApplyModifiedProperties()) FEasyUIMaskPreviewQueue.Request(mask);
        if (GUILayout.Button("Refresh Preview")) FEasyUIMaskPreviewQueue.Request(mask);
        if (!mask.isActiveAndEnabled)
            EditorGUILayout.HelpBox("Preview is paused while this object or component is disabled.", MessageType.Info);
        else if (!string.IsNullOrEmpty(mask.FailureReason))
            EditorGUILayout.HelpBox(mask.FailureReason, MessageType.Warning);
        else
            EditorGUILayout.LabelField("State", mask.IsPreparing
                ? (mask.IsReady ? "Preparing; previous snapshot retained" : "Preparing first snapshot")
                : (mask.IsReady ? "Ready" : "Waiting for preview"));
    }

    private static void AssignPackageResources(SerializedObject serialized)
    {
        // Resolve relative to the package's own script, so Assets copies and UPM use one path.
        string scriptPath = AssetDatabase.GetAssetPath(serialized.FindProperty("m_Script").objectReferenceValue);
        string folder = System.IO.Path.GetDirectoryName(scriptPath).Replace('\\', '/');
        serialized.FindProperty("extractionTemplate").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>(folder + "/PrecisionAlphaExtraction.mat");
        serialized.FindProperty("coverageTemplate").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>(folder + "/PrecisionCoverage.mat");
    }
}

[InitializeOnLoad]
internal static class FEasyUIMaskPreviewQueue
{
    private static readonly HashSet<FEasyUIMaskEditor> Editors = new HashSet<FEasyUIMaskEditor>();
    private static readonly HashSet<FEasyUIMask> Pending = new HashSet<FEasyUIMask>();
    private static readonly HashSet<FEasyUIMask> Previews = new HashSet<FEasyUIMask>();
    private static readonly List<FEasyUIMask> Retire = new List<FEasyUIMask>();

    static FEasyUIMaskPreviewQueue()
    {
        EditorApplication.update += Update;
        Undo.undoRedoPerformed += RefreshObserved;
        Selection.selectionChanged += SelectionChanged;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
        EditorApplication.quitting += Shutdown;
    }
    internal static void Register(FEasyUIMaskEditor editor) { Editors.Add(editor); Request(editor.Mask); }
    internal static void Unregister(FEasyUIMaskEditor editor)
    {
        Editors.Remove(editor);
        FEasyUIMask mask = editor.Mask;
        if (mask == null) return;
        foreach (FEasyUIMaskEditor other in Editors)
            if (other != null && other.Mask == mask) return;
        Pending.Remove(mask); Previews.Remove(mask); mask.ReleaseEditorPreview();
    }
    internal static void Request(FEasyUIMask mask) { if (mask != null) Pending.Add(mask); }
    private static bool CanPreview(FEasyUIMask mask)
    {
        return mask != null && !EditorUtility.IsPersistent(mask) && mask.isActiveAndEnabled
            && mask.gameObject.scene.IsValid() && mask.gameObject.scene.isLoaded
            && StageUtility.GetStageHandle(mask.gameObject).Equals(StageUtility.GetCurrentStageHandle())
            && (Application.IsPlaying(mask.gameObject)
                || (Selection.activeGameObject == mask.gameObject && Selection.gameObjects.Length == 1));
    }
    private static void Update()
    {
        if (EditorApplication.isCompiling || (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode)) return;
        Editors.RemoveWhere(editor => editor == null);
        Retire.Clear();
        foreach (FEasyUIMask mask in Previews)
            if (!CanPreview(mask)) Retire.Add(mask);
        foreach (FEasyUIMask mask in Retire)
        { if (mask != null) mask.ReleaseEditorPreview(); Previews.Remove(mask); }
        foreach (FEasyUIMaskEditor editor in Editors)
        {
            FEasyUIMask mask = editor.Mask;
            if (!CanPreview(mask)) continue;
            if (mask.ConsumeEditorRefreshRequest()) Request(mask);
            bool force = Pending.Remove(mask);
            bool sameStage = mask.targetUI == null || (!EditorUtility.IsPersistent(mask.targetUI)
                && mask.targetUI.gameObject.scene.IsValid() && mask.targetUI.gameObject.scene.isLoaded
                && StageUtility.GetStageHandle(mask.targetUI.gameObject).Equals(StageUtility.GetStageHandle(mask.gameObject)));
            try
            {
                if (!Application.IsPlaying(mask.gameObject)) Previews.Add(mask);
                // Playing instances use LateUpdate; Inspector requests must survive until it runs.
                if (!Application.IsPlaying(mask.gameObject) || force || !sameStage)
                    mask.RefreshEditorPreview(force, sameStage);
                if (force || mask.IsPreparing)
                { EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll(); editor.Repaint(); }
            }
            catch (System.Exception exception) { Debug.LogException(exception, mask); }
        }
    }
    private static void RefreshObserved()
    { foreach (FEasyUIMaskEditor editor in Editors) if (editor != null) Request(editor.Mask); }
    private static void ClearPreviews()
    {
        foreach (FEasyUIMask mask in Previews) if (mask != null) mask.ReleaseEditorPreview();
        Previews.Clear(); Pending.Clear();
    }
    private static void SelectionChanged() { ClearPreviews(); RefreshObserved(); }
    private static void PlayModeChanged(PlayModeStateChange state)
    {
        ClearPreviews();
        if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode) RefreshObserved();
    }
    private static void Shutdown()
    {
        ClearPreviews();
        EditorApplication.update -= Update;
        Undo.undoRedoPerformed -= RefreshObserved;
        Selection.selectionChanged -= SelectionChanged;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
        EditorApplication.quitting -= Shutdown;
        Editors.Clear();
    }
}
