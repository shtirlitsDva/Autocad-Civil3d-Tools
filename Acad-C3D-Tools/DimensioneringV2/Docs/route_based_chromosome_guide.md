# Route-Based Chromosome Integration Guide

This document describes how to integrate the **CoherencyManager** and **RouteChromosome** solution into the existing C# / .NET 8 project that uses **QuikGraph** and **GeneticSharp**.

The goal of this solution is:

- To represent each GA individual as a **set of selected routes** (one per terminal) in a subgraph.
- To guarantee **connectivity** from the subgraph’s source to all terminals by construction (assuming route enumeration is correct).
- To centralize route management and indexing in a **CoherencyManager**.

You are an AI agent that must integrate this into an existing project that already has:
- Domain graph structures.
- Decomposed subgraphs (each `UndirectedGraph<TNode, TEdge>` is already provided).
- A known source node and set of terminals per subgraph.

Only the route-based chromosome and the coherency layer are required.

---

## 1. Overview of the Approach

For each **subgraph** (an `UndirectedGraph<TNode, TEdge>`):

1. We precompute a set of **simple paths** (no edge reuse, no vertex cycles) from the **source node** to each **terminal**.
2. For each terminal:
   - We store a list of these routes.
   - Each route has a stable **route index** (0..N-1).
3. A GA chromosome for that subgraph:
   - Has one **gene per terminal**.
   - Gene value = an `int` that is the **index of the chosen route** for that terminal.
4. Decoding a chromosome:
   - For each terminal, retrieve the selected route.
   - Take the union of all edges in all selected routes.
   - This union defines the candidate network for that subgraph.

This representation makes every chromosome **feasible** (provided enumeration covered valid routes) and simplifies mutation / crossover: they only operate on integer indices per terminal.

---

## 2. Route Model

### Class: `Route<TNode, TEdge>`

Represents a single route from the source to one terminal.

```csharp
public sealed class Route<TNode, TEdge>
    where TEdge : IEdge<TNode>
{
    public int RouteIndex { get; }
    public TNode Terminal { get; }
    public IReadOnlyList<TEdge> Edges { get; }

    public Route(int routeIndex, TNode terminal, IReadOnlyList<TEdge> edges)
    {
        RouteIndex = routeIndex;
        Terminal = terminal;
        Edges = edges;
    }
}
```

**Responsibilities:**

- Holds:
  - `RouteIndex` – index within that terminal’s route list.
  - `Terminal` – the terminal node this route ends at.
  - `Edges` – ordered list of edges from source to terminal.

**Assumptions:**

- `Edges` form a **simple path**:
  - No repeated nodes.
  - No repeated edges.
- Direction is implied by sequence; `TEdge` is assumed undirected (QuikGraph’s `IEdge<TNode>`).

---

## 3. CoherencyManager

### Class: `CoherencyManager<TNode, TEdge>`

Central registry of all routes for a **single subgraph**.

```csharp
using QuikGraph;
using System.Collections.Generic;

public sealed class CoherencyManager<TNode, TEdge>
    where TEdge : IEdge<TNode>
{
    private readonly Dictionary<TNode, List<Route<TNode, TEdge>>> _routesByTerminal = new();
    private readonly UndirectedGraph<TNode, TEdge> _graph;
    private readonly TNode _source;

    public CoherencyManager(
        UndirectedGraph<TNode, TEdge> subgraph,
        TNode source,
        IEnumerable<TNode> terminals)
    {
        _graph = subgraph;
        _source = source;
        Initialize(terminals);
    }

    private void Initialize(IEnumerable<TNode> terminals)
    {
        foreach (var terminal in terminals)
        {
            var allRoutes = EnumerateSimpleRoutes(_source, terminal);
            var routeList = new List<Route<TNode, TEdge>>();

            int idx = 0;
            foreach (var route in allRoutes)
            {
                routeList.Add(new Route<TNode, TEdge>(idx++, terminal, route));
            }

            _routesByTerminal[terminal] = routeList;
        }
    }

    public IReadOnlyList<Route<TNode, TEdge>> GetRoutes(TNode terminal)
        => _routesByTerminal[terminal];

    public Route<TNode, TEdge> GetRoute(TNode terminal, int routeIndex)
        => _routesByTerminal[terminal][routeIndex];

    private IEnumerable<IReadOnlyList<TEdge>> EnumerateSimpleRoutes(TNode start, TNode target)
    {
        var result = new List<IReadOnlyList<TEdge>>();
        var visitedNodes = new HashSet<TNode>();
        var visitedEdges = new HashSet<TEdge>();
        var stack = new List<TEdge>();

        void DFS(TNode u)
        {
            if (u!.Equals(target))
            {
                result.Add(new List<TEdge>(stack));
                return;
            }

            visitedNodes.Add(u);

            foreach (var edge in _graph.AdjacentEdges(u))
            {
                if (visitedEdges.Contains(edge))
                    continue;

                var v = edge.GetOtherVertex(u);
                if (visitedNodes.Contains(v))
                    continue;

                visitedEdges.Add(edge);
                stack.Add(edge);

                DFS(v);

                stack.RemoveAt(stack.Count - 1);
                visitedEdges.Remove(edge);
            }

            visitedNodes.Remove(u);
        }

        DFS(start);
        return result;
    }
}

public static class EdgeExtensions
{
    public static TNode GetOtherVertex<TNode, TEdge>(this TEdge edge, TNode v)
        where TEdge : IEdge<TNode>
    {
        return edge.Source.Equals(v) ? edge.Target : edge.Source;
    }
}
```

