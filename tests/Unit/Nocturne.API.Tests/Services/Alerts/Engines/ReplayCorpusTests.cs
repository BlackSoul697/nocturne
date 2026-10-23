using System.Text.Json;
using FluentAssertions;
using Nocturne.Alerts.ParityCorpus.Generator.Harness;
using Nocturne.API.Services.Alerts.Engines;
using Nocturne.Core.Contracts.Alerts;
using Xunit;

namespace Nocturne.API.Tests.Services.Alerts.Engines;

/// <summary>
/// Seam-level replay corpus parity: every replay scenario through each
/// <see cref="IAlertReplayEngine"/> must reproduce the committed snapshot.
/// </summary>
public class ReplayCorpusTests
{
    public static TheoryData<string> ScenarioNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in CorpusLocator.EnumerateReplayScenarioNames())
            data.Add(name);
        return data;
    }

    [Fact]
    public void Replay_corpus_is_not_empty()
    {
        CorpusLocator.EnumerateReplayScenarioNames().Should().HaveCountGreaterThanOrEqualTo(9);
    }

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public Task Managed_replay_reproduces_the_committed_snapshot(string scenarioName) =>
        AssertReproducesAsync(ReplayScenarioRunner.Managed(), scenarioName, "managed");

    [NativeTheory]
    [MemberData(nameof(ScenarioNames))]
    public Task Rust_replay_reproduces_the_committed_snapshot(string scenarioName) =>
        AssertReproducesAsync(ReplayScenarioRunner.Rust(), scenarioName, "rust");

    private static async Task AssertReproducesAsync(IAlertReplayEngine engine, string scenarioName, string label)
    {
        var (scenario, expected) = await CorpusLocator.LoadReplayScenarioAsync(scenarioName);

        var actual = await ReplayScenarioRunner.RunAsync(engine, scenario, CancellationToken.None);

        var failures = new List<string>();
        JsonNodeDiff.Compare(
            JsonSerializer.SerializeToNode(actual, CorpusJson.Options), expected,
            $"replay scenario {scenarioName}", label, failures);
        failures.Should().BeEmpty(string.Join("\n", failures));
    }
}
