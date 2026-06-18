using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace IntersectUtilities.GraphWriteV2.Theming.UI
{
    /// <summary>
    /// Renders a single themed label to a PNG by piping a one-node DOT graph through <c>dot</c>
    /// (the same Graphviz binary GRAPHWRITEV2 already depends on — it must be on PATH). DOT is fed
    /// via stdin as UTF-8 so Danish characters (Rør, Bøjning) survive, and the PNG is read back from
    /// stdout — no temp files, no file locks. Returns a frozen, cross-thread-safe BitmapImage, or
    /// null with a message on failure (so the caller can show "Graphviz not found" instead of crashing).
    /// </summary>
    internal static class GraphvizPreviewRenderer
    {
        private static readonly UTF8Encoding Utf8NoBom = new(false);

        public static BitmapImage? Render(string labelMarkup, out string? error)
        {
            error = null;
            string dot =
                "digraph{ bgcolor=\"transparent\" node[shape=plaintext margin=0] " +
                $"n[label=<{labelMarkup}>] }}";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "dot",
                    Arguments = "-Tpng",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardInputEncoding = Utf8NoBom,
                    StandardErrorEncoding = Utf8NoBom,
                };

                using var p = Process.Start(psi);
                if (p is null) { error = "Could not start 'dot'."; return null; }

                using (var stdin = p.StandardInput)
                {
                    stdin.Write(dot);
                }

                using var ms = new MemoryStream();
                p.StandardOutput.BaseStream.CopyTo(ms);
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (ms.Length == 0)
                {
                    error = string.IsNullOrWhiteSpace(stderr) ? "Graphviz produced no output." : stderr.Trim();
                    return null;
                }

                ms.Position = 0;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch (Exception ex)
            {
                error = $"Graphviz 'dot' not found on PATH or failed: {ex.Message}";
                return null;
            }
        }
    }
}
