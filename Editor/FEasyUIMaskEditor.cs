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
    private sealed class Preview
    {
        internal FEasyUIMask Mask;
        internal bool Ready, Preparing;
    }
    private static readonly Dictionary<FEasyUIMask, Preview> Previews = new Dictionary<FEasyUIMask, Preview>();
    private static readonly List<Preview> Work = new List<Preview>();
    private static readonly List<FEasyUIMask> Retire = new List<FEasyUIMask>();
    private static readonly List<FEasyUIMask> Requests = new List<FEasyUIMask>();
    private static readonly List<FEasyUIMaskEditor> Repaints = new List<FEasyUIMaskEditor>();
    private static GameObject selectedObject;
    private static FEasyUIMask selectedMask;
    private static bool singleSelection, clearing;

    static FEasyUIMaskPreviewQueue()
    {
        EditorApplication.update += Update;
        Undo.undoRedoPerformed += RefreshObserved;
        Selection.selectionChanged += SelectionChanged;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
        EditorApplication.quitting += Shutdown;
        CacheSelection();
    }
    internal static void Register(FEasyUIMaskEditor editor)
    {
        Editors.Add(editor);
        FEasyUIMask mask = editor.Mask;
        if (mask != null && singleSelection && selectedObject == mask.gameObject) selectedMask = mask;
        EnsurePreview(mask);
    }
    internal static void Unregister(FEasyUIMaskEditor editor)
    {
        Editors.Remove(editor);
        // Unity may replace Editors while the same object remains selected.
        // Selection/stage/object lifetime owns the preview, not this observer.
    }
    internal static void Request(FEasyUIMask mask)
    { if (mask != null) { EnsurePreview(mask); Pending.Add(mask); } }
    private static bool IsAvailable(FEasyUIMask mask)
    {
        return mask != null && !EditorUtility.IsPersistent(mask) && mask.isActiveAndEnabled
            && mask.gameObject.scene.IsValid() && mask.gameObject.scene.isLoaded
            && StageUtility.GetStageHandle(mask.gameObject).Equals(StageUtility.GetCurrentStageHandle());
    }
    private static bool CanPreview(FEasyUIMask mask)
    {
        return IsAvailable(mask) && !Application.IsPlaying(mask.gameObject)
            && singleSelection && selectedObject == mask.gameObject;
    }
    private static void EnsurePreview(FEasyUIMask mask)
    {
        if (clearing || !CanPreview(mask) || Previews.ContainsKey(mask)) return;
        Previews.Add(mask, new Preview { Mask = mask });
        Pending.Add(mask);
    }
    private static bool SameStage(FEasyUIMask mask)
    {
        return mask.targetUI == null || (!EditorUtility.IsPersistent(mask.targetUI)
            && mask.targetUI.gameObject.scene.IsValid() && mask.targetUI.gameObject.scene.isLoaded
            && StageUtility.GetStageHandle(mask.targetUI.gameObject).Equals(StageUtility.GetStageHandle(mask.gameObject)));
    }
    private static void CacheSelection()
    {
        selectedObject = Selection.activeGameObject;
        singleSelection = selectedObject != null && Selection.gameObjects.Length == 1;
        selectedMask = singleSelection ? selectedObject.GetComponent<FEasyUIMask>() : null;
    }
    private static void Update()
    {
        if (EditorApplication.isCompiling || (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode)) return;
        Editors.RemoveWhere(editor => editor == null);
        EnsurePreview(selectedMask);
        Work.Clear();
        foreach (Preview preview in Previews.Values) Work.Add(preview);
        bool repaint = false;
        // Refresh can rebuild the Inspector and change its Editor registrations.
        // Iterate object sessions through a reusable work list instead.
        foreach (Preview preview in Work)
        {
            FEasyUIMask mask = preview.Mask;
            Preview current;
            if (!Previews.TryGetValue(mask, out current) || current != preview) continue;
            if (!CanPreview(mask))
            {
                Previews.Remove(mask); Pending.Remove(mask);
                if (mask != null) mask.ReleaseEditorPreview();
                continue;
            }
            bool force = Pending.Remove(mask) | mask.ConsumeEditorRefreshRequest();
            try
            {
                mask.RefreshEditorPreview(force, SameStage(mask));
                repaint |= force || mask.IsPreparing || preview.Ready != mask.IsReady || preview.Preparing != mask.IsPreparing;
                preview.Ready = mask.IsReady; preview.Preparing = mask.IsPreparing;
            }
            catch (System.Exception exception) { Debug.LogException(exception, mask); }
        }
        Requests.Clear();
        foreach (FEasyUIMask mask in Pending)
            if (!Previews.ContainsKey(mask)) Requests.Add(mask);
        foreach (FEasyUIMask mask in Requests)
        {
            Pending.Remove(mask);
            // Actual playing instances retain their LateUpdate path. Inspector
            // requests queue work without activating the host or its parents.
            if (!IsAvailable(mask) || !Application.IsPlaying(mask.gameObject)) continue;
            try { mask.RefreshEditorPreview(true, SameStage(mask)); repaint = true; }
            catch (System.Exception exception) { Debug.LogException(exception, mask); }
        }
        if (repaint)
        {
            EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
            Repaints.Clear();
            foreach (FEasyUIMaskEditor editor in Editors) if (editor != null) Repaints.Add(editor);
            foreach (FEasyUIMaskEditor editor in Repaints) if (editor != null) editor.Repaint();
        }
    }
    private static void RefreshObserved()
    {
        Request(selectedMask);
        foreach (FEasyUIMaskEditor editor in Editors) if (editor != null) Request(editor.Mask);
    }
    private static void ClearPreviews()
    {
        if (clearing) return;
        clearing = true;
        try
        {
            Retire.Clear();
            foreach (FEasyUIMask mask in Previews.Keys) Retire.Add(mask);
            Previews.Clear(); Pending.Clear();
            foreach (FEasyUIMask mask in Retire) if (mask != null) mask.ReleaseEditorPreview();
            Retire.Clear();
        }
        finally { clearing = false; }
    }
    private static void SelectionChanged() { ClearPreviews(); CacheSelection(); EnsurePreview(selectedMask); }
    private static void PlayModeChanged(PlayModeStateChange state)
    {
        ClearPreviews();
        if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)
        { CacheSelection(); EnsurePreview(selectedMask); RefreshObserved(); }
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
