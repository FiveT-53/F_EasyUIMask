using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using F_EasyUIMask;

[CustomEditor(typeof(FEasyUIMask))]
public class FEasyUIMaskEditor : Editor
{
    private bool showGraphic = true;
    private bool showMaskArea = true;
    private bool isEditingArea;
    private int ownedHotControl;
    private readonly Vector3[] corners = new Vector3[4];
    internal FEasyUIMask Mask { get { return target as FEasyUIMask; } }

    private void OnEnable()
    {
        FEasyUIMaskRefreshQueue.Register(this);
    }

    private void OnDisable()
    {
        EndEditing();
        FEasyUIMaskRefreshQueue.Unregister(this);
    }

    internal void EndEditing()
    {
        isEditingArea = false;
        ReleaseOwnedControl();
    }

    private void ReleaseOwnedControl()
    {
        if (ownedHotControl != 0 && GUIUtility.hotControl == ownedHotControl)
            GUIUtility.hotControl = 0;
        ownedHotControl = 0;
    }

    public override void OnInspectorGUI()
    {
        if (targets.Length != 1 || serializedObject.isEditingMultipleObjects)
        {
            EditorGUILayout.HelpBox("Select one mask to edit its area.", MessageType.Info);
            return;
        }
        FEasyUIMask mask = Mask;
        if (mask == null)
            return;
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        if (mask.GetComponent<CanvasRenderer>() == null)
        {
            EditorGUILayout.HelpBox("CanvasRenderer is missing.", MessageType.Warning);
            if (GUILayout.Button("Fix Missing CanvasRenderer"))
                Undo.AddComponent<CanvasRenderer>(mask.gameObject);
        }

        showGraphic = EditorGUILayout.Foldout(showGraphic, "Graphic", true);
        if (showGraphic)
            DrawPropertiesExcluding(serializedObject, "m_Script", "realTimeUpdate", "areaSelectMode", "targetOffset", "targetSize", "targetUI", "invertMask");

        showMaskArea = EditorGUILayout.Foldout(showMaskArea, "Mask Area Settings", true);
        if (showMaskArea)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                PropertyField("realTimeUpdate", "Real Time Update");
                PropertyField("areaSelectMode", "Target Area Select Mode");
                FEasyUIMask.AreaSelectMode mode = (FEasyUIMask.AreaSelectMode)serializedObject.FindProperty("areaSelectMode").enumValueIndex;
                if (mode == FEasyUIMask.AreaSelectMode.Manual)
                {
                    bool editing = GUILayout.Toggle(isEditingArea, "Edit Area", "Button");
                    if (editing != isEditingArea)
                    {
                        if (editing)
                            isEditingArea = true;
                        else
                            EndEditing();
                        SceneView.RepaintAll();
                    }
                    PropertyField("targetOffset", "Offset (from Pivot)");
                    SerializedProperty size = serializedObject.FindProperty("targetSize");
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(size, new GUIContent("Size"));
                    if (EditorGUI.EndChangeCheck())
                        size.vector2Value = Vector2.Max(Vector2.zero, size.vector2Value);
                }
                else
                {
                    EndEditing();
                    PropertyField("targetUI", "Target");
                }
                PropertyField("invertMask", "Invert Mask");
            }
        }
        if (serializedObject.ApplyModifiedProperties())
            FEasyUIMaskRefreshQueue.Request(mask, true);
        if (GUILayout.Button("Refresh Preview"))
            FEasyUIMaskRefreshQueue.Request(mask, true);
        if (!string.IsNullOrEmpty(mask.EditorPreviewFailure))
            EditorGUILayout.HelpBox(mask.EditorPreviewFailure, MessageType.Warning);
    }

    private void OnSceneGUI()
    {
        FEasyUIMask mask = Mask;
        if (mask == null || Selection.activeGameObject != mask.gameObject || Selection.gameObjects.Length != 1
            || !FEasyUIMaskRefreshQueue.CanRefresh(mask))
        {
            EndEditing();
            return;
        }
        if (mask.areaSelectMode != FEasyUIMask.AreaSelectMode.Manual)
        {
            EndEditing();
            return;
        }
        Event current = Event.current;
        if (isEditingArea && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
        {
            EndEditing();
            current.Use();
            Repaint();
            SceneView.RepaintAll();
            return;
        }

        serializedObject.Update();
        SerializedProperty offsetProperty = serializedObject.FindProperty("targetOffset");
        SerializedProperty sizeProperty = serializedObject.FindProperty("targetSize");
        Vector2 offset = offsetProperty.vector2Value;
        Vector2 size = Vector2.Max(Vector2.zero, sizeProperty.vector2Value);
        if (!Finite(offset.x) || !Finite(offset.y) || !Finite(size.x) || !Finite(size.y))
            return;
        RectTransform rt = mask.rectTransform;
        Vector2 min = offset - size * 0.5f;
        Vector2 max = offset + size * 0.5f;
        float determinant = rt.localToWorldMatrix.determinant;
        if (!Finite(min.x) || !Finite(min.y) || !Finite(max.x) || !Finite(max.y)
            || !Finite(determinant) || determinant == 0)
            return;
        corners[0] = rt.TransformPoint(min.x, min.y, 0);
        corners[1] = rt.TransformPoint(max.x, min.y, 0);
        corners[2] = rt.TransformPoint(max.x, max.y, 0);
        corners[3] = rt.TransformPoint(min.x, max.y, 0);
        Color previousColor = Handles.color;
        try
        {
            Handles.color = new Color(0, 1, 0.5f, 0.8f);
            Handles.DrawPolyLine(corners[0], corners[1], corners[2], corners[3], corners[0]);
            if (!isEditingArea)
                return;
            if (current.alt || Tools.current == Tool.View)
            {
                ReleaseOwnedControl();
                return;
            }
            if (current.type == EventType.Layout)
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID("FEasyUIMaskArea".GetHashCode(), FocusType.Passive));

            bool changed = false;
            Vector3 centerWorld = rt.TransformPoint(offset.x, offset.y, 0);
            EditorGUI.BeginChangeCheck();
            Vector3 newCenter = FreeMoveHandleCompat(centerWorld, HandleUtility.GetHandleSize(centerWorld) * 0.1f, Vector3.one, Handles.RectangleHandleCap, 0);
            if (EditorGUI.EndChangeCheck())
            {
                offset = rt.InverseTransformPoint(newCenter);
                min = offset - size * 0.5f;
                max = offset + size * 0.5f;
                changed = true;
            }

            Vector3 top = rt.TransformPoint(offset.x, max.y, 0);
            Vector3 bottom = rt.TransformPoint(offset.x, min.y, 0);
            Vector3 left = rt.TransformPoint(min.x, offset.y, 0);
            Vector3 right = rt.TransformPoint(max.x, offset.y, 0);
            EditorGUI.BeginChangeCheck();
            Vector3 newTop = FreeMoveHandleCompat(top, HandleUtility.GetHandleSize(top) * 0.08f, Vector3.one, Handles.SphereHandleCap, 1);
            Vector3 newBottom = FreeMoveHandleCompat(bottom, HandleUtility.GetHandleSize(bottom) * 0.08f, Vector3.one, Handles.SphereHandleCap, 2);
            Vector3 newLeft = FreeMoveHandleCompat(left, HandleUtility.GetHandleSize(left) * 0.08f, Vector3.one, Handles.SphereHandleCap, 3);
            Vector3 newRight = FreeMoveHandleCompat(right, HandleUtility.GetHandleSize(right) * 0.08f, Vector3.one, Handles.SphereHandleCap, 4);
            if (EditorGUI.EndChangeCheck())
            {
                float minX = Mathf.Min(rt.InverseTransformPoint(newLeft).x, rt.InverseTransformPoint(newRight).x);
                float maxX = Mathf.Max(rt.InverseTransformPoint(newLeft).x, rt.InverseTransformPoint(newRight).x);
                float minY = Mathf.Min(rt.InverseTransformPoint(newBottom).y, rt.InverseTransformPoint(newTop).y);
                float maxY = Mathf.Max(rt.InverseTransformPoint(newBottom).y, rt.InverseTransformPoint(newTop).y);
                size = new Vector2(maxX - minX, maxY - minY);
                offset = new Vector2(minX * 0.5f + maxX * 0.5f, minY * 0.5f + maxY * 0.5f);
                changed = true;
            }
            if (changed && Finite(offset.x) && Finite(offset.y) && Finite(size.x) && Finite(size.y))
            {
                offsetProperty.vector2Value = offset;
                sizeProperty.vector2Value = size;
                if (serializedObject.ApplyModifiedProperties())
                    FEasyUIMaskRefreshQueue.Request(mask, true);
            }
        }
        finally
        {
            Handles.color = previousColor;
        }
    }

    private static bool Finite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private Vector3 FreeMoveHandleCompat(Vector3 position, float size, Vector3 snap, Handles.CapFunction cap, int hint)
    {
        int controlId = GUIUtility.GetControlID(GetInstanceID() ^ hint, FocusType.Passive);
#if UNITY_2022_3_OR_NEWER
        Vector3 result = Handles.FreeMoveHandle(controlId, position, size, snap, cap);
#else
        Vector3 result = Handles.FreeMoveHandle(controlId, position, Quaternion.identity, size, snap, cap);
#endif
        if (GUIUtility.hotControl == controlId)
            ownedHotControl = controlId;
        else if (ownedHotControl == controlId)
            ownedHotControl = 0;
        return result;
    }

    private void PropertyField(string name, string label)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label));
    }
}

