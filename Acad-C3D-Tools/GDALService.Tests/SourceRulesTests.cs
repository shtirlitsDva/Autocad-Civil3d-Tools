using System.Runtime.CompilerServices;

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
// And the architecture's boundaries (spec "dependency-rules"):
//   - GDAL (`OSGeo`) only under Terrain/GdalBackend/;
//   - the file system (File, Directory, DirectoryInfo, FileInfo) only in the tile catalog;
//   - a capability never names another capability;
//   - Domain/ and Common/ depend on nothing else in the service.
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

    private static bool Under(string file, params string[] folders) =>
        file.StartsWith(Path.Combine(folders) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static IEnumerable<IdentifierNameSyntax> Names(string file, params string[] names) =>
        Parse(file).DescendantNodes().OfType<IdentifierNameSyntax>().Where(n => names.Contains(n.Identifier.Text));

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void Gdal_is_used_only_in_the_gdal_backend(string file)
    {
        if (Under(file, "Terrain", "GdalBackend")) { return; }
        Assert.Empty(Names(file, "OSGeo").Select(n => Where(n, file)));
    }

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void The_file_system_is_used_only_by_the_tile_catalog(string file)
    {
        if (Path.GetFileName(file) == "FileSystemTileCatalog.cs") { return; }
        Assert.Empty(Names(file, "File", "Directory", "DirectoryInfo", "FileInfo").Select(n => Where(n, file)));
    }

    // Capabilities are the classes under Capabilities/ that implement ICapability.
    private static readonly Lazy<string[]> CapabilityNames = new(() =>
    [
        .. RelativeSourceFiles()
            .Where(f => Under(f, "Capabilities"))
            .SelectMany(f => Parse(f).DescendantNodes().OfType<ClassDeclarationSyntax>())
            .Where(c => c.BaseList?.Types.Any(t => t.Type.ToString() == "ICapability") == true)
            .Select(c => c.Identifier.Text),
    ]);

    [Fact]
    public void The_rules_see_every_capability() => Assert.Equal(5, CapabilityNames.Value.Length);

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void A_capability_never_names_another(string file)
    {
        if (!Under(file, "Capabilities")) { return; }
        var own = Parse(file).DescendantNodes().OfType<ClassDeclarationSyntax>().Select(c => c.Identifier.Text).ToHashSet();
        Assert.Empty(Names(file, [.. CapabilityNames.Value.Where(name => !own.Contains(name))]).Select(n => Where(n, file)));
    }

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void Domain_and_common_depend_on_nothing_else_in_the_service(string file)
    {
        if (!Under(file, "Domain") && !Under(file, "Common")) { return; }
        Assert.Empty(Parse(file).DescendantNodes().OfType<UsingDirectiveSyntax>()
            .Where(u => u.Name?.ToString() is string name && name.StartsWith("GDALService.", StringComparison.Ordinal)
                        && name != "GDALService.Common")
            .Select(n => Where(n, file)));
    }
}
