using System.Text.Json.Nodes;
using FluentAssertions;
using Nocturne.Alerts.ParityCorpus.Generator.Harness;
using Nocturne.API.Services.Alerts;
using Nocturne.API.Tests.TestDoubles;
using Nocturne.Core.Alerts.Native;
using Nocturne.Core.Models.Alerts;
using Xunit;

namespace Nocturne.API.Tests.Services.Alerts;

[Trait("Category", "Unit")]
public class AlertRuleConditionValidatorTests
{
    private const string SmartSnoozeOn = """{"snooze":{"smartSnooze":true,"conditions":[{"type":"threshold","threshold":{"value":70}}]}}""";
    private const string SmartSnoozeOff = """{"snooze":{"smartSnooze":false,"conditions":[{"type":"threshold","threshold":{"value":70}}]}}""";

    [NativeFact]
    public void Rejects_a_body_that_cannot_be_evaluated()
    {
        var issues = new AlertRuleConditionValidator().Validate(
            AlertConditionType.Composite,
            """{"operator":"and","conditions":[{"type":"threshold","threshold":{"value":70}}]}""",
            false, null, null);

        issues.Should().Equal(new RustValidationIssue("condition", "composite[0].threshold", "direction_missing", "direction"));
    }

    [NativeFact]
    public void Rejects_an_auto_resolve_tree_that_cannot_be_evaluated_only_while_enabled()
    {
        const string autoResolve = """{"type":"composite","composite":{"conditions":[{"type":"trend"}]}}""";

        new AlertRuleConditionValidator().Validate(AlertConditionType.Threshold, """{"direction":"below","value":70}""", true, autoResolve, null)
            .Should().Equal(
                new RustValidationIssue("auto_resolve", "auto_resolve", "operator_missing", "operator"),
                new RustValidationIssue("auto_resolve", "auto_resolve[0].trend", "field_missing", "bucket"));
        new AlertRuleConditionValidator().Validate(AlertConditionType.Threshold, """{"direction":"below","value":70}""", false, autoResolve, null)
            .Should().BeEmpty();
    }

    [NativeFact]
    public void Rejects_smart_snooze_conditions_only_when_smart_snooze_is_on()
    {
        new AlertRuleConditionValidator().Validate(AlertConditionType.Threshold, """{"direction":"below","value":70}""", false, null, SmartSnoozeOn)
            .Should().Equal(new RustValidationIssue("snooze", "snooze[0].threshold", "direction_missing", "direction"));
        new AlertRuleConditionValidator().Validate(AlertConditionType.Threshold, """{"direction":"below","value":70}""", false, null, SmartSnoozeOff)
            .Should().BeEmpty();
    }

    [NativeTheory]
    [InlineData("""{"direction":"below","value":"low"}""", "invalid_field", "value")]
    [InlineData("null", "payload_missing", null)]
    [InlineData("{", "not_an_object", null)]
    public void Rejects_a_body_that_does_not_read(string body, string reason, string? field)
    {
        new AlertRuleConditionValidator().Validate(AlertConditionType.Threshold, body, false, null, null)
            .Should().Equal(new RustValidationIssue("condition", "threshold", reason, field));
    }


    [NativeFact]
    public void Rejects_an_unresolvable_timezone()
    {
        new AlertRuleConditionValidator().Validate(
                AlertConditionType.TimeOfDay, """{"from":"22:00","to":"06:00","timezone":"Etc/Unknown"}""", false, null, null)
            .Should().Equal(new RustValidationIssue("condition", "time_of_day", "invalid_field", "timezone"));
    }

    private const string PumpModeSpan = """{"type":"state_span_active","state_span_active":{"category":"PumpMode","is_active":true}}""";

