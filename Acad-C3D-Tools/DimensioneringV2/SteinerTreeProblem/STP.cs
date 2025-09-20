using Autodesk.AutoCAD.ApplicationServices;

using DimensioneringV2.GraphFeatures;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DimensioneringV2.SteinerTreeProblem
{
    internal class STP
    {
        private Dictionary<int, STP_Node> _nodes = new Dictionary<int, STP_Node>();
        private Dictionary<int, STP_Node> _terminals = new Dictionary<int, STP_Node>();
        private Dictionary<int, int[]> _coordinates = new Dictionary<int, int[]>();
        private HashSet<STP_Edge> _edges = new HashSet<STP_Edge>();
        internal void AddNode(int name)
        {
            if (!_nodes.ContainsKey(name))
            {
                _nodes.Add(name, new STP_Node(name));
            }
        }
        internal void AddNode(NodeJunction node)
        {
            AddNode(node.STP_Node);
            _coordinates.Add(node.STP_Node, [(int)node.Location.X, (int)node.Location.Y]);
        }
        internal void AddTerminal(int name)
        {
            if (!_terminals.ContainsKey(name))
            {
                _terminals.Add(name, new STP_Node(name));
            }
        }
        internal void AddEdge(int source, int target, int weight)
        {
            if (_nodes.ContainsKey(source) && _nodes.ContainsKey(target))
            {
                _edges.Add(new STP_Edge(_nodes[source], _nodes[target], weight));
            }
            else
            {
                throw new ArgumentException("Source or target node does not exist!");
            }
        }
        internal HashSet<STP_Edge> Edges => _edges;
        internal bool HasNode(int name)
        {
            return _nodes.ContainsKey(name);
        }
        private static readonly string _solverPath = @"X:\AutoCAD DRI - 01 Civil 3D\STPSolver\PMsolver.exe";
        private static readonly string _solverInputFileName = "stp_input.stp";
        private static readonly string _solverOutputFileName = "stp_output.stp";
        internal string RunSTPSolver()
        {
            if (!File.Exists(_solverPath))
                throw new FileNotFoundException("STP Solver not found!");

            var fullPath = Path.GetDirectoryName(
                Application.DocumentManager.MdiActiveDocument.Database.Filename);

            var solverInput = Path.Combine(fullPath, _solverInputFileName);
            var solverOutput = Path.Combine(fullPath, _solverOutputFileName);

            using (StreamWriter writer = new StreamWriter(solverInput))
            {
                void wl(object obj)
                {
                    if (obj == null) return;
                    if (obj is string str)
                    {
                        writer.WriteLine(str);
                    }
                    else
                    {
                        writer.WriteLine(obj.ToString());
                    }
                }

                wl("33D32945 STP File, STP Format Version 1.0");
                wl("");
                wl("SECTION Comment");
                wl("Name    \"STP Input File\"");
                wl("Creator \"Norsyn\"");
                wl("Remark  \"Used to find Steiner Tree for pipe network.\"");
                wl("END");
                wl("");
                wl("SECTION Graph");
                wl("Nodes " + _nodes.Count);
                wl("Edges " + _edges.Count);
                foreach (var edge in _edges.OrderBy(x => x.Source.Name).ThenBy(x => x.Target.Name))
                {
                    wl($"E {edge.Source.Name} {edge.Target.Name} {edge.Weight}");
                }
                wl("END");
                wl("");
                wl("SECTION Terminals");
                wl("Terminals " + _terminals.Count);
                foreach (var terminal in _terminals.OrderBy(x => x.Value.Name))
                {
                    wl($"T {terminal.Value.Name}");
                }
                wl("END");
                wl("");
                wl("SECTION Coordinates");
                foreach (var coordinate in _coordinates.OrderBy(x => x.Key))
                {
                    wl("DD " + coordinate.Key + " " + coordinate.Value[0] + " " + coordinate.Value[1]);
                }
                wl("END");
                wl("");
                wl("EOF");
            }

            string args = $"\"{solverInput}\" -timelimit 1500 -logfilename \"{solverOutput}\"";
            //var process = System.Diagnostics.Process.Start(_solverPath, args);
            //process.WaitForExit();

            Thread.Sleep(10000);

            if (File.Exists(solverOutput))
            {
                return solverOutput;
            }
            else
            {
                throw new FileNotFoundException("STP Solver did not produce an output file!");
            }
        }
        internal static STP ParseOutput(string filePath)
        {
            STP stp = new STP();
            string[] lines = File.ReadAllLines(filePath);
            string currentSection = null;

            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

                // Detect section headers
                if (trimmedLine.StartsWith("SECTION"))
                {
                    currentSection = trimmedLine.Substring("SECTION".Length).Trim();
                    continue;
                }

                // Detect section endings
                if (trimmedLine.Equals("End", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = null;
                    continue;
                }

                // Parse based on current section
                switch (currentSection)
                {
                    case "Finalsolution":
                        ParseFinalSolution(trimmedLine, stp);
                        break;

                    default:
                        // Handle other sections if needed
                        break;
                }
            }

            return stp;
        }
        private static void ParseFinalSolution(string line, STP stp)
        {
            // Split the line into parts
            string[] parts = line.Split(' ');

            // Determine the line type
            if (parts[0] == "Vertices")
            {
                // Handle vertices count (not used directly, but could be validated)
                int verticesCount = int.Parse(parts[1]);
            }
            else if (parts[0] == "V")
            {
                // Add a node
                int nodeName = int.Parse(parts[1]);
                stp.AddNode(nodeName);
            }
            else if (parts[0] == "Edges")
            {
                // Handle edges count (not used directly, but could be validated)
                int edgesCount = int.Parse(parts[1]);
            }
            else if (parts[0] == "E")
            {
                // Add an edge
                int source = int.Parse(parts[1]);
                int target = int.Parse(parts[2]);
                int weight = 1; // Assuming weight is 1 as not provided in the example
                stp.AddEdge(source, target, weight);
            }
        }
    }
}
