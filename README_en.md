# F_Easy UI Mask

[中文](README.md)

A rectangular overlay or cutout guide with Manual and UITarget selection. It inherits `MaskableGraphic` and can be clipped by ancestor masks; **it does not automatically clip its children**.

## Requirements and verification

- Minimum compatibility targets: Unity 2019.4+ / Tuanjie 1.8.4+. Minimum-version imports remain unverified.
- Requires `com.unity.ugui`, declared as `1.0.0`. Both plugin assemblies explicitly reference `UnityEngine.UI`.
- Runtime, Editor and Player-conditional source compilation against Unity 2022.3.62f3 assemblies passed, along with isolated state, rectangle mesh and ownership tests.
- A valid local Editor license is unavailable. Clean import, Scene interaction, native lifecycle, Prefab persistence and Player build/run acceptance remain pending. Source checks do not replace engine validation.
- Engine-specific `.meta` files for the new `Runtime/FEasyUIMaskTargetArea.cs` must still be generated and committed using the respective engines. Existing GUIDs are preserved; complete engine-branch and package-install acceptance is pending.

## Installation and branches

Use `Window > Package Manager > + > Add package from git URL` with the appropriate branch:

| Engine | Branch | Git URL |
| --- | --- | --- |
| Unity | `unity` | `https://github.com/FiveT-53/F_EasyUIMask.git#unity` |
| Tuanjie | `tuanjie` | `https://github.com/FiveT-53/F_EasyUIMask.git#tuanjie` |

This repository maintains separate metadata on its engine branches. Preserve each branch's existing GUIDs. `main` contains shared source without `.meta` files: commit source changes there first, synchronize them to the engine branches, and generate metadata for new assets using the corresponding engine. Do not regenerate existing asset GUIDs.

For manual installation, install Unity UI using Package Manager, then copy the matching engine branch into `Assets`. UPM requests the UI dependency from the manifest. Both installation paths still require clean-import verification in licensed engines.

Existing `v1.0.0-unity` / `v1.0.0-tuanjie` tags refer to the old version and do not contain these Unreleased fixes. No new tag or release was created.

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
