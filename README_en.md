# F_Easy UI Mask

[中文](README.md)

A rectangular overlay or cutout guide with Manual and UITarget selection. It inherits `MaskableGraphic` and can be clipped by ancestor masks; **it does not automatically clip its children**.

## Requirements and verification

- Release baseline: Unity 2022.3 LTS. All 26 manual quick-acceptance cases passed on Unity 2022.3.62f3c1. Earlier versions are outside the current release support range.
- UPM resolves `com.unity.ugui` 1.0.0 automatically. Runtime and Editor assemblies reference `UnityEngine.UI` explicitly.
- Native regression checks cover mesh/material recovery after invalid configuration. Full Player builds, complex projection matrices and other editor versions still require verification.
- Tuanjie compatibility is unverified. Unity results do not establish Tuanjie support. The same standard UPM package is intended for future verification rather than separate engine-metadata release branches.

## Install using a Git URL

1. Install Git and ensure `git --version` works. Restart Unity Hub and the Editor after changing PATH.
2. Open `Window > Package Manager`, then `+ > Add package from git URL`.
3. Paste the maintained package URL:

```text
https://github.com/FiveT-53/F_EasyUIMask.git#main
```

No manual source download, Assets copy or prior Unity UI installation is needed. The repository root is a complete UPM package with source, assembly definitions, assets and stable `.meta` files.

Alternatively, add this entry to the existing `dependencies` in your project's `Packages/manifest.json`:

```json
"com.fivet.feasyuimask": "https://github.com/FiveT-53/F_EasyUIMask.git#main"
```

### Updates and pinned versions

- `main` is the maintained branch. UPM locks Git dependencies to a commit. Re-add the same Git URL in Package Manager to fetch a new commit; do not delete the entire lock file or Library.
- For production, replace `#main` with an existing release tag or full commit hash. Old `v1.0.0*` tags do not include the 2.0.0 fixes.
- `unity` / `tuanjie` remain historical branches, not installation entry points for new versions. Re-add the `#main` URL to update an existing installation of the same package.
- If a copy was previously installed under Assets, back it up and remove that plugin copy before installing through UPM to avoid duplicate assemblies/types. Preserve your own application scripts.

### Troubleshooting

- Git executable not found: check PATH and restart Hub and the Editor.
- GitHub connection failure: verify Git network access separately from script compilation errors.
- Duplicate types/assemblies: check Assets, embedded packages and local packages for another copy.
- Report the first complete Package Manager/Console error, editor version and Git URL.

See [release maintenance](Documentation~/Release.md) for the development and publishing workflow.
## Quick start

1. Create a UI GameObject under a Canvas, add `F_Easy UI Mask`, and configure its outer RectTransform, color and `raycastTarget`.
2. Manual: set `targetOffset` relative to the mask pivot and `targetSize` in local units, or use `Edit Area` in Scene view.
3. UITarget: assign `targetUI`. Camera / World Space root canvases on both sides require `worldCamera`; Overlay uses no camera.
4. `invertMask=false` covers the intersection; `true` covers the outer rect outside the hole.
5. Active, enabled playing components attempt their first refresh in a subsequent `LateUpdate`, even with real-time updates disabled. Initialize the object as inactive when it must start hidden.

Attach this controller to an **independent active object outside the mask hierarchy** and assign its serialized reference. `Close()` will then leave the controller active.

```csharp
using UnityEngine;
using F_EasyUIMask;

public class MaskExample : MonoBehaviour
{
    [SerializeField] private FEasyUIMask mask;

    public void ShowFor(RectTransform target)
    {
        if (mask == null || target == null) return;
        mask.Init(); // Optional; does not show or hide the object.
        mask.areaSelectMode = FEasyUIMask.AreaSelectMode.UITarget;
        mask.targetUI = target;
        mask.invertMask = true;
        mask.RefreshMask();
    }

    public void Hide()
    {
        if (mask != null) mask.Close();
    }
}
```

## Geometry and interaction

Let T be the original target rectangle, O the current mask RectTransform, and I = O ∩ T. Rendering and hit testing share I without overwriting T.

