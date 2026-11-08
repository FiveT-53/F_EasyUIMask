using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace F_EasyUIMask
{
[AddComponentMenu("UI/F_Easy UI Mask")]
[RequireComponent(typeof(CanvasRenderer))]
public class FEasyUIMask : MaskableGraphic, ICanvasRaycastFilter
{
    public RectTransform targetUI;
    public bool realTimeUpdate;
    public bool invertMask;
    [Range(0.001f, 1)] public float alphaThreshold = 0.5f;
    [Range(16, 2048)] public int maxTextureSize = 2048;
    [SerializeField, HideInInspector] private Material extractionTemplate;
    [SerializeField, HideInInspector] private Material coverageTemplate;

    private PrecisionState _state;
    private bool _pendingRefresh, _pendingResample;
    private string _lastFailure;
    private UnityEngine.Object _lastFailureContext;
    public string FailureReason { get { return _lastFailure; } }
    public bool IsReady { get { return _state != null && _state.IsReady; } }
    public bool IsPreparing { get { return _state != null && _state.IsPreparing; } }
    public Vector2 EffectiveTexelSize { get { return _state == null ? Vector2.zero : _state.EffectiveTexelSize; } }

    public override Material material
    {
        get { return _state != null && _state.CoverageMaterial != null ? _state.CoverageMaterial : base.material; }
        set { base.material = value; }
    }

    /// <summary>Assign package material assets explicitly when creating a mask at runtime.</summary>
    public void ConfigureResources(Material extraction, Material coverage)
    {
        extractionTemplate = extraction;
        coverageTemplate = coverage;
        ReleaseState();
        RequestRefresh(true);
    }

    /// <summary>Prepares this instance without activating its object or parents.</summary>
    public void Init()
    {
        if (_state != null && _state.IsDisposed) ReleaseState();
        if (Application.IsPlaying(gameObject) && isActiveAndEnabled && _state == null)
            _state = new PrecisionState(this);
    }

    protected override void Awake() { base.Awake(); Init(); }
    protected override void OnEnable()
    {
        base.OnEnable();
        Canvas.preWillRenderCanvases += BeforeCanvas;
        RequestRefresh(true);
    }
    protected override void OnDisable()
    {
        Canvas.preWillRenderCanvases -= BeforeCanvas;
        ReleaseState();
        _pendingRefresh = _pendingResample = false;
        base.OnDisable();
    }
    protected override void OnDestroy() { ReleaseState(); base.OnDestroy(); }
    protected override void OnTransformParentChanged()
    { base.OnTransformParentChanged(); StructureChanged(); }
    protected override void OnCanvasHierarchyChanged()
    { base.OnCanvasHierarchyChanged(); StructureChanged(); }
    protected override void OnRectTransformDimensionsChange()
    { base.OnRectTransformDimensionsChange(); StructureChanged(); }

    private void StructureChanged()
    {
        if (_state != null) _state.CheckHost();
        RequestRefresh(false);
    }
    private void BeforeCanvas() { if (_state != null) _state.CheckHost(); }
    private void RequestRefresh(bool resample)
    { _pendingRefresh = true; _pendingResample |= resample; }

    /// <summary>Queues a fresh sample; may activate only this GameObject.</summary>
    public void RefreshMask()
    {
        if (!Application.IsPlaying(gameObject)) return;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        RequestRefresh(true);
    }
    public void Close() { ReleaseState(); gameObject.SetActive(false); }
    private void LateUpdate()
    {
        if (Application.IsPlaying(gameObject) && isActiveAndEnabled) Tick();
    }
    private void Tick()
    {
        if (_state != null && _state.IsDisposed) ReleaseState();
        if (_state == null) { _state = new PrecisionState(this); RequestRefresh(true); }
        _state.CheckHost();
        if (!_pendingRefresh && !(realTimeUpdate && _state.HasChanges())) return;
        bool force = _pendingResample;
        _pendingRefresh = _pendingResample = false;
        _state.Refresh(force);
    }
    private void ReleaseState()
    {
        if (_state == null) return;
        _state.Dispose();
        _state = null;
        canvasRenderer.Clear();
        SetMaterialDirty(); SetVerticesDirty();
    }
    private void OnApplicationQuit()
    {
        if (_state != null) _state.Shutdown();
        ReleaseState();
    }
    private void ReportFailure(string failure, UnityEngine.Object context)
    {
        if (_lastFailure == failure && _lastFailureContext == context) return;
        _lastFailure = failure; _lastFailureContext = context;
        if (Application.IsPlaying(gameObject)) Debug.LogWarning("FEasyUIMask: " + failure, this);
    }
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!raycastTarget) return true;
        return isActiveAndEnabled && _state != null && _state.Contains(screenPoint, eventCamera);
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (_state != null) _state.Populate(mesh, color);
    }

