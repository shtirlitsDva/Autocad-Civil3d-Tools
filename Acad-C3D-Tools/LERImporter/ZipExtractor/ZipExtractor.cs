using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static IntersectUtilities.UtilsCommon.Utils;

namespace LERImporter
{
    public class ZipExtractor
    {
        public static void ExtractZipFile(string zipFilePath, string outputDirectory)
        {
            using (FileStream zipStream = new FileStream(zipFilePath, FileMode.Open))
            {
                ExtractFromStream(zipStream, outputDirectory);
            }
        }
        private static void ExtractFromStream(Stream stream, string outputDirectory)
        {
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (Path.GetExtension(entry.FullName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract nested zip files to a memory stream and then process recursively
                        using (MemoryStream entryMemoryStream = new MemoryStream())
                        {
                            entry.Open().CopyTo(entryMemoryStream);
                            entryMemoryStream.Seek(0, SeekOrigin.Begin); // Reset memory stream position
                            ExtractFromStream(entryMemoryStream, outputDirectory);
                        }
                    }
                    else if (Path.GetFileName(entry.FullName).Equals("consolidated.gml", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract non-zip entries to the specified output directory
                        string destinationPath = Path.Combine(outputDirectory, entry.FullName);
                        if (!Directory.Exists(Path.GetDirectoryName(destinationPath)))
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                            
                        ExtractEntry(entry, destinationPath);
                    }
                }
            }
        }
        private static void ExtractEntry(ZipArchiveEntry entry, string destinationPath)
        {
            using (Stream entryStream = entry.Open())
            using (FileStream fileStream = new FileStream(destinationPath, FileMode.Create))
            {
                entryStream.CopyTo(fileStream);
            }
        }
        public static void UnzipFilesInDirectory(string directoryPath)
        {
            string[] zipFiles = Directory.GetFiles(directoryPath, "*.zip");
            foreach (var zipFilePath in zipFiles)
            {
                string outputDirectory = Path.Combine(directoryPath, Path.GetFileNameWithoutExtension(zipFilePath));
                Directory.CreateDirectory(outputDirectory);
                ExtractZipFile(zipFilePath, outputDirectory);
            }
        }
    }
}