[InitializeOnLoad]
internal static class FEasyUIMaskRefreshQueue
{
    private static readonly HashSet<FEasyUIMaskEditor> Editors = new HashSet<FEasyUIMaskEditor>();
    private static readonly Dictionary<FEasyUIMask, bool> Pending = new Dictionary<FEasyUIMask, bool>();

    static FEasyUIMaskRefreshQueue()
    {
        EditorApplication.update += Update;
        Undo.undoRedoPerformed += RefreshObserved;
        Selection.selectionChanged += SelectionChanged;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
    }

    internal static void Register(FEasyUIMaskEditor editor)
    {
        Editors.Add(editor);
        Request(editor.Mask, true);
    }

    internal static void Unregister(FEasyUIMaskEditor editor)
    {
        Editors.Remove(editor);
        FEasyUIMask mask = editor.Mask;
        if (mask != null)
        {
            foreach (FEasyUIMaskEditor other in Editors)
                if (other != null && other.Mask == mask)
                    return;
            Pending.Remove(mask);
        }
    }

    internal static void Request(FEasyUIMask mask, bool force)
    {
        if (mask == null)
            return;
        bool previous;
        Pending.TryGetValue(mask, out previous);
        Pending[mask] = previous || force;
    }

    internal static bool CanRefresh(FEasyUIMask mask)
    {
        return mask != null && !EditorUtility.IsPersistent(mask)
            && mask.gameObject.scene.IsValid() && mask.gameObject.scene.isLoaded
            && StageUtility.GetStageHandle(mask.gameObject).Equals(StageUtility.GetCurrentStageHandle());
    }

