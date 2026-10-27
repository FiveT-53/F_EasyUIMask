using UnityEngine;
using UnityEngine.UI;

namespace F_EasyUIMask
{
[AddComponentMenu("UI/F_Easy UI Mask")]
[RequireComponent(typeof(CanvasRenderer))]
public class FEasyUIMask : MaskableGraphic, ICanvasRaycastFilter
{
    private RectTransform _targetArea;
    private Vector2 _targetMin = Vector2.zero;
    private Vector2 _targetMax = Vector2.zero;
    private bool _hasAppliedState;
    private bool _appliedInvertMask;
    private bool _pendingRefresh;
    private bool _pendingForceVertices;
    private bool _isRefreshing;
    private string _lastFailure;
    private Object _lastFailureContext;

    public enum AreaSelectMode
    {
        Manual,
        UITarget
    }
    [HideInInspector] public bool realTimeUpdate = false;
    [HideInInspector] public AreaSelectMode areaSelectMode;
    [HideInInspector] public Vector2 targetOffset = Vector2.zero;
    [HideInInspector] public Vector2 targetSize = new Vector2(50, 50);
    [HideInInspector] public RectTransform targetUI;
    [HideInInspector] public bool invertMask = false;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    public void Init()
    {
        if (IsPlayingObject())
            EnsureInitialized();
    }

    private bool IsPlayingObject()
    {
        // Also excludes Prefab Stage objects while the Editor is in Play Mode.
        return Application.IsPlaying(gameObject);
    }

    private void EnsureInitialized()
    {
        // Never claim a user object by name. Serialized ownership recovery is finished in batch 4.
        if (_targetArea != null && _targetArea.parent == transform && _targetArea.childCount == 0)
            return;

        _targetArea = null;
        GameObject area = new GameObject("TargetArea", typeof(RectTransform), typeof(LayoutElement));
        area.transform.SetParent(transform, false);
        area.layer = gameObject.layer;
        area.GetComponent<LayoutElement>().ignoreLayout = true;
        _targetArea = (RectTransform)area.transform;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        _hasAppliedState = false;
        RequestRefresh(false);
    }

    protected override void OnDisable()
    {
        _pendingRefresh = false;
        _pendingForceVertices = false;
        _hasAppliedState = false;
        base.OnDisable();
    }

    protected override void OnTransformParentChanged()
    {
        base.OnTransformParentChanged();
        RequestRefresh(false);
    }

    protected override void OnCanvasHierarchyChanged()
    {
        base.OnCanvasHierarchyChanged();
        RequestRefresh(false);
    }

    private void RequestRefresh(bool forceVertices)
    {
        _pendingRefresh = true;
        _pendingForceVertices |= forceVertices;
    }

    public void RefreshMask()
    {
        RefreshMaskInternal(true, true);
    }

    private void RefreshMaskInternal(bool forceVertices, bool activateSelf)
    {
        if (!IsPlayingObject())
            return;
        if (_isRefreshing)
        {
            RequestRefresh(forceVertices);
            return;
        }

        _isRefreshing = true;
        try
        {
            if (activateSelf && !gameObject.activeSelf)
                gameObject.SetActive(true);
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                RequestRefresh(forceVertices);
                return;
            }

            // Consume the current request before callbacks can enqueue another one.
            forceVertices |= _pendingForceVertices;
            _pendingRefresh = false;
            _pendingForceVertices = false;

            bool candidateInvert = invertMask;
            Vector2 candidateMin;
            Vector2 candidateMax;
            string failure;
            Object failureContext;
            if (!TryCalculateTargetBounds(out candidateMin, out candidateMax, out failure, out failureContext))
            {
                InvalidateAppliedState(forceVertices);
                ReportFailure(failure, failureContext);
                return;
            }

            EnsureInitialized();
            UpdateTargetArea(candidateMin, candidateMax);
            // Resource callbacks may disable or close the component during a refresh.
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                InvalidateAppliedState(false);
                return;
            }

