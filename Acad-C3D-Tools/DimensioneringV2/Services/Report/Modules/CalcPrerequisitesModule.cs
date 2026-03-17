using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report.DataModels;
using DimensioneringV2.Services.Report.Styles;

using NorsynHydraulicCalc;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Services.Report.Modules;

/// <summary>
/// Renders §5 Beregningsforudsætninger: frozen settings, nyttetimer,
/// and pipe configuration tables.
/// </summary>
internal class CalcPrerequisitesModule : IReportModule
{
    public ReportModuleId Id => ReportModuleId.CalcPrerequisites;
    public string DisplayName => "Beregningsforudsætninger";
    public bool IsImplemented => true;

    public void Compose(IDocumentContainer container, ReportDataContext context)
    {
        var s = context.CurrentSection;

        container.Page(page =>
        {
            page.Size(ReportStyles.PageSizeA4);
            page.MarginLeft(ReportStyles.MarginLeft, Unit.Point);
            page.MarginRight(ReportStyles.MarginRight, Unit.Point);
            page.MarginTop(ReportStyles.MarginTop, Unit.Point);
            page.MarginBottom(ReportStyles.MarginBottom, Unit.Point);

            page.Header().Text($"{s}  Beregningsforudsætninger")
                .FontSize(ReportStyles.FontSizeH1)
                .FontColor(ReportStyles.ColorPrimary).Bold();

            page.Content().PaddingTop(8).Column(col =>
            {
                col.Spacing(ReportStyles.SectionSpacing);

                // §s.1 Fælles input
                col.Item().Text($"{s}.1  Fælles input")
                    .FontSize(ReportStyles.FontSizeH2).SemiBold();
                col.Item().Element(c => ComposeSettingsTable(c, context));

                // §s.2 Nyttetimer
                col.Item().Text($"{s}.2  Nyttetimer")
                    .FontSize(ReportStyles.FontSizeH2).SemiBold();
                col.Item().Element(c => ComposeNyttetimerSection(c, context));

                // §s.3 Rørindstillinger — Fordelingsledninger
                col.Item().Text($"{s}.3  Rørindstillinger — Fordelingsledninger")
                    .FontSize(ReportStyles.FontSizeH2).SemiBold();
                col.Item().Element(c => ComposePipeConfigTable(c, context, isFL: true));

                // §s.4 Rørindstillinger — Stikledninger
                col.Item().Text($"{s}.4  Rørindstillinger — Stikledninger")
                    .FontSize(ReportStyles.FontSizeH2).SemiBold();
                col.Item().Element(c => ComposePipeConfigTable(c, context, isFL: false));
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

    private static void ComposeSettingsTable(IContainer container, ReportDataContext ctx)
    {
        var s = ctx.Settings;

        var rows = new List<(string Label, string Value, string Unit)>
        {
            ("Medietype", s.MedieType.ToString(), ""),
            ("Fremløbstemperatur", $"{s.TempFrem:F0}", "°C"),
            ("Afkøling varme ΔT", $"{s.AfkølingVarme:F0}", "K"),
            ("Afkøling brugsvand ΔT", $"{s.AfkølingBrugsvand:F0}", "K"),
            ("Faktor varmtvandstillæg", $"{s.FactorVarmtVandsTillæg:F2}", ""),
            ("Brugsvandsprioritering", s.UseBrugsvandsprioritering ? "Ja" : "Nej", ""),
            ("Faktor opvarmning u. brugsvandsprioritering",
                $"{s.FactorTillægForOpvarmningUdenBrugsvandsprioritering:F2}", ""),
            ("Ruhed Stål", $"{s.RuhedSteel:F3}", "mm"),
            ("Ruhed PertFlextra", $"{s.RuhedPertFlextra:F3}", "mm"),
            ("Ruhed AluPEX", $"{s.RuhedAluPEX:F3}", "mm"),
            ("Ruhed Cu", $"{s.RuhedCu:F3}", "mm"),
            ("Ruhed Pe", $"{s.RuhedPe:F3}", "mm"),
            ("Ruhed AquaTherm11", $"{s.RuhedAquaTherm11:F3}", "mm"),
            ("Procenttillæg til tryktab", $"{s.ProcentTillægTilTryktab}", "%"),
            ("Tillæg til holdetryk MVS", $"{s.TillægTilHoldetrykMVS:F1}", "mVS"),
            ("Min. differenstryk over hovedhaner",
                $"{s.MinDifferentialPressureOverHovedHaner:F2}", "bar"),
            ("Maks. tryktab stikledning", $"{s.MaxPressureLossStikSL:F2}", "bar"),
            ("Steiner tree enumeration tid", $"{s.TimeToSteinerTreeEnumeration}", "s"),
            ("Beregningstype", s.CalculationType.ToString(), ""),
            ("Report to console", s.ReportToConsole ? "Ja" : "Nej", ""),
            ("Cache resultater", s.CacheResults ? "Ja" : "Nej", ""),
            ("Cache præcision", $"{s.CachePrecision}", "decimaler"),
        };

        RenderKeyValueTable(container, rows);
    }

    private static void ComposeNyttetimerSection(IContainer container, ReportDataContext ctx)
    {
        var s = ctx.Settings;

        container.Column(col =>
        {
            col.Spacing(6);

            // Global nyttetimer values
            var globalRows = new List<(string Label, string Value, string Unit)>
            {
                ("Systemnyttetimer ved 1 forbruger",
                    $"{s.SystemnyttetimerVed1Forbruger}", "h/år"),
                ("Systemnyttetimer ved 50+ forbrugere",
                    $"{s.SystemnyttetimerVed50PlusForbrugere}", "h/år"),
                ("Bygningsnyttetimer (standard)",
                    $"{s.BygningsnyttetimerDefault}", "h/år"),
            };

            col.Item().Element(c => RenderKeyValueTable(c, globalRows));

            // Per-building-code table from frozen config
            var frozenConfig = ctx.Network.FrozenNyttetimerConfig;
            if (frozenConfig != null && frozenConfig.Entries.Count > 0)
            {
                col.Item().PaddingTop(6).Text($"Nyttetimerkonfiguration: {frozenConfig.Name}")
                    .FontSize(ReportStyles.FontSizeH3).SemiBold();

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(ReportStyles.ColorHeaderBg)
                            .Padding(ReportStyles.TableCellPadding)
                            .Text("Kode").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                        header.Cell().Background(ReportStyles.ColorHeaderBg)
                            .Padding(ReportStyles.TableCellPadding)
                            .AlignRight()
                            .Text("Nyttetimer [h/år]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                    });

                    foreach (var entry in frozenConfig.Entries)
                    {
                        table.Cell()
                            .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                            .Padding(ReportStyles.TableCellPadding)
                            .Text(entry.AnvendelsesKode).FontSize(ReportStyles.FontSizeSmall);

                        table.Cell()
                            .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                            .Padding(ReportStyles.TableCellPadding)
                            .AlignRight()
                            .Text($"{entry.Nyttetimer}").FontSize(ReportStyles.FontSizeSmall);
                    }
                });
            }
        });
    }

    private static void ComposePipeConfigTable(
        IContainer container, ReportDataContext ctx, bool isFL)
    {
        var s = ctx.Settings;
        var pipeTypes = s.GetPipeTypes();
        var medium = s.MedieType;

        PipeTypeConfiguration? config = null;
        if (isFL)
        {
            s.AllPipeConfigsFL.TryGetValue(medium, out config);
        }
        else
        {
            s.AllPipeConfigsSL.TryGetValue(medium, out config);
        }

        if (config == null || config.Priorities.Count == 0)
        {
            container.Text("Ingen rørindstillinger konfigureret.")
                .FontSize(ReportStyles.FontSizeBody).Italic();
            return;
        }

        container.Column(col =>
        {
            col.Spacing(6);

            foreach (var priority in config.Priorities.OrderBy(p => p.Priority))
            {
                col.Item().Text($"Prioritet {priority.Priority}: {pipeTypes.GetPipeType(priority.PipeType)}  " +
                    $"(DN{priority.MinDn}–DN{priority.MaxDn})")
                    .FontSize(ReportStyles.FontSizeH3).SemiBold();

                var enabledCriteria = priority.AcceptCriteria
                    .Where(c => c.NominalDiameter >= priority.MinDn
                             && c.NominalDiameter <= priority.MaxDn)
                    .OrderBy(c => c.NominalDiameter)
                    .ToList();

                if (enabledCriteria.Count == 0)
                {
                    col.Item().Text("Ingen DN-dimensioner valgt.")
                        .FontSize(ReportStyles.FontSizeSmall).Italic();
                    continue;
                }

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(ReportStyles.ColorHeaderBg)
                            .Padding(ReportStyles.TableCellPadding)
                            .Text("DN").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                        header.Cell().Background(ReportStyles.ColorHeaderBg)
                            .Padding(ReportStyles.TableCellPadding)
                            .AlignRight()
                            .Text("Maks. hastighed [m/s]")
                            .FontSize(ReportStyles.FontSizeSmall).SemiBold();
                        header.Cell().Background(ReportStyles.ColorHeaderBg)
                            .Padding(ReportStyles.TableCellPadding)
                            .AlignRight()
                            .Text("Maks. trykgradient [Pa/m]")
                            .FontSize(ReportStyles.FontSizeSmall).SemiBold();
                    });

                    foreach (var c in enabledCriteria)
                    {
                        table.Cell()
                            .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                            .Padding(ReportStyles.TableCellPadding)
                            .Text($"DN{c.NominalDiameter}").FontSize(ReportStyles.FontSizeSmall);

                        table.Cell()
                            .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                            .Padding(ReportStyles.TableCellPadding)
                            .AlignRight()
                            .Text($"{c.MaxVelocity:F2}").FontSize(ReportStyles.FontSizeSmall);

                        table.Cell()
                            .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                            .Padding(ReportStyles.TableCellPadding)
                            .AlignRight()
                            .Text($"{c.MaxPressureGradient}").FontSize(ReportStyles.FontSizeSmall);
                    }
                });
            }
        });
    }

    private static void RenderKeyValueTable(
        IContainer container, List<(string Label, string Value, string Unit)> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(4);
                columns.RelativeColumn(2);
                columns.ConstantColumn(50);
            });

            foreach (var (label, value, unit) in rows)
            {
                table.Cell()
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text(label).FontSize(ReportStyles.FontSizeBody);

                table.Cell()
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text(value).FontSize(ReportStyles.FontSizeBody);

                table.Cell()
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .PaddingLeft(6).PaddingVertical(ReportStyles.TableCellPadding)
                    .Text(unit).FontSize(ReportStyles.FontSizeBody);
            }
        });
    }
}
