using System.Text.Json;
using Nocturne.API.Services.Alerts.Evaluators;
using Nocturne.Core.Alerts.Native;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Alerts;

namespace Nocturne.API.Services.Alerts.Engines;

/// <summary>One leaf of a rule's condition tree as the engine reads it.</summary>
/// <param name="Kind">The kind node dispatch resolves, or null for an unknown or empty slot.</param>
/// <param name="Params">The operands the engine decoded, or JSON null when there are none.</param>
internal readonly record struct ConditionLeaf(AlertConditionType? Kind, JsonElement Params)
{
    /// <summary>The operand <paramref name="name"/> as a GUID, or null when absent or not one.</summary>
    public Guid? GuidParam(string name) =>
        StringParam(name) is { } text && Guid.TryParse(text, out var id) ? id : null;

    /// <summary>The operand <paramref name="name"/> as a defined member of <typeparamref name="TEnum"/>.</summary>
    public TEnum? EnumParam<TEnum>(string name) where TEnum : struct, Enum =>
        StringParam(name) is { } text && Enum.TryParse<TEnum>(text, ignoreCase: true, out var value)
                                      && Enum.IsDefined(value)
            ? value
            : null;

    /// <summary>The operand <paramref name="name"/> when it is a string.</summary>
    public string? StringParam(string name) =>
        Params.ValueKind == JsonValueKind.Object
        && Params.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

/// <summary>
/// A rule's condition trees read through the engine (<c>describe</c> and
/// <c>references_wall_clock</c>), so the host never parses a condition itself: what it finds in a
/// tree is what evaluation reads.
/// </summary>
/// <remarks>
/// A body that is not JSON goes to the engine as a JSON string, as evaluation sends it, and reads
/// as one leaf of the rule's own kind with no operands. An auto-resolve tree that is not JSON, or
/// that the engine cannot evaluate, has no leaves.
/// </remarks>
internal static class RuleTrees
{
    /// <summary>
    /// The leaves of the rule's body in pre-order, then those of its auto-resolve tree when
    /// <paramref name="includeAutoResolve"/> and auto-resolve is enabled.
    /// </summary>
    /// <exception cref="RustAlertEngineException">The engine rejected the request.</exception>
    public static IReadOnlyList<ConditionLeaf> Leaves(AlertRuleSnapshot rule, bool includeAutoResolve) =>
        Leaves(
            rule.ConditionType,
            Body(rule.ConditionType, rule.ConditionParams),
            includeAutoResolve && rule.AutoResolveEnabled ? AutoResolve(rule.AutoResolveParams) : null);

    /// <summary>The leaves of a body stored as <paramref name="conditionParams"/>.</summary>
    /// <exception cref="RustAlertEngineException">The engine rejected the request.</exception>
    public static IReadOnlyList<ConditionLeaf> Leaves(AlertConditionType type, string conditionParams) =>
        Leaves(type, Body(type, conditionParams), null);

    /// <summary>The leaves of a body as a request carries it.</summary>
    /// <exception cref="RustAlertEngineException">The engine rejected the request.</exception>
    public static IReadOnlyList<ConditionLeaf> Leaves(AlertConditionType type, JsonElement conditionParams) =>
        Leaves(type, conditionParams, null);

    /// <summary>The rule ids the <c>alert_state</c> leaves among <paramref name="leaves"/> name.</summary>
    public static IEnumerable<Guid> AlertStateTargets(IEnumerable<ConditionLeaf> leaves) =>
        leaves.Where(leaf => leaf.Kind == AlertConditionType.AlertState)
            .Select(leaf => leaf.GuidParam("alert_id"))
            .OfType<Guid>();

    /// <summary>
    /// Whether the rule must also be evaluated on the wall clock (docs/alerts/engine-semantics.md §5.1).
    /// </summary>
    /// <exception cref="RustAlertEngineException">The engine rejected the request.</exception>
    public static bool ReferencesWallClock(AlertRuleSnapshot rule) =>
        RustAlertEngine.ReferencesWallClock(new RustRuleConditionsRequest
        {
            ConditionType = AlertConditionTypeNames.ToWireString(rule.ConditionType),
            ConditionParams = Body(rule.ConditionType, rule.ConditionParams),
            AutoResolveEnabled = rule.AutoResolveEnabled,
            AutoResolveParams = rule.AutoResolveEnabled ? AutoResolve(rule.AutoResolveParams) : null,
        });

    /// <summary>
    /// The body as evaluation sends it: timezone ids canonicalised, and a body that is not JSON
    /// as a JSON string, which the engine cannot read as a payload.
    /// </summary>
    public static JsonElement Body(AlertConditionType type, string conditionParams)
    {
        try
        {
            return RustEnvelopeMapper.ParseJson(ConditionTimeZones.CanonicaliseRule(type, conditionParams));
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(conditionParams);
        }
    }

    /// <summary>The auto-resolve tree as evaluation sends it, or null when it is blank or not JSON.</summary>
    public static JsonElement? AutoResolve(string? autoResolveParams)
    {
        if (string.IsNullOrWhiteSpace(autoResolveParams))
            return null;
        try
        {
            return RustEnvelopeMapper.ParseNode(autoResolveParams);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<ConditionLeaf> Leaves(
        AlertConditionType type, JsonElement body, JsonElement? autoResolve)
    {
        var response = RustAlertEngine.Describe(new RustDescribeRequest
        {
            ConditionType = AlertConditionTypeNames.ToWireString(type),
            ConditionParams = body,
            AutoResolveParams = autoResolve,
        });
        var leaves = new List<ConditionLeaf>();
        Collect(response.Tree!.Value, leaves);
        if (response.AutoResolveTree is { ValueKind: JsonValueKind.Object } autoResolveTree)
            Collect(autoResolveTree, leaves);
        return leaves;
    }

    private static void Collect(JsonElement node, List<ConditionLeaf> leaves)
    {
        if (node.ValueKind != JsonValueKind.Object)
            return;

        if (node.TryGetProperty("leaf_id", out _))
        {
            var kind = node.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String
                ? AlertConditionTypeNames.FromWireString(k.GetString())
                : null;
            var operands = node.TryGetProperty("params", out var p) ? p.Clone() : default;
            leaves.Add(new ConditionLeaf(kind, operands));
            return;
        }

        if (node.TryGetProperty("conditions", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
                Collect(child, leaves);
        }
        if (node.TryGetProperty("child", out var only))
            Collect(only, leaves);
    }
}
