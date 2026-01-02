# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Dates are in JST (UTC+09:00).

## [Unreleased]
- No unreleased changes.

## [1.0.1] - 2026-01-02
### Changed
- Include `UNITY_ASSERTIONS` in fail-fast `Conditional` guards so validations can run when assertions are enabled in Player builds.
- Docs: Sync `README.md` and `README.ja.md` (structure/terminology), clarify DEV/EDITOR/ASSERTIONS fail-fast scope in Player builds, and apply minor wording/formatting cleanup.

### Fixed
- Deactivate before destroy (`DeactivateAndDestroy`) to avoid same-frame re-detection when `Destroy` is deferred and object searches can re-find the instance within the same frame.

## [1.0.0] - 2026-01-01
### Added
- Initial release.

[Unreleased]: https://github.com/tomo-ludens/unity-policy-singleton/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/tomo-ludens/unity-policy-singleton/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/tomo-ludens/unity-policy-singleton/releases/tag/v1.0.0
