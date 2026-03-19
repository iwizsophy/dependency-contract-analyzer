using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace DependencyContractAnalyzer.Tests;

public sealed class ReleasePrChangelogValidationTests
{
    [Fact]
    public void ValidateReleasePrChangelogSucceedsWhenChangelogAdvancesPastLatestMainTag()
    {
        using TemporaryGitRepository repository = TemporaryGitRepository.Create();
        repository.WriteChangelog(
            """
            # Changelog

            ## [Unreleased]

            ## [1.1.0]

            ### Added

            - Pending release notes

            ## [1.0.0]

            ### Added

            - Initial release
            """);

        repository.CommitAll("Add changelog");
        repository.CreateAnnotatedTag("v1.0.0", "1.0.0");

        RunValidationScript(repository.Path);
    }

    [Fact]
    public void ValidateReleasePrChangelogFailsWhenChangelogDoesNotAdvancePastLatestMainTag()
    {
        using TemporaryGitRepository repository = TemporaryGitRepository.Create();
        repository.WriteChangelog(
            """
            # Changelog

            ## [Unreleased]

            ## [1.0.0]

            ### Added

            - Initial release
            """);

        repository.CommitAll("Add changelog");
        repository.CreateAnnotatedTag("v1.0.0", "1.0.0");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => RunValidationScript(repository.Path));
        Assert.Contains("CHANGELOG.md must contain at least one version section newer than the latest main release tag v1.0.0.", exception.Message, StringComparison.Ordinal);
    }

    private static void RunValidationScript(string repositoryPath)
    {
        string scriptPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts", "Validate-ReleasePrChangelog.ps1"));
        ProcessStartInfo startInfo = new()
        {
            FileName = GetPowerShellExecutableName(),
            WorkingDirectory = repositoryPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-RepositoryRoot");
        startInfo.ArgumentList.Add(repositoryPath);
        startInfo.ArgumentList.Add("-ReleaseBranchName");
        startInfo.ArgumentList.Add("main");
        startInfo.ArgumentList.Add("-SkipFetch");

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start PowerShell process.");
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode == 0) {
            return;
        }

        string combinedOutput = string.Join(
            Environment.NewLine,
            new[] { standardOutput, standardError }.Where(static text => !string.IsNullOrWhiteSpace(text)));

        throw new InvalidOperationException(combinedOutput);
    }

    private static string GetPowerShellExecutableName()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pwsh.exe" : "pwsh";
    }

    private sealed class TemporaryGitRepository : IDisposable
    {
        private TemporaryGitRepository(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryGitRepository Create()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dca-release-pr-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);

            TemporaryGitRepository repository = new(path);
            repository.RunGit("init", "--initial-branch=main");
            repository.RunGit("config", "user.name", "Dependency Contract Analyzer Tests");
            repository.RunGit("config", "user.email", "tests@example.invalid");

            return repository;
        }

        public void WriteChangelog(string content)
        {
            File.WriteAllText(System.IO.Path.Combine(Path, "CHANGELOG.md"), content.ReplaceLineEndings("\n"));
        }

        public void CommitAll(string message)
        {
            RunGit("add", "CHANGELOG.md");
            RunGit("commit", "-m", message);
        }

        public void CreateAnnotatedTag(string tagName, string message)
        {
            RunGit("tag", "-a", tagName, "-m", message);
        }

        public void Dispose()
        {
            try {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException) {
            }
            catch (UnauthorizedAccessException) {
            }
        }

        private void RunGit(params string[] arguments)
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "git",
                WorkingDirectory = Path,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            foreach (string argument in arguments) {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git.");
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0) {
                return;
            }

            string combinedOutput = string.Join(
                Environment.NewLine,
                new[] { standardOutput, standardError }.Where(static text => !string.IsNullOrWhiteSpace(text)));

            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed.{Environment.NewLine}{combinedOutput}");
        }
    }
}
