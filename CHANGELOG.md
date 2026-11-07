# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Added

- Dedicated `FEasyUIMask` precision component for one standard Simple Image's actual mesh, UV and texture Alpha, with asynchronous shared rendering/hit snapshots, explicit material resource configuration and selected-object GPU previews.
- Package extraction/coverage materials and shaders, precision Inspector status/quality/error reporting, host mapping invalidation, exact input comparison, material/atlas notifications and callback-safe resource ownership.

### Changed

- Rename the 2.0 rectangle component to `FEasyUIRectMask`, preserving its script GUID, serialized rectangle fields, helper ownership and Scene editor. Audit consumer type names: the old `FEasyUIMask` name now means precision.
- Remove the P0 experiment switch, target-local probe, public diagnostic fields and P0 product menus. Precision rejects Sliced/Filled/Tiled, split Alpha and custom materials/effects; target ancestor visibility clipping is excluded.
- Validate 386 automated checks and five actual mouse clicks in isolated source-copy Unity 2022.3.62f3c1 Built-in/D3D11 Windows Mono Players and Editors, covering precision/resources, migration, previews, reloads and representative in-flight scheduling. UPM import is blocked by a local resolver fault; full acceptance and arbitrary same-frame host/layout ordering remain pending. Package version is unchanged.
- Record user confirmation of all 26 numbered manual-scene cases. The transient P14 projection warning was traced to test-switch timing and accepted without a fix; stable rendering and clicks were reported normal.

### Fixed

- Retain precision edit/Prefab previews across Inspector replacements without forcing resampling, and use Canvas/observer dimensions instead of Editor-context Screen dimensions to detect host changes. Real selection, stage and lifecycle transitions still release preview resources.

- Advance edit-mode precision readback explicitly through the editor loop, including in-flight cleanup after deselection; retain the Player command-buffer readback path.

- Retain the P0 screen-region grid alignment and uniform downsampling pitch in the new precision component; representative P1 precision regression passed.

## [2.0.0] - 2026-10-28

### Fixed

- Restore CanvasRenderer material bindings after invalid configuration, alongside the mesh rebuild.

- Scoped Scene input to active Manual area editing and unified Inspector/handle edits through serialized properties and a coalesced main-thread refresh queue.
- Added resource-free edit/Prefab Stage previews and Undo/Redo refresh, with runtime inspection respecting inactive and disabled objects.
- Clipped forward and inverted meshes to the real outer rect, using non-overlapping quads and the same closed-boundary intersection for raycasts.
- Added serialized helper ownership, reload recovery and conservative cleanup that preserves user content and unowned objects.
- Guarded initialization against user hierarchy-callback reentry and released only this editor's captured handle control on exit/navigation.
- Converted target world corners through both root Canvas cameras into mask-local bounds, with explicit projection validation.
- Made Manual offsets and outer geometry respect the mask pivot, with per-axis negative-size normalization.
- Declared the Unity UI 1.0.0 package dependency and explicit Runtime/Editor references to `UnityEngine.UI`.
- Routed all five Scene handles through a compatibility helper using the rotation overload on older versions and the modern overload on Unity 2022.3 or newer.
- Added a shared committed bounds/inversion state, explicit versus automatic refresh paths, exact change detection and failure invalidation without deactivating the host.
- Scheduled a first refresh on enable, preserved requests raised during refresh, and guarded refresh reentry and initialization when a Canvas is missing. Initialization no longer hides the GameObject.

### Added

- Public `Close()` invalidates state and deactivates the host without resetting configuration.

### Changed

- Publish one complete UPM package from main with stable metadata; retire engine-specific branches as installation targets.
- Set the release baseline to Unity 2022.3 and use a major version for changed initialization, failure and coordinate semantics.

- Active components now attempt their first runtime refresh automatically, even with real-time updates disabled. Initialize the GameObject as inactive when it must start hidden.
- Rendering and raycast filtering read committed state rather than live configuration or helper existence.

### Documentation

- Replace outdated license/metadata warnings with actual manual acceptance results and explicit unverified platforms.
- Document direct Git installation, dependency resolution, pinned versions and the single-package release workflow.

## [1.0.0] - 2026-03-31

### Added

- Initial release
- Forward and inverted rectangular mask support
- Two area selection modes: Manual and UITarget
- Raycast filtering based on `raycastTarget`
- Optional real-time update (per-frame refresh)
- Custom Inspector with Scene view handles
- Undo support for all Scene edits
- Custom component icon
- `package.json` for Unity Package Manager installation

### Known Limitations

- Only rectangular mask shapes are supported in this version
