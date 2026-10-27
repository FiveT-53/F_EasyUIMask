# F_Easy UI Mask

> 🏠 **中文**: [README.md](README.md)

Simplifies UI mask configuration. Supports configurable forward/inverted rectangular mask and two area selection modes: manual and target UI reference.


## Features

- **Forward / Inverted Mask** — Toggle between cutting out the target area or covering everything except the target area
- **Two Area Selection Modes**
  - *Manual*: Define offset and size directly via handles in Scene view
  - *UITarget*: Automatically follow a specified RectTransform
- **Raycast Filtering** — Respects `raycastTarget` to control whether clicks can pass through the masked area
- **Real-time Update** — Optional per-frame refresh for following moving targets
- **Custom Inspector** — Built-in Scene handles for intuitive area editing
- **Undo Support** — All edits in the Scene view are undoable

## Known Limitations

- Currently only **rectangular** mask shapes are supported

## Requirements

- 团结引擎 1.8.4+ / Unity 2019.4+
- Requires Unity UI (`com.unity.ugui`, declared as `1.0.0` in the package manifest). UPM requests this dependency automatically; install Unity UI through Package Manager before a manual Assets installation.
- `.meta` files are engine-specific. Make sure to install from the correct branch for your engine.

These versions are minimum compatibility targets; cross-version engine acceptance is pending. Source compilation was checked against Unity 2022.3.62f3 assemblies. Clean import, Scene interaction and Player builds remain unverified because a valid local Editor license was unavailable. Unity 2019.4 and Tuanjie 1.8.4 still require validation in their respective environments.

> [!NOTE]
> GUIDs are **not compatible** between Unity and 团结引擎. Always install from the correct branch for your engine.

## Installation

### Via Package Manager (UPM)

1. Open `Window > Package Manager`
2. Click `+` in the top-left corner → `Add package from git URL`
3. Paste the URL for your engine:

| Engine | Branch | Install URL |
|--------|--------|-------------|
| 团结引擎 | `tuanjie` | `https://github.com/FiveT-53/F_EasyUIMask.git#tuanjie` |
| Unity | `unity` | `https://github.com/FiveT-53/F_EasyUIMask.git#unity` |

> If no branch is specified, the default (`main`) branch contains source code without `.meta` files and is not directly installable.

### Via Git URL with Version

You can also pin to a specific version tag:

```
https://github.com/FiveT-53/F_EasyUIMask.git#v1.0.0-tuanjie
https://github.com/FiveT-53/F_EasyUIMask.git#v1.0.0-unity
```

### Manual

If installing manually, download the branch for your engine and copy the `FEasyUIMask` folder into your project's `Assets` folder.

### Source maintenance

Commit shared source changes on `main`, then synchronize them to `unity` and `tuanjie`. Preserve existing `.meta` files on each engine branch and generate metadata for new assets with the corresponding engine. Do not copy GUIDs between engines or regenerate existing asset GUIDs.

## Quick Start

1. Add `F_Easy UI Mask` component to a `UI` GameObject via `Add Component`, `Empty` GameObject is recommended
2. Choose `Area Select Mode`:
   - **Manual**: Adjust `Offset` and `Size`, use the Scene `Edit Area` button to edit visually
   - **UITarget**: Drag a target RectTransform into the `Target` field
3. Toggle `Invert Mask` to switch between forward and inverted modes
4. In Play Mode, an active, enabled component attempts its first refresh in a subsequent `LateUpdate`, even with `Real-time Update` disabled. Call `RefreshMask()` to commit later target, size or inversion changes, or enable `Real-time Update` to check for changes each frame.

```csharp
// Refresh from code
mask.GetComponent<F_EasyUIMask>().RefreshMask();
```

`RefreshMask()` may activate the mask GameObject itself, but does not activate its parent or enable a disabled component. Every valid explicit refresh requests vertex rebuilding; rendering takes effect during the normal Canvas rebuild. With real-time updates disabled, field assignments preserve the previous committed bounds and inversion mode until an explicit refresh or a structural change triggers another refresh. For targets driven by automatic layout, refresh explicitly after layout finishes.

Migration: `Awake()` / `Init()` no longer hide the entire GameObject. Set the object initially inactive if your code relies on it starting hidden. A detected missing Canvas, target or required camera clears only this component's rendering and hit state, leaving the host active. Real-time updates can recover when configuration becomes valid. With real-time updates disabled, a failed request is consumed; explicitly refresh or wait for a new enable, parent or Canvas hierarchy event.

UITarget projects the target's four world corners through its root Canvas camera, then maps the screen points onto the mask plane through the mask's root Canvas camera. The result is a mask-local axis-aligned bounding rectangle. Rotation and perspective can leave empty corners; transparency, child bounds and parent Mask visibility do not shrink it.

Manual `targetOffset` is relative to the mask pivot and sizes use local UI units. Negative size components clamp to zero without rewriting fields; zero size is a valid empty target. To preserve the old runtime center with a non-center pivot, use `newOffset = oldOffset + mask.rectTransform.rect.center`. Assets are not migrated automatically.

UITarget Camera / World Space root canvases require an explicit `worldCamera`; Overlay uses no camera. Both sides must use the same display; individual camera viewports are supported. RenderTexture, stereo cameras and targets crossing camera clipping planes are unsupported. World Space follows the chosen observer only, without simultaneous multi-view alignment. Manual local calculations do not require a camera.

## API Reference

### Public Properties

| Property | Type | Description |
|----------|------|-------------|
| `areaSelectMode` | `AreaSelectMode` | `Manual` or `UITarget` |
| `realTimeUpdate` | `bool` | Update every frame (may impact performance) |
| `invertMask` | `bool` | Toggle forward/inverted mask |
| `targetOffset` | `Vector2` | Mask area center offset (Manual mode) |
| `targetSize` | `Vector2` | Mask area size (Manual mode) |
| `targetUI` | `RectTransform` | Target UI reference (UITarget mode) |

### Public Methods

| Method | Description |
|--------|-------------|
| `RefreshMask()` | Recalculate and update the mask area immediately |
| `Close()` | Hide the mask GameObject |

## License

MIT License. See [LICENSE](LICENSE) for details.
