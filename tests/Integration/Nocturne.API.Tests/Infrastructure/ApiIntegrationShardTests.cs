using FluentAssertions;
using Xunit;

namespace Nocturne.API.Tests.Integration.Infrastructure;

/// <summary>The rules of <see cref="ApiIntegrationShards"/>, which states their reasons.</summary>
public class ApiIntegrationShardTests
{
    private const string Rules = "the shard rules on ApiIntegrationShards";

    private static readonly Type[] Types = typeof(ApiIntegrationTestBase).Assembly.GetTypes();

    private static readonly Type[] ApiTests =
        [.. Types.Where(type => typeof(ApiIntegrationTestBase).IsAssignableFrom(type))];

    [Fact]
    public void EveryApiIntegrationTestNamesOneShardOnItself()
    {
        var offenders = ApiTests
            .Where(type => !type.IsAbstract)
            .Where(type => DeclaredCollections(type) is not [var name] || !ApiIntegrationShards.All.Contains(name))
            .Select(type => $"{type.FullName} [{string.Join(", ", DeclaredCollections(type))}]")
            .ToList();

        offenders.Should().BeEmpty("each class names one of {0} on itself, per {1}",
            string.Join(", ", ApiIntegrationShards.All), Rules);
    }

    [Fact]
    public void NoBaseClassNamesACollection()
    {
        var offenders = ApiTests
            .Where(type => type.IsAbstract && DeclaredCollections(type).Count > 0)
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty("no base names a collection, per {0}", Rules);
    }

    [Fact]
    public void NoApiIntegrationTestTakesTheFixtureAsAClassFixture()
    {
        var offenders = ApiTests
            .Where(type => type.GetInterfaces().Contains(typeof(IClassFixture<ApiIntegrationTestFixture>)))
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty("a class shares its shard's host rather than booting its own, per {0}", Rules);
    }

    [Fact]
    public void EveryShardIsACollectionWithAnApiFixtureOfItsOwn()
    {
        var definitions = Types
            .Where(type => type.GetInterfaces().Contains(typeof(ICollectionFixture<ApiIntegrationTestFixture>)))
            .Select(type => type.GetCustomAttributesData()
                .Where(data => data.AttributeType == typeof(CollectionDefinitionAttribute))
                .Select(data => (string?)data.ConstructorArguments[0].Value)
                .SingleOrDefault())
            .ToList();

        definitions.Should().BeEquivalentTo(ApiIntegrationShards.All,
            "each shard needs one collection definition declaring the fixture, which is what gives " +
            "it an API host of its own, and a definition outside the shards is a collection the " +
            "suite no longer runs in parallel");
    }

    [Fact]
    public void EveryShardHoldsTests()
    {
        var used = ApiTests.Where(type => !type.IsAbstract).SelectMany(DeclaredCollections).ToHashSet();

        ApiIntegrationShards.All.Should().OnlyContain(shard => used.Contains(shard),
            "an empty shard runs no test, so the others carry its share of the suite");
        ApiTests.Count(type => !type.IsAbstract).Should().BeGreaterThan(50,
            "the suite has dozens of API test classes, and finding few means the scan lost them " +
            "rather than that the suite shrank");
    }

    private static List<string> DeclaredCollections(Type type) =>
        [.. type.GetCustomAttributesData()
            .Where(data => data.AttributeType == typeof(CollectionAttribute))
            .Select(data => (string)data.ConstructorArguments[0].Value!)];
}
