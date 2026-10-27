# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Fixed

- Declared the Unity UI 1.0.0 package dependency and explicit Runtime/Editor references to `UnityEngine.UI`.
- Routed all five Scene handles through a compatibility helper using the rotation overload on older versions and the modern overload on Unity 2022.3 or newer.
- Added a shared committed bounds/inversion state, explicit versus automatic refresh paths, exact change detection and failure invalidation without deactivating the host.
- Scheduled a first refresh on enable, preserved requests raised during refresh, and guarded refresh reentry and initialization when a Canvas is missing. Initialization no longer hides the GameObject.

### Changed

- Active components now attempt their first runtime refresh automatically, even with real-time updates disabled. Initialize the GameObject as inactive when it must start hidden.
- Rendering and raycast filtering read committed state rather than live configuration or helper existence. Geometry clipping, target projection and serialized helper ownership are still pending in the staged repair.

### Documentation

- Clarified manual installation dependencies, source synchronization between engine branches, and the outstanding engine validation. Unity 2022.3.62f3 source compilation was checked; Editor import and Player verification are blocked by the local license. Minimum Unity/Tuanjie versions remain unverified.

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
