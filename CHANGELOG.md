# Changelog

All notable changes to the **OpenDefinery Desktop App** are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-08-02

### Changed
- **Migrated to the new OpenDefinery API client.** The app now uses the same
  maintained client as the Revit add-ins, targeting the new Django API:
  - Authentication moved from Drupal CSRF/Basic auth to **DRF token auth**
    (`/v1/auth/token/`, `Definery.Init` + `Token`/`IsAuthenticated`).
  - Pagination moved from offset-based to **page-based** (`?page=`), with the
    pager wired through each request so totals and Next/Prev work again.
  - Model reconciliation: `SharedParameter` → `DefineryParameter`,
    `AddCollection` → `AddToCollection`, `Id` → `DefineryId`, and the
    `Visible`/`UserModifiable` flags are now proper booleans.

### Removed
- The legacy Drupal-era API/model code (`SharedParameter`, `Node`, `Tag`) and
  its request layer.
- Dependencies **RestSharp**, **Newtonsoft.Json**, **CompareNETObjects**, and the
  unused **System.Web** reference.

### Added
- **System.Text.Json** as the single JSON dependency for the API client.
- SDK-style project, signing, and installer scaffolding (from the earlier
  modernization pass).

## [0.0.4]

### Added
- Browse and search OpenDefinery collections and their shared parameters.
- Create, fork, and edit parameters; batch-upload parameters to a collection.
- Export a collection's parameters to a Revit shared-parameter `.txt` file.
- Public/private setting for collections; right-click context menu on rows.

### Fixed
- Export functions and batch-loading efficiency; assorted UI and error-handling
  fixes (see commit history for detail).

[0.1.0]: https://github.com/TripleZeroLabs/OpenDefinery-DesktopApp/releases/tag/v0.1.0
[0.0.4]: https://github.com/TripleZeroLabs/OpenDefinery-DesktopApp/releases/tag/v0.0.4
