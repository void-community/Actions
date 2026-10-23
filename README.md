# Actions

Reusable GitHub Actions for void-community projects. Pin them to a commit:

```yaml
uses: void-community/actions/<action>@<commit-sha> # v1.0.0
```

## glibc-sysroot

Links a NativeAOT library for Linux against glibc 2.17 instead of the runner's glibc. A library published on `ubuntu-24.04` needs glibc 2.34, so it does not load on Ubuntu 20.04, Debian 11, RHEL 7 or 8, or the `focal` images of eclipse-temurin. The action copies the Ubuntu 18.04 root file system out of .NET's pinned cross-build image (the one .NET builds its own Linux binaries in), installs clang and lld, and gives the properties to pass to `dotnet publish`:

```yaml
- name: Prepare the glibc 2.17 sysroot
  id: sysroot
  if: runner.os == 'Linux'
  uses: void-community/actions/glibc-sysroot@<commit-sha> # v1.0.0
  with:
    rid: ${{ matrix.rid }}   # linux-x64 or linux-arm64
- run: dotnet publish MyAgent.csproj -c Release -r ${{ matrix.rid }} --self-contained ${{ steps.sysroot.outputs.properties }}
```

| Output | |
|---|---|
| `sysroot` | `/crossrootfs/x64` or `/crossrootfs/arm64` |
| `properties` | `-p:SysRoot=<sysroot> -p:LinkerFlavor=lld`, plus `-p:ObjCopyName=llvm-objcopy` when the runner's architecture is not the target's |

It runs on x64 and arm64 runners with Docker. The image itself only runs on x64, so the action copies files out of a created container rather than building inside it. A library linked this way asks for glibc symbols up to 2.17 on arm64 and 2.16 on x64; `linux-arm` is not covered, because the arm32 root file system of .NET 10's image is newer. [JvmBridge](https://github.com/void-community/JvmBridge) uses it for its own agents and runs them on CentOS 7 (glibc 2.17) in CI; see its [**Publishing for Linux**](https://github.com/void-community/JvmBridge/blob/main/docs/guide/publishing.md) guide.

This repository's own workflow runs the action for both targets on x64 and arm64 runners and checks the glibc floor of a library linked against the sysroot.
