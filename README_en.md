# F_Easy UI Mask

[中文](README.md)

A rectangular mask for Unity UGUI, designed for onboarding guides, UI highlights and click blocking. Define an area manually or let a transparent cutout follow a UI element.

- **Inverted mask**: keep the target area clear and cover its surroundings to highlight a button or panel.
- **Forward mask**: cover the target area for a localized overlay.
- Includes Scene editing handles, runtime target tracking and area-based UI raycast filtering.

> This component draws an overlay. It does not automatically clip child UI like Unity's `Mask` or `RectMask2D`.

## Requirements

- Unity 2022.3 LTS; verified on **2022.3.62f3c1**.
- UGUI; the `com.unity.ugui` dependency is installed automatically.
- Tuanjie and other Unity versions have not been verified.

Tuanjie compatibility testing and adaptation are planned for a future release. The current release does not claim Tuanjie support. Verified Tuanjie versions will be listed in this README and the changelog once testing is complete; no release date is set.

## Installation

Install Git and check that `git --version` works. Restart Unity Hub and the Editor after installing Git.

Open **Window → Package Manager → ＋ → Add package from git URL** and enter:

```text
https://github.com/FiveT-53/F_EasyUIMask.git#v2.0.0
```

Wait for compilation to finish. No manual source download or Assets copy is needed.

If you previously copied this plugin into your project, back up and remove that copy first to avoid duplicate types or assemblies. To upgrade, add the new release's Git URL in Package Manager; pinned releases do not update automatically.

## Quick start: highlight a button

1. Create a UI Image under your Canvas, name it `GuideMask`, and stretch its RectTransform over the area you want to cover.
2. Remove its **Image component** and add **F_Easy UI Mask**. Place `GuideMask` last among the Canvas children so it draws above the target UI.
3. Set **Color** to a translucent black, for example with an alpha of `0.65`.
4. Set **Area Select Mode** to `UITarget` and assign the button's RectTransform to **Target UI**.
5. Enable **Invert Mask** to leave the target clear and darken its surroundings. Enable **Real Time Update** if the target will move.
6. Enter Play mode. With **Raycast Target** enabled, the cutout passes clicks through while the covered area blocks them.

The Canvas needs a GraphicRaycaster and the scene needs an EventSystem for button interaction. Keep the target button outside the `GuideMask` hierarchy so hiding the mask does not hide the button too.

For Screen Space - Camera or World Space, assign the correct camera to the root Canvas of both the mask and the target.

### Define an area manually

Set **Area Select Mode** to `Manual`, then configure **Target Offset** and **Target Size**. Click **Edit Area** in the Inspector to drag the center and edges in Scene view; press Escape to exit editing.

`Target Offset` is relative to the mask's own pivot. After moving a target in Edit mode, select the mask and click **Refresh Preview** to update its preview.

## Common settings

| Setting | Purpose |
| --- | --- |
| `areaSelectMode` | `Manual` for an explicit area; `UITarget` for a target UI rectangle |
| `targetUI` | RectTransform to follow in UITarget mode |
| `targetOffset` / `targetSize` | Manual area center offset and dimensions, in mask-local coordinates |
| `invertMask` | On: cover the outside and leave the inside clear; off: cover the inside |
| `realTimeUpdate` | Follow target or configuration changes during Play mode |
| `color` | Overlay color and opacity |
| `raycastTarget` | Block UI clicks in the covered area; independent of color opacity |

The mask only operates inside its own RectTransform. Parts of the target outside that boundary are not covered. UITarget uses a rectangular bounding area, so rotated targets may have clear corners around them; it does not trace transparent pixels in an image.

## Scripting

Save this as `MaskExample.cs`, attach it to an independent object outside the mask hierarchy, and assign `mask` in the Inspector. Call `ShowFor(target)` to show the guide and `Hide()` to close it.

```csharp
using UnityEngine;
using F_EasyUIMask;

public class MaskExample : MonoBehaviour
{
    [SerializeField] private FEasyUIMask mask;

    public void ShowFor(RectTransform target)
    {
        if (mask == null || target == null) return;
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

- With real-time updates disabled, call `RefreshMask()` after changing parameters or moving the target to update rendering and click filtering together.
- `RefreshMask()` can activate the mask GameObject, but does not activate its parents or enable a disabled component.
- `Close()` deactivates the entire mask GameObject, including its children. Start the object inactive if it should initially be hidden.
- A missing target clears the mask's rendering and click blocking. After restoring the reference, real-time mode recovers automatically; otherwise, refresh explicitly.

See the [behavior and API reference](Documentation~/Reference.md) for the full API, coordinate and camera restrictions, editor previews and migration from 1.x.

## Troubleshooting

- **Git not found or download failed**: check Git on PATH and network access to GitHub, then restart Hub and the Editor.
- **Duplicate types or assemblies**: look for another copy under Assets, local packages or embedded packages.
- **Mask not visible**: check active objects and parents, component enablement, color alpha, target reference and Canvas cameras.
- **Cutout does not follow the target**: enable Real Time Update or call `RefreshMask()` after making changes.
- **Cannot click inside the cutout**: check the EventSystem, the Canvas GraphicRaycaster and other UI elements in front of the target with Raycast Target enabled.

When reporting a problem, include your editor and plugin versions, reproduction steps and the first complete Console error.

## License

[MIT](LICENSE). See the [CHANGELOG](CHANGELOG.md) for updates and [release maintenance](Documentation~/Release.md) for contributing.
