using CsvHelper.Configuration.Attributes;
using CsvHelper.Configuration;
using CsvHelper;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.ProjectsManager
{
    internal class ProjectsManager
    {
        private static readonly string path = "X:\\AutoCAD DRI - 01 Civil 3D\\Stier.csv";

        public HashSet<Project> Projects = new HashSet<Project>();

        public ProjectsManager() 
        {
            Projects = DeserializeProjects(path);
        }

        private static HashSet<Project> DeserializeProjects(string path)
        {
            if (!File.Exists(path))
                throw new System.Exception(
                    "X:\\AutoCAD DRI - 01 Civil 3D\\Stier.csv findes ikke!");

            using var reader = new StreamReader(path);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true,
                MissingFieldFound = null
            };

            using var csv = new CsvHelper.CsvReader(reader, config);
            var records = csv.GetRecords<CsvRow>().ToHashSet();

            return records
                .GroupBy(r => r.PrjId)
                .Select(g => new Project
                {
                    Name = g.Key,
                    Phases = g.Select(r => new Phase
                    {
                        Name = r.Etape,
                        Ler = r.Ler,
                        Surface = r.Surface,
                        Alignments = r.Alignments,
                        Fremtid = r.Fremtid,
                        WorkingFolder = r.WorkingFolder
                    }).ToHashSet()
                }).ToHashSet();
        }
    }


    public class Project
    {
        [Name("PrjId")]
        public string Name { get; set; }
        public HashSet<Phase> Phases { get; set; } = new HashSet<Phase>();
    }

    public class Phase
    {
        [Name("Etape")]
        public string Name { get; set; }

        public string Ler { get; set; }
        public string Surface { get; set; }
        public string Alignments { get; set; }
        public string Fremtid { get; set; }
        public string WorkingFolder { get; set; }
    }

    public class CsvRow
    {
        public string PrjId { get; set; }
        public string Etape { get; set; }
        public string Ler { get; set; }
        public string Surface { get; set; }
        public string Alignments { get; set; }
        public string Fremtid { get; set; }
        public string WorkingFolder { get; set; }
    }
}
