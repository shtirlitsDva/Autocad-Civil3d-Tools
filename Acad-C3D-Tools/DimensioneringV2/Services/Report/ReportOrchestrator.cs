using DimensioneringV2.Models;
using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report.Styles;

using Microsoft.Win32;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace DimensioneringV2.Services.Report;

/// <summary>
/// Main entry point for report generation.
/// Dispatches modules based on their NetworkAffinity:
/// ProjectLevel → once, PerNetwork → once per graph, Both → totals then per-graph.
/// </summary>
internal static class ReportOrchestrator
{
    internal static void Generate(HydraulicNetwork hn, ReportProfile profile)
    {
        // Set da-DK locale for all numeric formatting in report generation
        var previousCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = ReportStyles.DaDk;
        try
        {
            var context = ReportDataExtractor.Extract(hn, profile);

            var enabledModules = profile.Modules
                .Where(m => m.IsEnabled)
                .OrderBy(m => m.SortOrder)
                .Select(m => ReportModuleRegistry.Get(m.ModuleId))
                .Where(m => m != null && m.IsImplemented)
                .ToList();

            if (enabledModules.Count == 0)
            {
                MessageBox.Show(
                    "Ingen aktive rapportmoduler. Aktiver mindst ét modul i rapportindstillingerne.",
                    "Rapport",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            bool isMultiNetwork = context.OrderedGraphs.Count > 1;

            var document = Document.Create(container =>
            {
                int sectionCounter = 0;

                foreach (var module in enabledModules)
                {
                    if (module!.HasSectionNumber)
                        context.CurrentSection = ++sectionCounter;

                    context.SubSectionCounter = 0;

                    switch (module.Affinity)
                    {
                        case NetworkAffinity.ProjectLevel:
                            SetTotalScope(context);
                            module.Compose(container, context);
                            break;

                        case NetworkAffinity.PerNetwork:
                            if (isMultiNetwork)
                            {
                                for (int i = 0; i < context.OrderedGraphs.Count; i++)
                                {
                                    SetNetworkScope(context, i);
                                    module.Compose(container, context);
                                }
                            }
                            else
                            {
                                SetNetworkScope(context, 0);
                                module.Compose(container, context);
                            }
                            break;

                        case NetworkAffinity.Both:
                            if (isMultiNetwork)
                            {
                                // Total pass first
                                SetTotalScope(context);
                                module.Compose(container, context);

                                // Then per-network passes
                                for (int i = 0; i < context.OrderedGraphs.Count; i++)
                                {
                                    SetNetworkScope(context, i);
                                    module.Compose(container, context);
                                }
                            }
                            else
                            {
                                // Single network: one call with total scope
                                SetTotalScope(context);
                                module.Compose(container, context);
                            }
                            break;
                    }
                }
            });

            string defaultName = SanitizeFileName($"{hn.Id ?? "rapport"}_rapport.pdf");
            var dialog = new SaveFileDialog
            {
                Filter = "PDF-filer (*.pdf)|*.pdf",
                FileName = defaultName,
                Title = "Gem rapport"
            };

            if (dialog.ShowDialog() == true)
            {
                document.GeneratePdf(dialog.FileName);

                Process.Start(new ProcessStartInfo
                {
                    FileName = dialog.FileName,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Fejl ved generering af rapport:\n{ex.Message}",
                "Rapport fejl",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previousCulture;
        }
    }

    private static void SetTotalScope(ReportDataContext context)
    {
        var data = context.TotalData;
        context.Summary = data.Summary;
        context.ComplianceChecks = data.ComplianceChecks;
        context.SupplyPoints = data.SupplyPoints;
        context.Scope = NetworkScope.Total(
            context.OrderedGraphs,
            context.OrderedGraphs.Count == 1);
    }

    private static void SetNetworkScope(ReportDataContext context, int index)
    {
        var data = context.PerNetworkData[index];
        context.Summary = data.Summary;
        context.ComplianceChecks = data.ComplianceChecks;
        context.SupplyPoints = data.SupplyPoints;
        context.Scope = NetworkScope.ForGraph(
            context.OrderedGraphs[index],
            index + 1,
            context.OrderedGraphs.Count == 1);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
