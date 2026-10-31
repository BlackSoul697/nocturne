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
/// <see cref="ServiceRegistrationExtensions"/>, no code names one, nor the module's concrete service or
/// decomposer, whose static members are as reachable as the ports. Consumers use the module's service
/// port and their tests mock it. <c>internal</c> cannot say this inside one assembly.
/// </summary>
/// <remarks>
/// <para>
/// The rule is a name, not a list of leak shapes: any identifier in code that names a port counts,
/// however it is injected, resolved, aliased or passed. Doc comments, comments and string literals
/// do not, so a type resolved from a string is not seen. Constructor reflection over the API and test
/// assemblies is the second net; it places each type by the file that declares it, not by its namespace.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public class LegacyModulePortGuardTests
{
    private sealed record Module(string Folder, string Concept, string[] Ports, string[] Classes)
    {
        public bool Owns(string path) =>
            path.StartsWith($"src/API/Nocturne.API/Services/{Folder}/", StringComparison.Ordinal)
            || path.StartsWith($"src/API/Nocturne.API/Services/V4/{Concept}Decomposer", StringComparison.Ordinal)
            || path.StartsWith($"tests/Unit/Nocturne.API.Tests/Services/{Folder}/", StringComparison.Ordinal)
            || path.StartsWith($"tests/Unit/Nocturne.API.Tests/Services/V4/{Concept}Decomposer", StringComparison.Ordinal);
    }

    private static readonly Module[] Modules =
    [
        new("Entries", "Entry", ["IEntryStore", "IEntryCache", "IEntryDecomposer"], ["EntryService", "EntryDecomposer"]),
        new("Treatments", "Treatment", ["ITreatmentStore", "ITreatmentCache", "ITreatmentDecomposer"], ["TreatmentService", "TreatmentDecomposer"]),
        new("DeviceStatuses", "DeviceStatus", ["IDeviceStatusStore", "IDeviceStatusDecomposer"], ["DeviceStatusService", "DeviceStatusDecomposer"]),
    ];

    private const string Registration = "src/API/Nocturne.API/Extensions/ServiceRegistrationExtensions.cs";

    private static readonly string[] ReflectedSources = ["src/API/Nocturne.API", "tests/Unit/Nocturne.API.Tests"];

    private static readonly IReadOnlyDictionary<string, Module> ModuleOfPort =
        Modules.SelectMany(m => m.Ports.Select(p => (p, m))).ToDictionary(x => x.p, x => x.m);

    private static readonly IReadOnlyDictionary<string, Module> ModuleOfName =
        Modules.SelectMany(m => m.Ports.Concat(m.Classes).Select(n => (n, m))).ToDictionary(x => x.n, x => x.m);

    /// <summary>
    /// A file outside a module that names one of its concrete classes, with the reason. Each is a
    /// consumer reaching into the module and is to leave this list by going through the module's
    /// service, not to be joined by another.
    /// </summary>
    private sealed record Exemption(string Path, string Name, string Reason);

    private const string BuildsDecomposer = "builds the decomposer over fake repositories to test its writes";

    private const string CallsStaticMapper = "calls a pure static mapper of the decomposer";

    private static readonly Exemption[] Exemptions =
    [
        new("src/API/Nocturne.API/Services/ConnectorPublishing/TreatmentPublisher.cs", "TreatmentDecomposer", "calls the decomposer's static upstream fingerprint"),
        new("tests/Integration/Nocturne.API.Tests/TreatmentBatchFailureIntegrationTests.cs", "TreatmentDecomposer", BuildsDecomposer),
        new("tests/Unit/Nocturne.API.Tests/Controllers/V1/TreatmentsPostUpsertTests.cs", "TreatmentDecomposer", BuildsDecomposer),
        new("tests/Unit/Nocturne.API.Tests/Services/ConnectorPublishing/ClientIdRekeyTests.cs", "TreatmentDecomposer", BuildsDecomposer),
        new("tests/Unit/Nocturne.API.Tests/Services/ConnectorPublishing/RecentTreatmentRepublishTests.cs", "TreatmentDecomposer", BuildsDecomposer),
        new("tests/Unit/Nocturne.API.Tests/Services/ConnectorPublishing/RecentGlucoseAndDeviceStatusPublishTests.cs", "DeviceStatusDecomposer", BuildsDecomposer),
        new("tests/Unit/Nocturne.API.Tests/TestDoubles/DeviceStatusDecomposerDoubles.cs", "DeviceStatusDecomposer", BuildsDecomposer),
        new("tests/Unit/Nocturne.API.Tests/Services/V4/BatchResendMatchesSingleTests.cs", "EntryDecomposer", BuildsDecomposer),
        new("tests/Unit/Nocturne.API.Tests/Services/V4/BatchResendMatchesSingleTests.cs", "TreatmentDecomposer", BuildsDecomposer),
        new("tests/Unit/Nocturne.API.Tests/Services/V4/BatchResendMatchesSingleTests.cs", "DeviceStatusDecomposer", BuildsDecomposer),
        new("tests/Unit/Nocturne.API.Tests/Services/V4/ResendKeepsCorrelationIdTests.cs", "EntryDecomposer", BuildsDecomposer),
        new("tests/Unit/Nocturne.API.Tests/Services/V4/ResendKeepsCorrelationIdTests.cs", "TreatmentDecomposer", BuildsDecomposer),
        new("tests/Unit/Nocturne.API.Tests/Services/V4/PreBolusWriteBackPullBackTests.cs", "TreatmentDecomposer", BuildsDecomposer),
        new("tests/Unit/Nocturne.API.Tests/Services/V4/TreatmentRequiredColumnsTests.cs", "TreatmentDecomposer", CallsStaticMapper),
        new("tests/Unit/Nocturne.API.Tests/Services/Analytics/ChartDataServiceTests.cs", "TreatmentDecomposer", CallsStaticMapper),
        new("tests/Unit/Nocturne.API.Tests/Services/Analytics/LoopAnalysisTempBasalCancelTests.cs", "TreatmentDecomposer", CallsStaticMapper),
        new("tests/Unit/Nocturne.API.Tests/Services/Seeding/DemoLoopAnalysisTests.cs", "TreatmentDecomposer", CallsStaticMapper),
        new("tests/Unit/Nocturne.API.Tests/Services/Seeding/DemoLoopHealthTests.cs", "TreatmentDecomposer", CallsStaticMapper),
    ];

    private static bool IsExempt(string path, string name) =>
        Exemptions.Any(e => e.Path == path && e.Name == name);

    private static readonly Assembly Api = typeof(ServiceRegistrationExtensions).Assembly;

    [Fact]
    public void NothingTakesAPortInAConstructorOutsideItsModule()
    {
        var files = DeclaringFiles();
        var takers = ConstructorTakers(Api, files)
            .Concat(ConstructorTakers(typeof(LegacyModulePortGuardTests).Assembly, files));

        takers.Should().BeEmpty("inject the module's service, not its store, cache or decomposer");
    }

    [Fact]
    public void NothingNamesAModuleInternalInSourceOutsideItsModule()
    {
        var namers = Sources("src")
            .Where(s => s.Path != Registration)
            .SelectMany(s => NamedModuleTypes(s.Tree)
                .Where(hit => !ModuleOfName[hit.Name].Owns(s.Path) && !IsExempt(s.Path, hit.Name))
                .Select(hit => $"{s.Path}: {hit.Member}({hit.Name})"))
            .Distinct();

        namers.Should().BeEmpty("use the module's service, not its store, cache, decomposer or concrete classes");
    }

    [Fact]
    public void NothingNamesAModuleInternalInATestOutsideItsModule()
    {
        var namers = Sources("tests")
            .SelectMany(s => NamedModuleTypes(s.Tree)
                .Where(hit => !ModuleOfName[hit.Name].Owns(s.Path) && !IsExempt(s.Path, hit.Name))
                .Select(hit => $"{s.Path}({hit.Name})"))
            .Distinct();

        namers.Should().BeEmpty("a consumer's test mocks the module's service");
    }

    [Fact]
    public void EveryExemptionStillNamesItsClass()
    {
        var sources = Sources("src").Concat(Sources("tests")).ToDictionary(s => s.Path);

        var stale = Exemptions
            .Where(e => !sources.TryGetValue(e.Path, out var source)
                || !NamedModuleTypes(source.Tree).Any(hit => hit.Name == e.Name))
            .Select(e => $"{e.Path}({e.Name})");

        stale.Should().BeEmpty("an exemption outlives the reference it excuses");
        Exemptions.Should().OnlyContain(e => Modules.Any(m => m.Classes.Contains(e.Name)), "only a concrete class is excusable");
        Exemptions.Should().OnlyContain(e => !ModuleOfName[e.Name].Owns(e.Path), "a file inside its module needs no exemption");
    }

    [Fact]
    public void EveryPortIsDeclaredOnceAndInternal()
    {
        var contracts = Assembly.Load("Nocturne.Core.Contracts");
        var declared = Api.GetTypes().Concat(contracts.GetTypes())
            .Where(t => t.IsInterface && ModuleOfPort.ContainsKey(t.Name))
            .ToList();

        declared.Select(t => t.Name).Should().BeEquivalentTo(ModuleOfPort.Keys, "a renamed port would drop out of the guard");
        declared.Should().OnlyContain(t => !t.IsPublic && t.Assembly == Api);
    }

    [Theory]
    [InlineData("Nocturne.API.Services.Entries.IEntryStore")]
    [InlineData("Nocturne.API.Services.Treatments.ITreatmentCache")]
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
    [InlineData("class C { object M() => Mock.Of<Nocturne.API.Services.Treatments.ITreatmentCache>(); }", "C.M(ITreatmentCache)")]
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
    [InlineData("class C { double M(Entry e) => EntryDecomposer.WrittenMgdl(e); }", "C.M(EntryDecomposer)")]
    [InlineData("class C { void M(JsonElement p) => JsonMergePatch.ApplyInPlace(e, p, EntryService.CarrySgvAndMgdl); }", "C.M(EntryService)")]
    [InlineData("class C { object M() => new TreatmentService(null); }", "C.M(TreatmentService)")]
    [InlineData("class C { ILogger<DeviceStatusDecomposer> L; }", "C.L(DeviceStatusDecomposer)")]
    [InlineData("using Service = Nocturne.API.Services.DeviceStatuses.DeviceStatusService; class C { }", "(file)(DeviceStatusService)")]
    [InlineData("class C { readonly Mock<ITreatmentService> _t = new(); string S = \"EntryService\"; } // TreatmentDecomposer", "")]
    public void DetectsANamedModuleType(string source, string expected) =>
        string.Join(",", NamedModuleTypes(CSharpSyntaxTree.ParseText(source)).Select(h => $"{h.Member}({h.Name})"))
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
    /// Every identifier in code naming a port or a module's concrete class, with the member it sits in. Doc
    /// comments and comments are trivia and a string literal is one token, so neither holds an identifier.
    /// </summary>
    private static IEnumerable<(string Member, string Name)> NamedModuleTypes(SyntaxTree tree) =>
        tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(name => ModuleOfName.ContainsKey(name.Identifier.ValueText))
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
        var files = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var source in ReflectedSources.SelectMany(RepoSources.Under))
        {
            foreach (var type in source.Tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
                         .Where(t => !t.Ancestors().OfType<BaseTypeDeclarationSyntax>().Any()))
            {
                var ns = string.Join(".", type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString()));
                var arity = type is TypeDeclarationSyntax { TypeParameterList.Parameters.Count: > 0 and var count } ? $"`{count}" : "";
                var name = (ns.Length > 0 ? ns + "." : "") + type.Identifier.ValueText + arity;

                if (!files.TryGetValue(name, out var paths))
                    files[name] = paths = [];
                paths.Add(source.Path);
            }
        }

        return files;
    }

    private static IEnumerable<SourceFile> Sources(string tree) =>
        RepoSources.Under(tree).Where(s => ModuleOfName.Keys.Any(name => s.Text.Contains(name, StringComparison.Ordinal)));
}
