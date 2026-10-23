# Actions

Reusable GitHub Actions for void-community projects. There are no releases or tags: pin an action to a commit of `main` and name the branch in the comment, so that Renovate updates the pin when `main` moves:

```yaml
uses: void-community/actions/<action>@<commit-sha> # main
```

## glibc-sysroot

Links a NativeAOT library for Linux against an old glibc instead of the runner's. A library published on `ubuntu-24.04` needs glibc 2.34, so it does not load on Ubuntu 20.04, Debian 11, RHEL 7 or 8, or the `focal` images of eclipse-temurin. The action prepares a sysroot, installs clang and lld, and gives the properties to pass to `dotnet publish`:

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
| `sysroot` | `/crossrootfs/x64` or `/crossrootfs/arm64` for glibc 2.17, `/crossrootfs/x64-glibc-2.12` for 2.12 |
| `properties` | `-p:SysRoot=<sysroot> -p:LinkerFlavor=lld`, plus `-p:ObjCopyName=llvm-objcopy` when the runner's architecture is not the target's |

**glibc 2.17** copies the Ubuntu 18.04 root file system out of .NET's pinned cross-build image (the one .NET builds its own Linux binaries in). A library linked against it asks for glibc symbols up to 2.17 on arm64 and 2.16 on x64. The image only runs on x64, so the action copies files out of a created container rather than building inside it, and it needs Docker. `linux-arm` is not covered, because the arm32 root file system of .NET 10's image is newer.

**glibc 2.12** reaches CentOS 6, Debian 7 and the last Java 6 images. The action unpacks the last CentOS 6.10 packages from vault.centos.org, pinned by checksum: glibc and its headers, the kernel headers, and gcc's start files and libgcc. glibc 2.12 lacks `aligned_alloc` (2.16), which the .NET runtime calls, so the action adds a hidden copy built on `posix_memalign` to the sysroot's `libc_nonshared.a`, which every link against that glibc pulls in. A library linked against it asks for glibc symbols up to 2.12 and exports no `aligned_alloc` of its own. arm64 has no glibc older than 2.17, where glibc's arm64 port starts.

[JvmBridge](https://github.com/void-community/JvmBridge) uses both for its own agents: it runs them on CentOS 7 (glibc 2.17) and, for Java 6 and 7, on CentOS 6 (glibc 2.12) in CI. See its [**Publishing for Linux**](https://github.com/void-community/JvmBridge/blob/main/docs/guide/publishing.md) guide.

This repository's own workflow runs the action for every target and glibc on x64 and arm64 runners, checks the glibc floor of a library linked against the sysroot, and runs it in CentOS 6 for glibc 2.12.
