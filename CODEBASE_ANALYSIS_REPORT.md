# Jellyfin Server Codebase Analysis Report

## 1) Executive Summary

This repository is a large, modular .NET server codebase centered on Jellyfin backend functionality (API, media metadata, encoding/transcoding, networking, and server runtime). It follows a multi-project architecture with clear domain boundaries and strong test coverage via dedicated test projects.

At a high level:
- The solution contains **42 C# projects** (`*.csproj`), including **16 test projects**.
- The repository contains roughly **1,960 C# source files**.
- `Jellyfin.Server` provides the executable startup path and web host configuration.
- `Jellyfin.Api` exposes HTTP endpoints through a controller-driven API surface.
- Core domain logic is distributed across `MediaBrowser.*`, `Emby.*`, and `Jellyfin.*` projects.

## 2) Repository Shape and Scale

### High-level inventory
- Projects: 42 total (`*.csproj`) with 16 in `tests/`.
- Source files: ~1,960 `.cs` files.
- API surface: 60 controllers and 52 API model classes in `Jellyfin.Api`.

### Top-level module density (by file count)
- `MediaBrowser.Controller` (364 files)
- `src/` (368 files, several modernized Jellyfin components)
- `MediaBrowser.Model` (309 files)
- `Emby.Server.Implementations` (295 files)
- `Jellyfin.Api` (170 files)
- `tests/` (304 files)

This shape suggests a mature monorepo-style backend with a large internal API and many subsystem boundaries.

## 3) Runtime Architecture Overview

## Entry point and process orchestration
`Jellyfin.Server/Program.cs` is the primary startup entry point. It:
- Parses command line startup options.
- Initializes application paths and logging.
- Validates hosted web client content when web hosting is enabled.
- Performs startup migrations.
- Builds and runs the ASP.NET host.
- Coordinates restart behavior after specific operations (e.g., restore).

## Web host and middleware pipeline
`Jellyfin.Server/Startup.cs` configures:
- API services, EF database context, Swagger, auth/authz, and named HTTP clients.
- Hosted background services for recordings, auto-discovery, metadata sync notifications, etc.
- Middleware pipeline for base URL mapping, exception handling, websockets, compression, static files, auth, metrics, and health checks.

Notably, the pipeline conditionally hosts the web client, supports metrics endpoints, and maps API controllers plus health checks.

## Composition root and platform services
`Emby.Server.Implementations/ApplicationHost.cs` acts as a major composition root and server application host abstraction. It wires key infrastructure concerns including:
- Plugin management.
- Network and certificate support.
- Configuration management.
- Core service provider references and runtime metadata.

This class reflects historical layering plus current Jellyfin-specific integration.

## 4) Project and Domain Boundaries

The solution is organized into broad functional areas:

- **Server shell/runtime**:
  - `Jellyfin.Server`
  - `Jellyfin.Server.Implementations`

- **API and transport**:
  - `Jellyfin.Api`
  - `src/Jellyfin.Networking`

- **Media metadata/providers/local metadata**:
  - `MediaBrowser.Providers`
  - `MediaBrowser.LocalMetadata`
  - `MediaBrowser.XbmcMetadata`

- **Domain abstractions/models**:
  - `MediaBrowser.Controller`
  - `MediaBrowser.Model`
  - `MediaBrowser.Common`

- **Media encoding/transcoding**:
  - `MediaBrowser.MediaEncoding`
  - `src/Jellyfin.MediaEncoding.Hls`
  - `src/Jellyfin.MediaEncoding.Keyframes`

- **Naming/photos/legacy-support projects**:
  - `Emby.Naming`
  - `Emby.Photos`
  - `Emby.Server.Implementations`

- **Data/storage**:
  - `Jellyfin.Data`
  - `src/Jellyfin.Database/*`

- **Quality and experimentation**:
  - `tests/*`
  - `fuzz/*`

Overall, the structure indicates domain separation with practical coupling through shared interfaces and host-level service registration.

## 5) Tooling, Standards, and Dependency Management

## Platform and SDK
- `global.json` pins the SDK to **.NET 10.0.0** with `latestMinor` roll-forward.

## Build-wide policy
`Directory.Build.props` enforces:
- Nullable reference types (`<Nullable>enable</Nullable>`).
- Warnings as errors by default.
- Analyzer-rich debug builds (`AnalysisMode=AllEnabledByDefault`).
- Shared analyzer resources (`BannedSymbols.txt`, `stylecop.json`).
- Custom analyzer project (`src/Jellyfin.CodeAnalysis`) attached for debug configurations.

## Package version governance
`Directory.Packages.props` uses central package management (`ManagePackageVersionsCentrally=true`) and lists dependencies spanning:
- ASP.NET Core / Extensions 10.x
- EF Core 10.x
- Serilog ecosystem
- Testing stack (xUnit, Moq, AutoFixture, coverage tooling)
- Media-centric and metadata libraries (TMDb, MusicBrainz, TagLib, SkiaSharp, etc.)

This setup is consistent with enterprise-scale dependency hygiene.

## 6) Testing and CI Posture

The repository has broad test partitioning by subsystem (`Jellyfin.Api.Tests`, `Jellyfin.Server.Tests`, `Jellyfin.Networking.Tests`, etc.).

CI workflow `.github/workflows/ci-tests.yml` runs `dotnet test Jellyfin.sln` across Linux, macOS, and Windows with code coverage collection and report merge steps.

This cross-OS matrix indicates high confidence expectations for runtime portability.

## 7) Observed Strengths

- **Strong modularization** across API, encoding, metadata, networking, and data layers.
- **Mature quality controls** (warnings-as-errors, analyzers, centralized packages).
- **Robust automated testing intent** with many test projects and cross-platform CI.
- **Operational concerns handled in startup** (migrations, storage checks, health checks, metrics, web-host options).

## 8) Potential Maintenance Risks / Complexity Drivers

- **Large monorepo complexity** (~2k C# files) increases onboarding cost and change blast radius.
- **Historical namespace split** (`MediaBrowser.*`, `Emby.*`, `Jellyfin.*`) may obscure ownership and architectural intent for new contributors.
- **Central composition roots** (startup + application host) can become high-coupling hotspots over time.
- **Many plugin/provider integrations** create external dependency and behavior variability.

## 9) Suggested Next Steps

1. Produce a **dependency graph** (project-reference and namespace-level) to visualize coupling hotspots.
2. Add/maintain **architecture decision records (ADRs)** for boundaries between legacy (`Emby/MediaBrowser`) and newer `Jellyfin.*` modules.
3. Track **test execution and coverage by subsystem** (not only merged coverage) to detect weak modules.
4. Consider incremental **ownership mapping** (CODEOWNERS by subsystem) to improve review routing and maintainability.

## 10) Environment Notes for This Analysis

- The local environment does not currently provide the `dotnet` CLI binary, so build/test execution and solution tooling commands could not be run in this session.
- The report is based on static repository inspection (files, project metadata, startup/runtime source, and workflow configuration).
