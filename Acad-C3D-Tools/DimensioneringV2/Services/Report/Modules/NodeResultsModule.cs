using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report;
using DimensioneringV2.Services.Report.Styles;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Services.Report.Modules;

/// <summary>
/// Renders §7.5 Knudepunkter: table of building and root nodes
/// with power demand and pressure results.
/// In multi-network mode, renders once per network with scoped graphs.
/// </summary>
internal class NodeResultsModule : IReportModule
{
    public ReportModuleId Id => ReportModuleId.NodeResults;
    public string DisplayName => "Knudepunkter";
    public bool IsImplemented => true;
    public NetworkAffinity Affinity => NetworkAffinity.PerNetwork;

    public void Compose(IDocumentContainer container, ReportDataContext context)
    {
        var scope = context.Scope!;

        // Collect unique nodes across scoped graphs
        var seen = new HashSet<NodeJunction>();
        var nodeData = new List<(NodeJunction Node, double Effekt, double PressureLoss, double DifferentialPressure)>();

        foreach (var graph in scope.Graphs)
        {
            foreach (var node in graph.Vertices)
            {
                if (string.IsNullOrEmpty(node.NodeId)) continue;
                if (!seen.Add(node)) continue;
                if (!node.IsBuildingNode && !node.IsRootNode) continue;

                double effekt = 0;
                double pressureLoss = 0;
                double differentialPressure = 0;

                if (node.IsBuildingNode)
                {
                    foreach (var edge in graph.AdjacentEdges(node))
                    {
                        var f = edge.PipeSegment;
                        pressureLoss = f.PressureLossAtClientSupply + f.PressureLossAtClientReturn;
                        differentialPressure = f.DifferentialPressureAtClient;
                        effekt = f.Effekt;
                    }
                }

                nodeData.Add((node, effekt, pressureLoss, differentialPressure));
            }
        }

        var sortedNodes = nodeData.OrderBy(n => n.Node.NodeId, NodeIdComparer.Instance).ToList();

        // Build heading
        string heading;
        if (scope.IsSingleNetworkMode)
        {
            heading = $"{context.CurrentSection}  Knudepunkter";
        }
        else
        {
            var sub = ++context.SubSectionCounter;
            heading = $"{context.CurrentSection}.{sub}  Knudepunkter \u2014 {scope.NetworkDisplayName}";
        }

        container.Page(page =>
        {
            page.Size(ReportStyles.PageSizeA4);
            page.MarginLeft(ReportStyles.MarginLeft, Unit.Point);
            page.MarginRight(ReportStyles.MarginRight, Unit.Point);
            page.MarginTop(ReportStyles.MarginTop, Unit.Point);
            page.MarginBottom(ReportStyles.MarginBottom, Unit.Point);

            page.Header().Text(heading)
                .FontSize(ReportStyles.FontSizeH1)
                .FontColor(ReportStyles.ColorPrimary).Bold();

            page.Content().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);   // Knude-id
                    columns.RelativeColumn(1.5f); // Effektbehov [kW]
                    columns.RelativeColumn(1.5f); // Tryktab frem [bar]
                    columns.RelativeColumn(1.5f); // Tilg. differenstryk [bar]
                });

                table.Header(header =>
                {
                    header.Cell().Background(ReportStyles.ColorHeaderBg)
                        .Padding(ReportStyles.TableCellPadding)
                        .Text("Knude-id").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                    header.Cell().Background(ReportStyles.ColorHeaderBg)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text("Effektbehov [kW]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                    header.Cell().Background(ReportStyles.ColorHeaderBg)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text("Tryktab frem [bar]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                    header.Cell().Background(ReportStyles.ColorHeaderBg)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text("Tilg. differenstryk [bar]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                });

                bool alternate = false;
                foreach (var (node, effekt, pressureLoss, differentialPressure) in sortedNodes)
                {
                    var bg = alternate ? ReportStyles.ColorAlternateRowBg : "#FFFFFF";

                    table.Cell().Background(bg)
                        .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                        .Padding(ReportStyles.TableCellPadding)
                        .Text($"{node.NodeId}").FontSize(ReportStyles.FontSizeSmall);

                    table.Cell().Background(bg)
                        .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text($"{effekt:F2}").FontSize(ReportStyles.FontSizeSmall);

                    table.Cell().Background(bg)
                        .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text($"{pressureLoss:F3}").FontSize(ReportStyles.FontSizeSmall);

                    table.Cell().Background(bg)
                        .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text($"{differentialPressure:F3}").FontSize(ReportStyles.FontSizeSmall);

                    alternate = !alternate;
                }
            });

            page.Footer().AlignRight().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(ReportStyles.FontSizeFooter));
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });
    }
}
