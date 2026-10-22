using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nocturne.API.Authorization;
using Nocturne.API.Controllers.V4.Analytics;
using Nocturne.Core.Models.Authorization;
using Nocturne.Core.Models.LoopAnalysis;
using Xunit;

namespace Nocturne.API.Tests.Authorization;

public partial class AnalyticsReadScopeGuardTests
{
    private static LoopAnalysisAggregate EventAggregate() => new() { P50 = 1, Mean = 1, N = 1 };

    private static EventResponseRange EventRange(int cohort = 1) => new()
    {
        Cohorts =
        [
            new EventCohortResult
            {
                Cohort = cohort,
                Anchors = 1,
                Events = 1,
                Slots =
                [
                    new EventResponseSlot
                    {
                        Glucose = EventAggregate(), GlucoseChange = EventAggregate(), NetLoopCorrection = EventAggregate(),
                        NetInsulinSinceAnchor = EventAggregate(), NetLoopCorrectionSinceAnchor = EventAggregate(),
                        Iob = EventAggregate(), Cob = EventAggregate(),
                    },
                ],
                Metrics = new EventMetricsSummary { StartGlucose = EventAggregate(), NetInsulin = EventAggregate(), IobAtAnchor = EventAggregate() },
            },
        ],
        Events = [new EventResponseEvent { Date = "2024-06-03", Metrics = new EventMetrics { StartGlucose = 100, NetInsulin = 2, IobAtAnchor = 1 } }],
    };

    private static EventResponseReport EventReport(EventAnchorKind anchor) => new()
    {
        Definition = new EventResponseDefinition { Cohorts = [new EventCohortDefinition { Cohort = 1, Anchor = anchor }] },
        Primary = EventRange(),
        Comparison = EventRange(),
    };

    public static TheoryData<string[], LoopAnalysisSeries[]> EventScopeCombinations => new()
    {
        { AllLoopAnalysisCategories, [] },
        { [Scope.GlucoseRead, Scope.DevicesRead], [LoopAnalysisSeries.EventNetInsulin, LoopAnalysisSeries.EventIob, LoopAnalysisSeries.EventCob, LoopAnalysisSeries.EventList] },
        { [Scope.TreatmentsRead, Scope.DevicesRead], [LoopAnalysisSeries.EventGlucose] },
    };

    [Theory]
    [MemberData(nameof(EventScopeCombinations))]
    public void LoopAnalysis_Events_WithheldSeries_ListExactlyWhatIsEmptied(string[] scopes, LoopAnalysisSeries[] expected)
    {
        var data = LoopAnalysisReadScopeGuard.Redact(EventReport(EventAnchorKind.SensorStart), Granted(scopes));

        data.WithheldSeries.Should().BeEquivalentTo(expected);
        bool Withheld(LoopAnalysisSeries series) => expected.Contains(series);
        foreach (var range in new[] { data.Primary, data.Comparison! })
        {
            var cohort = range.Cohorts.Single();
            var slot = cohort.Slots.Single();
            (slot.Glucose is null).Should().Be(Withheld(LoopAnalysisSeries.EventGlucose));
            (slot.GlucoseChange is null).Should().Be(Withheld(LoopAnalysisSeries.EventGlucose));
            (cohort.Metrics.StartGlucose is null).Should().Be(Withheld(LoopAnalysisSeries.EventGlucose));
            (slot.NetLoopCorrection is null).Should().Be(Withheld(LoopAnalysisSeries.EventNetInsulin));
            (slot.NetInsulinSinceAnchor is null).Should().Be(Withheld(LoopAnalysisSeries.EventNetInsulin));
            (cohort.Metrics.NetInsulin is null).Should().Be(Withheld(LoopAnalysisSeries.EventNetInsulin));
            (slot.Iob is null).Should().Be(Withheld(LoopAnalysisSeries.EventIob));
            (cohort.Metrics.IobAtAnchor is null).Should().Be(Withheld(LoopAnalysisSeries.EventIob));
            (slot.Cob is null).Should().Be(Withheld(LoopAnalysisSeries.EventCob));
            (range.Events.Count == 0).Should().Be(Withheld(LoopAnalysisSeries.EventList));
        }
    }

    [Fact]
    public void LoopAnalysis_Events_WithNoCategory_LeaveNothingButTheAllowlist()
    {
        var data = LoopAnalysisReadScopeGuard.Redact(RedactionCoverage.Populated<EventResponseReport>(), Granted());

        RedactionCoverage.AssertWithheld(
            data,
            [typeof(EventResponseRange), typeof(EventResponseDefinition)],
            [
                "EventResponseRange.From", "EventResponseRange.To",
                "EventResponseDefinition.MinutesBefore", "EventResponseDefinition.MinutesAfter",
            ]);
    }

