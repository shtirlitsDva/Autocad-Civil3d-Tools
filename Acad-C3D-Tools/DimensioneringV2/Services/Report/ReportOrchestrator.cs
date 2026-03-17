using DimensioneringV2.Models;
using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report.Styles;

using Microsoft.Win32;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace DimensioneringV2.Services.Report;

/// <summary>
/// Main entry point for report generation.
/// Extracts data, builds QuestPDF document from enabled modules, and generates PDF.
/// </summary>
internal static class ReportOrchestrator
{
    internal static void Generate(HydraulicNetwork hn, ReportProfile profile)
    {
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

            var document = Document.Create(container =>
            {
                int sectionCounter = 0;
                foreach (var module in enabledModules)
                {
                    if (module!.HasSectionNumber)
                        context.CurrentSection = ++sectionCounter;

                    module.Compose(container, context);
                }
            });

            // Show save dialog
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

                // Open the generated PDF
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
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
