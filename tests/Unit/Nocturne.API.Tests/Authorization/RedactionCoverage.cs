using System.Collections;
using System.Reflection;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace Nocturne.API.Tests.Authorization;

/// <summary>
/// Checks a scope guard against every property of the types it narrows, so a property added to a
/// response cannot ship unredacted: <see cref="Populated{T}"/> builds a response with every
/// property set, the guard redacts it for a caller with no category, and
/// <see cref="AssertWithheld"/> fails on any property still holding something other than null, an
/// empty collection or its type's default, unless it is allowlisted as served whatever the scope.
/// </summary>
/// <example>
/// <code>
/// RedactionCoverage.AssertWithheld(
///     Guard.Redact(RedactionCoverage.Populated&lt;XResponse&gt;(), Granted()),
///     [typeof(XSlot), typeof(XSummary)],
///     ["XSlot.Kept"]);
/// </code>
/// </example>
internal static class RedactionCoverage
{
    /// <summary>Past any response's depth; only a recursive type reaches it, and reaching it fails.</summary>
    private const int MaxDepth = 16;

    public static T Populated<T>() where T : new() => (T)Populate(typeof(T), 0)!;

    /// <param name="types">The types whose properties the guard is responsible for.</param>
    /// <param name="allowed">Properties served whatever the caller's scopes, as <c>Type.Property</c>.</param>
    public static void AssertWithheld(object root, IReadOnlyCollection<Type> types, IReadOnlyCollection<string> allowed)
    {
        var leaks = new List<string>();
        var checkedTypes = new HashSet<Type>();
        Walk(root, 0);

        checkedTypes.Should().BeEquivalentTo(types, "every type the guard narrows must be reached from the response");
        leaks.Should().BeEmpty("a caller with no category must get none of these");

        void Walk(object? value, int depth)
        {
            if (value is null || IsScalar(value.GetType()))
                return;
            if (depth > MaxDepth)
            {
                leaks.Add($"depth cap reached at {value.GetType().Name}");
                return;
            }
            if (value is IEnumerable items)
            {
                foreach (var item in items)
                    Walk(item, depth + 1);
                return;
            }

            var type = value.GetType();
            var guarded = types.Contains(type);
            if (guarded)
                checkedTypes.Add(type);

            foreach (var property in Served(type))
            {
                var child = property.GetValue(value);
                if (guarded && !allowed.Contains($"{type.Name}.{property.Name}") && Holds(child))
                    leaks.Add($"{type.Name}.{property.Name}");
                Walk(child, depth + 1);
            }
        }
    }

    private static bool Holds(object? value) => value switch
    {
        null => false,
        string text => text.Length > 0,
        IEnumerable items => items.Cast<object?>().Any(),
        _ when value.GetType().IsValueType => !value.Equals(Activator.CreateInstance(value.GetType())),
        _ => true,
    };

    private static IEnumerable<PropertyInfo> Served(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0
                && p.GetCustomAttribute<JsonIgnoreAttribute>() is null);

    private static object? Populate(Type type, int depth)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return Populate(underlying, depth);
        if (type == typeof(string)) return "x";
        if (type == typeof(bool)) return true;
        if (type.IsEnum) return Enum.GetValues(type).Cast<object>().Last();
        if (type == typeof(DateTime)) return new DateTime(2024, 6, 3, 0, 0, 0, DateTimeKind.Utc);
        if (type == typeof(DateOnly)) return new DateOnly(2024, 6, 3);
        if (type.IsPrimitive || type == typeof(decimal)) return Convert.ChangeType(1, type);
        if (depth > MaxDepth)
            throw new InvalidOperationException($"Depth cap reached populating {type.Name}; a recursive type needs a hand-built fixture.");

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = (IList)Activator.CreateInstance(type)!;
            list.Add(Populate(type.GetGenericArguments()[0], depth + 1));
            return list;
        }

        if (type.GetConstructor(Type.EmptyTypes) is null)
            return null;
        var instance = Activator.CreateInstance(type)!;
        foreach (var property in Served(type))
            property.SetValue(instance, Populate(property.PropertyType, depth + 1));
        return instance;
    }

    private static bool IsScalar(Type type) =>
        type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
        || type == typeof(DateTime) || type == typeof(DateOnly) || Nullable.GetUnderlyingType(type) is not null;
}