    [Fact]
    public void LoopAnalysis_Events_WithDevicesOnly_KeepOnlyTheSensorStartCountsAndShape()
    {
        var data = RedactionCoverage.Populated<EventResponseReport>();
        data.Definition.Cohorts.Single().Anchor = EventAnchorKind.SensorStart;

        RedactionCoverage.AssertWithheld(
            LoopAnalysisReadScopeGuard.Redact(data, Granted(Scope.DevicesRead)),
            [typeof(EventCohortResult), typeof(EventResponseSlot), typeof(EventMetricsSummary)],
            [
                "EventCohortResult.Cohort", "EventCohortResult.Anchors", "EventCohortResult.NotIsolated",
                "EventCohortResult.MissingAnchorGlucose", "EventCohortResult.Events",
                "EventCohortResult.Slots", "EventCohortResult.Metrics",
                "EventResponseSlot.Slot", "EventResponseSlot.Minute",
            ]);
    }

    [Fact]
    public void LoopAnalysis_Events_WithoutGlucose_WithholdEveryGlucoseFigureOfEachEvent()
    {
        var data = RedactionCoverage.Populated<EventResponseReport>();
        data.Definition.Cohorts.Single().Anchor = EventAnchorKind.Meal;

        RedactionCoverage.AssertWithheld(
            LoopAnalysisReadScopeGuard.Redact(data, Granted(Scope.TreatmentsRead, Scope.DevicesRead)),
            [typeof(EventMetrics)],
            [
                "EventMetrics.TotalInsulin", "EventMetrics.NetInsulin", "EventMetrics.GramsPerUnit",
                "EventMetrics.IobAtAnchor", "EventMetrics.IobAtEnd", "EventMetrics.NetInsulinFirst30",
                "EventMetrics.NetInsulinFirst60", "EventMetrics.NetInsulinFirst120",
            ]);
    }

    public static TheoryData<EventAnchorKind, string[], bool> AnchorAccess => new()
    {
        { EventAnchorKind.Meal, [Scope.GlucoseRead, Scope.DevicesRead], false },
        { EventAnchorKind.Meal, [Scope.TreatmentsRead], true },
        { EventAnchorKind.CorrectionBolus, [Scope.GlucoseRead], false },
        { EventAnchorKind.ExerciseStart, [Scope.TreatmentsRead], true },
        { EventAnchorKind.SensorStart, [Scope.GlucoseRead, Scope.TreatmentsRead], false },
        { EventAnchorKind.SensorStart, [Scope.DevicesRead], true },
        { EventAnchorKind.UnannouncedRise, [Scope.GlucoseRead], false },
        { EventAnchorKind.UnannouncedRise, [Scope.TreatmentsRead], false },
        { EventAnchorKind.UnannouncedRise, [Scope.GlucoseRead, Scope.TreatmentsRead], true },
    };

    [Theory]
    [MemberData(nameof(AnchorAccess))]
    public void LoopAnalysis_Events_ACohortNeedsTheCategoriesItsAnchorIsDrawnFrom(EventAnchorKind anchor, string[] scopes, bool kept)
    {
        var definition = new EventResponseDefinition
        {
            Cohorts = [new EventCohortDefinition { Cohort = 2, Anchor = anchor }],
        };

        var ignored = LoopAnalysisReadScopeGuard.NarrowEvents(definition, Granted(scopes));

        definition.Cohorts.Should().HaveCount(kept ? 1 : 0);
        ignored.Should().Equal(kept ? [] : ["anchor2"]);
        LoopAnalysisReadScopeGuard.UnavailableAnchors(Granted(scopes)).Select(a => a.Anchor)
            .Contains(anchor).Should().Be(!kept);
    }

    [Fact]
    public void LoopAnalysis_Events_WithoutTreatments_DropTheOptions()
    {
        var definition = new EventResponseDefinition { IsolatedOnly = true, RequireBolusCluster = true };

        var ignored = LoopAnalysisReadScopeGuard.NarrowEvents(definition, Granted(Scope.GlucoseRead, Scope.DevicesRead));

        ignored.Should().Equal("isolatedOnly", "requireBolusCluster");
        definition.IsolatedOnly.Should().BeFalse();
        definition.RequireBolusCluster.Should().BeFalse();
        LoopAnalysisReadScopeGuard.UnavailableEventFilters(Granted(Scope.GlucoseRead)).Select(f => f.Filter)
            .Should().Equal("isolatedOnly", "requireBolusCluster");
        LoopAnalysisReadScopeGuard.UnavailableEventFilters(Granted(Scope.TreatmentsRead)).Should().BeEmpty();
    }

