using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nocturne.API.Extensions;

namespace Nocturne.API.Tests.Infrastructure;

/// <summary>
/// A legacy concept's store, cache and decomposer are collaborators of its module
/// (<c>Services/Entries</c>, <c>Services/Treatments</c>, <c>Services/DeviceStatuses</c>, and the
/// concept's decomposer in <c>Services/V4</c>): outside the module, its own tests and
/// <see cref="ServiceRegistrationExtensions"/>, no code names one. Consumers use the module's service
/// and their tests mock it. <c>internal</c> cannot say this inside one assembly.
/// </summary>
/// <remarks>
/// <para>
/// The rule is a name, not a list of leak shapes: any identifier in code that names a port counts,
/// however it is injected, resolved, aliased or passed. Doc comments, comments and string literals
/// do not, so a type resolved from a string is not seen. Constructor reflection over the API and test
/// assemblies is the second net; it places each type by the file that declares it, not by its namespace.
/// </para>
/// <para>
/// The exemptions are the usages the treatment (#2969) and device status (#2970) slices remove; each of
/// those deletes its group.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public class LegacyModulePortGuardTests
{
    private sealed record Module(string Folder, string Concept, string[] Ports)
    {
        public bool Owns(string path) =>
            path.StartsWith($"src/API/Nocturne.API/Services/{Folder}/", StringComparison.Ordinal)
            || path.StartsWith($"src/API/Nocturne.API/Services/V4/{Concept}Decomposer", StringComparison.Ordinal)
            || path.StartsWith($"tests/Unit/Nocturne.API.Tests/Services/{Folder}/", StringComparison.Ordinal)
            || path.StartsWith($"tests/Unit/Nocturne.API.Tests/Services/V4/{Concept}Decomposer", StringComparison.Ordinal);
    }

    private static readonly Module[] Modules =
    [
        new("Entries", "Entry", ["IEntryStore", "IEntryCache", "IEntryDecomposer"]),
        new("Treatments", "Treatment", ["ITreatmentStore", "ITreatmentCache", "ITreatmentDecomposer"]),
        new("DeviceStatuses", "DeviceStatus", ["IDeviceStatusDecomposer"]),
    ];

    private const string Registration = "src/API/Nocturne.API/Extensions/ServiceRegistrationExtensions.cs";

    private static readonly string[] ReflectedSources = ["src/API/Nocturne.API", "tests/Unit/Nocturne.API.Tests"];

    /// <summary>Constructors outside a module that take one of its ports, as <c>Type(Port)</c>.</summary>
    private static readonly string[] ConstructorExemptions =
    [
        // #2969 Treatment
        "Nocturne.API.Controllers.V1.CountController(ITreatmentStore)",
        "Nocturne.API.Services.ConnectorPublishing.TreatmentPublisher(ITreatmentDecomposer)",
        "Nocturne.API.Services.Seeding.SampleDataSeeder(ITreatmentDecomposer)",
        "Nocturne.API.Services.V4.LegacyTreatmentStore(ITreatmentDecomposer)",

        // #2970 DeviceStatus
        "Nocturne.API.Controllers.V1.DeviceStatusController(IDeviceStatusDecomposer)",
        "Nocturne.API.Controllers.V3.DeviceStatusController(IDeviceStatusDecomposer)",
        "Nocturne.API.Services.ConnectorPublishing.DevicePublisher(IDeviceStatusDecomposer)",
        "Nocturne.API.Services.Seeding.SampleDataSeeder(IDeviceStatusDecomposer)",
    ];

    /// <summary>Members of <c>src</c> outside a module that name one of its ports, as <c>path: Member(Port)</c>.</summary>
    private static readonly string[] SourceExemptions =
    [
        // #2969 Treatment
        "src/API/Nocturne.API/Controllers/V1/CountController.cs: CountController._treatmentStore(ITreatmentStore)",
        "src/API/Nocturne.API/Controllers/V1/CountController.cs: CountController.CountController(ITreatmentStore)",
        "src/API/Nocturne.API/Services/ConnectorPublishing/TreatmentPublisher.cs: TreatmentPublisher._treatmentDecomposer(ITreatmentDecomposer)",
        "src/API/Nocturne.API/Services/ConnectorPublishing/TreatmentPublisher.cs: TreatmentPublisher.TreatmentPublisher(ITreatmentDecomposer)",
        "src/API/Nocturne.API/Services/Migration/MigrationJobService.cs: MigrationJob.s_treatmentsCollection(ITreatmentDecomposer)",
        "src/API/Nocturne.API/Services/Seeding/SampleDataSeeder.cs: SampleDataSeeder._treatmentDecomposer(ITreatmentDecomposer)",
        "src/API/Nocturne.API/Services/Seeding/SampleDataSeeder.cs: SampleDataSeeder.SampleDataSeeder(ITreatmentDecomposer)",
        "src/API/Nocturne.API/Services/V4/LegacyTreatmentStore.cs: LegacyTreatmentStore.LegacyTreatmentStore(ITreatmentDecomposer)",
        "src/API/Nocturne.API/Services/V4/LegacyTreatmentStore.cs: LegacyTreatmentStore._decomposer(ITreatmentDecomposer)",

        // #2970 DeviceStatus
        "src/API/Nocturne.API/Controllers/V1/DeviceStatusController.cs: DeviceStatusController._decomposer(IDeviceStatusDecomposer)",
        "src/API/Nocturne.API/Controllers/V1/DeviceStatusController.cs: DeviceStatusController.DeviceStatusController(IDeviceStatusDecomposer)",
        "src/API/Nocturne.API/Controllers/V3/DeviceStatusController.cs: DeviceStatusController._decomposer(IDeviceStatusDecomposer)",
        "src/API/Nocturne.API/Controllers/V3/DeviceStatusController.cs: DeviceStatusController.DeviceStatusController(IDeviceStatusDecomposer)",
        "src/API/Nocturne.API/Services/ConnectorPublishing/DevicePublisher.cs: DevicePublisher._decomposer(IDeviceStatusDecomposer)",
        "src/API/Nocturne.API/Services/ConnectorPublishing/DevicePublisher.cs: DevicePublisher.DevicePublisher(IDeviceStatusDecomposer)",
        "src/API/Nocturne.API/Services/Migration/MigrationJobService.cs: MigrationJob.s_deviceStatusCollection(IDeviceStatusDecomposer)",
        "src/API/Nocturne.API/Services/Migration/MigrationJobService.cs: MigrationJob.TransformDeviceStatusAsync(IDeviceStatusDecomposer)",
        "src/API/Nocturne.API/Services/Seeding/SampleDataSeeder.cs: SampleDataSeeder._deviceStatusDecomposer(IDeviceStatusDecomposer)",
        "src/API/Nocturne.API/Services/Seeding/SampleDataSeeder.cs: SampleDataSeeder.SampleDataSeeder(IDeviceStatusDecomposer)",
    ];

    /// <summary>Test files outside a module that name one of its ports, as <c>path(Port)</c>.</summary>
    private static readonly string[] TestExemptions =
    [
        // #2969 Treatment
        "tests/Integration/Nocturne.API.Tests/PercentTempBasalIntegrationTests.cs(ITreatmentDecomposer)",
        "tests/Integration/Nocturne.API.Tests/TreatmentBatchFailureIntegrationTests.cs(ITreatmentDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Authorization/ActivityReadScopeGuardTests.cs(ITreatmentStore)",
        "tests/Unit/Nocturne.API.Tests/Controllers/V1/CountControllerTests.cs(ITreatmentStore)",
        "tests/Unit/Nocturne.API.Tests/Controllers/V1/TreatmentFindDateWindowTests.cs(ITreatmentDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Controllers/V1/TreatmentFindDateWindowTests.cs(ITreatmentStore)",
        "tests/Unit/Nocturne.API.Tests/Controllers/V1/TreatmentsCreatePartialFailureTests.cs(ITreatmentCache)",
        "tests/Unit/Nocturne.API.Tests/Controllers/V1/TreatmentsCreatePartialFailureTests.cs(ITreatmentDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Controllers/V1/TreatmentsPostUpsertTests.cs(ITreatmentCache)",
        "tests/Unit/Nocturne.API.Tests/Controllers/V1/TreatmentsSaveFailedUpdateTests.cs(ITreatmentCache)",
        "tests/Unit/Nocturne.API.Tests/Controllers/V1/TreatmentsSaveFailedUpdateTests.cs(ITreatmentDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Migration/MigrationJobHarness.cs(ITreatmentDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Services/ConnectorPublishing/ClientIdRekeyTests.cs(ITreatmentCache)",
        "tests/Unit/Nocturne.API.Tests/Services/ConnectorPublishing/PublishSkippedDeletedTests.cs(ITreatmentCache)",
        "tests/Unit/Nocturne.API.Tests/Services/ConnectorPublishing/PublishSkippedDeletedTests.cs(ITreatmentDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Services/ConnectorPublishing/RecentTreatmentRepublishTests.cs(ITreatmentCache)",
        "tests/Unit/Nocturne.API.Tests/Services/ConnectorPublishing/TreatmentPublisherTests.cs(ITreatmentDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Services/Seeding/SampleDataSeederMixedZoneTests.cs(ITreatmentDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Services/V4/LegacyTreatmentRoundTripTests.cs(ITreatmentCache)",
        "tests/Unit/Nocturne.API.Tests/Services/V4/TreatmentClientIdRoundTripTests.cs(ITreatmentCache)",

        // #2970 DeviceStatus
        "tests/Integration/Nocturne.API.Tests/DeviceStatusV3StoredStatusIntegrationTests.cs(IDeviceStatusDecomposer)",
        "tests/Integration/Nocturne.API.Tests/LoopClosedLoopIntegrationTests.cs(IDeviceStatusDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Controllers/DeviceStatusCreateIdentityTests.cs(IDeviceStatusDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Controllers/LegacyPostBodyTests.cs(IDeviceStatusDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Controllers/V1/LegacyDeleteByIdTests.cs(IDeviceStatusDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Controllers/V1/LegacyReadCountCapTests.cs(IDeviceStatusDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Controllers/V3/DeviceStatusHistoryHeaderTests.cs(IDeviceStatusDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Controllers/V3/UploaderIdBindingTests.cs(IDeviceStatusDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Controllers/V3/V3CreateResponseTests.cs(IDeviceStatusDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Controllers/V3/V3DeviceStatusFindTests.cs(IDeviceStatusDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Controllers/V3/V3FilterOperatorMergeTests.cs(IDeviceStatusDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Controllers/V3/V3SearchTotalTests.cs(IDeviceStatusDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Controllers/V3/V3SortTests.cs(IDeviceStatusDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Migration/MigrationJobHarness.cs(IDeviceStatusDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Services/ConnectorPublishing/DevicePublisherTests.cs(IDeviceStatusDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Services/ConnectorPublishing/PublishSkippedDeletedTests.cs(IDeviceStatusDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Services/ConnectorPublishing/RecentGlucoseAndDeviceStatusPublishTests.cs(IDeviceStatusDecomposer)",
        "tests/Unit/Nocturne.API.Tests/Services/Seeding/SampleDataSeederMixedZoneTests.cs(IDeviceStatusDecomposer)",
    ];

    private static readonly IReadOnlyDictionary<string, Module> ModuleOfPort =
        Modules.SelectMany(m => m.Ports.Select(p => (p, m))).ToDictionary(x => x.p, x => x.m);

    private static readonly Assembly Api = typeof(ServiceRegistrationExtensions).Assembly;

    [Fact]
    public void OnlyTheExemptionsTakeAPortInAConstructorOutsideItsModule()
    {
        var files = DeclaringFiles();
        var takers = ConstructorTakers(Api, files)
            .Concat(ConstructorTakers(typeof(LegacyModulePortGuardTests).Assembly, files))
            .ToList();

        takers.Except(ConstructorExemptions).Should().BeEmpty("inject the module's service, not its store, cache or decomposer");
        ConstructorExemptions.Except(takers).Should().BeEmpty("a stale exemption would silently cover a future injection");
    }

    [Fact]
    public void OnlyTheExemptionsNameAPortInSourceOutsideItsModule()
    {
        var namers = Sources("src")
            .Where(s => s.Path != Registration)
            .SelectMany(s => NamedPorts(s.Tree)
                .Where(hit => !ModuleOfPort[hit.Port].Owns(s.Path))
                .Select(hit => $"{s.Path}: {hit.Member}({hit.Port})"))
            .Distinct()
            .ToList();

        namers.Except(SourceExemptions).Should().BeEmpty("use the module's service, not its store, cache or decomposer");
        SourceExemptions.Except(namers).Should().BeEmpty("a stale exemption would silently cover a future use");
    }

    [Fact]
    public void OnlyTheExemptionsNameAPortInATestOutsideItsModule()
    {
        var namers = Sources("tests")
            .SelectMany(s => NamedPorts(s.Tree)
                .Where(hit => !ModuleOfPort[hit.Port].Owns(s.Path))
                .Select(hit => $"{s.Path}({hit.Port})"))
            .Distinct()
            .ToList();

        namers.Except(TestExemptions).Should().BeEmpty("a consumer's test mocks the module's service");
        TestExemptions.Except(namers).Should().BeEmpty("a stale exemption would silently cover a future mock");
    }

    [Fact]
    public void EveryPortIsDeclaredOnceAndTheEntryPortsAreInternal()
    {
        var contracts = Assembly.Load("Nocturne.Core.Contracts");
        var declared = Api.GetTypes().Concat(contracts.GetTypes())
            .Where(t => t.IsInterface && ModuleOfPort.ContainsKey(t.Name))
            .ToList();

        declared.Select(t => t.Name).Should().BeEquivalentTo(ModuleOfPort.Keys, "a renamed port would drop out of the guard");
        declared.Where(t => ModuleOfPort[t.Name].Folder == "Entries").Should().OnlyContain(t => !t.IsPublic && t.Assembly == Api);
    }

    [Theory]
    [InlineData("Nocturne.API.Services.Entries.IEntryStore")]
    [InlineData("Nocturne.Core.Contracts.Treatments.ITreatmentCache, Nocturne.Core.Contracts")]
    public void FindsAPortWrappedInAParameterType(string portName)
    {
        var port = Api.GetType(portName) ?? Type.GetType(portName, throwOnError: true)!;

        foreach (var parameter in new[]
                 {
                     port, typeof(Lazy<>).MakeGenericType(port), typeof(Func<>).MakeGenericType(port),
                     typeof(IEnumerable<>).MakeGenericType(port), port.MakeArrayType(),
                 })
            PortsIn(parameter).Should().Equal([port.Name], parameter.ToString());

        PortsIn(typeof(Lazy<IComparable>)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("class C { readonly Mock<IEntryStore> _store = new(); }", "C._store(IEntryStore)")]
    [InlineData("class C { object M() => Mock.Of<Nocturne.Core.Contracts.Treatments.ITreatmentCache>(); }", "C.M(ITreatmentCache)")]
    [InlineData("using Port = Nocturne.API.Services.Entries.IEntryDecomposer; class C { }", "(file)(IEntryDecomposer)")]
    [InlineData("class C(IEntryStore store) { }", "C.C(IEntryStore)")]
    [InlineData("namespace N; class X<T> where T : IEntryStore { }", "X(IEntryStore)")]
    [InlineData("class C { void M(IServiceProvider sp) { var t = typeof(IEntryCache); sp.GetService(t); } }", "C.M(IEntryCache)")]
    [InlineData("class C { object M(IServiceProvider sp) => ActivatorUtilities.GetServiceOrCreateInstance<IEntryDecomposer>(sp); }", "C.M(IEntryDecomposer)")]
    [InlineData("class C { object M(IServiceProvider sp) => sp.GetRequiredService<IEnumerable<IEntryStore>>(); }", "C.M(IEntryStore)")]
    [InlineData("class C : ControllerBase { Task<IActionResult> Get(ITreatmentStore store) => null; }", "C.Get(ITreatmentStore)")]
    [InlineData("class H : Hub { Task Send(IDeviceStatusDecomposer d) => null; }", "H.Send(IDeviceStatusDecomposer)")]
    [InlineData("class W { Task InvokeAsync(HttpContext c, IEntryStore s) => null; }", "W.InvokeAsync(IEntryStore)")]
    [InlineData("class C { void M(WebApplication app) => app.MapGet(\"/\", Handle); static int Handle(ITreatmentCache c) => 1; }", "C.Handle(ITreatmentCache)")]
    [InlineData("class C { string N = nameof(IEntryStore); }", "C.N(IEntryStore)")]
    [InlineData("/// <see cref=\"IEntryStore\"/>\nclass C { string S = \"IEntryStore\"; } // IEntryStore", "")]
    [InlineData("class C { readonly Mock<IEntryService> _entries = new(); }", "")]
    public void DetectsANamedPort(string source, string expected) =>
        string.Join(",", NamedPorts(CSharpSyntaxTree.ParseText(source)).Select(h => $"{h.Member}({h.Port})"))
            .Should().Be(expected);

    /// <summary>
    /// Constructors taking a port, as <c>Type(Port)</c>, of types declared outside the port's module.
    /// A type with no declaration in <paramref name="files"/> counts as outside.
    /// </summary>
    private static IEnumerable<string> ConstructorTakers(Assembly assembly, IReadOnlyDictionary<string, List<string>> files) =>
        assembly.GetTypes()
            .Where(t => !t.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            .SelectMany(t => t
                .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(c => c.GetParameters())
                .SelectMany(p => PortsIn(p.ParameterType))
                .Where(port => !(files.TryGetValue(Outermost(t).FullName!, out var paths)
                    && paths.All(ModuleOfPort[port].Owns)))
                .Select(port => $"{t.FullName!.Replace('+', '.')}({port})"))
            .Distinct();

    private static Type Outermost(Type type) => type.DeclaringType is { } outer ? Outermost(outer) : type;

    /// <summary>The ports a parameter type names, directly or as a generic argument (<c>Lazy&lt;T&gt;</c>, <c>Func&lt;T&gt;</c>).</summary>
    private static IEnumerable<string> PortsIn(Type type)
    {
        if (type.HasElementType)
            return PortsIn(type.GetElementType()!);

        if (type.IsGenericType)
            return type.GetGenericArguments().SelectMany(PortsIn);

        return ModuleOfPort.ContainsKey(type.Name) ? [type.Name] : [];
    }

    /// <summary>
    /// Every identifier in code naming a port, with the member it sits in. Doc comments and comments are
    /// trivia and a string literal is one token, so neither holds an identifier.
    /// </summary>
    private static IEnumerable<(string Member, string Port)> NamedPorts(SyntaxTree tree) =>
        tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(name => ModuleOfPort.ContainsKey(name.Identifier.ValueText))
            .Select(name => (MemberOf(name), name.Identifier.ValueText))
            .Distinct();

    /// <summary>
    /// <see cref="EnclosingMember.Of"/>, except that a primary constructor parameter is the constructor
    /// (<c>Type.Type</c>), a name in a type's base list or constraints is the type, and a name outside
    /// every type, such as a <c>using</c> alias, is <c>(file)</c>.
    /// </summary>
    private static string MemberOf(SyntaxNode node)
    {
        if (node.Ancestors().OfType<ParameterListSyntax>().FirstOrDefault()?.Parent is TypeDeclarationSyntax primary)
            return TypePath(primary) + "." + primary.Identifier.ValueText;

        return node.Ancestors().FirstOrDefault(a => a is BaseTypeDeclarationSyntax
                or (MemberDeclarationSyntax and not BaseNamespaceDeclarationSyntax)) switch
        {
            null => "(file)",
            BaseTypeDeclarationSyntax type => TypePath(type),
            _ => EnclosingMember.Of(node),
        };
    }

    private static string TypePath(BaseTypeDeclarationSyntax type) =>
        string.Join(".", type.AncestorsAndSelf().OfType<BaseTypeDeclarationSyntax>().Reverse().Select(t => t.Identifier.ValueText));

    /// <summary>Each top-level type's reflection name (<c>Namespace.Name`arity</c>) to the files declaring it.</summary>
    private static Dictionary<string, List<string>> DeclaringFiles()
    {
        var root = RepositoryRoot();
        var files = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var (path, tree) in ReflectedSources.SelectMany(dir => Parse(root, dir, _ => true)))
        {
            foreach (var type in tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
                         .Where(t => !t.Ancestors().OfType<BaseTypeDeclarationSyntax>().Any()))
            {
                var ns = string.Join(".", type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString()));
                var arity = type is TypeDeclarationSyntax { TypeParameterList.Parameters.Count: > 0 and var count } ? $"`{count}" : "";
                var name = (ns.Length > 0 ? ns + "." : "") + type.Identifier.ValueText + arity;

                if (!files.TryGetValue(name, out var paths))
                    files[name] = paths = [];
                paths.Add(path);
            }
        }

        return files;
    }

    private static IEnumerable<(string Path, SyntaxTree Tree)> Sources(string tree) =>
        Parse(RepositoryRoot(), tree, text => ModuleOfPort.Keys.Any(port => text.Contains(port, StringComparison.Ordinal)));

    private static IEnumerable<(string Path, SyntaxTree Tree)> Parse(string root, string tree, Func<string, bool> wanted)
    {
        foreach (var path in Directory.EnumerateFiles(Path.Combine(root, tree), "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            var text = File.ReadAllText(path);
            if (!wanted(text))
                continue;

            yield return (Path.GetRelativePath(root, path).Replace('\\', '/'), CSharpSyntaxTree.ParseText(text));
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "tests", "Unit")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"No tests/Unit directory above {AppContext.BaseDirectory}.");
    }
}
