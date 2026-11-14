# Actions

Shared CI for .NET repositories. A repository adds one workflow file and pins it to `v1`, which moves when `main` passes its tests.

## Workflow

Copy this file to `.github/workflows/ci.yml` as it is. Set the values under `with` and remove the entries the repository does not use. Keep the `nuget` job even if the repository publishes no packages: it is skipped then.

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
  workflow_dispatch:

permissions:
  packages: write
  pages: write
  id-token: write
  contents: write
  issues: write
  pull-requests: write

jobs:
  dotnet:
    uses: void-community/actions/.github/workflows/dotnet.yml@v1
    with:
      # Version scheme: release-please or timestamp
      version: timestamp
      # Projects packed and pushed to nuget.org, separated by spaces
      pack: src/MyLibrary
      # Project published as an executable for every runtime
      publish: src/MyApp/MyApp.csproj
      # GHCR repository the published project is pushed to
      container: my-owner/my-app

  nuget:
    needs: dotnet
    if: ${{ needs.dotnet.outputs.nuget == 'true' }}
    runs-on: ubuntu-latest
    steps:
      - uses: void-community/actions/nuget-push@v1
```

A pull request builds and tests the solution in the repository root. A push to the default branch also publishes. Every input is optional:

| Input | |
|---|---|
| `version` | `release-please` or `timestamp`; without it the build picks its own version |
| `dotnet-version` | The .NET SDK, `latest` by default |
| `operating-systems` | The runners the tests run on, as a JSON list; Linux, macOS and Windows on x64 and arm64 by default |
| `pack` | The projects to pack and push to nuget.org, separated by spaces |
| `publish` | The `.csproj` to publish as an executable for every runtime, uploaded on every run |
| `container` | The GHCR repository to push `publish` to as `latest`, for example `my-owner/my-app` |

What an input asks of the repository:

- `version: release-please`: keep `.github/RELEASE_PLEASE/config.json` and `manifest.json`.
- `version: timestamp`: the build computes its version from the `VersionTimestamp` property.
- `pack`: add a trusted publishing policy on nuget.org that names the repository and `ci.yml`. The packages are pushed when a release is created, or on every push to the default branch with `version: timestamp`.

Require the `dotnet / required` check in branch protection.

## Renovate

Renovate runs from this repository every hour. Add the repository to the list in [`maintenance-renovate.yml`](.github/workflows/maintenance-renovate.yml), then delete its own Renovate workflow and `renovate.json`. Keep a `renovate.json` only for rules of its own.

## glibc-sysroot

Links a NativeAOT library for Linux against an old glibc instead of the runner's, so that it loads on older distributions:

```yaml
- name: Prepare the glibc 2.17 sysroot
  id: sysroot
  if: runner.os == 'Linux'
  uses: void-community/actions/glibc-sysroot@v1
  with:
    rid: ${{ matrix.rid }}   # linux-x64 or linux-arm64
    # glibc: '2.12'          # linux-x64 only
- run: dotnet publish MyAgent.csproj -c Release -r ${{ matrix.rid }} --self-contained ${{ steps.sysroot.outputs.properties }}
```

| Input | |
|---|---|
| `rid` | `linux-x64` or `linux-arm64` |
| `glibc` | `2.17` (the default) or, for `linux-x64` only, `2.12` |

| Output | |
|---|---|
| `sysroot` | The sysroot directory |
| `properties` | The properties to pass to `dotnet publish` |
