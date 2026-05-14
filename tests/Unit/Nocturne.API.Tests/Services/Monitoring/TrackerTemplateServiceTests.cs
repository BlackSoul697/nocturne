using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Services.Monitoring;
using Nocturne.Core.Contracts.Monitoring;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.Alerts;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Xunit;

namespace Nocturne.API.Tests.Services.Monitoring;

[Trait("Category", "Unit")]
public class TrackerTemplateServiceTests
{
    private readonly Mock<IPatientDeviceRepository> _patientDeviceRepo;
    private readonly DbContextOptions<NocturneDbContext> _dbOptions;
    private readonly Mock<IDbContextFactory<NocturneDbContext>> _contextFactory;
    private readonly TrackerTemplateService _sut;

    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public TrackerTemplateServiceTests()
    {
        _patientDeviceRepo = new Mock<IPatientDeviceRepository>();

        _dbOptions = new DbContextOptionsBuilder<NocturneDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _contextFactory = new Mock<IDbContextFactory<NocturneDbContext>>();
        _contextFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new NocturneDbContext(_dbOptions) { TenantId = TenantId });

        var logger = new Mock<ILogger<TrackerTemplateService>>();

        _sut = new TrackerTemplateService(
            _patientDeviceRepo.Object,
            _contextFactory.Object,
            logger.Object
        );
    }

    /// <summary>Creates a fresh context for assertion queries after the service has disposed its own.</summary>
    private NocturneDbContext CreateAssertionContext() =>
        new(_dbOptions) { TenantId = TenantId };

    [Fact]
    public async Task GetAvailableTemplates_Returns_CorrectConsumables_ForCgmDevice()
    {
        // Arrange — user has a Dexcom G7
        _patientDeviceRepo.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PatientDevice
                {
                    Id = Guid.NewGuid(),
                    CatalogId = "dexcom-g7",
                    DeviceCategory = DeviceCategory.CGM,
                    Manufacturer = "Dexcom",
                    Model = "Dexcom G7",
                    IsCurrent = true,
                }
            });

        // Act
        var templates = await _sut.GetAvailableTemplatesAsync();

        // Assert — G7 has no separate transmitter, so only sensor + universal (insulin-in-use)
        templates.Should().Contain(t => t.ConsumableCatalogId == "sensor");
        templates.Should().NotContain(t => t.ConsumableCatalogId == "transmitter");
        templates.Should().Contain(t => t.ConsumableCatalogId == "insulin-in-use");

        var sensorTemplate = templates.First(t => t.ConsumableCatalogId == "sensor");
        sensorTemplate.DefaultLifespanHours.Should().Be(240); // 10 days * 24
        sensorTemplate.DeviceName.Should().Be("Dexcom G7");
        sensorTemplate.IsHardCutoff.Should().BeTrue();
    }

    [Fact]
    public async Task GetAvailableTemplates_Returns_CorrectConsumables_ForPatchPump()
    {
        // Arrange — user has an Omnipod 5
        _patientDeviceRepo.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PatientDevice
                {
                    Id = Guid.NewGuid(),
                    CatalogId = "omnipod-5",
                    DeviceCategory = DeviceCategory.InsulinPump,
                    Manufacturer = "Insulet",
                    Model = "Omnipod 5",
                    IsCurrent = true,
                }
            });

        // Act
        var templates = await _sut.GetAvailableTemplatesAsync();

        // Assert — patch pump gets pod, not infusion-set/reservoir/tubing
        templates.Should().Contain(t => t.ConsumableCatalogId == "pod");
        templates.Should().NotContain(t => t.ConsumableCatalogId == "infusion-set");
        templates.Should().NotContain(t => t.ConsumableCatalogId == "reservoir");
        templates.Should().NotContain(t => t.ConsumableCatalogId == "insulin-tubing");

        var podTemplate = templates.First(t => t.ConsumableCatalogId == "pod");
        podTemplate.DefaultLifespanHours.Should().Be(80);
        podTemplate.DeviceName.Should().Be("Omnipod 5");
    }

    [Fact]
    public async Task GetAvailableTemplates_Includes_UniversalConsumables()
    {
        // Arrange — no devices
        _patientDeviceRepo.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<PatientDevice>());

        // Act
        var templates = await _sut.GetAvailableTemplatesAsync();

        // Assert — insulin-in-use is universal
        templates.Should().Contain(t => t.ConsumableCatalogId == "insulin-in-use");
        var insulinTemplate = templates.First(t => t.ConsumableCatalogId == "insulin-in-use");
        insulinTemplate.DeviceName.Should().BeNull();
    }

    [Fact]
    public async Task ApplyTemplate_Creates_Definition_WithCorrectLifespan()
    {
        // Act
        var result = await _sut.ApplyTemplateAsync(new TemplateApplication("sensor", 240));

        // Assert
        result.TrackerDefinitionId.Should().NotBeEmpty();

        await using var db = CreateAssertionContext();
        var definition = await db.TrackerDefinitions.FindAsync(result.TrackerDefinitionId);
        definition.Should().NotBeNull();
        definition!.Name.Should().Be("Sensor");
        definition.LifespanHours.Should().Be(240);
        definition.Icon.Should().Be("activity");
        definition.TenantId.Should().Be(TenantId);
    }

    [Fact]
    public async Task ApplyTemplate_Creates_TwoAlarmRules_WithCorrectConditions()
    {
        // Act
        var result = await _sut.ApplyTemplateAsync(new TemplateApplication("sensor", 240));

        // Assert
        result.AlertRuleIds.Should().HaveCount(2);

        await using var db = CreateAssertionContext();
        var rules = await db.AlertRules
            .Where(r => result.AlertRuleIds.Contains(r.Id))
            .OrderBy(r => r.SortOrder)
            .ToListAsync();

        rules.Should().HaveCount(2);

        // Warning rule
        var warningRule = rules[0];
        warningRule.Name.Should().Be("Sensor Warning");
        warningRule.Severity.Should().Be(AlertRuleSeverity.Warning);
        warningRule.ConditionType.Should().Be(AlertConditionType.TrackerRemaining);
        warningRule.AutoResolveEnabled.Should().BeTrue();
        warningRule.TenantId.Should().Be(TenantId);

        var warningParams = JsonDocument.Parse(warningRule.ConditionParams);
        warningParams.RootElement.GetProperty("definition_id").GetGuid().Should().Be(result.TrackerDefinitionId);
        warningParams.RootElement.GetProperty("operator").GetString().Should().Be("<=");
        warningParams.RootElement.GetProperty("hours").GetDecimal().Should().Be(24m);

        // Urgent rule
        var urgentRule = rules[1];
        urgentRule.Name.Should().Be("Sensor Urgent");
        urgentRule.Severity.Should().Be(AlertRuleSeverity.Critical);
        urgentRule.ConditionType.Should().Be(AlertConditionType.TrackerRemaining);

        var urgentParams = JsonDocument.Parse(urgentRule.ConditionParams);
        urgentParams.RootElement.GetProperty("hours").GetDecimal().Should().Be(2m);
    }

    [Fact]
    public async Task ApplyTemplate_Stamps_SourceTemplateMetadata()
    {
        // Act
        var result = await _sut.ApplyTemplateAsync(new TemplateApplication("pod", null));

        // Assert
        await using var db = CreateAssertionContext();
        var rules = await db.AlertRules
            .Where(r => result.AlertRuleIds.Contains(r.Id))
            .ToListAsync();

        foreach (var rule in rules)
        {
            rule.SourceTemplate.Should().NotBeNullOrEmpty();

            var metadata = JsonDocument.Parse(rule.SourceTemplate!);
            metadata.RootElement.GetProperty("template").GetString().Should().Be("pod");
            metadata.RootElement.GetProperty("trackerDefinitionId").GetGuid().Should().Be(result.TrackerDefinitionId);
            metadata.RootElement.GetProperty("consumableCatalogId").GetString().Should().Be("pod");
        }
    }

    [Fact]
    public void ResolveLifespanHours_Sensor_UsesDeviceSensorDuration()
    {
        var consumable = ConsumableCatalog.GetById("sensor")!;
        var device = DeviceCatalog.GetById("libre-3")!;

        var result = TrackerTemplateService.ResolveLifespanHours(consumable, device);

        result.Should().Be(14 * 24); // Libre 3 = 14 days
    }

    [Fact]
    public void ResolveLifespanHours_Transmitter_UsesDeviceTransmitterDuration()
    {
        var consumable = ConsumableCatalog.GetById("transmitter")!;
        var device = DeviceCatalog.GetById("dexcom-g6")!;

        var result = TrackerTemplateService.ResolveLifespanHours(consumable, device);

        result.Should().Be(90 * 24); // G6 transmitter = 90 days
    }

    [Fact]
    public void ResolveLifespanHours_Pod_UsesDeviceSpecificLifespan()
    {
        var consumable = ConsumableCatalog.GetById("pod")!;

        var omnipod5 = DeviceCatalog.GetById("omnipod-5")!;
        TrackerTemplateService.ResolveLifespanHours(consumable, omnipod5).Should().Be(80);

        var omnipodDash = DeviceCatalog.GetById("omnipod-dash")!;
        TrackerTemplateService.ResolveLifespanHours(consumable, omnipodDash).Should().Be(72);
    }
}
