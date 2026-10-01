using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using NUnit.Framework;
using Toren.App.ViewModels;

namespace Toren.UnitTests.Ui;

/// <summary>
/// Measures opening a workspace through the application's real composition: the main window,
/// its view models, controllers and services, running headless. It reports when the Explorer
/// tree, the startup-project selector and the first workspace diagnostics are ready, and how
/// long the UI thread was ever blocked meanwhile. Rendering and input latency are not covered.
///
/// Run explicitly, for example:
/// <code>
/// TOREN_PROBE_WORKSPACE=/path/Solution.sln TOREN_PROBE_OUTPUT=/tmp/open.json \
///   dotnet test tests/Toren.UnitTests -c Release --filter "FullyQualifiedName~WorkspaceOpeningProbe"
/// </code>
/// </summary>
[TestFixture]
[Explicit("A measurement, not a test: needs TOREN_PROBE_WORKSPACE.")]
[Category("Probe")]
public sealed class WorkspaceOpeningProbe
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    [AvaloniaTest]
    public async Task MeasureWorkspaceOpening()
    {
        var workspace = Environment.GetEnvironmentVariable("TOREN_PROBE_WORKSPACE");
        if (string.IsNullOrWhiteSpace(workspace))
        {
            Assert.Ignore("Set TOREN_PROBE_WORKSPACE to a folder, solution or project.");
        }

        var limit = TimeSpan.FromSeconds(
            double.TryParse(Environment.GetEnvironmentVariable("TOREN_PROBE_LIMIT_SECONDS"), out var seconds) ? seconds : 900);
        var profile = Path.Combine(TestContext.CurrentContext.WorkDirectory, "TestResults", $"probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(profile);
        var window = Toren.App.App.CreateMainWindow(profile, new UiInteractiveProcessRunner());
        var model = (MainWindowViewModel)window.DataContext!;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // A UI thread that is free runs this timer every 15 ms; longer gaps are time it was blocked.
        var clock = Stopwatch.StartNew();
        var lastTick = clock.Elapsed;
        var stalls = new List<(double AtSeconds, double Milliseconds)>();
        long peakWorkingSet = 0;
        using var process = Process.GetCurrentProcess();
        var heartbeat = new DispatcherTimer(TimeSpan.FromMilliseconds(15), DispatcherPriority.Background, (_, _) =>
        {
            var gap = clock.Elapsed - lastTick;
            lastTick = clock.Elapsed;
            if (gap > TimeSpan.FromMilliseconds(100))
            {
                stalls.Add((clock.Elapsed.TotalSeconds, gap.TotalMilliseconds));
            }
        });
        heartbeat.Start();

        try
        {
            var runTargets = window.FindControl<ComboBox>("StartupProjectSelector")!;
            TimeSpan? treeReady = null;
            TimeSpan? runTargetsReady = null;
            clock.Restart();
            lastTick = clock.Elapsed;
            var opening = Directory.Exists(workspace)
                ? model.OpenDirectoryAsync(workspace)
                : model.OpenWorkspaceFileAsync(workspace);

            while (clock.Elapsed < limit && !(opening.IsCompleted && runTargetsReady is not null))
            {
                await Task.Delay(20);
                process.Refresh();
                peakWorkingSet = Math.Max(peakWorkingSet, process.WorkingSet64);
                if (treeReady is null && model.Explorer.Roots.Count > 0 && model.Explorer.Roots[0].IsLoaded)
                {
                    treeReady = clock.Elapsed;
                }

                if (runTargetsReady is null && runTargets.ItemsSource is System.Collections.IEnumerable targets
                    && targets.Cast<object>().Any())
                {
                    runTargetsReady = clock.Elapsed;
                }

                // A workspace without runnable projects never fills the selector.
                if (opening.IsCompleted && runTargetsReady is null && clock.Elapsed > TimeSpan.FromSeconds(5)
                    && !runTargets.IsEnabled && model.StatusText.Length > 0 && clock.Elapsed > limit / 2)
                {
                    break;
                }
            }

            TimeSpan? openCompleted = opening.IsCompleted ? clock.Elapsed : null;
            if (opening.IsCompleted)
            {
                await opening;
            }

            var result = new
            {
                workspace,
                utc = DateTime.UtcNow,
                explorerTreeSeconds = treeReady?.TotalSeconds,
                startupProjectsSeconds = runTargetsReady?.TotalSeconds,
                startupProjects = runTargets.ItemsSource?.Cast<object>().Count() ?? 0,
                workspaceDiagnosticsSeconds = openCompleted?.TotalSeconds,
                problems = model.Problems.Items.Count,
                limitSeconds = limit.TotalSeconds,
                uiThread = new
                {
                    stallsOver100Ms = stalls.Count,
                    longestStallMs = stalls.Count == 0 ? 0 : stalls.Max(static stall => stall.Milliseconds),
                    totalStalledMs = stalls.Sum(static stall => stall.Milliseconds),
                    stalls = stalls.Select(static stall => new { endedAtSeconds = stall.AtSeconds, milliseconds = stall.Milliseconds }),
                },
                peakWorkingSetMb = peakWorkingSet / 1048576.0,
            };
            var json = JsonSerializer.Serialize(result, SerializerOptions);
            TestContext.Progress.WriteLine(json);
            if (Environment.GetEnvironmentVariable("TOREN_PROBE_OUTPUT") is { Length: > 0 } output)
            {
                await File.WriteAllTextAsync(output, json);
            }
        }
        finally
        {
            heartbeat.Stop();
            window.Close();
            model.Dispose();
        }
    }
}