    public static TheoryData<AlertConditionType, string, bool, string?, string?, RustValidationIssue> PumpModeSpans => new()
    {
        {
            AlertConditionType.StateSpanActive, """{"category":"PumpMode","is_active":true}""", false, null, null,
            new RustValidationIssue("condition", "state_span_active", "pump_mode_category", "category")
        },
        {
            AlertConditionType.Sustained, """{"minutes":10,"child":{"type":"not","not":{"child":""" + PumpModeSpan + "}}}", false, null, null,
            new RustValidationIssue("condition", "sustained[0].not[0].state_span_active", "pump_mode_category", "category")
        },
        {
            AlertConditionType.Threshold, """{"direction":"below","value":70}""", true,
            """{"type":"composite","composite":{"operator":"or","conditions":[{"type":"trend","trend":{"bucket":"flat"}},""" + PumpModeSpan + "]}}", null,
            new RustValidationIssue("auto_resolve", "auto_resolve[1].state_span_active", "pump_mode_category", "category")
        },
        {
            AlertConditionType.Threshold, """{"direction":"below","value":70}""", false, null,
            """{"snooze":{"smartSnooze":true,"conditions":[""" + PumpModeSpan + "]}}",
            new RustValidationIssue("snooze", "snooze[0].state_span_active", "pump_mode_category", "category")
        },
    };

    [NativeTheory]
    [MemberData(nameof(PumpModeSpans))]
    public void Rejects_a_generic_state_span_on_the_pump_mode_category(
        AlertConditionType type, string body, bool autoResolveEnabled, string? autoResolve, string? clientConfiguration,
        RustValidationIssue expected)
    {
        new AlertRuleConditionValidator().Validate(type, body, autoResolveEnabled, autoResolve, clientConfiguration)
            .Should().Equal(expected);
    }

    private const string LegacyLow = """{"direction":"below","value":70,"legacyLabel":"Low"}""";

    private static StoredConditionTrees Stored(
        string conditionParams, string? autoResolve = null, string clientConfiguration = "{}") =>
        new(AlertConditionType.Threshold, conditionParams, autoResolve, clientConfiguration);

    [NativeFact]
    public void An_edit_strips_an_unknown_property_the_stored_rule_already_had()
    {
        var check = new AlertRuleConditionValidator().ValidateUpdate(
            AlertConditionType.Threshold, """{"direction":"below","value":65,"legacyLabel":"Low"}""", false, null, "{}",
            Stored(LegacyLow));

        check.Issues.Should().BeEmpty();
        check.Stripped.Should().Equal(new RustStrippedField("condition", "threshold", "legacyLabel"));
        check.ConditionParams.Should().Be("""{"direction":"below","value":65}""");
    }

    [NativeFact]
    public void An_edit_still_rejects_an_unknown_property_the_stored_rule_did_not_have()
    {
        var check = new AlertRuleConditionValidator().ValidateUpdate(
            AlertConditionType.SignalLoss, """{"timeoutMinutes":20}""", false, null, "{}",
            new StoredConditionTrees(AlertConditionType.SignalLoss, """{"timeout_minutes":20}""", null, "{}"));

        check.Issues.Should().Contain(new RustValidationIssue("condition", "signal_loss", "unknown_field", null));
        check.Stripped.Should().BeEmpty();
    }

    [NativeFact]
    public void An_edit_strips_stored_unknown_properties_from_the_auto_resolve_tree_and_snooze_conditions()
    {
        const string autoResolve = """{"type":"threshold","note":"x","threshold":{"direction":"above","value":90}}""";
        const string snooze = """{"snooze":{"smartSnooze":true,"maxCount":2,"conditions":[{"type":"iob","iob":{"operator":">","value":1,"units":"U"}}]}}""";
        const string snoozeOff = """{"snooze":{"smartSnooze":false,"conditions":[{"type":"iob","iob":{"operator":">","value":2,"units":"U"}}]}}""";

        var check = new AlertRuleConditionValidator().ValidateUpdate(
            AlertConditionType.Threshold, """{"direction":"below","value":70}""", true, autoResolve, snooze,
            Stored("""{"direction":"below","value":70}""", autoResolve, snoozeOff));

        check.Issues.Should().BeEmpty();
        check.Stripped.Should().Equal(
            new RustStrippedField("auto_resolve", "auto_resolve", "note"),
            new RustStrippedField("snooze", "snooze[0].iob", "units"));
        check.AutoResolveParams.Should().Be("""{"type":"threshold","threshold":{"direction":"above","value":90}}""");
        JsonNode.DeepEquals(
                JsonNode.Parse(check.ClientConfiguration!),
                JsonNode.Parse("""{"snooze":{"smartSnooze":true,"maxCount":2,"conditions":[{"type":"iob","iob":{"operator":">","value":1}}]}}"""))
            .Should().BeTrue(check.ClientConfiguration);
    }
}