### Responsibilities

- Accept a **subgraph**, its **source node**, and its **terminals**.
- For each terminal:
  - Enumerate all simple paths from `source` to that terminal (`EnumerateSimpleRoutes`).
  - Wrap each path as a `Route<TNode, TEdge>` with a stable `RouteIndex`.
- Provide access:
  - `GetRoutes(terminal)` → all routes for that terminal.
  - `GetRoute(terminal, routeIndex)` → specific route.

### Important Constraints

- Path enumeration is **DFS-based**, with:
  - `visitedNodes` to prevent node reuse.
  - `visitedEdges` to prevent edge reuse.
- No path is allowed to reuse any edge.
- This can explode in graphs with many cycles; later, you may:
  - Add **depth limits**,
  - Apply **cost filters**,
  - Or implement **K-shortest paths** instead of brute-force DFS.

The agent must ensure that route enumeration is **bounded** in practice, based on the project’s performance requirements.

---

## 4. Chromosome: RouteChromosome

### Class: `RouteChromosome<TNode, TEdge>`

Represents an individual solution for **one subgraph**:

- Chromosome length = number of terminals in the subgraph.
- Each gene = `int` indicating selected route index for that terminal.

```csharp
using GeneticSharp.Domain.Chromosomes;
using GeneticSharp.Domain.Randomizations;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class RouteChromosome<TNode, TEdge> : ChromosomeBase
    where TEdge : IEdge<TNode>
{
    private readonly CoherencyManager<TNode, TEdge> _manager;
    private readonly TNode[] _terminals;

    public RouteChromosome(CoherencyManager<TNode, TEdge> manager)
        : base(managerTerminalsCount(manager))
    {
        _manager = manager;
        _terminals = manager.Terminals().ToArray();
        CreateGenes();
    }

    private static int managerTerminalsCount<TN, TE>(CoherencyManager<TN, TE> m)
        where TE : IEdge<TN>
    {
        return m.Terminals().Count();
    }

    public override Gene GenerateGene(int geneIndex)
    {
        var terminal = _terminals[geneIndex];
        var routes = _manager.GetRoutes(terminal);

        int max = routes.Count;
        if (max == 0)
            throw new InvalidOperationException($"Terminal {terminal} has no routes.");

        int idx = RandomizationProvider.Current.GetInt(0, max);
        return new Gene(idx);
    }

    public override IChromosome CreateNew()
    {
        return new RouteChromosome<TNode, TEdge>(_manager);
    }

    public Route<TNode, TEdge> GetSelectedRoute(int geneIndex)
    {
        var t = _terminals[geneIndex];
        int ri = (int)GetGene(geneIndex).Value!;
        return _manager.GetRoute(t, ri);
    }

    public IEnumerable<TEdge> DecodeActiveEdges()
    {
        var set = new HashSet<TEdge>();

        for (int i = 0; i < Length; i++)
        {
            var route = GetSelectedRoute(i);
            foreach (var e in route.Edges)
                set.Add(e);
        }

        return set;
    }
}
```

