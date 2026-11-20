# Actions

Shared CI for .NET repositories. A repository adds one workflow file, which follows `main` of this repository.

## Workflow

Copy this file to `.github/workflows/ci.yml` as it is. Set the values under `with` and remove the entries the repository does not use. Keep the `nuget` job even if the repository publishes no packages: it is skipped then.

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
  workflow_dispatch:
  schedule:
    - cron: "0 * * * *"

permissions:
  packages: write
  pages: write
  id-token: write
  contents: write
  issues: write
  pull-requests: write

jobs:
  entrypoint:
    uses: void-community/actions/.github/workflows/entrypoint.yml@main
    secrets:
      RENOVATE_TOKEN: ${{ secrets.RENOVATE_TOKEN }}
    with:
      # Version scheme: release-please or timestamp
      version: timestamp
      # Projects packed and pushed to nuget.org, separated by spaces
      pack: src/MyLibrary
      # Projects published as executables for every runtime, separated by spaces
      publish: src/MyApp
      # GHCR repositories the published projects are pushed to, in the same order
      container: my-owner/my-app

  # TODO: Remove once nuget.org lets the entrypoint push the packages: https://github.com/NuGet/NuGetGallery/issues/11000
  nuget:
    needs: entrypoint
    if: ${{ needs.entrypoint.outputs.nuget == 'true' }}
    runs-on: ubuntu-latest
    steps:
      - uses: void-community/actions/nuget-push@main
```

A pull request builds and tests the solution in the repository root. A push to the default branch also publishes, once the tests passed. A repository without a solution is not tested, and gets no GitHub release and no images. Renovate runs every hour, and with a manual run. Every input is optional:

| Input | |
|---|---|
| `version` | `release-please` or `timestamp`, given to the build as its `Version`; without it the build picks its own version |
| `dotnet-version` | The .NET SDK, `latest` by default. Renovate updates a version pinned here |
| `operating-systems` | The runners that test, publish and build the images, as a JSON list; Linux, macOS and Windows on x64 and arm64 by default. A runtime is only tested and published where its runner is in the list |
| `shards` | The number of jobs the tests are split into for every runtime, 8 by default; 1 runs them in one job |
| `check-formatting` | The tests fail when the build rewrote sources, as a build that formats them does; `false` turns that off |
| `pack` | The projects to pack and push to nuget.org, separated by spaces |
| `publish` | The projects to publish as executables for every runtime, separated by spaces; uploaded on every run. The `linux-x64` and `linux-arm64` ones load on glibc 2.17 and later |
| `bundle` | A target of the published projects that makes the release files from the output of every runtime; without it they are one archive for each project and runtime |
| `release` | `false` publishes the projects for the tests only, without a GitHub release |
| `linux-packages` | More apt packages for the Linux runners, separated by spaces |
| `container` | The GHCR repositories to push the published projects to as `latest`, one for each project of `publish` in the same order, for example `my-owner/my-app` |

What an input asks of the repository:

- `version: release-please`: keep `.github/RELEASE_PLEASE/config.json` and `manifest.json`.
- `version: timestamp`: nothing. Every run gets a version from the date and the time of day, such as `26.11.15.1396`.
- `publish`: nothing. A release run also makes a GitHub release with the files and a table of downloads, one for each project when there are several: every push to the default branch with `version: timestamp`, and each release that release-please creates. The tests run after the build and find its output under `artifacts`, as `publish-<runtime>/<project>`, and the release files in `artifacts/bundle`.
- `bundle`: the target writes the release files to `BundleDirectory` and finds the output of every runtime under `RuntimesDirectory`, as `publish-<runtime>/<project>`.
- `pack`: add a trusted publishing policy on nuget.org that names the repository and `ci.yml`. The packages are pushed when a release is created, or on every push to the default branch with `version: timestamp`. The build and the tests come after the packing and find the packages in `artifacts/packages`.

The tests run once for every runtime: on the runner of the runtime, as x86 on Windows for `win-x86`, and in a container of the runtime for `linux-arm`, `linux-musl-x64` and `linux-musl-arm64`, whose .NET SDK is the image's. They run with VSTest or Microsoft.Testing.Platform, whichever the test projects use.

## Renovate

Renovate needs a `RENOVATE_TOKEN` secret in the repository or its organization. Delete the repository's own Renovate workflow and `renovate.json`, and keep a `renovate.json` only for rules of its own.

## glibc-sysroot

Links a NativeAOT library for Linux against an old glibc instead of the runner's, so that it loads on older distributions. Pin it to a commit of `main` and name the branch in the comment, so that Renovate updates the pin and the repository's CI tests each update:

```yaml
- name: Prepare the glibc 2.17 sysroot
  id: sysroot
  if: runner.os == 'Linux'
  uses: void-community/actions/glibc-sysroot@<commit-sha> # main
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
