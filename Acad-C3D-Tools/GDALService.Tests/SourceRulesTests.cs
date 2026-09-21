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
public class SourceRulesTests
{
    private static readonly string[] EdgeFiles = ["GdalEdge.cs", "JsonEdge.cs", "FileSystemEdge.cs", "StreamEdge.cs"];
    private const string LoopGuardFile = "ServiceLoop.cs";

    private static string ServiceDir([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "GDALService"));

    private static IEnumerable<string> RelativeSourceFiles() =>
        Directory.EnumerateFiles(ServiceDir(), "*.cs", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(ServiceDir(), f))
            .Where(f => !f.StartsWith("obj" + Path.DirectorySeparatorChar) && !f.StartsWith("bin" + Path.DirectorySeparatorChar));

    public static TheoryData<string> SourceFiles() => new(RelativeSourceFiles());

    private static SyntaxNode Parse(string relative)
    {
        var text = File.ReadAllText(Path.Combine(ServiceDir(), relative));
        return CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
    }

    private static string Where(SyntaxNode node, string relative) =>
        $"{relative}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {node}";

    [Fact]
    public void The_rules_see_the_service_source() =>
        Assert.Contains(Path.Combine("Raster", "GdalEdge.cs"), RelativeSourceFiles());

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
                                        && !(a.Pattern is ConstantPatternSyntax { Expression: LiteralExpressionSyntax })))
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
}