| State | Forward coverage / blocking | Inverted coverage / blocking |
| --- | --- | --- |
| Ordinary intersection | I | O excluding I |
| No intersection or zero-size Manual target | Empty | All O |
| Target covers the outer rect | All O | Empty |
| Invalid projection, missing configuration or unusable outer rect | Empty | Empty |

Blocking assumes `raycastTarget=true`. Target boundaries belong to the intersection and pass through in inverted mode. Points outside O never hit. Disabling `raycastTarget` removes this Graphic as a target without guaranteeing that other UI or descendants pass through. Alpha and raycast participation are independent.

Manual negative size components clamp to zero without rewriting fields; zero is a valid empty target. NaN, Infinity and overflow invalidate the state. A zero-area or non-invertible UITarget is invalid, unlike a valid empty Manual target.

UITarget projects its own four world corners through the two root Canvas configurations into a mask-local axis-aligned bounding rectangle. Rotation, mirroring and perspective can leave rectangular empty corners; transparency, children and ancestor Mask visibility do not shrink the target.

Different parents, canvases, CanvasScalers and camera viewports on the same display are supported. World Space uses the explicitly chosen observer. Cross-display projection, RenderTexture output, stereo cameras, simultaneous multi-view alignment and targets crossing camera clipping planes are unsupported. Not every custom rendering/compositing topology can be detected. Manual local calculations do not require a camera.

## Public fields and methods

| Field | Type | Meaning |
| --- | --- | --- |
| `areaSelectMode` | `AreaSelectMode` | `Manual` or `UITarget` |
| `realTimeUpdate` | `bool` | Check changes each frame while active and enabled |
| `invertMask` | `bool` | false for forward, true for inverted |
| `targetOffset` | `Vector2` | Target center relative to the mask pivot |
| `targetSize` | `Vector2` | Width and height in mask-local UI units |
| `targetUI` | `RectTransform` | Followed target |

| Method | Contract |
| --- | --- |
| `Init()` | Idempotently prepares runtime resources without showing or hiding; a Canvas is not required yet; normally optional |
| `RefreshMask()` | Commits current bounds and mode, may activate this GameObject, never activates its parent or enables the component, and forces a vertex rebuild request |
| `Close()` | Invalidates and deactivates the whole GameObject, affecting its components and descendants; repeatable without clearing configuration |

With real-time updates disabled, assignments retain the previous complete state until an explicit refresh or structural event requests another refresh. Real-time updates dirty vertices only when custom state changes. Rendering takes effect during normal Canvas rebuilding, not synchronously before the call returns. Complex layout has no same-frame zero-delay guarantee; refresh after layout completes when needed.

Detected failure clears only this component's rendering and hit state, leaving the host active. Real-time mode keeps attempting recovery. Otherwise each request is consumed regardless of success and waits for an explicit refresh or another enable/parent/Canvas hierarchy event. A deliberately closed object never reactivates itself.

## Editor and helpers

Inspector, handles, reselection and Undo/Redo share a coalesced main-thread queue. Only a single active selection in Manual / Edit Area captures editing input. Escape exits; Alt/View navigation remains available. Multi-object area editing is not supported.

`Refresh Preview` never activates objects or enables components. Edit Mode and Prefab Stage update only nonserialized caches, without creating helpers or modifying persistent assets; cross-stage targets are rejected. Playing scene instances use the runtime path without activation. Edit-mode real-time checking applies only to the selected mask. With it disabled, refresh manually after moving a target or camera. There is no automatic whole-scene preview.

Runtime `TargetArea` helpers use serialized owner markers for recovery after domain reloads. Same-name user objects are never claimed, migrated or deleted. Moved helpers or those containing user children or extra components are preserved with a warning. Cleanup touches only directly owned areas without user content. Rendering and hit testing do not depend on helpers.

## Migration

- Awake / Init no longer hide the host automatically. Initialize the GameObject as inactive when it must start hidden.
- Manual offsets now use the mask pivot. Preserve an old non-center-pivot runtime center with `newOffset = oldOffset + mask.rectTransform.rect.center`. No bulk migration occurs.
- With real-time updates disabled, call RefreshMask after assignments to commit complete bounds and mode. Configuration failure no longer deactivates the host.

## License

MIT. See [LICENSE](LICENSE).
