using System.Runtime.CompilerServices;

using GDALService.Capabilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GDALService.Tests;

// The null and exception rules (AGENTS.md, "Null and exceptions"), checked
// against GDALService's own source so they cannot quietly erode:
//   - no null-forgiving `!` anywhere;
//   - no `goto`;
//   - `null` and `catch` only in the edge files that face third-party code, and
//     `catch` additionally in the request loop's one last-resort guard;
//   - no `_` discard arm in a switch that matches on types - a discard there would
//     swallow a newly added union case instead of failing the build;
//   - no type test (`is`, `as`) and no switch statement outside the edges.
// And the architecture's boundaries (spec "dependency-rules"), judged on
// resolved symbols:
//   - each namespace uses only the parts of the service below it;
//   - GDAL (`OSGeo`) only in Terrain.GdalBackend;
//   - the file system (File, Directory, DirectoryInfo, FileInfo) only in the tile catalog;
//   - only the composition root names an edge implementation;
//   - a capability never names another capability.
public class SourceRulesTests
{
    private static readonly string[] EdgeFiles =
        ["GdalEdge.cs", "GdalBootstrap.cs", "JsonEdge.cs", "FileSystemTileCatalog.cs", "StreamEdge.cs"];
    private const string LoopGuardFile = "ServiceLoop.cs";

    private static string ServiceDir([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "GDALService"));

