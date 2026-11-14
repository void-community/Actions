using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Xunit;

namespace GlibcSysroot.Tests;

public class GlibcSysrootTests
{
    private const string CentOs6Image = "centos:6@sha256:a93df2e96e07f56ea48f215425c6f1673ab922927894595bb5c0ee4c5a955133";

    // A library that calls the functions a NativeAOT runtime takes from glibc (threads, the dynamic loader and aligned_alloc) is linked against
    // the sysroot, must ask for no glibc newer than the one the sysroot promises, and must bind every symbol it takes from glibc to a version.
    // Every case runs on an x64 and an arm64 runner, so each target is linked from a runner of its own architecture and from the other one.
    [GitHubLinuxRunnerTheory]
    [InlineData("linux-x64", "2.17", "x86_64-linux-gnu")]
    [InlineData("linux-arm64", "2.17", "aarch64-linux-gnu")]
    [InlineData("linux-x64", "2.12", "x86_64-linux-gnu")]
    public void Sysroot_LinksLibraryThatLoadsOnItsGlibc(string runtimeIdentifier, string glibc, string triple)
    {
        var workingDirectory = Directory.CreateTempSubdirectory("glibc-sysroot-").FullName;
        var outputs = RunAction(runtimeIdentifier, glibc, workingDirectory);
        var sysroot = outputs["sysroot"];

        if (glibc is "2.17")
            Assert.Contains("VERSION=\"18.04", File.ReadAllText(Path.Combine(sysroot, "etc/os-release")));
        else
            Assert.Contains("__GLIBC_MINOR__\t12", File.ReadAllText(Path.Combine(sysroot, "usr/include/features.h")));

        Assert.Contains($"-p:SysRoot={sysroot} -p:LinkerFlavor=lld", outputs["properties"]);

        File.Copy(Path.Combine(AppContext.BaseDirectory, "probe.c"), Path.Combine(workingDirectory, "probe.c"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "main.c"), Path.Combine(workingDirectory, "main.c"));

        Run(workingDirectory, "clang", $"--target={triple}", $"--sysroot={sysroot}", "-fuse-ld=lld", "-shared", "-fPIC", "-pthread", "probe.c", "-o", "libprobe.so", "-ldl");
        Run(workingDirectory, "clang", $"--target={triple}", $"--sysroot={sysroot}", "-fuse-ld=lld", "main.c", "-o", "probe", "-L.", "-lprobe", "-Wl,-rpath,$ORIGIN");

        var newestGlibc = Regex.Matches(Run(workingDirectory, "readelf", "-V", "libprobe.so"), @"GLIBC_([0-9.]+)").Max(match => Version.Parse(match.Groups[1].Value));

        Assert.NotNull(newestGlibc);
        Assert.True(newestGlibc <= Version.Parse(glibc), $"The library asks for glibc {newestGlibc}, newer than {glibc}.");

        var unboundSymbols = Run(workingDirectory, "readelf", "--dyn-syms", "-W", "libprobe.so")
            .Split('\n')
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(columns => columns.Length >= 8 && columns[4] is "GLOBAL" && columns[6] is "UND" && !columns[7].Contains('@'))
            .Select(columns => columns[7]);

        Assert.Empty(unboundSymbols);

        // CentOS 6 has glibc 2.12, and its image only runs on x64
        if (glibc is "2.12" && RuntimeInformation.OSArchitecture is Architecture.X64)
            Run(workingDirectory, "docker", "run", "--rm", "-v", $"{workingDirectory}:/probe:ro", CentOs6Image, "/probe/probe");
    }

    // The action is one bash step, so its script is the block that follows "run: |" and it runs here as a runner would run it
    private static Dictionary<string, string> RunAction(string runtimeIdentifier, string glibc, string workingDirectory)
    {
        var actionDirectory = Path.Combine(FindRepositoryRoot(), "glibc-sysroot");
        var script = File.ReadLines(Path.Combine(actionDirectory, "action.yml")).SkipWhile(line => line.Trim() is not "run: |").Skip(1).Select(line => line.Length > 8 ? line[8..] : string.Empty);
        var outputsPath = Path.Combine(workingDirectory, "outputs");

        File.WriteAllLines(Path.Combine(workingDirectory, "action.sh"), script);
        File.WriteAllText(outputsPath, string.Empty);

        var environment = new Dictionary<string, string>
        {
            ["RID"] = runtimeIdentifier,
            ["GLIBC"] = glibc,
            ["GITHUB_OUTPUT"] = outputsPath,
            ["GITHUB_ACTION_PATH"] = actionDirectory
        };

        Run(workingDirectory, environment, "bash", "--noprofile", "--norc", "-e", "-o", "pipefail", "action.sh");

        return File.ReadLines(outputsPath).Select(line => line.Split('=', 2)).ToDictionary(pair => pair[0], pair => pair[1]);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "glibc-sysroot", "action.yml")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("glibc-sysroot/action.yml was not found above the test assembly");
    }

    private static string Run(string workingDirectory, string fileName, params string[] arguments)
    {
        return Run(workingDirectory, [], fileName, arguments);
    }

    private static string Run(string workingDirectory, Dictionary<string, string> environment, string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var (name, value) in environment)
            startInfo.Environment[name] = value;

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"{fileName} did not start");

        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();

        process.WaitForExit();

        Assert.True(process.ExitCode is 0, $"{fileName} {string.Join(' ', arguments)} exited with {process.ExitCode}:{Environment.NewLine}{error.Result}{output}");

        return output;
    }
}

// The action installs packages and writes /crossrootfs with sudo, so the tests only run where it is meant to run
public sealed class GitHubLinuxRunnerTheoryAttribute : TheoryAttribute
{
    public GitHubLinuxRunnerTheoryAttribute()
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is not "true")
            Skip = "glibc-sysroot runs on a Linux runner of GitHub Actions";
    }
}
