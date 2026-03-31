using UnityEditor;
using UnityEngine;
using FEasyUIMask;

[CustomEditor(typeof(FEasyUIMask))]
public class FEasyUIMaskEditor : Editor
{
    [Tooltip("Dropdown State")]
    private bool showGraphic = true;
    private bool showMaskArea = true;

    [Tooltip("Region Edit")]
    private bool isEditingArea;
    private static readonly GUIContent editButtonContent = new GUIContent("Edit Area", "Edit mask area");

    [Tooltip("Resources")]
    const string k_IconGUID = "BnsftC77AXl7eybUv3/IY4nGpgBOiO9RzYDRbmfNo5T217TfQMjAsw4=";

    void OnEnable()
    {
        SceneView.duringSceneGui += OnScene;

        string iconPath = AssetDatabase.GUIDToAssetPath(k_IconGUID);
        if (!string.IsNullOrEmpty(iconPath))
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (icon != null)
                EditorGUIUtility.SetIconForObject(target, icon);
        }
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnScene;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var script = (FEasyUIMask)target;
        if (script.GetComponent<CanvasRenderer>() == null)
        {
            EditorGUILayout.HelpBox("CanvasRenderer is missing! Click Fix to add it.", MessageType.Warning);
            if (GUILayout.Button("Fix Missing CanvasRenderer"))
            {
                Undo.AddComponent<CanvasRenderer>(script.gameObject);
            }
            EditorGUILayout.Space();
        }

        showGraphic = EditorGUILayout.Foldout(showGraphic, "Graphic", true);
        if (showGraphic)
        {
            DrawDefaultInspector();
        }

        EditorGUILayout.Space();

        showMaskArea = EditorGUILayout.Foldout(showMaskArea, "Mask Area Setting", true);
        if (showMaskArea)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                var realTimeProperty = serializedObject.FindProperty("realTimeUpdate");
                PropertyField("realTimeUpdate", "Real Time Update");
                if (realTimeProperty.boolValue)
                {
                    EditorGUILayout.HelpBox("Real time update may cause significant performance overhead. Use with caution.", MessageType.Warning);
                }

                EditorGUILayout.Space();

                PropertyField("areaSelectMode", "Target Area Select Mode");
                var mode = (FEasyUIMask.AreaSelectMode)serializedObject.FindProperty("areaSelectMode").enumValueIndex;

                EditorGUILayout.Space();

                if (mode == FEasyUIMask.AreaSelectMode.Manual)
                {
                    bool newEditing = GUILayout.Toggle(isEditingArea, editButtonContent, "Button");
                    if (newEditing != isEditingArea)
                    {
                        isEditingArea = newEditing;
                        SceneView.RepaintAll();
                    }

                    EditorGUILayout.Space();

                    PropertyField("targetOffset", "Offset");
                    PropertyField("targetSize", "Size");
                }
                else
                {
                    PropertyField("targetUI", "Target");
                }

                EditorGUILayout.Space();
                PropertyField("invertMask", "Invert Mask");
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void OnScene(SceneView view)
    {
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        var script = target as FEasyUIMask;
        if (!script || script.areaSelectMode != FEasyUIMask.AreaSelectMode.Manual) return;

        RectTransform rt = script.rectTransform;
        Vector2 offset = script.targetOffset;
        Vector2 size = script.targetSize;

        Vector2 min = offset - size / 2f;
        Vector2 max = offset + size / 2f;

        Vector3[] corners = new Vector3[4];
        corners[0] = rt.TransformPoint(min.x, min.y, 0);
        corners[1] = rt.TransformPoint(max.x, min.y, 0);
        corners[2] = rt.TransformPoint(max.x, max.y, 0);
        corners[3] = rt.TransformPoint(min.x, max.y, 0);

        //Draw Region
        Handles.color = new Color(0, 1, 0.5f, 0.8f);
        Handles.DrawPolyLine(corners[0], corners[1], corners[2], corners[3], corners[0]);
        //Handles.DrawSolidRectangleWithOutline(corners, new Color(0, 0.5f, 0.2f, 0.1f), Color.green);

        if (!isEditingArea) return;

        //Offset Handle
        EditorGUI.BeginChangeCheck();
        Vector3 centerWorld = rt.TransformPoint(offset.x, offset.y, 0);
        Vector3 newCenterWorld = Handles.FreeMoveHandle(
            centerWorld, HandleUtility.GetHandleSize(centerWorld) * 0.1f, Vector3.one, Handles.RectangleHandleCap);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(script, "Edit Offset");
            script.targetOffset = rt.InverseTransformPoint(newCenterWorld);
        }

        //Four-sided Handles
        Vector3 topCenter = rt.TransformPoint((min.x + max.x) / 2f, max.y, 0);
        Vector3 bottomCenter = rt.TransformPoint((min.x + max.x) / 2f, min.y, 0);
        Vector3 leftCenter = rt.TransformPoint(min.x, (min.y + max.y) / 2f, 0);
        Vector3 rightCenter = rt.TransformPoint(max.x, (min.y + max.y) / 2f, 0);

        EditorGUI.BeginChangeCheck();

        Vector3 newTopCenter = Handles.FreeMoveHandle(
            topCenter,
            HandleUtility.GetHandleSize(topCenter) * 0.08f,
            Vector3.one,
            Handles.SphereHandleCap);

        Vector3 newBottomCenter = Handles.FreeMoveHandle(
            bottomCenter,
            HandleUtility.GetHandleSize(bottomCenter) * 0.08f,
            Vector3.one,
            Handles.SphereHandleCap);

        Vector3 newLeftCenter = Handles.FreeMoveHandle(
            leftCenter,
            HandleUtility.GetHandleSize(leftCenter) * 0.08f,
            Vector3.one,
            Handles.SphereHandleCap);

        Vector3 newRightCenter = Handles.FreeMoveHandle(
            rightCenter,
            HandleUtility.GetHandleSize(rightCenter) * 0.08f,
            Vector3.one,
            Handles.SphereHandleCap);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(script, "Edit Area");

            Vector2 localCenter = rt.InverseTransformPoint(newCenterWorld);
            Vector2 localTop = rt.InverseTransformPoint(newTopCenter);
            Vector2 localBottom = rt.InverseTransformPoint(newBottomCenter);
            Vector2 localLeft = rt.InverseTransformPoint(newLeftCenter);
            Vector2 localRight = rt.InverseTransformPoint(newRightCenter);

            float newMinX = Mathf.Min(localLeft.x, localRight.x);
            float newMaxX = Mathf.Max(localLeft.x, localRight.x);
            float newMinY = Mathf.Min(localBottom.y, localTop.y);
            float newMaxY = Mathf.Max(localBottom.y, localTop.y);

            script.targetSize = new Vector2(newMaxX - newMinX, newMaxY - newMinY);
            script.targetOffset = new Vector2((newMinX + newMaxX) / 2f, (newMinY + newMaxY) / 2f);
        }
    }

    void PropertyField(string name, string label)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label));
    }
}