#### Extension: `CoherencyManagerExtensions`

Used to expose the terminal list without changing the internal structure.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

public static class CoherencyManagerExtensions
{
    public static IEnumerable<TNode> Terminals<TNode, TEdge>(this CoherencyManager<TNode, TEdge> manager)
        where TEdge : IEdge<TNode>
    {
        var field = typeof(CoherencyManager<TNode, TEdge>)
            .GetField("_routesByTerminal",
                      System.Reflection.BindingFlags.NonPublic |
                      System.Reflection.BindingFlags.Instance);

        if (field?.GetValue(manager) is Dictionary<TNode, List<Route<TNode, TEdge>>> dict)
            return dict.Keys;

        return Enumerable.Empty<TNode>();
    }
}
```

### Responsibilities

- Maintain a **fixed terminal order** for gene indices.
- Randomly initialize each gene:
  - Select a route index uniformly from `[0, routesCount-1]` for that terminal.
- Provide helpers to:
  - Get the selected `Route` for a given gene index.
  - Decode the chromosome into a **set of active edges** (`DecodeActiveEdges`).

### Assumptions

- The `CoherencyManager` is fully initialized and:
  - Each terminal has at least one route.
- `DecodeActiveEdges` is used by fitness evaluation to:
  - Compute total cost.
  - Check connectivity (if needed as a safety check).
  - Apply any engineering constraints and penalties.
- GeneticSharp expects fitness to be **maximized**. If the project minimizes cost, the fitness function should typically return `-cost` (and penalties added to cost before negation).

---

## 5. Integration Guide

You must integrate as follows for each **subgraph GA instance**:

1. **Inputs required from the existing system**:
   - `UndirectedGraph<TNode, TEdge> subgraph` – already decomposed.
   - `TNode source` – entry / supply node for this subgraph.
   - `IEnumerable<TNode> terminals` – terminals within this subgraph.

2. **Instantiate the CoherencyManager**:

   ```csharp
   var manager = new CoherencyManager<TNode, TEdge>(
       subgraph,
       source,
       terminals);
   ```

3. **Create a RouteChromosome**:

   ```csharp
   var chromosome = new RouteChromosome<TNode, TEdge>(manager);
   ```

4. **Use this chromosome as the prototype in GeneticSharp**:
   - `RouteChromosome` becomes the `adamChromosome` when configuring the `Population`.
   - `CreateNew()` is invoked by GeneticSharp when generating new individuals.

5. **Fitness evaluation** (logic to be added in the project):
   - Call `DecodeActiveEdges()` on the chromosome.
   - Use the returned `IEnumerable<TEdge>` to:
     - Compute total cost of the candidate network.
     - Optionally verify connectivity and other constraints.
   - Return a fitness value that GeneticSharp maximizes (e.g. negative total cost with penalties).

---

## 6. Invariants and Requirements

You must preserve the following invariants:

1. `EnumerateSimpleRoutes` must produce **simple paths only**:
   - No edge reuse.
   - No node reuse.
2. Each terminal must have at least **one route**:
   - If a terminal has zero routes → either the graph is disconnected or path search must be adapted.
3. `RouteIndex` must be **stable** for the lifetime of the GA run:
   - Once the `CoherencyManager` is constructed, do not modify the route lists.
4. Chromosome length must always equal the number of terminals in that subgraph.

---

## 7. Future Extensions (Optional)

Do not implement unless requested, but be aware of possible further enhancements:

- Add **route enumeration limits**:
  - Depth bound, cost threshold, or max number of routes per terminal.
- Replace naive DFS route enumeration with **K-shortest simple path** generation for better control.
- Introduce custom **mutation** operators:
  - For example, bias mutations toward routes similar to the current ones or toward routes that share edges with other terminals.
- Add **caching** or memoization for route enumeration if multiple GAs share subgraphs.

---

## 8. Summary

The provided implementation:

- Defines a **CoherencyManager** that:
  - Enumerates simple routes in a subgraph.
  - Registers them per terminal with route indices.
- Defines a **RouteChromosome** that:
  - Uses one integer gene per terminal to select a route.
  - Decodes into a set of active edges representing a candidate network.

The AI agent must:

- Integrate these classes into the existing project.
- Use them as the GA chromosome representation for subgraphs.
- Wire the chromosome into the existing GA setup and fitness logic.
