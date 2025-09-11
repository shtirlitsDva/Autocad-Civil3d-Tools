using System;
using System.Collections.Generic;
using System.Linq;

using Mapsui;

using QuikGraph;
using QuikGraph.Algorithms;

using DimensioneringV2.GraphFeatures;

namespace DimensioneringV2.Services
{
	/// <summary>
	/// Encapsulates graph membership lookup and shortest path computation between map features.
	/// Exposes an IFeature-based API; internally maps to graphs and edges.
	/// </summary>
	internal class PathFindingService
	{
		private readonly IEnumerable<UndirectedGraph<NodeJunction, EdgePipeSegment>> _graphs;
		private readonly Dictionary<IFeature, UndirectedGraph<NodeJunction, EdgePipeSegment>> _featureToGraph = new();
		private readonly Dictionary<IFeature, EdgePipeSegment> _featureToEdge = new();
		private readonly Dictionary<EdgePipeSegment, (NodeJunction a, NodeJunction b)> _edgeEndpoints = new();

		public PathFindingService(IEnumerable<UndirectedGraph<NodeJunction, EdgePipeSegment>> graphs)
		{
			_graphs = graphs ?? throw new ArgumentNullException(nameof(graphs));
			BuildCaches();
		}

		private void BuildCaches()
		{
			_featureToGraph.Clear();
			_featureToEdge.Clear();
			_edgeEndpoints.Clear();

			foreach (var graph in _graphs)
			{
				foreach (var edge in graph.Edges)
				{
					var feature = (IFeature)edge.PipeSegment;
					_featureToGraph[feature] = graph;
					_featureToEdge[feature] = edge;
					_edgeEndpoints[edge] = (edge.Source, edge.Target);
				}
			}
		}

		public bool AreInSameGraph(IFeature a, IFeature b)
		{
			return _featureToGraph.TryGetValue(a, out var ga)
				&& _featureToGraph.TryGetValue(b, out var gb)
				&& ReferenceEquals(ga, gb);
		}

		public bool TryComputePath(IFeature from, IFeature to, out IReadOnlyList<IFeature> path)
		{
			path = Array.Empty<IFeature>();
			if (!_featureToGraph.TryGetValue(from, out var graph)) return false;
			if (!_featureToGraph.TryGetValue(to, out var graphTo) || !ReferenceEquals(graph, graphTo)) return false;

			var fromEdge = _featureToEdge[from];
			var toEdge = _featureToEdge[to];

			// Precompute shortest paths from both endpoints of the start edge.
			var weight = new Func<EdgePipeSegment, double>(e => e.PipeSegment.Length);
			var tryFromA = graph.ShortestPathsDijkstra(weight, _edgeEndpoints[fromEdge].a);
			var tryFromB = graph.ShortestPathsDijkstra(weight, _edgeEndpoints[fromEdge].b);

			// Choose best of four combinations between endpoints
			var endpointsTo = _edgeEndpoints[toEdge];
			var candidates = new List<IEnumerable<EdgePipeSegment>>();
			if (tryFromA(endpointsTo.a, out var p1)) candidates.Add(p1);
			if (tryFromA(endpointsTo.b, out var p2)) candidates.Add(p2);
			if (tryFromB(endpointsTo.a, out var p3)) candidates.Add(p3);
			if (tryFromB(endpointsTo.b, out var p4)) candidates.Add(p4);

			if (candidates.Count == 0) return false;

			// Select shortest by accumulated length
			IEnumerable<EdgePipeSegment> best = candidates
				.OrderBy(seq => seq.Sum(e => e.PipeSegment.Length))
				.First();

			path = best.Select(e => (IFeature)e.PipeSegment).ToList();
			return path.Count > 0;
		}

		public bool TryGetGraph(IFeature feature, out UndirectedGraph<NodeJunction, EdgePipeSegment> graph)
			=> _featureToGraph.TryGetValue(feature, out graph!);
	}
}