#if UNITY_EDITOR
    private int _editorRefreshRequested;
    protected override void OnValidate()
    {
        // No Canvas, hierarchy or GPU operations on the validation thread.
        System.Threading.Interlocked.Exchange(ref _editorRefreshRequested, 1);
    }
    public bool ConsumeEditorRefreshRequest()
    { return System.Threading.Interlocked.Exchange(ref _editorRefreshRequested, 0) != 0; }
    public void RefreshEditorPreview(bool force, bool sameStage)
    {
        if (!sameStage) { ReleaseState(); ReportFailure("Target and mask must belong to the same editor stage.", targetUI); return; }
        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded || !isActiveAndEnabled) return;
        if (force)
        {
            // Apply Graphic/MaskableGraphic editor invalidation on the main thread.
            base.OnValidate();
            RequestRefresh(true);
        }
        if (Application.IsPlaying(gameObject)) return;
        if (_state != null) _state.PumpEditorReadback();
        Tick();
    }
    public void ReleaseEditorPreview()
    { if (!Application.IsPlaying(gameObject)) ReleaseState(); }
#endif

    private static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static bool IsUsableOuter(Rect r)
    {
        return IsFinite(r.xMin) && IsFinite(r.xMax) && IsFinite(r.yMin) && IsFinite(r.yMax)
            && IsFinite(r.width) && IsFinite(r.height) && r.width > 0 && r.height > 0;
    }
    private static bool ContainsClosed(Rect r, Vector2 p)
    { return p.x >= r.xMin && p.x <= r.xMax && p.y >= r.yMin && p.y <= r.yMax; }

    private static void DestroyOwned(UnityEngine.Object resource)
    {
        if (resource == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) { UnityEngine.Object.DestroyImmediate(resource); return; }
#endif
        UnityEngine.Object.Destroy(resource);
    }

    // Compare the actual values, rather than hashes or Unity's approximate matrix operator.
    private struct HostState : IEquatable<HostState>
    {
        internal Canvas Root;
        internal Camera Camera;
        internal Matrix4x4 LocalToWorld, View, Projection;
        internal Rect Outer, Viewport;
        internal RenderTexture Output;
        internal RenderMode Mode;
        internal int Display, Width, Height;
        internal float Near, Far, Scale;
        internal bool Stereo, PixelPerfect;

        internal static HostState Capture(FEasyUIMask owner)
        {
            HostState state = new HostState();
            state.LocalToWorld = owner.rectTransform.localToWorldMatrix;
            state.Outer = owner.rectTransform.rect;
#if UNITY_EDITOR
            bool editing = !Application.IsPlaying(owner.gameObject);
            if (!editing)
#endif
            { state.Width = Screen.width; state.Height = Screen.height; }
            Canvas canvas = owner.canvas;
            state.Root = canvas == null ? null : canvas.rootCanvas;
            if (state.Root == null) return state;
            state.Mode = state.Root.renderMode;
            state.Scale = state.Root.scaleFactor;
            state.PixelPerfect = state.Root.pixelPerfect;
            state.Display = state.Root.targetDisplay;
            state.Camera = state.Mode == RenderMode.ScreenSpaceOverlay ? null : state.Root.worldCamera;
#if UNITY_EDITOR
            // Screen dimensions vary with the active Editor/Scene GUI context.
            // An edit preview belongs to its Canvas/observer, not that window.
            if (editing && state.Mode == RenderMode.ScreenSpaceOverlay)
            {
                state.Viewport = state.Root.pixelRect;
                state.Width = Mathf.RoundToInt(state.Viewport.width);
                state.Height = Mathf.RoundToInt(state.Viewport.height);
            }
#endif
            if (state.Camera == null) return state;
            state.View = state.Camera.worldToCameraMatrix; state.Projection = state.Camera.projectionMatrix;
            state.Viewport = state.Camera.pixelRect; state.Output = state.Camera.targetTexture;
            state.Near = state.Camera.nearClipPlane; state.Far = state.Camera.farClipPlane;
            state.Display = state.Camera.targetDisplay; state.Stereo = state.Camera.stereoEnabled;
#if UNITY_EDITOR
            if (editing)
            {
                state.Width = Mathf.RoundToInt(state.Viewport.width);
                state.Height = Mathf.RoundToInt(state.Viewport.height);
            }
#endif
            return state;
        }
        public bool Equals(HostState other)
        {
            return ReferenceEquals(Root, other.Root) && ReferenceEquals(Camera, other.Camera)
                && LocalToWorld.Equals(other.LocalToWorld) && View.Equals(other.View) && Projection.Equals(other.Projection)
                && Outer.Equals(other.Outer) && Viewport.Equals(other.Viewport) && ReferenceEquals(Output, other.Output)
                && Mode == other.Mode && Display == other.Display && Width == other.Width && Height == other.Height
                && Near.Equals(other.Near) && Far.Equals(other.Far) && Scale.Equals(other.Scale)
                && Stereo == other.Stereo && PixelPerfect == other.PixelPerfect;
        }
    }

    private struct InputState : IEquatable<InputState>
    {
        private RectTransform target;
        private Image image;
        private Sprite sprite;
        private Texture texture;
        private Material material, extraction, coverage;
        private Shader extractionShader, coverageShader;
        private Canvas canvas;
        private Camera camera;
        private Matrix4x4 transform, view, projection;
        private Rect rect, viewport;
        private RenderTexture output;
        private Image.Type type;
        private RenderMode mode;
        private bool enabled, invert, aspect, spriteMesh, stereo;
        private float threshold, near, far, scale, referencePixels, pixelsMultiplier, mipBias;
        private int limit, display, aniso;
        private uint textureVersion;
        private FilterMode filter;
        private TextureWrapMode wrapU, wrapV;

        internal static InputState Capture(FEasyUIMask owner, Image image)
        {
            InputState state = new InputState
            {
                target = owner.targetUI, image = image, invert = owner.invertMask,
                threshold = owner.alphaThreshold, limit = owner.maxTextureSize,
                extraction = owner.extractionTemplate, coverage = owner.coverageTemplate,
                extractionShader = owner.extractionTemplate == null ? null : owner.extractionTemplate.shader,
                coverageShader = owner.coverageTemplate == null ? null : owner.coverageTemplate.shader
            };
            if (image == null) return state;
            state.enabled = image.isActiveAndEnabled; state.sprite = image.overrideSprite;
            state.texture = image.mainTexture; state.material = image.material;
            state.transform = image.transform.localToWorldMatrix; state.rect = image.rectTransform.rect;
            state.type = image.type; state.aspect = image.preserveAspect; state.spriteMesh = image.useSpriteMesh;
            state.pixelsMultiplier = image.pixelsPerUnitMultiplier;
            if (state.texture != null)
            {
                state.textureVersion = state.texture.updateCount;
                state.filter = state.texture.filterMode; state.wrapU = state.texture.wrapModeU;
                state.wrapV = state.texture.wrapModeV; state.aniso = state.texture.anisoLevel;
                state.mipBias = state.texture.mipMapBias;
            }
            Canvas canvas = image.GetComponentInParent<Canvas>(true);
            state.canvas = canvas == null ? null : canvas.rootCanvas;
            if (state.canvas == null) return state;
            state.mode = state.canvas.renderMode; state.scale = state.canvas.scaleFactor;
            state.referencePixels = state.canvas.referencePixelsPerUnit; state.display = state.canvas.targetDisplay;
            state.camera = state.mode == RenderMode.ScreenSpaceOverlay ? null : state.canvas.worldCamera;
            if (state.camera == null) return state;
            state.view = state.camera.worldToCameraMatrix; state.projection = state.camera.projectionMatrix;
            state.viewport = state.camera.pixelRect; state.output = state.camera.targetTexture;
            state.near = state.camera.nearClipPlane; state.far = state.camera.farClipPlane;
            state.display = state.camera.targetDisplay; state.stereo = state.camera.stereoEnabled;
            return state;
        }
        public bool Equals(InputState other)
        {
            return ReferenceEquals(target, other.target) && ReferenceEquals(image, other.image)
                && ReferenceEquals(sprite, other.sprite) && ReferenceEquals(texture, other.texture)
                && ReferenceEquals(material, other.material) && ReferenceEquals(extraction, other.extraction)
                && ReferenceEquals(coverage, other.coverage) && ReferenceEquals(extractionShader, other.extractionShader)
                && ReferenceEquals(coverageShader, other.coverageShader) && ReferenceEquals(canvas, other.canvas)
                && ReferenceEquals(camera, other.camera) && ReferenceEquals(output, other.output)
                && transform.Equals(other.transform) && view.Equals(other.view) && projection.Equals(other.projection)
                && rect.Equals(other.rect) && viewport.Equals(other.viewport) && type == other.type && mode == other.mode
                && enabled == other.enabled && invert == other.invert && aspect == other.aspect && spriteMesh == other.spriteMesh
                && stereo == other.stereo && threshold.Equals(other.threshold) && near.Equals(other.near) && far.Equals(other.far)
                && scale.Equals(other.scale) && referencePixels.Equals(other.referencePixels)
                && pixelsMultiplier.Equals(other.pixelsMultiplier) && limit == other.limit && display == other.display
                && textureVersion == other.textureVersion && filter == other.filter && wrapU == other.wrapU && wrapV == other.wrapV
                && aniso == other.aniso && mipBias.Equals(other.mipBias);
        }
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


    // Owns only generated resources. The in-flight slot survives disposal until its callback.
    private sealed class PrecisionState : IDisposable
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly UnityEngine.Profiling.CustomSampler extractionSampler =
            UnityEngine.Profiling.CustomSampler.Create("FEasyUIMask.Precision.ExtractionGPU", true);
#endif
        private sealed class Snapshot
        {
            internal RenderTexture Texture;
            internal Material Coverage;
            internal byte[] Pixels;
            internal Rect Region, Outer;
            internal Matrix4x4 ScreenFromLocal;
            internal bool Invert;
            internal int Version, Frame;
            internal float StartTime;
            internal void Dispose()
            {
                if (Texture != null) { Texture.Release(); DestroyOwned(Texture); }
                if (Coverage != null) DestroyOwned(Coverage);
                Texture = null;
                Coverage = null;
                Pixels = null;
            }
        }

        private readonly FEasyUIMask owner;
        private readonly Mesh projectedMesh;
        private readonly List<Vector3> positions = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> indices = new List<int>();
        private readonly List<Vector3> projected = new List<Vector3>();
        private readonly List<Component> components = new List<Component>();
        private readonly Vector3[] corners = new Vector3[4];
        private Image observed;
        private Material extraction;
        private Snapshot active, pending, spare;
        private bool disposed, refreshing, verticesDirty = true;
        private bool resourcesReleased;
        private bool retryRequested;
        private float retryAfter;
#if UNITY_EDITOR
        private AsyncGPUReadbackRequest editorReadback;
        private bool editorReadbackIssued;
        internal void PumpEditorReadback()
        {
            if (pending != null && editorReadbackIssued)
                editorReadback.Update();
        }
#endif
        private int epoch, version;
        private bool hasInput, hasHost;
        private InputState input;
        private HostState host;
        private Image pendingIdentity;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private int extractionCount, committedVersion, latencyFrames;
        private float latencyMilliseconds;
#endif
        internal bool IsReady { get { return active != null; } }
        internal bool IsDisposed { get { return disposed; } }
        internal bool IsPreparing { get { return pending != null; } }
        internal Vector2 EffectiveTexelSize { get { return active == null ? Vector2.zero : new Vector2(active.Region.width / active.Texture.width, active.Region.height / active.Texture.height); } }
        internal Material CoverageMaterial { get { return active == null ? null : active.Coverage; } }

        internal PrecisionState(FEasyUIMask owner)
        {
            this.owner = owner;
            projectedMesh = new Mesh { name = "FEasyUIMask precision projected", hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.U2D.SpriteAtlasManager.atlasRegistered += OnAtlasRegistered;
            Application.quitting += Shutdown;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
            UnityEditor.EditorApplication.update += PumpEditorReadback;
#endif
        }

        private void ObserveTarget()
        {
            Image next = owner.targetUI == null ? null : owner.targetUI.GetComponent<Image>();
            if (ReferenceEquals(next, observed)) return;
            if (observed != null)
            {
                observed.UnregisterDirtyVerticesCallback(OnVerticesDirty);
                observed.UnregisterDirtyMaterialCallback(OnVerticesDirty);
            }
            observed = next;
            if (observed != null)
            {
                observed.RegisterDirtyVerticesCallback(OnVerticesDirty);
                observed.RegisterDirtyMaterialCallback(OnVerticesDirty);
            }
            verticesDirty = true;
        }
        private void OnVerticesDirty() { verticesDirty = true; }
        private void OnAtlasRegistered(UnityEngine.U2D.SpriteAtlas atlas) { verticesDirty = true; }
        internal bool HasChanges()
        {
            ObserveTarget();
            return verticesDirty || !hasInput || !input.Equals(InputState.Capture(owner, observed))
                || (retryRequested && Time.realtimeSinceStartup >= retryAfter);
        }
        internal void CheckHost()
        {
            if (disposed || owner == null) return;
            HostState next = HostState.Capture(owner);
            if (hasHost && !host.Equals(next))
            {
                Revoke();
                owner.RequestRefresh(true);
            }
            host = next; hasHost = true;
        }

        internal void Refresh(bool force)
        {
            if (disposed || refreshing) { owner.RequestRefresh(force); return; }
            refreshing = true;
            ObserveTarget();
            CheckHost();
            try
            {
                retryRequested = false;
                string failure;
                Camera sourceCamera, maskCamera;
                if (!Validate(out sourceCamera, out maskCamera, out failure)) { Fail(failure); return; }
                // A different target must never receive a previous target's in-flight result.
                if (pending != null && pendingIdentity != observed) epoch++;
                if (pending != null) { owner.RequestRefresh(force); return; }
                verticesDirty = false;
                Canvas.ForceUpdateCanvases();
                if (disposed || !owner.isActiveAndEnabled) return;
                ObserveTarget();
                CheckHost();
                if (!Validate(out sourceCamera, out maskCamera, out failure)) { Fail(failure); return; }
                input = InputState.Capture(owner, observed); hasInput = true;
                bool empty = !observed.isActiveAndEnabled;
                // GetMesh returns the renderer's mesh in 2022.3. Borrow it read-only; never clear or destroy it.
                Mesh sourceMesh = empty ? null : observed.canvasRenderer.GetMesh();
                positions.Clear(); uvs.Clear(); indices.Clear();
                if (sourceMesh != null)
                {
                    sourceMesh.GetVertices(positions);
                    sourceMesh.GetUVs(0, uvs);
                    if (sourceMesh.subMeshCount > 0) sourceMesh.GetTriangles(indices, 0);
                }
                if (!empty && (positions.Count == 0 || indices.Count == 0))
                {
                    if (sourceMesh != null && observed.useSpriteMesh && observed.overrideSprite.triangles.Length == 0)
                        empty = true;
                    else { Fail("Active Image mesh is not ready or has no triangles.", true); return; }
                }
                if (positions.Count != uvs.Count) { Fail("mesh UV0 is missing."); return; }
                Matrix4x4 sourceProjection = ScreenMatrix(observed.rectTransform, sourceCamera);
                Matrix4x4 maskProjection = ScreenMatrix(owner.rectTransform, maskCamera);
                Rect outer = owner.rectTransform.rect;
                Rect outerScreen;
                if (!ProjectRect(owner.rectTransform, maskCamera, maskProjection, out outerScreen))
                { Fail("mask crosses the camera clip range or is degenerate."); return; }
                Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
                projected.Clear();
                for (int i = 0; i < positions.Count; i++)
                {
                    Vector3 world = observed.transform.TransformPoint(positions[i]);
                    Vector3 depth = sourceCamera == null ? world : sourceCamera.WorldToScreenPoint(world);
                    Vector4 h = sourceProjection * new Vector4(positions[i].x, positions[i].y, positions[i].z, 1);
                    if (!IsFinite(h.w) || h.w <= 0 || !IsFinite(h.x) || !IsFinite(h.y)
                        || !IsFinite(uvs[i].x) || !IsFinite(uvs[i].y)
                        || (sourceCamera != null && !WithinClipDepth(depth.z, sourceCamera)))
                    { Fail("target mesh crosses the clip range."); return; }
                    Vector2 screen = new Vector2(h.x / h.w, h.y / h.w);
                    if (!IsFinite(screen.x) || !IsFinite(screen.y)) { Fail("Projected mesh exceeds the numeric range."); return; }
                    min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
                    projected.Add(new Vector3(screen.x, screen.y, h.w));
                }
                min = Vector2.Max(min, outerScreen.min); max = Vector2.Min(max, outerScreen.max);
                empty |= min.x >= max.x || min.y >= max.y;
                int limit = Mathf.Clamp(owner.maxTextureSize, 16, Mathf.Min(2048, SystemInfo.maxTextureSize));
                Rect region;
                int width, height;
                if (empty) { region = new Rect(0, 0, 1, 1); width = height = 1; }
                else region = ScreenRegion(min, max, limit, out width, out height);
                Snapshot slot = spare ?? new Snapshot(); spare = null;
                pending = slot; pendingIdentity = observed;
                slot.Outer = outer; slot.Region = region; slot.ScreenFromLocal = maskProjection;
                slot.Invert = owner.invertMask; slot.Version = ++version;
                slot.Frame = Time.frameCount; slot.StartTime = Time.realtimeSinceStartup;
                if (slot.Texture == null || slot.Texture.width != width || slot.Texture.height != height)
                {
                    slot.Dispose();
                    slot.Texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                    { name = "FEasyUIMask precision alpha", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                        antiAliasing = 1, useMipMap = false, hideFlags = HideFlags.HideAndDontSave };
                    if (!slot.Texture.Create()) { Fail("RGBA32 RenderTexture creation failed.", true); pending = null; slot.Dispose(); return; }
                }
                if (slot.Coverage == null) slot.Coverage = new Material(owner.coverageTemplate) { hideFlags = HideFlags.HideAndDontSave };
                slot.Coverage.shader = owner.coverageTemplate.shader;
                slot.Coverage.CopyPropertiesFromMaterial(owner.coverageTemplate);
                if (extraction == null) extraction = new Material(owner.extractionTemplate) { hideFlags = HideFlags.HideAndDontSave };
                extraction.shader = owner.extractionTemplate.shader;
                extraction.CopyPropertiesFromMaterial(owner.extractionTemplate);
                extraction.mainTexture = observed.mainTexture;
                extraction.SetFloat("_Threshold", owner.alphaThreshold);
                projectedMesh.Clear();
                for (int i = 0; i < projected.Count; i++)
                {
                    Vector3 p = projected[i];
                    projected[i] = new Vector3((p.x - region.xMin) / region.width * 2 - 1,
                        (p.y - region.yMin) / region.height * 2 - 1, p.z);
                }
                if (!empty)
                {
                    projectedMesh.SetVertices(projected); projectedMesh.SetUVs(0, uvs);
                    projectedMesh.SetTriangles(indices, 0, false);
                }
                int requestEpoch = epoch;
                using (var command = new CommandBuffer { name = "FEasyUIMask precision extract" })
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    command.BeginSample(extractionSampler);
#endif
                    command.SetRenderTarget(slot.Texture);
                    command.ClearRenderTarget(false, true, Color.clear);
                    if (!empty) command.DrawMesh(projectedMesh, Matrix4x4.identity, extraction, 0, 0);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    command.EndSample(extractionSampler);
#endif
                    // Enqueue the readback with the draw. A thrown request cannot leave a
                    // separately submitted draw holding resources that the catch releases.
#if UNITY_EDITOR
                    if (Application.IsPlaying(owner.gameObject))
#endif
                        command.RequestAsyncReadback(slot.Texture, 0, TextureFormat.RGBA32,
                            request => Complete(request, slot, requestEpoch));
                    Graphics.ExecuteCommandBuffer(command);
                }
#if UNITY_EDITOR
                // Edit Mode has no continuously rendered player loop. Keep a request
                // handle that the selected-object editor queue can explicitly update.
                if (!Application.IsPlaying(owner.gameObject))
                {
                    editorReadback = AsyncGPUReadback.Request(slot.Texture, 0, TextureFormat.RGBA32,
                        request => Complete(request, slot, requestEpoch));
                    editorReadback.forcePlayerLoopUpdate = true;
                    editorReadbackIssued = true;
                }
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                extractionCount++;
#endif
                owner._lastFailure = null;
                owner._lastFailureContext = null;
            }
            catch (Exception exception)
            {
                Fail(exception.Message, true);
                if (pending != null) { pending.Dispose(); pending = null; }
            }
            finally
            {
                refreshing = false;
            }
        }

        private static Rect ScreenRegion(Vector2 min, Vector2 max, int limit, out int width, out int height)
        {
            // Quantize bounds and dimensions together. Rounding only the RT size
            // resamples a different grid even when no downsampling is requested.
            double texel = 1;
            double extent = Math.Max((double)max.x - min.x, (double)max.y - min.y);
            for (;;)
            {
                double x0 = Math.Floor(min.x / texel) - 1, y0 = Math.Floor(min.y / texel) - 1;
                double x1 = Math.Ceiling(max.x / texel) + 1, y1 = Math.Ceiling(max.y / texel) + 1;
                if (x1 - x0 <= limit && y1 - y0 <= limit)
                {
                    width = (int)(x1 - x0); height = (int)(y1 - y0);
                    return Rect.MinMaxRect((float)(x0 * texel), (float)(y0 * texel), (float)(x1 * texel), (float)(y1 * texel));
                }
                // Reserve two guard texels plus room for grid alignment. Recompute
                // the region as well; clamping dimensions alone changes the pitch.
                texel = Math.Max(texel * 1.001, extent / (limit - 3));
            }
        }

        private bool Validate(out Camera sourceCamera, out Camera maskCamera, out string failure)
        {
            sourceCamera = maskCamera = null;
            failure = null;
            if (observed == null)
                failure = "Bind a standard Simple Image on Target UI.";
            else if (observed.GetType() != typeof(Image) || observed.type != Image.Type.Simple)
                failure = "Only a standard Simple Image is supported; Sliced, Filled and Tiled are outside P1.";
            else if (observed.overrideSprite == null)
                failure = "target Sprite is missing.";
            else if (observed.overrideSprite.associatedAlphaSplitTexture != null)
                failure = "split Alpha is unsupported.";
            else if (observed.mainTexture == null)
                failure = "Target texture is missing.";
            else if (observed.material != observed.defaultMaterial || HasMeshModifiers())
                failure = "custom materials and mesh effects are unsupported.";
            else if (!SystemInfo.supportsAsyncGPUReadback || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32))
                failure = "asynchronous RGBA32 readback is required.";
            else if (SystemInfo.maxTextureSize < 16 || !SystemInfo.IsFormatSupported(
                UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,
                UnityEngine.Experimental.Rendering.FormatUsage.ReadPixels))
                failure = "This device does not support the required RGBA32 readback format.";
            else if (owner.extractionTemplate == null || owner.coverageTemplate == null
                || owner.extractionTemplate.shader == null || owner.coverageTemplate.shader == null
                || !owner.extractionTemplate.shader.isSupported || !owner.coverageTemplate.shader.isSupported)
                failure = "supported extraction and coverage material assets are required.";
            else if (owner.extractionTemplate.shader.name != "FEasyUIMask/Precision/AlphaExtraction"
                || owner.coverageTemplate.shader.name != "FEasyUIMask/Precision/Coverage")
                failure = "Use the package precision extraction and coverage materials.";
            else if (!IsFinite(owner.alphaThreshold) || owner.alphaThreshold < 0.001f || owner.alphaThreshold > 1)
                failure = "Alpha threshold must be in [0.001, 1].";
            else if (!HasInvertibleTransform(owner.rectTransform) || !IsUsableOuter(owner.rectTransform.rect))
                failure = "mask outer rectangle is invalid.";
            if (failure != null) return false;
            if (!HasInvertibleTransform(observed.rectTransform) || !IsUsableOuter(observed.rectTransform.rect))
            { failure = "Target must have an invertible transform and a finite positive rectangle."; return false; }
#if UNITY_EDITOR
            if (!observed.gameObject.scene.IsValid() || !observed.gameObject.scene.isLoaded
                || Application.IsPlaying(observed.gameObject) != Application.IsPlaying(owner.gameObject)
                || !UnityEditor.SceneManagement.StageUtility.GetStageHandle(observed.gameObject).Equals(
                    UnityEditor.SceneManagement.StageUtility.GetStageHandle(owner.gameObject)))
            { failure = "Target and mask must be loaded objects in the same playing world and editor stage."; return false; }
#endif
            int sourceDisplay, maskDisplay;
            if (!TryGetProjection(observed.GetComponentInParent<Canvas>(true), out sourceCamera, out sourceDisplay, out failure)
                || !TryGetProjection(owner.canvas, out maskCamera, out maskDisplay, out failure)) return false;
            if (sourceDisplay != maskDisplay) { failure = "Cross-display projection is unsupported."; return false; }
            Rect projectedRect;
            if (!ProjectRect(observed.rectTransform, sourceCamera, ScreenMatrix(observed.rectTransform, sourceCamera), out projectedRect))
            { failure = "Target crosses the camera clip range or has a degenerate projection."; return false; }
            return true;
        }

        private bool HasMeshModifiers()
        {
            components.Clear(); observed.GetComponents(components);
            for (int i = 0; i < components.Count; i++)
                if (components[i] is IMeshModifier) return true;
            return false;
        }

        private void Complete(AsyncGPUReadbackRequest request, Snapshot slot, int requestEpoch)
        {
            if (pending == slot) pending = null;
            if (!disposed && owner != null) CheckHost();
            if (disposed || owner == null || requestEpoch != epoch || !owner.isActiveAndEnabled
                || pendingIdentity == null || owner.targetUI != pendingIdentity.rectTransform)
            {
                slot.Dispose();
                if (disposed) ReleaseExtractionResources();
                // Explicit/structural requests raised while this slot was in flight
                // remain queued. A field assignment with real-time off is not a request.
                return;
            }
            if (request.hasError) { slot.Dispose(); Fail("asynchronous readback failed.", true); return; }
            try
            {
                var data = request.GetData<byte>();
                if (data.Length != slot.Texture.width * slot.Texture.height * 4)
                    throw new InvalidOperationException("Unexpected RGBA32 readback length.");
                if (slot.Pixels == null || slot.Pixels.Length != data.Length) slot.Pixels = new byte[data.Length];
                data.CopyTo(slot.Pixels);
                slot.Coverage.SetTexture("_MaskTex", slot.Texture);
                slot.Coverage.SetFloat("_Invert", slot.Invert ? 1 : 0);
                if (spare != null) spare.Dispose();
                spare = active; active = slot;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                committedVersion = slot.Version; latencyFrames = Time.frameCount - slot.Frame;
                latencyMilliseconds = (Time.realtimeSinceStartup - slot.StartTime) * 1000;
#endif
                owner.SetMaterialDirty(); owner.SetVerticesDirty();
#if UNITY_EDITOR
                if (!Application.IsPlaying(owner.gameObject))
                {
                    UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                    UnityEditor.SceneView.RepaintAll();
                }
#endif
            }
            catch (Exception exception) { slot.Dispose(); Fail(exception.Message, true); }
        }

        private void Revoke()
        {
            epoch++;
            if (active != null) { active.Dispose(); active = null; }
            owner.canvasRenderer.Clear();
            if (!CanvasUpdateRegistry.IsRebuildingGraphics())
            { owner.SetMaterialDirty(); owner.SetVerticesDirty(); }
            else owner.RequestRefresh(true);
        }

        private void Fail(string failure, bool retry = false)
        {
            Revoke();
            input = InputState.Capture(owner, observed); hasInput = true; verticesDirty = false;
            retryRequested = retry; retryAfter = Time.realtimeSinceStartup + 0.25f;
            owner.ReportFailure(failure, observed);
        }

        internal bool Contains(Vector2 screenPoint, Camera camera)
        {
            CheckHost();
            if (active == null) return false;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(owner.rectTransform, screenPoint, camera, out local)
                || !ContainsClosed(active.Outer, local)) return false;
            Vector4 h = active.ScreenFromLocal * new Vector4(local.x, local.y, 0, 1);
            if (!IsFinite(h.w) || h.w <= 0 || !IsFinite(h.x) || !IsFinite(h.y)) return false;
            float u = (h.x / h.w - active.Region.xMin) / active.Region.width;
            float v = (h.y / h.w - active.Region.yMin) / active.Region.height;
            bool shape = false;
            if (u >= 0 && u < 1 && v >= 0 && v < 1)
            {
                int x = Mathf.FloorToInt(u * active.Texture.width), y = Mathf.FloorToInt(v * active.Texture.height);
                shape = active.Pixels[(y * active.Texture.width + x) * 4] >= 128;
            }
            return active.Invert ? !shape : shape;
        }

        internal void Populate(VertexHelper mesh, Color color)
        {
            CheckHost();
            if (active == null) return;
            Rect r = active.Outer;
            AddVertex(mesh, new Vector3(r.xMin, r.yMin), color);
            AddVertex(mesh, new Vector3(r.xMin, r.yMax), color);
            AddVertex(mesh, new Vector3(r.xMax, r.yMax), color);
            AddVertex(mesh, new Vector3(r.xMax, r.yMin), color);
            mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(2, 3, 0);
        }

        private void AddVertex(VertexHelper mesh, Vector3 position, Color color)
        {
            Vector4 h = active.ScreenFromLocal * new Vector4(position.x, position.y, 0, 1);
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position; vertex.color = color;
            // Store homogeneous mask UV in the standard UV0 stream. UGUI stencil material
            // clones do not preserve uniforms that are absent from ShaderLab Properties.
            vertex.uv0 = new Vector4((h.x - active.Region.xMin * h.w) / active.Region.width,
                (h.y - active.Region.yMin * h.w) / active.Region.height, h.w, 0);
            mesh.AddVert(vertex);
        }

        private static Matrix4x4 ScreenMatrix(RectTransform rect, Camera camera)
        {
            if (camera == null) return rect.localToWorldMatrix;
            Rect viewport = camera.pixelRect;
            Matrix4x4 pixels = Matrix4x4.identity;
            pixels.SetRow(0, new Vector4(viewport.width * 0.5f, 0, 0, viewport.x + viewport.width * 0.5f));
            pixels.SetRow(1, new Vector4(0, viewport.height * 0.5f, 0, viewport.y + viewport.height * 0.5f));
            return pixels * camera.projectionMatrix * camera.worldToCameraMatrix * rect.localToWorldMatrix;
        }

        private bool ProjectRect(RectTransform rect, Camera camera, Matrix4x4 projection, out Rect result)
        {
            Rect r = rect.rect;
            corners[0] = new Vector3(r.xMin, r.yMin); corners[1] = new Vector3(r.xMin, r.yMax);
            corners[2] = new Vector3(r.xMax, r.yMax); corners[3] = new Vector3(r.xMax, r.yMin);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            for (int i = 0; i < 4; i++)
            {
                Vector4 p = projection * new Vector4(corners[i].x, corners[i].y, 0, 1);
                if (!IsFinite(p.w) || p.w <= 0 || !IsFinite(p.x) || !IsFinite(p.y)
                    || (camera != null && !WithinClipDepth(camera.WorldToScreenPoint(rect.TransformPoint(corners[i])).z, camera)))
                { result = new Rect(); return false; }
                Vector2 screen = new Vector2(p.x / p.w, p.y / p.w);
                min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
            }
            result = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return IsUsableOuter(result);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; epoch++;
            UnityEngine.U2D.SpriteAtlasManager.atlasRegistered -= OnAtlasRegistered;
            if (observed != null)
            {
                observed.UnregisterDirtyVerticesCallback(OnVerticesDirty);
                observed.UnregisterDirtyMaterialCallback(OnVerticesDirty);
            }
            if (active != null) active.Dispose();
            if (spare != null) spare.Dispose();
            active = spare = null;
            // pending is disposed by Complete, after the GPU is finished with it.
            if (pending == null) ReleaseExtractionResources();
        }

        // Only teardown may wait for this owner's GPU request. Normal refresh/click never blocks on readback.
        internal void Shutdown()
        {
            Dispose();
            if (pending != null)
            {
                // The drain is queued after this instance's draw/readback, and is used
                // only during teardown. Normal disposal remains callback-driven.
                AsyncGPUReadbackRequest drain = AsyncGPUReadback.Request(pending.Texture, 0, TextureFormat.RGBA32);
                drain.WaitForCompletion();
                if (pending != null) { pending.Dispose(); pending = null; }
            }
            ReleaseExtractionResources();
        }

        private void ReleaseExtractionResources()
        {
            if (resourcesReleased) return;
            resourcesReleased = true;
            if (projectedMesh != null) DestroyOwned(projectedMesh);
            if (extraction != null) { DestroyOwned(extraction); extraction = null; }
            UnregisterReload();
        }

        private void UnregisterReload()
        {
            Application.quitting -= Shutdown;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            UnityEditor.EditorApplication.playModeStateChanged -= OnEditorPlayModeChanged;
            UnityEditor.EditorApplication.update -= PumpEditorReadback;
#endif
        }
#if UNITY_EDITOR
        private void OnEditorPlayModeChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change == UnityEditor.PlayModeStateChange.ExitingEditMode
                || change == UnityEditor.PlayModeStateChange.ExitingPlayMode) Shutdown();
        }
#endif
    }
}
}