    private static IEnumerable<string> RelativeSourceFiles() =>
        Directory.EnumerateFiles(ServiceDir(), "*.cs", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(ServiceDir(), f))
            .Where(f => !f.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                        && !f.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal));

    public static TheoryData<string> SourceFiles() => [.. RelativeSourceFiles()];

    private static SyntaxNode Parse(string relative)
    {
        var text = File.ReadAllText(Path.Combine(ServiceDir(), relative));
        return CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
    }

    private static string Where(SyntaxNode node, string relative) =>
        $"{relative}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {node}";

    [Fact]
    public void The_rules_see_the_service_source() =>
        Assert.Contains(Path.Combine("Terrain", "GdalBackend", "GdalEdge.cs"), RelativeSourceFiles());

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void No_null_forgiving_operator(string file) =>
        Assert.Empty(Parse(file).DescendantNodes()
            .Where(n => n.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            .Select(n => Where(n, file)));

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void No_goto(string file) =>
        Assert.Empty(Parse(file).DescendantNodes().OfType<GotoStatementSyntax>().Select(n => Where(n, file)));

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void Null_only_at_the_edges(string file)
    {
        if (EdgeFiles.Contains(Path.GetFileName(file))) { return; }
        Assert.Empty(Parse(file).DescendantNodes()
            .Where(n => n.IsKind(SyntaxKind.NullLiteralExpression))
            .Select(n => Where(n, file)));
    }

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void Catch_only_at_the_edges_and_in_the_loop_guard(string file)
    {
        var name = Path.GetFileName(file);
        var catches = Parse(file).DescendantNodes().OfType<CatchClauseSyntax>().ToList();
        if (EdgeFiles.Contains(name)) { return; }
        if (name == LoopGuardFile)
        {
            Assert.Single(catches);
            return;
        }
        Assert.Empty(catches.Select(n => Where(n, file)));
    }

    // A `_` arm is allowed only beside literal arms (a switch over strings, such
    // as the request type). Beside anything else - a type, a union case, an enum
    // member - it would absorb a newly added case. Syntax alone cannot tell a
    // type from a constant (`Outside =>` parses as a constant pattern), so every
    // non-literal arm counts.
    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void No_discard_arm_beside_non_literal_arms(string file) =>
        Assert.Empty(Parse(file).DescendantNodes().OfType<SwitchExpressionSyntax>()
            .Where(s => s.Arms.Any(a => a.Pattern is DiscardPatternSyntax)
                     && s.Arms.Any(a => a.Pattern is not DiscardPatternSyntax
                                           and not ConstantPatternSyntax { Expression: LiteralExpressionSyntax }))
            .Select(n => Where(n, file)));

    // `x is Some<T> s`, `x is Fault` and `x as T` handle the case they name and
    // silently skip every other one, and so does a switch statement: none of them
    // is checked for exhaustiveness. Outside the edges a union is matched only by
    // a switch expression, so a new case fails the build where it is unhandled.
    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void Unions_are_matched_only_by_switch_expressions(string file)
    {
        if (EdgeFiles.Contains(Path.GetFileName(file))) { return; }
        Assert.Empty(Parse(file).DescendantNodes()
            .Where(n => n is IsPatternExpressionSyntax or SwitchStatementSyntax
                     || n.IsKind(SyntaxKind.IsExpression) || n.IsKind(SyntaxKind.AsExpression))
            .Select(n => Where(n, file)));
    }

    // The boundary rules below judge what a name *means*, not how it is spelled:
    // every name is resolved against a compilation of the whole service, so a
    // `global using`, an alias or a fully qualified name cannot slip past them.
#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    // What the build adds to the service's own files: the SDK's implicit global
    // usings and the GDAL package's GdalConfiguration, both generated into obj/
    // by the build of this configuration, which the test build has just run.
    private static IEnumerable<string> GeneratedSourceFiles()
    {
        var obj = Path.Combine(ServiceDir(), "obj", "x64", Configuration, "net11.0");
        return Directory.EnumerateFiles(obj, "GDALService.GlobalUsings.g.cs", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(Path.Combine(obj, "NuGet"), "GdalConfiguration.cs", SearchOption.AllDirectories));
    }

    private static readonly Lazy<(CSharpCompilation Compilation, Dictionary<string, SyntaxTree> Trees)> Service = new(() =>
    {
        var options = new CSharpParseOptions(LanguageVersion.Preview);
        var trees = RelativeSourceFiles().ToDictionary(
            f => f,
            f => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(ServiceDir(), f)), options, path: f));
        var generated = GeneratedSourceFiles()
            .Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), options, path: f));
        // The test host's trusted assemblies are the framework plus everything
        // the service references (GDAL's managed API, the DI container).
        var references = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")?.ToString() ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("GDALService.Rules", trees.Values.Concat(generated), references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication, nullableContextOptions: NullableContextOptions.Enable));
        return (compilation, trees);
    });

    // Every type, method, property, field and event a file names, with where.
    private static IEnumerable<(SyntaxNode Node, ISymbol Symbol)> Referenced(string file)
    {
        var (compilation, trees) = Service.Value;
        var model = compilation.GetSemanticModel(trees[file]);
        return trees[file].GetRoot().DescendantNodes().OfType<SimpleNameSyntax>()
            .Select(name => (Node: (SyntaxNode)name, Symbol: model.GetSymbolInfo(name).Symbol))
            .Where(r => r.Symbol is ITypeSymbol or IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol)
            .Select(r => (r.Node, Symbol: r.Symbol ?? throw new InvalidOperationException()));
    }

    private static string NamespaceOf(ISymbol symbol) => symbol.ContainingNamespace?.ToDisplayString() ?? "";

    private static string DeclaredNamespace(string file) =>
        Service.Value.Trees[file].GetRoot().DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>()
            .Select(n => n.Name.ToString()).Single();

    private static INamedTypeSymbol? TypeOf(ISymbol symbol) => symbol as INamedTypeSymbol ?? symbol.ContainingType;

    // A symbol declared in generated code (GdalConfiguration) is third-party,
    // however its namespace is spelled; it is no part of the service's layers.
    private static bool IsGenerated(ISymbol symbol) =>
        TypeOf(symbol) is INamedTypeSymbol type
        && type.Locations.Any(l => l.SourceTree is SyntaxTree tree && !Service.Value.Trees.ContainsValue(tree) && l.IsInSource);

    // Every name must resolve, or a rule could miss it. The one error allowed is
    // CS8795: source generators ([GeneratedRegex]) do not run here, so a
    // generated partial method has its declaration but not its body.
    [Fact]
    public void The_rules_compile_the_service_as_the_build_does() =>
        Assert.Empty(Service.Value.Compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error && d.Id != "CS8795")
            .Select(d => d.ToString()));

    // Which parts of the service each namespace may use; every namespace may use
    // its own. The composition root (GDALService) wires everything and may use all.
    private static readonly Dictionary<string, string[]> MayUse = new()
    {
        ["GDALService.Common"] = [],
        ["GDALService.Domain"] = ["GDALService.Common"],
        ["GDALService.Terrain"] = ["GDALService.Common", "GDALService.Domain"],
        ["GDALService.Terrain.GdalBackend"] = ["GDALService.Common", "GDALService.Terrain"],
        ["GDALService.Project"] = ["GDALService.Common", "GDALService.Terrain"],
        ["GDALService.Protocol"] = ["GDALService.Common", "GDALService.Domain", "GDALService.Terrain"],
        ["GDALService.Capabilities"] =
            ["GDALService.Common", "GDALService.Domain", "GDALService.Protocol", "GDALService.Project", "GDALService.Terrain"],
        ["GDALService.Hosting"] = ["GDALService.Common", "GDALService.Protocol", "GDALService.Capabilities"],
    };

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void Each_part_uses_only_the_parts_below_it(string file)
    {
        var own = DeclaredNamespace(file);
        if (own == "GDALService") { return; }
        Assert.True(MayUse.ContainsKey(own), $"{file}: namespace {own} has no entry in the dependency table");
        Assert.Empty(Referenced(file)
            .Where(r => !IsGenerated(r.Symbol) && NamespaceOf(r.Symbol) is var used
                        && (used == "GDALService" || used.StartsWith("GDALService.", StringComparison.Ordinal))
                        && used != own && !MayUse[own].Contains(used))
            .Select(r => Where(r.Node, file)));
    }

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void Gdal_is_used_only_in_the_gdal_backend(string file)
    {
        if (DeclaredNamespace(file) == "GDALService.Terrain.GdalBackend") { return; }
        Assert.Empty(Referenced(file)
            .Where(r => NamespaceOf(r.Symbol).StartsWith("OSGeo", StringComparison.Ordinal) || IsGenerated(r.Symbol))
            .Select(r => Where(r.Node, file)));
    }

    private static readonly string[] FileSystemTypes =
        ["System.IO.File", "System.IO.Directory", "System.IO.FileInfo", "System.IO.DirectoryInfo"];

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void The_file_system_is_used_only_by_the_tile_catalog(string file)
    {
        if (Path.GetFileName(file) == "FileSystemTileCatalog.cs") { return; }
        Assert.Empty(Referenced(file)
            .Where(r => TypeOf(r.Symbol) is INamedTypeSymbol type && FileSystemTypes.Contains(type.ToDisplayString()))
            .Select(r => Where(r.Node, file)));
    }

    // The edges are the implementations of the I/O interfaces and everything in
    // the GDAL backend. Only the composition root chooses them; the rest of the
    // service sees the interfaces. Edge code may name other edge code.
    private static readonly string[] BoundaryInterfaces =
        ["GDALService.Project.ITileCatalog", "GDALService.Terrain.IRasterFactory",
         "GDALService.Terrain.IRaster", "GDALService.Terrain.IPixelReader"];

    private static bool IsEdge(INamedTypeSymbol type) =>
        type.ContainingNamespace.ToDisplayString() == "GDALService.Terrain.GdalBackend"
        || (type.TypeKind == TypeKind.Class && type.AllInterfaces.Any(i => BoundaryInterfaces.Contains(i.ToDisplayString())));

    private static IEnumerable<INamedTypeSymbol> DeclaredTypes(string file)
    {
        var model = Service.Value.Compilation.GetSemanticModel(Service.Value.Trees[file]);
        return Service.Value.Trees[file].GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
            .Select(declaration => model.GetDeclaredSymbol(declaration))
            .OfType<INamedTypeSymbol>();
    }

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void Only_the_composition_root_names_an_edge(string file)
    {
        if (DeclaredNamespace(file) == "GDALService" || DeclaredTypes(file).Any(IsEdge)) { return; }
        Assert.Empty(Referenced(file)
            .Where(r => TypeOf(r.Symbol) is INamedTypeSymbol type && IsEdge(type.OriginalDefinition))
            .Select(r => Where(r.Node, file)));
    }

    private static bool IsCapability(INamedTypeSymbol type) =>
        type is { TypeKind: TypeKind.Class, IsAbstract: false }
        && type.AllInterfaces.Any(i => i.ToDisplayString() == "GDALService.Capabilities.ICapability");

    [Fact]
    public void The_rules_see_every_capability() =>
        Assert.Equal(
            typeof(ICapability).Assembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ICapability).IsAssignableFrom(t))
                .Select(t => t.Name)
                .Order(StringComparer.Ordinal),
            Service.Value.Trees.Keys.SelectMany(DeclaredTypes).Where(IsCapability)
                .Select(t => t.Name)
                .Order(StringComparer.Ordinal));

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void A_capability_never_names_another(string file)
    {
        var own = DeclaredTypes(file).Where(IsCapability).ToList();
        if (own.Count == 0) { return; }
        Assert.Empty(Referenced(file)
            .Where(r => TypeOf(r.Symbol) is INamedTypeSymbol type && IsCapability(type)
                        && !own.Contains(type, SymbolEqualityComparer.Default))
            .Select(r => Where(r.Node, file)));
    }
}
