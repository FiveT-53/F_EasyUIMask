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
    private bool _reportedUnownedArea;
    private bool _reportedProtectedArea;

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
        if (IsOwnedArea(_targetArea) && IsSafeArea(_targetArea))
            return;

        if (_targetArea != null)
            ReportProtectedArea();
        _targetArea = null;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            RectTransform rect = child as RectTransform;
            if (IsOwnedArea(rect))
            {
                if (IsSafeArea(rect))
                    _targetArea = rect;
                else
                    ReportProtectedArea();
            }
            else if (child.name == "TargetArea" && !_reportedUnownedArea)
            {
                _reportedUnownedArea = true;
                Debug.LogWarning("FEasyUIMask: an unowned TargetArea was left untouched; a separate owned helper will be used.", this);
            }
        }
        if (_targetArea != null)
            return;

        GameObject area = new GameObject("TargetArea", typeof(RectTransform), typeof(LayoutElement), typeof(FEasyUIMaskTargetArea));
        area.transform.SetParent(transform, false);
        area.layer = gameObject.layer;
        area.GetComponent<FEasyUIMaskTargetArea>().Owner = this;
        area.GetComponent<LayoutElement>().ignoreLayout = true;
        _targetArea = (RectTransform)area.transform;
    }

    private bool IsOwnedArea(RectTransform area)
    {
        if (area == null || area.parent != transform)
            return false;
        FEasyUIMaskTargetArea marker = area.GetComponent<FEasyUIMaskTargetArea>();
        return marker != null && marker.Owner == this;
    }

    private static bool IsSafeArea(RectTransform area)
    {
        if (area.childCount != 0)
            return false;
        foreach (Component component in area.GetComponents<Component>())
        {
            if (!(component is RectTransform) && !(component is LayoutElement) && !(component is FEasyUIMaskTargetArea))
                return false;
        }
        return true;
    }

    private void ReportProtectedArea()
    {
        if (_reportedProtectedArea)
            return;
        _reportedProtectedArea = true;
        Debug.LogWarning("FEasyUIMask: a helper was moved or contains user content and has been left untouched.", this);
    }

    protected override void OnDestroy()
    {
        if (IsPlayingObject())
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                RectTransform area = transform.GetChild(i) as RectTransform;
                if (!IsOwnedArea(area))
                    continue;
                if (IsSafeArea(area))
                    Destroy(area.gameObject);
                else
                    ReportProtectedArea();
            }
        }
        base.OnDestroy();
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
        return IsFinite(min.x) && IsFinite(min.y) && IsFinite(max.x) && IsFinite(max.y)
            && IsFinite(max.x - min.x) && IsFinite(max.y - min.y);
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
        if (!IsOwnedArea(_targetArea) || !IsSafeArea(_targetArea))
        {
            ReportProtectedArea();
            _targetArea = null;
            return;
        }
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

        Rect outer = rectTransform.rect;
        if (!IsUsableOuter(outer) || !IsFinite(localPoint.x) || !IsFinite(localPoint.y) || !ContainsClosed(outer, localPoint))
            return false;
        Rect intersection;
        bool inTarget = TryGetIntersection(outer, _targetMin, _targetMax, out intersection)
            && ContainsClosed(intersection, localPoint);
        return _appliedInvertMask ? !inTarget : inTarget;
    }

    private static bool IsUsableOuter(Rect outer)
    {
        return IsFinite(outer.xMin) && IsFinite(outer.yMin) && IsFinite(outer.xMax) && IsFinite(outer.yMax)
            && IsFinite(outer.width) && IsFinite(outer.height) && outer.width > 0 && outer.height > 0;
    }

    private static bool TryGetIntersection(Rect outer, Vector2 min, Vector2 max, out Rect intersection)
    {
        float xMin = Mathf.Max(outer.xMin, min.x);
        float yMin = Mathf.Max(outer.yMin, min.y);
        float xMax = Mathf.Min(outer.xMax, max.x);
        float yMax = Mathf.Min(outer.yMax, max.y);
        intersection = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        return xMax > xMin && yMax > yMin;
    }

    private static bool ContainsClosed(Rect rect, Vector2 point)
    {
        return point.x >= rect.xMin && point.x <= rect.xMax && point.y >= rect.yMin && point.y <= rect.yMax;
    }

    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        toFill.Clear();
        Rect outer = rectTransform.rect;
        if (!_hasAppliedState || !IsUsableOuter(outer))
            return;

        Rect intersection;
        bool hasIntersection = TryGetIntersection(outer, _targetMin, _targetMax, out intersection);
        if (!_appliedInvertMask)
        {
            if (hasIntersection)
                AddQuad(toFill, intersection.xMin, intersection.yMin, intersection.xMax, intersection.yMax);
            return;
        }
        if (!hasIntersection)
        {
            AddQuad(toFill, outer.xMin, outer.yMin, outer.xMax, outer.yMax);
            return;
        }

        // Top/bottom span the outer width; left/right span only the hole height.
        AddQuad(toFill, outer.xMin, outer.yMin, outer.xMax, intersection.yMin);
        AddQuad(toFill, outer.xMin, intersection.yMax, outer.xMax, outer.yMax);
        AddQuad(toFill, outer.xMin, intersection.yMin, intersection.xMin, intersection.yMax);
        AddQuad(toFill, intersection.xMax, intersection.yMin, outer.xMax, intersection.yMax);
    }

    private void AddQuad(VertexHelper vertices, float xMin, float yMin, float xMax, float yMax)
    {
        if (xMax <= xMin || yMax <= yMin)
            return;
        int first = vertices.currentVertCount;
        vertices.AddVert(new Vector3(xMin, yMin, 0), color, Vector2.zero);
        vertices.AddVert(new Vector3(xMin, yMax, 0), color, Vector2.zero);
        vertices.AddVert(new Vector3(xMax, yMax, 0), color, Vector2.zero);
        vertices.AddVert(new Vector3(xMax, yMin, 0), color, Vector2.zero);
        vertices.AddTriangle(first, first + 1, first + 2);
        vertices.AddTriangle(first + 2, first + 3, first);
    }

    void LateUpdate()
    {
        if (IsPlayingObject() && isActiveAndEnabled && gameObject.activeInHierarchy
            && (_pendingRefresh || realTimeUpdate))
            RefreshMaskInternal(false, false);
    }

    /// <summary>Invalidates the mask and deactivates its GameObject without clearing configuration.</summary>
    public void Close()
    {
        InvalidateAppliedState(true);
        gameObject.SetActive(false);
    }
}
}
