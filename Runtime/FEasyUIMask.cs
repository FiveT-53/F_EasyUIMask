using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/F_Easy UI Mask")]
[RequireComponent(typeof(CanvasRenderer))]
public class FEasyUIMask : MaskableGraphic, ICanvasRaycastFilter
{
    [Tooltip("Target Region Parameters")]
    private RectTransform _targetArea;
    private Vector2 _targetMin = Vector2.zero;
    private Vector2 _targetMax = Vector2.zero;

    [Tooltip("Target Region Settings")]
    public enum AreaSelectMode
    {
        Manual,
        UITarget
    }
    [HideInInspector]public bool realTimeUpdate = false;
    [HideInInspector]public AreaSelectMode areaSelectMode;
    //Manual
    [HideInInspector]public Vector2 targetOffset = Vector2.zero;
    [HideInInspector]public Vector2 targetSize = new Vector2(50, 50);
    //UI
    [HideInInspector]public RectTransform targetUI;
    [HideInInspector]public bool invertMask = false;

    [Tooltip("Camera")]
    private Camera _camera;

    protected override void Awake()
    {
        base.Awake();

        Init();
    }

    public void Init()
    {
        _targetArea = transform.Find("TargetArea") as RectTransform;
        if (_targetArea == null)
        {
            CreateTargetArea();
        }

        _camera = canvas.worldCamera;

        Close();
    }

    private void CreateTargetArea()
    {
        GameObject area = new GameObject("TargetArea");
        area.transform.SetParent(transform);
        area.layer = gameObject.layer;

        RectTransform rect = area.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;

        _targetArea = rect;
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!raycastTarget)
            return true;

        if (_targetArea == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(_targetArea, screenPoint, eventCamera);
    }

    public void RefreshMask()
    {
        if(areaSelectMode == AreaSelectMode.Manual)
        {
            RefreshMaskByArea();
        }
        else
        {
            RefreshMaskByUI();
        }

        //Force update
        _targetArea.ForceUpdateRectTransforms();
        RefreshView();
    }

    private void RefreshMaskByArea()
    {
        gameObject.SetActive(true);

        Vector3 targetCenterWorld = rectTransform.TransformPoint(targetOffset);
        var screenPoint = RectTransformUtility.WorldToScreenPoint(_camera, targetCenterWorld);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, _camera, out Vector2 localPoint))
        {
            Close();
            return;
        }

        _targetArea.anchorMin = new Vector2(0.5f, 0.5f);
        _targetArea.anchorMax = new Vector2(0.5f, 0.5f);
        _targetArea.pivot = new Vector2(0.5f, 0.5f);
        _targetArea.anchoredPosition = targetOffset;
        _targetArea.sizeDelta = targetSize;
    }

    private void RefreshMaskByUI()
    {
        if (targetUI == null)
        {
            Debug.LogError("Did not find target UI");
            Close();
            return;
        }

        gameObject.SetActive(true);

        var screenPoint = RectTransformUtility.WorldToScreenPoint(_camera, targetUI.position);


        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, _camera, out Vector2 localPoint))
        {
            Close();
            return;
        }

        _targetArea.anchorMax = targetUI.anchorMax;
        _targetArea.anchorMin = targetUI.anchorMin;
        _targetArea.anchoredPosition = targetUI.anchoredPosition;
        _targetArea.anchoredPosition3D = targetUI.anchoredPosition3D;
        _targetArea.offsetMax = targetUI.offsetMax;
        _targetArea.offsetMin = targetUI.offsetMin;
        _targetArea.pivot = targetUI.pivot;
        _targetArea.sizeDelta = targetUI.sizeDelta;
        _targetArea.localPosition = targetUI.localPosition;
    }

        private void RefreshView()
    {
        Vector2 newMin;
        Vector2 newMax;

        if(_targetArea != null && _targetArea.gameObject.activeSelf)
        {
            //Get bounds
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(transform, _targetArea);
            newMin = bounds.min;
            newMax = bounds.max;
        }
        else
        {
            newMin = Vector2.zero;
            newMax = Vector2.zero;
        }

        if(_targetMin != newMin || _targetMax != newMax)
        {
            _targetMin = newMin;
            _targetMax = newMax;
            SetAllDirty();
        }
    }

    /// <summary>
    /// Update Vertex Data When Rendering
    /// </summary>
    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        toFill.Clear();

        var maskRect = rectTransform.rect;

        var maskRectLeftTop = new Vector2(-maskRect.width/2, maskRect.height/2);
        var maskRectLeftBottom = new Vector2(-maskRect.width/2, -maskRect.height/2);
        var maskRectRightTop = new Vector2(maskRect.width/2, maskRect.height/2);
        var maskRectRightBottom = new Vector2(maskRect.width/2, -maskRect.height/2);

        var targetRectLeftTop = new Vector2(_targetMin.x, _targetMax.y);
        var targetRectLeftBottom = _targetMin;
        var targetRectRightTop = _targetMax;
        var targetRectRightBottom = new Vector2(_targetMax.x, _targetMin.y);

        //Add Point(0~7)
        toFill.AddVert(maskRectLeftBottom, color, Vector2.zero);    //0
        toFill.AddVert(targetRectLeftBottom, color, Vector2.zero);  //1
        toFill.AddVert(targetRectRightBottom, color, Vector2.zero); //2
        toFill.AddVert(maskRectRightBottom, color, Vector2.zero);   //3
        toFill.AddVert(targetRectRightTop, color, Vector2.zero);    //4
        toFill.AddVert(maskRectRightTop, color, Vector2.zero);      //5
        toFill.AddVert(targetRectLeftTop, color, Vector2.zero);     //6
        toFill.AddVert(maskRectLeftTop, color, Vector2.zero);       //7

        if (invertMask)
        {
            toFill.AddTriangle(0, 1, 2);
            toFill.AddTriangle(2, 3, 0);
            toFill.AddTriangle(3, 2, 4);
            toFill.AddTriangle(4, 5, 3);
            toFill.AddTriangle(6, 7, 5);
            toFill.AddTriangle(5, 4, 6);
            toFill.AddTriangle(7, 6, 1);
            toFill.AddTriangle(1, 0, 7);          
        }
        else
        {
            toFill.AddTriangle(1, 4, 2);
            toFill.AddTriangle(1, 6, 4);
        }

    }

    void LateUpdate()
    {
        if (realTimeUpdate)
        {
            RefreshMask();
        }
    }

    private void Close()
    {
        gameObject.SetActive(false);
    }
}
