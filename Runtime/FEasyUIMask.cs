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
    private readonly Vector3[] _worldCorners = new Vector3[4];

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

        if (areaSelectMode == AreaSelectMode.Manual)
        {
            if (TryCalculateManualBounds(targetOffset, targetSize, out min, out max))
                return true;
            failure = "Manual offset and size must be finite and within the supported numeric range.";
            return false;
        }
        if (areaSelectMode != AreaSelectMode.UITarget)
        {
            failure = "Unknown area selection mode.";
            return false;
        }
        if (targetUI == null)
        {
            failure = "Target UI is missing.";
            return false;
        }

        failureContext = targetUI;
        Canvas sourceCanvas = targetUI.GetComponentInParent<Canvas>();
        Camera sourceCamera;
        Camera destinationCamera;
        int sourceDisplay;
        int destinationDisplay;
        if (!TryGetProjection(sourceCanvas, out sourceCamera, out sourceDisplay, out failure)
            || !TryGetProjection(currentCanvas, out destinationCamera, out destinationDisplay, out failure))
            return false;
        if (sourceDisplay != destinationDisplay)
        {
            failure = "Cross-display projection is not supported.";
            return false;
        }

        if (!HasInvertibleTransform(targetUI) || !HasInvertibleTransform(rectTransform)
            || targetUI.rect.width <= 0 || targetUI.rect.height <= 0)
        {
            failure = "Target and mask transforms must be invertible, and the target must have positive area.";
            return false;
        }

        targetUI.GetWorldCorners(_worldCorners);
        Vector2 first = Vector2.zero;
        Vector2 previous = Vector2.zero;
        double twiceArea = 0;
        for (int i = 0; i < 4; i++)
        {
            Vector3 world = _worldCorners[i];
            Vector3 screen = sourceCamera == null ? world : sourceCamera.WorldToScreenPoint(world);
            if (!IsFinite(screen.x) || !IsFinite(screen.y) || !IsFinite(screen.z)
                || (sourceCamera != null && !WithinClipDepth(screen.z, sourceCamera)))
            {
                failure = "Target crosses the camera clip range or has invalid projected corners.";
                return false;
            }

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, destinationCamera, out local)
                || !IsFinite(local.x) || !IsFinite(local.y)
                || (destinationCamera != null
                    && !WithinClipDepth(destinationCamera.WorldToScreenPoint(rectTransform.TransformPoint(local)).z, destinationCamera)))
            {
                failure = "Target cannot be projected onto the mask plane within the camera clip range.";
                return false;
            }

            if (i == 0)
                first = min = max = local;
            else
            {
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
                twiceArea += (double)previous.x * local.y - (double)previous.y * local.x;
            }
            previous = local;
        }
        twiceArea += (double)previous.x * first.y - (double)previous.y * first.x;
        if (twiceArea == 0 || double.IsNaN(twiceArea) || double.IsInfinity(twiceArea)
            || !IsFinite(max.x - min.x) || !IsFinite(max.y - min.y)
            || max.x <= min.x || max.y <= min.y)
        {
            failure = "Projected target has no usable area.";
            return false;
        }
        return true;
    }

    private static bool TryCalculateManualBounds(Vector2 offset, Vector2 size, out Vector2 min, out Vector2 max)
    {
        min = max = Vector2.zero;
        if (!IsFinite(offset.x) || !IsFinite(offset.y) || !IsFinite(size.x) || !IsFinite(size.y))
            return false;
        size = new Vector2(Mathf.Max(0, size.x), Mathf.Max(0, size.y));
        min = offset - size * 0.5f;
        max = offset + size * 0.5f;
        return IsFinite(min.x) && IsFinite(min.y) && IsFinite(max.x) && IsFinite(max.y);
    }

    private static bool HasInvertibleTransform(RectTransform rect)
    {
        float determinant = rect.localToWorldMatrix.determinant;
        return IsFinite(determinant) && determinant != 0;
    }

    private static bool WithinClipDepth(float depth, Camera camera)
    {
        return IsFinite(depth) && depth >= camera.nearClipPlane && depth <= camera.farClipPlane;
    }

    private static bool TryGetProjection(Canvas canvas, out Camera camera, out int display, out string failure)
    {
        camera = null;
        display = 0;
        failure = null;
        if (canvas == null)
        {
            failure = "Target must be under a Canvas.";
            return false;
        }
        Canvas root = canvas.rootCanvas;
        if (root.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            display = root.targetDisplay;
            return true;
        }
        camera = root.worldCamera;
        if (camera == null)
        {
            failure = "Camera and World Space canvases require an explicit worldCamera.";
            return false;
        }
        display = camera.targetDisplay;
        if (camera.targetTexture != null || camera.stereoEnabled)
        {
            failure = "RenderTexture and stereo camera projection are not supported.";
            return false;
        }
        return true;
    }

    private void UpdateTargetArea(Vector2 min, Vector2 max)
    {
        _targetArea.anchorMin = _targetArea.anchorMax = rectTransform.pivot;
        _targetArea.pivot = new Vector2(0.5f, 0.5f);
        _targetArea.localRotation = Quaternion.identity;
        _targetArea.localScale = Vector3.one;
        _targetArea.sizeDelta = max - min;
        Vector2 center = min * 0.5f + max * 0.5f;
        _targetArea.anchoredPosition3D = new Vector3(center.x, center.y, 0);
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

        var maskRectLeftTop = new Vector2(maskRect.xMin, maskRect.yMax);
        var maskRectLeftBottom = new Vector2(maskRect.xMin, maskRect.yMin);
        var maskRectRightTop = new Vector2(maskRect.xMax, maskRect.yMax);
        var maskRectRightBottom = new Vector2(maskRect.xMax, maskRect.yMin);

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
