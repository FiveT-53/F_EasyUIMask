# F_Easy UI Mask

Simplifies UI mask configuration. Supports configurable forward/inverted rectangular mask with two area selection modes: manual and target UI reference.


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
- `.meta` files are engine-specific. Make sure to install from the correct branch for your engine.

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

## Quick Start

1. Add `F_Easy UI Mask` component to a `UI` GameObject via `Add Component`, `Empty` GameObject is recommended
2. Choose `Area Select Mode`:
   - **Manual**: Adjust `Offset` and `Size`, use the Scene `Edit Area` button to edit visually
   - **UITarget**: Drag a target RectTransform into the `Target` field
3. Toggle `Invert Mask` to switch between forward and inverted modes
4. Call `RefreshMask()` from code when you want to manually update the mask area, you need to call `RefreshMask()` for the first time before `Real-time Update` (if you have selected)

```csharp
// Refresh from code
mask.GetComponent<F_EasyUIMask>().RefreshMask();
```

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
