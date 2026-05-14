using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts.Monitoring;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Alerts;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services.Monitoring;

/// <summary>
/// Creates tracker definitions and alarm rules from consumable catalog templates.
/// Joins the device catalog, consumable catalog, and patient devices to scaffold everything
/// in one shot. Generated alarm rules are stamped with <c>source_template</c> metadata.
/// </summary>
public class TrackerTemplateService : ITrackerTemplateService
{
    private readonly IPatientDeviceRepository _patientDeviceRepository;
    private readonly IDbContextFactory<NocturneDbContext> _contextFactory;
    private readonly ILogger<TrackerTemplateService> _logger;

    /// <summary>Default warning/urgent thresholds per consumable type (hours).</summary>
    private static readonly Dictionary<ConsumableType, (decimal WarnHours, decimal UrgentHours)> Thresholds = new()
    {
        [ConsumableType.Sensor] = (24m, 2m),
        [ConsumableType.Transmitter] = (168m, 24m),
        [ConsumableType.Pod] = (4m, 1m),
        [ConsumableType.InfusionSet] = (6m, 0m),
        [ConsumableType.Reservoir] = (12m, 2m),
        [ConsumableType.InsulinTubing] = (6m, 0m),
        [ConsumableType.InsulinInUse] = (72m, 24m),
    };

    public TrackerTemplateService(
        IPatientDeviceRepository patientDeviceRepository,
        IDbContextFactory<NocturneDbContext> contextFactory,
        ILogger<TrackerTemplateService> logger)
    {
        _patientDeviceRepository = patientDeviceRepository;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AvailableTemplate>> GetAvailableTemplatesAsync(CancellationToken ct = default)
    {
        var currentDevices = (await _patientDeviceRepository.GetCurrentAsync(ct)).ToList();

        var templates = new Dictionary<string, AvailableTemplate>();

        foreach (var device in currentDevices)
        {
            if (device.CatalogId is null)
                continue;

            var catalogEntry = DeviceCatalog.GetById(device.CatalogId);
            if (catalogEntry is null)
                continue;

            var consumables = ConsumableCatalog.GetForDevice(catalogEntry);

            foreach (var consumable in consumables)
            {
                // Skip universal consumables here; they're added below
                if (consumable.ApplicableDeviceCategory is null)
                    continue;

                if (templates.ContainsKey(consumable.Id))
                    continue;

                var lifespanHours = ResolveLifespanHours(consumable, catalogEntry);

                templates[consumable.Id] = new AvailableTemplate(
                    ConsumableCatalogId: consumable.Id,
                    Name: consumable.Name,
                    Icon: consumable.DefaultIcon,
                    DefaultLifespanHours: lifespanHours,
                    IsHardCutoff: consumable.IsHardCutoff,
                    DeviceName: catalogEntry.Name
                );
            }
        }

        // Add universal consumables regardless of devices
        foreach (var consumable in ConsumableCatalog.GetAll().Where(c => c.ApplicableDeviceCategory is null))
        {
            if (!templates.ContainsKey(consumable.Id))
            {
                templates[consumable.Id] = new AvailableTemplate(
                    ConsumableCatalogId: consumable.Id,
                    Name: consumable.Name,
                    Icon: consumable.DefaultIcon,
                    DefaultLifespanHours: consumable.DefaultLifespanHours,
                    IsHardCutoff: consumable.IsHardCutoff,
                    DeviceName: null
                );
            }
        }

        return templates.Values.ToList();
    }

    /// <inheritdoc />
    public async Task<TemplateResult> ApplyTemplateAsync(TemplateApplication request, CancellationToken ct = default)
    {
        var consumable = ConsumableCatalog.GetById(request.ConsumableCatalogId)
            ?? throw new ArgumentException($"Unknown consumable catalog ID: {request.ConsumableCatalogId}");

        var lifespanHours = request.LifespanHoursOverride ?? consumable.DefaultLifespanHours;

        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        var tenantId = db.TenantId;

        // Create tracker definition
        var definition = new TrackerDefinitionEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Name = consumable.Name,
            Category = consumable.DefaultTrackerCategory,
            Icon = consumable.DefaultIcon,
            LifespanHours = lifespanHours,
            Mode = TrackerMode.Duration,
            DashboardVisibility = DashboardVisibility.Always,
            IsFavorite = true,
        };

        db.TrackerDefinitions.Add(definition);

        // Create alarm rules
        var (warnHours, urgentHours) = Thresholds.GetValueOrDefault(
            consumable.ConsumableType, (24m, 2m));

        var sourceTemplate = JsonSerializer.Serialize(new
        {
            template = consumable.Id,
            trackerDefinitionId = definition.Id,
            consumableCatalogId = consumable.Id,
        });

        var alertRuleIds = new List<Guid>();

        // Warning rule
        var warningRule = CreateAlertRule(
            tenantId: tenantId,
            definitionId: definition.Id,
            name: $"{consumable.Name} Warning",
            severity: AlertRuleSeverity.Warning,
            operatorStr: "<=",
            thresholdHours: warnHours,
            sortOrder: 0,
            sourceTemplate: sourceTemplate);
        db.AlertRules.Add(warningRule);
        alertRuleIds.Add(warningRule.Id);

        // Urgent rule
        var urgentRule = CreateAlertRule(
            tenantId: tenantId,
            definitionId: definition.Id,
            name: $"{consumable.Name} Urgent",
            severity: AlertRuleSeverity.Critical,
            operatorStr: "<=",
            thresholdHours: urgentHours,
            sortOrder: 1,
            sourceTemplate: sourceTemplate);
        db.AlertRules.Add(urgentRule);
        alertRuleIds.Add(urgentRule.Id);

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Applied template {ConsumableId}: created definition {DefinitionId} with {RuleCount} alarm rules",
            consumable.Id, definition.Id, alertRuleIds.Count);

        return new TemplateResult(definition.Id, alertRuleIds);
    }

    /// <summary>
    /// Resolves the effective lifespan for a consumable based on device-specific data.
    /// </summary>
    internal static int? ResolveLifespanHours(ConsumableCatalogEntry consumable, DeviceCatalogEntry device)
    {
        return consumable.ConsumableType switch
        {
            ConsumableType.Sensor when device.Cgm is not null =>
                device.Cgm.SensorDurationDays * 24,

            ConsumableType.Transmitter when device.Cgm?.TransmitterDurationDays is not null =>
                device.Cgm.TransmitterDurationDays.Value * 24,

            ConsumableType.Pod => device.Id switch
            {
                "omnipod-5" => 80,
                "omnipod-dash" => 72,
                _ => consumable.DefaultLifespanHours,
            },

            _ => consumable.DefaultLifespanHours,
        };
    }

    private static AlertRuleEntity CreateAlertRule(
        Guid tenantId,
        Guid definitionId,
        string name,
        AlertRuleSeverity severity,
        string operatorStr,
        decimal thresholdHours,
        int sortOrder,
        string sourceTemplate)
    {
        var conditionParams = JsonSerializer.Serialize(new
        {
            definition_id = definitionId,
            @operator = operatorStr,
            hours = thresholdHours,
        });

        return new AlertRuleEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Name = name,
            ConditionType = AlertConditionType.TrackerRemaining,
            ConditionParams = conditionParams,
            Severity = severity,
            IsEnabled = true,
            AutoResolveEnabled = true,
            SortOrder = sortOrder,
            SourceTemplate = sourceTemplate,
        };
    }
}