    private static void Update()
    {
        if (EditorApplication.isCompiling || (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode))
            return;
        Editors.RemoveWhere(editor => editor == null);
        foreach (FEasyUIMaskEditor editor in Editors)
        {
            FEasyUIMask mask = editor.Mask;
            if (!CanRefresh(mask))
                continue;
            if (mask.ConsumeEditorRefreshRequest())
                Request(mask, true);
            if (mask.realTimeUpdate && !Application.IsPlaying(mask.gameObject)
                && Selection.activeGameObject == mask.gameObject && Selection.gameObjects.Length == 1)
                Request(mask, false);
        }
        if (Pending.Count == 0)
            return;
        var work = new List<KeyValuePair<FEasyUIMask, bool>>(Pending);
        Pending.Clear();
        bool refreshed = false;
        foreach (KeyValuePair<FEasyUIMask, bool> entry in work)
        {
            FEasyUIMask mask = entry.Key;
            if (!CanRefresh(mask))
                continue;
            bool sameStage = mask.areaSelectMode != FEasyUIMask.AreaSelectMode.UITarget || mask.targetUI == null
                || (!EditorUtility.IsPersistent(mask.targetUI) && mask.targetUI.gameObject.scene.IsValid()
                    && StageUtility.GetStageHandle(mask.targetUI.gameObject).Equals(StageUtility.GetStageHandle(mask.gameObject)));
            try
            {
                mask.RefreshEditorPreview(entry.Value, sameStage);
                refreshed = true;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, mask);
            }
        }
        if (refreshed)
        {
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }

    private static void RefreshObserved()
    {
        foreach (FEasyUIMaskEditor editor in Editors)
            if (editor != null)
                Request(editor.Mask, true);
    }

    private static void SelectionChanged()
    {
        Pending.Clear();
        foreach (FEasyUIMaskEditor editor in Editors)
            if (editor != null)
                editor.EndEditing();
        RefreshObserved();
    }

    private static void PlayModeChanged(PlayModeStateChange state)
    {
        Pending.Clear();
        foreach (FEasyUIMaskEditor editor in Editors)
            if (editor != null)
                editor.EndEditing();
        if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)
            RefreshObserved();
    }

    private static void Shutdown()
    {
        EditorApplication.update -= Update;
        Undo.undoRedoPerformed -= RefreshObserved;
        Selection.selectionChanged -= SelectionChanged;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
        Editors.Clear();
        Pending.Clear();
    }
}
