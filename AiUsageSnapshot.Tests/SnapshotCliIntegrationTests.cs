using System.Diagnostics;
using Xunit;

namespace AiUsageSnapshot.Tests;

public class SnapshotCliIntegrationTests
{
    [Fact]
    public void ExecuteWithSampleCsv_GeneratesAllExpectedFiles()
    {
        var repoRoot = FindRepoRoot();
        var appPath = Path.Combine(repoRoot, "ai_usage_snapshot.cs");
        var inputPath = Path.Combine(repoRoot, "samples", "input", "common-schema-sample.csv");
        var outputDir = Path.Combine(Path.GetTempPath(), "ai_usage_snapshot_test_" + Guid.NewGuid());
        Directory.CreateDirectory(outputDir);

        var result = RunSnapshot(appPath, inputPath, outputDir);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError.Trim());
        Assert.True(File.Exists(Path.Combine(outputDir, "user-summary.csv")));
        Assert.True(File.Exists(Path.Combine(outputDir, "department-summary.csv")));
        Assert.True(File.Exists(Path.Combine(outputDir, "service-summary.csv")));
        Assert.True(File.Exists(Path.Combine(outputDir, "dormant-candidates.csv")));
        Assert.True(File.Exists(Path.Combine(outputDir, "license-unused-candidates.csv")));
        Assert.True(File.Exists(Path.Combine(outputDir, "data-quality-report.md")));
        Assert.Equal(2, File.ReadAllLines(Path.Combine(outputDir, "dormant-candidates.csv")).Length);
        Assert.Equal(2, File.ReadAllLines(Path.Combine(outputDir, "license-unused-candidates.csv")).Length);
        Assert.Contains("RowsWithQualityFlags:", File.ReadAllText(Path.Combine(outputDir, "data-quality-report.md")));
    }

    [Fact]
    public void ExecuteWithMissingHeader_ReturnsSchemaError()
    {
        var repoRoot = FindRepoRoot();
        var appPath = Path.Combine(repoRoot, "ai_usage_snapshot.cs");
        var outputDir = Path.Combine(Path.GetTempPath(), "ai_usage_snapshot_test_" + Guid.NewGuid());
        Directory.CreateDirectory(outputDir);

        var invalidInput = Path.Combine(outputDir, "invalid-header.csv");
        File.WriteAllText(invalidInput, "user_key,department,service\nu1,Team,svc\n", System.Text.Encoding.UTF8);

        var result = RunSnapshot(appPath, invalidInput, outputDir);

        Assert.Equal(4, result.ExitCode);
        Assert.Contains("Missing required headers", string.Join("\n", result.StandardError));
    }

    private static SnapshotResult RunSnapshot(string appPath, string inputPath, string outputDir)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --file \"{appPath}\" -- --input \"{inputPath}\" --output \"{outputDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(appPath)
            }
        };

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(120_000);

        return new SnapshotResult(process.ExitCode, stdout, stderr);
    }

    private sealed record SnapshotResult(int ExitCode, string StandardOutput, string StandardError);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return AppContext.BaseDirectory;
    }
}
