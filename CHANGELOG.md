# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Fixed

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

- Active components now attempt their first runtime refresh automatically, even with real-time updates disabled. Initialize the GameObject as inactive when it must start hidden.
- Rendering and raycast filtering read committed state rather than live configuration or helper existence.

### Documentation

- Clarified manual installation dependencies, source synchronization between engine branches, and the outstanding engine validation. Unity 2022.3.62f3 source compilation was checked; Editor import and Player verification are blocked by the local license. Minimum Unity/Tuanjie versions remain unverified.
- Synchronized bilingual API, interaction, migration and preview documentation with an independently compiled controller example. New helper metadata still requires generation in the corresponding engines before complete package acceptance.

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
