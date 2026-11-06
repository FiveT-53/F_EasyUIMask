using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace F_EasyUIMask
{
public partial class FEasyUIMask
{
    [Tooltip("P0 experiment. Play Mode only; not a stable precision API.")]
    public bool p0PrecisionExperiment;
    [Range(0.001f, 1)] public float p0AlphaThreshold = 0.5f;
    [Range(16, 2048)] public int p0MaxTextureSize = 2048;
    public Material p0ExtractionTemplate;
    public Material p0CoverageTemplate;
    [Tooltip("Comparison probe: rasterize in target-local units instead of screen pixels.")]
    public bool p0TargetLocalProbe;
    [NonSerialized] public int p0ExtractionCount, p0CommittedVersion, p0LatencyFrames, p0MeshVertices;
    [NonSerialized] public float p0LatencyMilliseconds;
    [NonSerialized] public float p0SubmissionMilliseconds;
    private P0State _p0;

    public override Material material
    {
        get { return _p0 != null && _p0.CoverageMaterial != null ? _p0.CoverageMaterial : base.material; }
        set { base.material = value; }
    }

    private void TickP0()
    {
        if (_p0 == null)
        {
            InvalidateAppliedState(true);
            _p0 = new P0State(this);
            RequestRefresh(true);
        }
        _p0.ObserveTarget();
        if (!_pendingRefresh && !(realTimeUpdate && _p0.HasChanges()))
            return;
        // Requests raised by ForceUpdateCanvases are left pending for the next LateUpdate.
        bool force = _pendingForceVertices;
        _pendingRefresh = false;
        _pendingForceVertices = false;
        _p0.Refresh(force);
    }

    private void ReleaseP0()
    {
        if (_p0 == null)
            return;
        _p0.Dispose();
        _p0 = null;
        _hasAppliedState = false;
        SetMaterialDirty();
        SetVerticesDirty();
    }

    private void OnApplicationQuit()
    {
        if (_p0 != null) _p0.Shutdown();
    }

    // Owns only generated resources. The in-flight slot survives disposal until its callback.
    private sealed class P0State : IDisposable
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly UnityEngine.Profiling.CustomSampler extractionSampler =
            UnityEngine.Profiling.CustomSampler.Create("FEasyUIMask.P0.ExtractionGPU", true);
#endif
        private sealed class Slot
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
                if (Texture != null) { Texture.Release(); UnityEngine.Object.Destroy(Texture); }
                if (Coverage != null) UnityEngine.Object.Destroy(Coverage);
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
        private Slot active, pending, spare;
        private bool disposed, refreshing, verticesDirty = true;
        private int epoch, signature, contentHash, version;
        private bool hasContentHash;
        private Image pendingIdentity;
        private AsyncGPUReadbackRequest readback;
        internal Material CoverageMaterial { get { return active == null ? null : active.Coverage; } }

        internal P0State(FEasyUIMask owner)
        {
            this.owner = owner;
            projectedMesh = new Mesh { name = "FEasyUIMask P0 projected", hideFlags = HideFlags.HideAndDontSave };
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
#endif
        }

        internal void ObserveTarget()
        {
            Image next = owner.targetUI == null ? null : owner.targetUI.GetComponent<Image>();
            if (next == observed) return;
            if (observed != null) observed.UnregisterDirtyVerticesCallback(OnVerticesDirty);
            observed = next;
            if (observed != null) observed.RegisterDirtyVerticesCallback(OnVerticesDirty);
            verticesDirty = true;
        }

        private void OnVerticesDirty() { verticesDirty = true; }

        internal bool HasChanges() { return verticesDirty || signature != Signature(); }

        private int Signature()
        {
            unchecked
            {
                int hash = owner.rectTransform.localToWorldMatrix.GetHashCode();
                hash = hash * 31 + owner.rectTransform.rect.GetHashCode();
                hash = hash * 31 + owner.invertMask.GetHashCode();
                hash = hash * 31 + owner.p0AlphaThreshold.GetHashCode();
                hash = hash * 31 + owner.p0MaxTextureSize;
                hash = hash * 31 + owner.p0TargetLocalProbe.GetHashCode();
                hash = hash * 31 + (int)owner.areaSelectMode;
                hash = hash * 31 + Screen.width * 17 + Screen.height;
                hash = hash * 31 + (owner.p0ExtractionTemplate == null ? 0 : owner.p0ExtractionTemplate.GetInstanceID());
                hash = hash * 31 + (owner.p0CoverageTemplate == null ? 0 : owner.p0CoverageTemplate.GetInstanceID());
                if (observed != null)
                {
                    hash = hash * 31 + observed.GetInstanceID();
                    hash = hash * 31 + observed.isActiveAndEnabled.GetHashCode();
                    hash = hash * 31 + observed.transform.localToWorldMatrix.GetHashCode();
                    hash = hash * 31 + observed.rectTransform.rect.GetHashCode();
                    hash = hash * 31 + (observed.mainTexture == null ? 0 : observed.mainTexture.GetInstanceID());
                }
                Camera camera; int display; string failure;
                if (TryGetProjection(owner.canvas, out camera, out display, out failure) && camera != null)
                    hash = hash * 31 + ScreenMatrix(owner.rectTransform, camera).GetHashCode();
                if (observed != null && TryGetProjection(observed.canvas, out camera, out display, out failure) && camera != null)
                    hash = hash * 31 + ScreenMatrix(observed.rectTransform, camera).GetHashCode();
                return hash;
            }
        }

        internal void Refresh(bool force)
        {
            if (disposed || refreshing) { owner.RequestRefresh(force); return; }
            refreshing = true;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            int countBefore = owner.p0ExtractionCount;
            try
            {
                string failure;
                Camera sourceCamera, maskCamera;
                if (!Validate(out sourceCamera, out maskCamera, out failure)) { Fail(failure); return; }
                // A different target must never receive a previous target's in-flight result.
                if (pending != null && pendingIdentity != observed) Revoke();
                if (pending != null) { owner.RequestRefresh(force); return; }
                verticesDirty = false;
                Canvas.ForceUpdateCanvases();
                if (disposed || !owner.isActiveAndEnabled) return;
                if (!Validate(out sourceCamera, out maskCamera, out failure)) { Fail(failure); return; }
                signature = Signature();
                bool empty = !observed.isActiveAndEnabled || (observed.type == Image.Type.Filled && observed.fillAmount <= 0);
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
                    Fail("P0: active Image mesh is not ready or has no triangles."); return;
                }
                if (positions.Count != uvs.Count) { Fail("P0: mesh UV0 is missing."); return; }
                Matrix4x4 sourceProjection = ScreenMatrix(observed.rectTransform, sourceCamera);
                Matrix4x4 maskProjection = ScreenMatrix(owner.rectTransform, maskCamera);
                Rect outer = owner.rectTransform.rect;
                Rect outerScreen;
                if (!ProjectRect(owner.rectTransform, maskCamera, maskProjection, out outerScreen))
                { Fail("P0: mask crosses the camera clip range or is degenerate."); return; }
                if (owner.p0TargetLocalProbe)
                {
                    Matrix4x4 planar = sourceProjection;
                    planar.SetColumn(2, new Vector4(0, 0, 1, 0));
                    planar.SetRow(2, new Vector4(0, 0, 1, 0));
                    if (!IsFinite(planar.determinant) || planar.determinant == 0)
                    { Fail("P0: target-local projection is degenerate."); return; }
                    maskProjection = planar.inverse * maskProjection;
                    if (!ProjectRect(owner.rectTransform, null, maskProjection, out outerScreen))
                    { Fail("P0: target-local outer projection is invalid."); return; }
                }
                Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
                projected.Clear();
                int hash = signature;
                for (int i = 0; i < positions.Count; i++)
                {
                    Vector3 world = observed.transform.TransformPoint(positions[i]);
                    Vector3 depth = sourceCamera == null ? world : sourceCamera.WorldToScreenPoint(world);
                    Vector4 h = sourceProjection * new Vector4(positions[i].x, positions[i].y, positions[i].z, 1);
                    if (!IsFinite(h.w) || h.w <= 0 || !IsFinite(h.x) || !IsFinite(h.y)
                        || (sourceCamera != null && !WithinClipDepth(depth.z, sourceCamera)))
                    { Fail("P0: target mesh crosses the clip range."); return; }
                    Vector2 screen = new Vector2(h.x / h.w, h.y / h.w);
                    if (owner.p0TargetLocalProbe) screen = positions[i];
                    min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
                    projected.Add(new Vector3(screen.x, screen.y, owner.p0TargetLocalProbe ? 1 : h.w));
                    unchecked { hash = hash * 31 + positions[i].GetHashCode(); hash = hash * 31 + uvs[i].GetHashCode(); }
                }
                unchecked { for (int i = 0; i < indices.Count; i++) hash = hash * 31 + indices[i]; }
                if (!force && hasContentHash && contentHash == hash && active != null) return;
                contentHash = hash; hasContentHash = true;
                min = Vector2.Max(min, outerScreen.min); max = Vector2.Min(max, outerScreen.max);
                empty |= min.x >= max.x || min.y >= max.y;
                Rect region = empty ? new Rect(0, 0, 1, 1) : Rect.MinMaxRect(min.x - 1, min.y - 1, max.x + 1, max.y + 1);
                int limit = Mathf.Clamp(owner.p0MaxTextureSize, 16, Mathf.Min(2048, SystemInfo.maxTextureSize));
                float scale = Mathf.Min(1, (limit - 2) / Mathf.Max(region.width - 2, region.height - 2));
                int width = empty ? 1 : Mathf.Clamp(Mathf.CeilToInt((region.width - 2) * scale) + 2, 3, limit);
                int height = empty ? 1 : Mathf.Clamp(Mathf.CeilToInt((region.height - 2) * scale) + 2, 3, limit);
                // The guard is one effective texel, including at reduced resolution.
                if (!empty) region = Rect.MinMaxRect(min.x - 1 / scale, min.y - 1 / scale, max.x + 1 / scale, max.y + 1 / scale);
                Slot slot = spare ?? new Slot(); spare = null;
                pending = slot; pendingIdentity = observed;
                slot.Outer = outer; slot.Region = region; slot.ScreenFromLocal = maskProjection;
                slot.Invert = owner.invertMask; slot.Version = ++version;
                slot.Frame = Time.frameCount; slot.StartTime = Time.realtimeSinceStartup;
                if (slot.Texture == null || slot.Texture.width != width || slot.Texture.height != height)
                {
                    slot.Dispose();
                    slot.Texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                    { name = "FEasyUIMask P0 alpha", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                        antiAliasing = 1, useMipMap = false, hideFlags = HideFlags.HideAndDontSave };
                    if (!slot.Texture.Create()) { Fail("P0: RGBA32 RenderTexture creation failed."); pending = null; slot.Dispose(); return; }
                }
                if (slot.Coverage == null) slot.Coverage = new Material(owner.p0CoverageTemplate) { hideFlags = HideFlags.HideAndDontSave };
                slot.Coverage.shader = owner.p0CoverageTemplate.shader;
                slot.Coverage.CopyPropertiesFromMaterial(owner.p0CoverageTemplate);
                if (extraction == null) extraction = new Material(owner.p0ExtractionTemplate) { hideFlags = HideFlags.HideAndDontSave };
                extraction.shader = owner.p0ExtractionTemplate.shader;
                extraction.CopyPropertiesFromMaterial(owner.p0ExtractionTemplate);
                extraction.mainTexture = observed.mainTexture;
                extraction.SetFloat("_Threshold", owner.p0AlphaThreshold);
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
                using (var command = new CommandBuffer { name = "FEasyUIMask P0 extract" })
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
                    Graphics.ExecuteCommandBuffer(command);
                }
                int requestEpoch = epoch;
                owner.p0ExtractionCount++; owner.p0MeshVertices = positions.Count;
                readback = AsyncGPUReadback.Request(slot.Texture, 0, TextureFormat.RGBA32,
                    request => Complete(request, slot, requestEpoch));
                owner._lastFailure = null;
            }
            catch (Exception exception)
            {
                Fail("P0: " + exception.Message);
                if (pending != null) { pending.Dispose(); pending = null; }
            }
            finally
            {
                if (owner.p0ExtractionCount != countBefore)
                    owner.p0SubmissionMilliseconds = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                refreshing = false;
            }
        }

        private bool Validate(out Camera sourceCamera, out Camera maskCamera, out string failure)
        {
            sourceCamera = maskCamera = null;
            failure = null;
            if (owner.areaSelectMode != AreaSelectMode.UITarget || observed == null)
                failure = "P0: select UITarget and a standard Image.";
            else if (observed.GetType() != typeof(Image) || observed.type == Image.Type.Tiled)
                failure = "P0: only standard Simple, Sliced and Filled Image probes are supported.";
            else if (observed.overrideSprite == null)
                failure = "P0: target Sprite is missing.";
            else if (observed.overrideSprite.associatedAlphaSplitTexture != null)
                failure = "P0: split Alpha is unsupported.";
            else if (observed.material != observed.defaultMaterial || HasMeshModifiers())
                failure = "P0: custom materials and mesh effects are unsupported.";
            else if (!SystemInfo.supportsAsyncGPUReadback || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32))
                failure = "P0: asynchronous RGBA32 readback is required.";
            else if (owner.p0ExtractionTemplate == null || owner.p0CoverageTemplate == null
                || !owner.p0ExtractionTemplate.shader.isSupported || !owner.p0CoverageTemplate.shader.isSupported)
                failure = "P0: supported extraction and coverage material assets are required.";
            else if (!IsFinite(owner.p0AlphaThreshold) || owner.p0AlphaThreshold <= 0 || owner.p0AlphaThreshold > 1)
                failure = "P0: Alpha threshold must be in (0, 1].";
            else if (!HasInvertibleTransform(owner.rectTransform) || !IsUsableOuter(owner.rectTransform.rect))
                failure = "P0: mask outer rectangle is invalid.";
            if (failure != null) return false;
            Vector2 min, max; UnityEngine.Object context;
            if (!owner.TryCalculateTargetBounds(out min, out max, out failure, out context)) return false;
            int display;
            return TryGetProjection(observed.canvas, out sourceCamera, out display, out failure)
                && TryGetProjection(owner.canvas, out maskCamera, out display, out failure);
        }

        private bool HasMeshModifiers()
        {
            components.Clear(); observed.GetComponents(components);
            for (int i = 0; i < components.Count; i++)
                if (components[i] is IMeshModifier) return true;
            return false;
        }

        private void Complete(AsyncGPUReadbackRequest request, Slot slot, int requestEpoch)
        {
            if (pending == slot) pending = null;
            if (disposed || owner == null || requestEpoch != epoch || !owner.isActiveAndEnabled)
            {
                slot.Dispose();
                if (disposed) UnregisterReload();
                return;
            }
            if (request.hasError) { slot.Dispose(); Fail("P0: asynchronous readback failed."); return; }
            var data = request.GetData<byte>();
            if (slot.Pixels == null || slot.Pixels.Length != data.Length) slot.Pixels = new byte[data.Length];
            data.CopyTo(slot.Pixels);
            slot.Coverage.SetTexture("_MaskTex", slot.Texture);
            slot.Coverage.SetFloat("_Invert", slot.Invert ? 1 : 0);
            spare = active; active = slot;
            owner.p0CommittedVersion = slot.Version;
            owner.p0LatencyFrames = Time.frameCount - slot.Frame;
            owner.p0LatencyMilliseconds = (Time.realtimeSinceStartup - slot.StartTime) * 1000;
            owner.SetMaterialDirty(); owner.SetVerticesDirty();
        }

        private void Revoke()
        {
            epoch++;
            hasContentHash = false;
            if (active != null) { active.Dispose(); active = null; }
            owner.canvasRenderer.Clear(); owner.SetMaterialDirty(); owner.SetVerticesDirty();
        }

        private void Fail(string failure) { Revoke(); owner.ReportFailure(failure, observed); }

        internal bool Contains(Vector2 screenPoint, Camera camera)
        {
            if (active == null) return false;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(owner.rectTransform, screenPoint, camera, out local)
                || !ContainsClosed(active.Outer, local)) return false;
            Vector4 h = active.ScreenFromLocal * new Vector4(local.x, local.y, 0, 1);
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
            if (observed != null) observed.UnregisterDirtyVerticesCallback(OnVerticesDirty);
            if (active != null) active.Dispose();
            if (spare != null) spare.Dispose();
            active = spare = null;
            // pending is disposed by Complete, after the GPU is finished with it.
            UnityEngine.Object.Destroy(projectedMesh);
            if (extraction != null) UnityEngine.Object.Destroy(extraction);
            if (pending == null) UnregisterReload();
        }

        // Only teardown may wait for this owner's GPU request. Normal refresh/click never blocks on readback.
        internal void Shutdown()
        {
            Dispose();
            if (pending != null)
            {
                readback.WaitForCompletion();
                if (pending != null) { pending.Dispose(); pending = null; }
            }
            UnregisterReload();
        }

        private void UnregisterReload()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
#endif
        }
    }
}
}
