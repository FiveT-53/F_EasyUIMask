# F_Easy UI Mask

[中文](README.md)

Overlay masks for Unity UGUI, designed for onboarding guides, UI highlights and click blocking. Define a rectangular area manually, or let a cutout follow the outline of a UI image.

- **Inverted mask**: keep the target area clear and cover its surroundings to highlight a button or panel.
- **Forward mask**: cover the target area for a localized overlay.
- Rectangle areas have Scene editing handles. Both components support runtime target tracking and UI raycast filtering within the mask area.

> This component draws an overlay. It does not automatically clip child UI like Unity's `Mask` or `RectMask2D`.

## Choose a component

| Component | Use it for |
| --- | --- |
| `FEasyUIRectMask` | A rectangular cutout around a button or panel, or an area with a manually chosen position and size |
| `FEasyUIMask` | A cutout following one Simple Image, including circles, stars, concave outlines and transparent holes |

The current source includes both components. A new release tag has not been published, and the package version remains 2.0.0 for now. Read [Upgrading from 2.0.0](#upgrading-from-200) before upgrading an existing project.

## Requirements

- Unity 2022.3 LTS; verified on **2022.3.62f3c1**. See [release maintenance](Documentation~/Release.md) for the tested environment and scope.
- UGUI; the `com.unity.ugui` dependency is installed automatically.
- Precision requires asynchronous GPU readback support; unsupported devices report an error.
- Tuanjie and other Unity versions have not been verified.

Tuanjie compatibility testing and adaptation are planned for a future release. The current release does not claim Tuanjie support. Verified Tuanjie versions will be listed in this README and the changelog once testing is complete; no release date is set.

## Installation

Install Git and check that `git --version` works. Restart Unity Hub and the Editor after installing Git.

Open **Window → Package Manager → ＋ → Add package from git URL** and enter this link to install the verified commit containing both components:

```text
https://github.com/FiveT-53/F_EasyUIMask.git#db691f136199e7e6cf69b30f161c2240f71d413a
```

Wait for compilation to finish. No manual source download or Assets copy is needed.

Once a new version is released, use its official tag. The existing `#v2.0.0` tag still installs the old rectangle version without the precision component; see its [v2.0.0 README](https://github.com/FiveT-53/F_EasyUIMask/blob/v2.0.0/README_en.md) for instructions. The `#unity` and `#tuanjie` installation links are no longer used. See [release maintenance](Documentation~/Release.md) for branch and version history.

If you previously copied this plugin into your project, back up and remove that copy first to avoid duplicate types or assemblies. To upgrade, add the new release's Git URL in Package Manager; pinned releases do not update automatically.

## Quick start: highlight a button

### Rectangle cutout

1. Create a UI Image under your Canvas, name it `GuideMask`, and stretch its RectTransform over the area you want to cover.
2. Remove its **Image component** and add **F_Easy UI Rect Mask**. Place `GuideMask` last among the Canvas children so it draws above the target UI.
3. Set **Color** to a translucent black, for example with an alpha of `0.65`.
4. Set **Area Select Mode** to `UITarget` and assign the button's RectTransform to **Target UI**.
5. Enable **Invert Mask** to leave the target clear and darken its surroundings. Enable **Real Time Update** if the target will move.
6. Enter Play mode. With **Raycast Target** enabled, the cutout passes clicks through while the covered area blocks them.

The Canvas needs a GraphicRaycaster and the scene needs an EventSystem for button interaction. Keep the target button outside the `GuideMask` hierarchy so hiding the mask does not hide the button too.

For Screen Space - Camera or World Space, assign the correct camera to the root Canvas of both the mask and the target.

### Define an area manually

Set **Area Select Mode** to `Manual`, then configure **Target Offset** and **Target Size**. Click **Edit Area** in the Inspector to drag the center and edges in Scene view; press Escape to exit editing.

`Target Offset` is relative to the mask's own pivot. After moving a target in Edit mode, select the mask and click **Refresh Preview** to update its preview.

### Follow an icon's outline

1. Create a new UI Image called `GuideMask`. Remove its Image component and add **F_Easy UI Mask**. Set its bounds, hierarchy position and color as above.
2. Assign the RectTransform of the icon's Image object to **Target UI**. It needs a standard **Image**, with **Image Type** set to `Simple` and a Sprite assigned.
3. Enable **Invert Mask**. Select the mask and wait for **Ready** in the Inspector to see its editor preview. Entering Play mode also prepares the mask automatically.
4. Enable **Real Time Update** if the icon moves or changes Sprite. With real-time updates off, click **Refresh Preview** in the Editor or call `RefreshMask()` at runtime.

The target is the icon itself; child objects are not combined automatically. If a button has separate background and icon objects, assign the Image you want to highlight.

Precision uses the Sprite's transparency without requiring Read/Write on the source texture. It currently supports one standard Simple Image. Sliced, Filled, Tiled, RawImage, TMP, split Alpha, custom materials and mesh effects are not supported. Target tint, CanvasGroup opacity and ancestor Mask/RectMask2D clipping do not affect the extracted outline.

The first result needs asynchronous preparation; until it is ready, the mask neither draws nor blocks clicks. Rendering and click filtering update together. See the [behavior reference](Documentation~/Reference.md) for precision and resource costs.

## Common settings

| Setting | Purpose |
| --- | --- |
| `targetUI` | RectTransform to follow; precision requires a supported Image on that object |
| `invertMask` | On: cover the outside and leave the inside clear; off: cover the inside |
| `realTimeUpdate` | Follow target or configuration changes during Play mode |
| `color` | Overlay color and opacity |
| `raycastTarget` | Block UI clicks in the covered area; independent of color opacity |
| `areaSelectMode` (rectangle) | `Manual` for an explicit area; `UITarget` for the target's rectangular bounds |
| `targetOffset` / `targetSize` (rectangle) | Manual area center offset and dimensions, in mask-local coordinates |
| `alphaThreshold` (precision) | Pixels at or above this Alpha belong to the target shape; default 0.5 |
| `maxTextureSize` (precision) | Internal sampling texture size cap; reducing it can save resources for large masks at the cost of a coarser outline |

Both masks operate only inside their own RectTransform. The rectangle component's UITarget uses rectangular bounds, which can leave clear corners around a rotated target. Use the precision component when you need the cutout to follow the image outline.

A cutout only means this mask lets the click through; the underlying button still follows its own hit rules. Setting Color Alpha to zero does not stop click blocking. Disable **Raycast Target** when you want the overlay to stop intercepting clicks.

## Scripting

This example uses the precision component. Save it as `MaskExample.cs`, attach it to an independent object outside the mask hierarchy, and assign a configured `mask` in the Inspector. Call `ShowFor(target)` to show the guide and `Hide()` to close it.

```csharp
using UnityEngine;
using F_EasyUIMask;

public class MaskExample : MonoBehaviour
{
    [SerializeField] private FEasyUIMask mask;

    public void ShowFor(RectTransform target)
    {
        if (mask == null || target == null) return;
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

For a rectangle mask, change the field type to `FEasyUIRectMask` and set `mask.areaSelectMode = FEasyUIRectMask.AreaSelectMode.UITarget;` inside `ShowFor()`. If you create a precision mask with `AddComponent` at runtime, also supply the package materials through `ConfigureResources()`; see the [behavior reference](Documentation~/Reference.md) for an example.

- With real-time updates disabled, call `RefreshMask()` after changing parameters or moving the target to update rendering and click filtering together.
- `RefreshMask()` can activate the mask GameObject, but does not activate its parents or enable a disabled component.
- `Close()` deactivates the entire mask GameObject, including its children. Start the object inactive if it should initially be hidden.
- A missing target clears the mask's rendering and click blocking. After restoring the reference, real-time mode recovers automatically; otherwise, refresh explicitly.

See the [behavior and API reference](Documentation~/Reference.md) for the full API, coordinate and camera restrictions, editor previews and migration from older versions.

## Upgrading from 2.0.0

Back up your project first. The old rectangle component is now named `FEasyUIRectMask`; `FEasyUIMask` is the new precision component.

- Existing rectangle components in scenes and Prefabs remain rectangle masks. They do not become precision masks automatically.
- Update rectangle consumer code from `FEasyUIMask` to `FEasyUIRectMask`, including fields, `GetComponent` calls and inheritance. Even where code still compiles, check that it refers to the intended component.
- To use precision, explicitly add a new **F_Easy UI Mask** component and assign its target and settings.

## Troubleshooting

- **Git not found or download failed**: check Git on PATH and network access to GitHub, then restart Hub and the Editor.
- **Duplicate types or assemblies**: look for another copy under Assets, local packages or embedded packages.
- **Mask not visible**: check active objects and parents, component enablement, color alpha, target reference and Canvas cameras.
- **Precision preview not ready**: read the Inspector message and check for a supported Simple Image target. If material references are missing, use **Package Resources → Restore Package Resources**.
- **Cutout does not follow the target**: enable Real Time Update or call `RefreshMask()` after making changes.
- **Cannot click inside the cutout**: check the EventSystem, the Canvas GraphicRaycaster and other UI elements in front of the target with Raycast Target enabled.

When reporting a problem, include your editor and plugin versions, reproduction steps and the first complete Console error.

## License

[MIT](LICENSE). See the [CHANGELOG](CHANGELOG.md) for updates and [release maintenance](Documentation~/Release.md) for contributing.
