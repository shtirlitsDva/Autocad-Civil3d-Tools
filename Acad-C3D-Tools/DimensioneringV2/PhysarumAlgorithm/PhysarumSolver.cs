using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra.Double;

namespace DimensioneringV2.PhysarumAlgorithm
{
    internal class PhysarumSolver
    {
        private readonly UndirectedGraph<PhyNode, PhyEdge> _graph;
        private readonly double _mu;
        private readonly double _timeStep;
        private readonly double _tolerance;
        private Action<int>? _callback;

        public PhysarumSolver(
            UndirectedGraph<PhyNode, PhyEdge> graph,
            Action<int>? callback = null,
            double mu = 1.0,
            double timeStep = 0.1,
            double tolerance = 1e-3)
        {
            _graph = graph;
            _mu = mu;
            _timeStep = timeStep;
            _tolerance = tolerance;
        }

        public void Run(int maxIterations = 100)
        {
            var nodeList = _graph.Vertices.ToList();
            var nodeIndex = nodeList.Select((n, i) => new { n, i }).ToDictionary(x => x.n, x => x.i);
            int n = nodeList.Count;

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                //if (iteration % 10 == 0)
                _callback?.Invoke(iteration);                

                // Step 1: Solve linear system for pressures
                var A = Matrix.Build.Dense(n, n);
                var b = Vector.Build.Dense(n);

                foreach (var node in nodeList)
                {
                    int i = nodeIndex[node];

                    if (node.IsSource)
                    {
                        A[i, i] = 1.0;
                        b[i] = 0.0;
                        continue;
                    }

                    double diagonal = 0.0;
                    foreach (var edge in _graph.AdjacentEdges(node))
                    {
                        var jNode = edge.GetOther(node);
                        int j = nodeIndex[jNode];

                        double coeff = edge.Conductance / edge.Length;

                        A[i, j] -= coeff;
                        diagonal += coeff;
                    }

                    A[i, i] = diagonal;
                    b[i] = node.ExternalDemand;
                }

                var p = A.Solve(b);

                for (int i = 0; i < n; i++)
                    nodeList[i].Pressure = p[i];

                // Step 2: Calculate edge flows
                foreach (var edge in _graph.Edges)
                {
                    double dp = edge.Source.Pressure - edge.Target.Pressure;
                    edge.Flow = edge.Conductance * dp / edge.Length;
                }

                // Step 3: Update conductances
                double maxDelta = 0.0;

                foreach (var edge in _graph.Edges)
                {
                    double newD = edge.Conductance + _timeStep * (Math.Abs(edge.Flow) - _mu * edge.Conductance);
                    newD = Math.Max(newD, 0.0);
                    double delta = Math.Abs(edge.Conductance - newD);
                    edge.Conductance = newD;
                    if (delta > maxDelta)
                        maxDelta = delta;
                }

                if (maxDelta < _tolerance)
                {
                    Console.WriteLine($"Converged at iteration {iteration}");
                    break;
                }
            }
        }
    }
}