    [Fact]
    public void LoopAnalysis_Events_RedactDropsACohortTheCallerCouldNotHaveAskedFor()
    {
        var data = LoopAnalysisReadScopeGuard.Redact(EventReport(EventAnchorKind.Meal), Granted(Scope.GlucoseRead, Scope.DevicesRead));

        data.Definition.Cohorts.Should().BeEmpty();
        data.Primary.Cohorts.Should().BeEmpty();
        data.Comparison!.Cohorts.Should().BeEmpty();
    }

    [Fact]
    public void LoopAnalysis_Events_OptionNames_AreTheQueryParametersTheBinderReads()
    {
        var parameters = typeof(EventResponseQuery).GetProperties()
            .Select(p => p.GetCustomAttribute<FromQueryAttribute>()?.Name)
            .OfType<string>()
            .ToHashSet();

        LoopAnalysisReadScopeGuard.UnavailableEventFilters(Granted()).Select(f => f.Filter)
            .Should().OnlyContain(name => parameters.Contains(name));
        parameters.Should().Contain(["anchor1", "anchor2", "anchor3"]);
    }

    [Fact]
    public async Task LoopAnalysis_EventHandler_NarrowsTheDefinitionAndReportsWhatItIgnored()
    {
        var (controller, service) = LoopAnalysisHandler(
            Granted(Scope.ReportsRead, Scope.GlucoseRead, Scope.DevicesRead), query: "?anchor1=Meal&anchor2=SensorStart&isolatedOnly=true");
        service
            .Setup(s => s.GetEventResponseAsync(It.IsAny<LoopAnalysisFilter>(), It.IsAny<EventResponseDefinition>(), It.IsAny<TimeZoneInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new EventResponseReport { Primary = EventRange(2) });

        var result = await controller.GetEventResponse(
            Week(), new EventResponseQuery { Anchor1 = EventAnchorKind.Meal, Anchor2 = EventAnchorKind.SensorStart, IsolatedOnly = true });

        var data = result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<EventResponseReport>().Subject;
        data.IgnoredFilters.Should().BeEquivalentTo(["anchor1", "isolatedOnly"]);
        data.Definition.Cohorts.Select(c => c.Anchor).Should().Equal(EventAnchorKind.SensorStart);
        data.UnavailableAnchors.Select(a => a.Anchor).Should().Contain(EventAnchorKind.Meal).And.NotContain(EventAnchorKind.SensorStart);
        data.UnavailableFilters.Select(f => f.Filter).Should().Contain(["isolatedOnly", "requireBolusCluster", "excludeOverrides"]);
        data.Primary.Events.Should().BeEmpty();
        data.Primary.Cohorts.Single().Slots.Single().Glucose.Should().NotBeNull();
        service.Verify(s => s.GetEventResponseAsync(
            It.IsAny<LoopAnalysisFilter>(),
            It.Is<EventResponseDefinition>(d => d.Cohorts.Count == 1 && !d.IsolatedOnly),
            It.IsAny<TimeZoneInfo>(),
            It.IsAny<CancellationToken>()));
    }

    [Theory]
    [InlineData("minutesAfter", 485)]
    [InlineData("minutesAfter", 30)]
    [InlineData("minutesBefore", 7)]
    public async Task LoopAnalysis_EventHandler_RejectsAWindowItCannotSlot(string parameter, int minutes)
    {
        var (controller, _) = LoopAnalysisHandler(Granted([Scope.ReportsRead, .. AllLoopAnalysisCategories]));
        var events = new EventResponseQuery();
        if (parameter == "minutesAfter") events.MinutesAfter = minutes; else events.MinutesBefore = minutes;

        var result = await controller.GetEventResponse(Week(), events);

        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public void LoopAnalysis_EventQuery_DefaultsToEveryMealAndRejectsGramsOnAnotherKind()
    {
        new EventResponseQuery().ToDefinition().Cohorts.Should().ContainSingle()
            .Which.Anchor.Should().Be(EventAnchorKind.Meal);
        new EventResponseQuery { Anchor2 = EventAnchorKind.CorrectionBolus }.ToDefinition().Cohorts
            .Should().ContainSingle().Which.Cohort.Should().Be(2);
        new EventResponseQuery { Anchor1 = EventAnchorKind.CorrectionBolus, MinGrams1 = 10 }.Problem().Should().NotBeNull();
        new EventResponseQuery { FromMinute2 = 60 }.Problem().Should().NotBeNull();
        new EventResponseQuery { FromMinute1 = 60, ToMinute1 = 60 }.Problem().Should().NotBeNull();
        new EventResponseQuery { FromMinute1 = 22 * 60, ToMinute1 = 120, MinGrams1 = 20, MaxGrams1 = 80 }.Problem().Should().BeNull();
    }
}