            CommitAppliedState(candidateMin, candidateMax, candidateInvert, forceVertices);
            _lastFailure = null;
            _lastFailureContext = null;
        }
        catch
        {
            InvalidateAppliedState(true);
            throw;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private bool TryCalculateTargetBounds(out Vector2 min, out Vector2 max,
        out string failure, out Object failureContext)
    {
        min = max = Vector2.zero;
        failure = null;
        failureContext = this;
        Canvas currentCanvas = canvas;
        if (currentCanvas == null)
        {
            failure = "Mask must be under a Canvas.";
            return false;
        }

        Canvas rootCanvas = currentCanvas.rootCanvas;
        Camera currentCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && currentCamera == null)
        {
            failure = "Canvas requires an explicit worldCamera.";
            failureContext = rootCanvas;
            return false;
        }

        Vector2 center;
        Vector2 size;
        if (areaSelectMode == AreaSelectMode.Manual)
        {
            // Transitional geometry: batch 3 fixes the original center-anchor convention.
            center = rectTransform.rect.center + targetOffset;
            size = targetSize;
        }
        else if (areaSelectMode == AreaSelectMode.UITarget)
        {
            if (targetUI == null)
            {
                failure = "Target UI is missing.";
                return false;
            }

            // Transitional geometry: batch 3 replaces local copying with four-corner projection.
            size = Vector2.Scale(rectTransform.rect.size, targetUI.anchorMax - targetUI.anchorMin) + targetUI.sizeDelta;
            center = (Vector2)targetUI.localPosition
                + Vector2.Scale(new Vector2(0.5f, 0.5f) - targetUI.pivot, size);
        }
        else
        {
            failure = "Unknown area selection mode.";
            return false;
        }

        min = center - size * 0.5f;
        max = center + size * 0.5f;
        if (!IsFinite(min.x) || !IsFinite(min.y) || !IsFinite(max.x) || !IsFinite(max.y)
            || size.x < 0 || size.y < 0)
        {
            failure = "Target bounds must be finite with non-negative size.";
            failureContext = areaSelectMode == AreaSelectMode.UITarget ? (Object)targetUI : this;
            return false;
        }
        return true;
    }

    private void UpdateTargetArea(Vector2 min, Vector2 max)
    {
        _targetArea.anchorMin = _targetArea.anchorMax = new Vector2(0.5f, 0.5f);
        _targetArea.pivot = new Vector2(0.5f, 0.5f);
        _targetArea.localRotation = Quaternion.identity;
        _targetArea.localScale = Vector3.one;
        _targetArea.sizeDelta = max - min;
        Vector2 center = min * 0.5f + max * 0.5f;
        _targetArea.localPosition = new Vector3(center.x, center.y, 0);
        _targetArea.gameObject.layer = gameObject.layer;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void CommitAppliedState(Vector2 min, Vector2 max, bool appliedInvert, bool forceVertices)
    {
        bool changed = !_hasAppliedState || _targetMin.x != min.x || _targetMin.y != min.y
            || _targetMax.x != max.x || _targetMax.y != max.y || _appliedInvertMask != appliedInvert;
        _targetMin = min;
        _targetMax = max;
        _appliedInvertMask = appliedInvert;
        _hasAppliedState = true;
        if (forceVertices || changed)
            SetVerticesDirty();
    }

    private void InvalidateAppliedState(bool forceVertices)
    {
        bool hadState = _hasAppliedState;
        _hasAppliedState = false;
        if (hadState || forceVertices)
        {
            canvasRenderer.Clear();
            SetVerticesDirty();
        }
    }

    private void ReportFailure(string failure, Object context)
    {
        if (_lastFailure == failure && _lastFailureContext == context)
            return;
        _lastFailure = failure;
        _lastFailureContext = context;
        Debug.LogWarning("FEasyUIMask: " + failure, this);
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!raycastTarget)
            return true;
        if (!_hasAppliedState || !isActiveAndEnabled)
            return false;

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out localPoint))
            return false;

        // Outer clipping and boundary cases are integrated with the mesh in batch 4.
        bool inTarget = _targetMax.x > _targetMin.x && _targetMax.y > _targetMin.y
            && localPoint.x >= _targetMin.x && localPoint.x <= _targetMax.x
            && localPoint.y >= _targetMin.y && localPoint.y <= _targetMax.y;
        return _appliedInvertMask ? !inTarget : inTarget;
    }

    /// <summary>
    /// Update Vertex Data When Rendering
    /// </summary>
    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        toFill.Clear();
        if (!_hasAppliedState)
            return;

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

        if (_appliedInvertMask)
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
        if (IsPlayingObject() && isActiveAndEnabled && gameObject.activeInHierarchy
            && (_pendingRefresh || realTimeUpdate))
            RefreshMaskInternal(false, false);
    }

    private void Close()
    {
        InvalidateAppliedState(true);
        gameObject.SetActive(false);
    }
}
}
