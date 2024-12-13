using DimensioneringV2.BruteForceOptimization;

using GeneticSharp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Genetic
{
    internal class GraphChromosome : BinaryChromosomeBase
    {
        private readonly List<BFEdge> _edges;

        public GraphChromosome(int length, List<BFEdge> edges) : base(length)
        {
            _edges = edges;
            CreateGenes();
        }

        public override IChromosome CreateNew()
        {
            return new GraphChromosome(Length, _edges);
        }

        public List<BFEdge> GetSelectedEdges()
        {
            var selectedEdges = new List<BFEdge>();
            for (int i = 0; i < Length; i++)
            {
                if (GetGene(i).Value is bool geneValue && geneValue)
                {
                    selectedEdges.Add(_edges[i]);
                }
            }
            return selectedEdges;
        }

        public List<BFEdge> GetNonSelectedEdges()
        {
            var nonSelectedEdges = new List<BFEdge>();
            for (int i = 0; i < Length; i++)
            {
                if (GetGene(i).Value is bool geneValue && !geneValue)
                {
                    nonSelectedEdges.Add(_edges[i]);
                }
            }
            return nonSelectedEdges;
        }
    }
}
