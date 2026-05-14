using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Services.Monitoring;
using Nocturne.API.Services.Realtime;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Abstractions;
using Nocturne.Infrastructure.Data.Entities;
using Xunit;

namespace Nocturne.API.Tests.Services.Monitoring;

[Trait("Category", "Unit")]
public class TrackerSuggestionServiceTests
{
    private readonly Mock<ITrackerRepository> _trackerRepo;
    private readonly Mock<IInAppNotificationRepository> _notificationRepo;
    private readonly Mock<ISignalRBroadcastService> _broadcast;
    private readonly TrackerSuggestionService _sut;

    private static readonly Guid DefinitionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public TrackerSuggestionServiceTests()
    {
        _trackerRepo = new Mock<ITrackerRepository>();
        _notificationRepo = new Mock<IInAppNotificationRepository>();
        _broadcast = new Mock<ISignalRBroadcastService>();
        var logger = new Mock<ILogger<TrackerSuggestionService>>();

        _sut = new TrackerSuggestionService(
            _trackerRepo.Object,
            _notificationRepo.Object,
            _broadcast.Object,
            logger.Object
        );
    }

    [Fact]
    public async Task EvaluateTreatment_SkipsSuggestion_WhenAutoTriggerHandlesEventType()
    {
        // Arrange — definition has auto-trigger for "Site Change"
        var definition = MakeDefinition(triggerEventTypes: """["Site Change"]""");

        _trackerRepo.Setup(r => r.GetDefinitionsByCategoryAsync(
            TrackerCategory.Cannula, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrackerDefinitionEntity> { definition });

        var treatment = new Treatment
        {
            Id = "treat-1",
            EventType = "Site Change",
            Mills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        // Act
        await _sut.EvaluateTreatmentForTrackerSuggestionAsync(treatment, "user-1");

        // Assert — no notification should be created
        _notificationRepo.Verify(
            r => r.CreateAsync(It.IsAny<InAppNotificationEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateTreatment_SkipsSuggestion_WhenAutoTriggerMatchesCaseInsensitive()
    {
        // Arrange — trigger is lowercase, treatment is mixed case
        var definition = MakeDefinition(triggerEventTypes: """["site change"]""");

        _trackerRepo.Setup(r => r.GetDefinitionsByCategoryAsync(
            TrackerCategory.Cannula, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrackerDefinitionEntity> { definition });

        var treatment = new Treatment
        {
            Id = "treat-2",
            EventType = "Site Change",
            Mills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        // Act
        await _sut.EvaluateTreatmentForTrackerSuggestionAsync(treatment, "user-1");

        // Assert
        _notificationRepo.Verify(
            r => r.CreateAsync(It.IsAny<InAppNotificationEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateTreatment_CreatesSuggestion_WhenNoAutoTriggerConfigured()
    {
        // Arrange — no trigger event types configured
        var definition = MakeDefinition(triggerEventTypes: "[]");

        _trackerRepo.Setup(r => r.GetDefinitionsByCategoryAsync(
            TrackerCategory.Cannula, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrackerDefinitionEntity> { definition });

        // No recent suggestion exists
        _notificationRepo.Setup(r => r.FindBySourceAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InAppNotificationEntity?)null);

        // Return a created notification
        _notificationRepo.Setup(r => r.CreateAsync(It.IsAny<InAppNotificationEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InAppNotificationEntity e, CancellationToken _) =>
            {
                e.Id = Guid.NewGuid();
                e.CreatedAt = DateTime.UtcNow;
                return e;
            });

        var treatment = new Treatment
        {
            Id = "treat-3",
            EventType = "Site Change",
            Mills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        // Act
        await _sut.EvaluateTreatmentForTrackerSuggestionAsync(treatment, "user-1");

        // Assert — notification should be created
        _notificationRepo.Verify(
            r => r.CreateAsync(It.IsAny<InAppNotificationEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluateTreatment_CreatesSuggestion_WhenAutoTriggerDoesNotMatchEventType()
    {
        // Arrange — trigger is for a different event type
        var definition = MakeDefinition(triggerEventTypes: """["Sensor Start"]""");

        _trackerRepo.Setup(r => r.GetDefinitionsByCategoryAsync(
            TrackerCategory.Cannula, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrackerDefinitionEntity> { definition });

        _notificationRepo.Setup(r => r.FindBySourceAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InAppNotificationEntity?)null);

        _notificationRepo.Setup(r => r.CreateAsync(It.IsAny<InAppNotificationEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InAppNotificationEntity e, CancellationToken _) =>
            {
                e.Id = Guid.NewGuid();
                e.CreatedAt = DateTime.UtcNow;
                return e;
            });

        var treatment = new Treatment
        {
            Id = "treat-4",
            EventType = "Site Change",
            Mills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        // Act
        await _sut.EvaluateTreatmentForTrackerSuggestionAsync(treatment, "user-1");

        // Assert — notification should be created (trigger doesn't match this event)
        _notificationRepo.Verify(
            r => r.CreateAsync(It.IsAny<InAppNotificationEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static TrackerDefinitionEntity MakeDefinition(
        string triggerEventTypes = "[]",
        string name = "Test Cannula Tracker")
    {
        return new TrackerDefinitionEntity
        {
            Id = DefinitionId,
            Name = name,
            Category = TrackerCategory.Cannula,
            TriggerEventTypes = triggerEventTypes,
            Icon = "activity"
        };
    }
}
