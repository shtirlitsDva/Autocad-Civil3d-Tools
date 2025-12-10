# Genetic Algorithms with Binary Chromosomes: Big Picture Overview and Implementation

## Genetic Algorithms Overview

Genetic Algorithms (GAs) are a class of
evolutionary optimization techniques inspired by natural selection[[1]](https://en.wikipedia.org/wiki/Genetic_algorithm#:~:text=In%20computer%20science%20%20and,164%20hyperparameter). They work by evolving a **population**
of candidate solutions (called **chromosomes**) over many generations to
maximize a **fitness** function (the measure of solution quality). The GA
process is iterative: in each generation, individuals are evaluated and the
fittest are more likely to be **selected** to reproduce. Through
biologically inspired operators like **crossover** (recombination of parent
solutions) and **mutation** (random variation), new offspring solutions are
created[[2]](https://en.wikipedia.org/wiki/Genetic_algorithm#:~:text=The%20evolution%20usually%20starts%20from,Commonly%2C%20the%20algorithm)[[3]](https://en.wikipedia.org/wiki/Genetic_algorithm#:~:text=The%20next%20step%20is%20to,and%20mutation). Over successive generations, the
population “evolves” toward better solutions. The algorithm stops when a
termination condition is met – for example, reaching a maximum number of
generations or attaining a satisfactory fitness level[[4]](https://en.wikipedia.org/wiki/Genetic_algorithm#:~:text=fit%20individuals%20are%20stochastically%20,been%20reached%20for%20the%20population). This approach is stochastic but often
finds high-quality solutions for difficult optimization problems where brute
force is impractical.

## Binary Chromosome Representation in GAs

A *chromosome* in GA represents one
solution, and it’s composed of smaller units called **genes**. One common
encoding is a **binary chromosome**, where each gene is a bit (0 or 1). In
fact, classic GAs traditionally represent solutions as fixed-length binary
strings[[5]](https://en.wikipedia.org/wiki/Genetic_algorithm#:~:text=In%20a%20genetic%20algorithm%2C%20a,5). This representation is popular because
it’s very general – virtually any problem’s variables can be encoded in binary
– and it simplifies genetic operations. For example, two bit-strings are easy
to align and swap segments during crossover, since all chromosomes have the
same length[[6]](https://en.wikipedia.org/wiki/Genetic_algorithm#:~:text=1,to%20evaluate%20the%20solution%20domain). Likewise, mutation on a binary chromosome
is naturally defined as flipping bits (changing a 0 to 1 or vice versa).

**GraphChromosome Example:** In your case, the custom GraphChromosome
class (shown above) is a concrete example of a binary chromosome. It extends
GeneticSharp’s BinaryChromosomeBase, meaning it inherits
the structure of a fixed-length sequence of genes each holding 0 or 1. Here
each gene indicates whether a specific graph edge is present or not (with the
convention 0 = edge ON/included, 1 = edge OFF/removed). This bit string encodes
a candidate solution (a subgraph configuration). The **length** of the
chromosome equals the number of potential removable edges in the graph (so
every possible non-mandatory edge corresponds to one gene). By using a binary
representation, standard GA operators can be applied – e.g. combining two graph
solutions by swapping bits, or mutating a solution by toggling an edge state.
The GraphChromosome adds domain-specific logic to ensure validity (for
instance, its TryMutate method checks graph connectivity
before removing an edge). But fundamentally, it behaves like a bit array under
the hood, which makes it compatible with GeneticSharp’s binary GA machinery.

## Key Components and Workflow of a Genetic Algorithm

GeneticSharp, the GA library you’re
using, provides all the **classic components of a genetic algorithm** –
genes, chromosomes, population, fitness evaluation, selection, crossover,
mutation, reinsertion (replacement), and termination[[7]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=GeneticSharp%20implements%20all%20the%20classic,your%20solution%E2%80%99s%20chromosome%20and%20fitness). Implementing a GA means configuring each
of these components for your problem. Let’s break down the typical GA workflow
and the role of each component:

1. **Initial Population:** Begin by creating an initial population of chromosomes. This is
   the first generation of candidate solutions. Often these are generated
   randomly to cover a broad search space[[8]](https://en.wikipedia.org/wiki/Genetic_algorithm#:~:text=The%20population%20size%20depends%20on,6), though you can also **seed** the
   population with known good solutions. In GeneticSharp, a **Population**
   is defined by a minimum and maximum size and an initial “Adam” chromosome
   (a prototype from which others can be created)[[9]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=In%20GeneticSharp%20a%20population%20is,directly%20use%20the%20Population%20class). For example, you might create a
   population of, say, 50 GraphChromosome
   individuals. Each GraphChromosome would start as a random valid graph
   configuration (your GraphChromosome
   constructor already handles random initialization, ensuring terminal nodes
   stay connected). This diverse first generation provides the starting point
   for the evolution.
2. **Fitness Evaluation:** Define a **fitness function** to evaluate how good each
   solution is. The fitness function maps a chromosome to a numeric score
   (higher typically means better, in GeneticSharp’s convention). This is
   problem-specific – in a graph optimization, fitness might measure
   something like the network’s efficiency, coherence, or the inverse of cost
   (you want to maximize whatever objective, or minimize cost by maximizing a
   negative cost fitness). You’ll implement GeneticSharp’s IFitness interface (or use FuncFitness for a quick
   lambda) to evaluate GraphChromosome
   instances[[10]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=The%20fitness%20function%20is%20where,a%20fast%20and%20optimum%20solution). For example, GraphFitness could analyze the graph represented by a chromosome (using the
   chromosome’s genes to remove edges from an original full graph) and
   compute a score – perhaps penalizing disconnected or infeasible solutions
   (which your chromosome design already prevents) and rewarding low cost or
   whatever metric you care about. A well-designed fitness function is
   critical, as it guides the GA toward optimal solutions[[11]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=The%20fitness%20function%20is%20where,a%20fast%20and%20optimum%20solution).
3. **Selection (Parent Selection):** In each generation, the GA must select some chromosomes from the
   current population to be **parents** for reproduction. Selection is
   usually biased towards fitter individuals (to propagate good traits) while
   still maintaining some diversity. GeneticSharp offers several classic
   selection strategies[[12]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Besides%20this%2C%20you%20can%20use,Stochastic%20Universal%20Sampling%20and%20Tournament), including:

4.      **Elite Selection:** always pick the top
best-performing individuals to survive or breed[[12]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Besides%20this%2C%20you%20can%20use,Stochastic%20Universal%20Sampling%20and%20Tournament).

5.      **Roulette Wheel Selection:** pick parents
probabilistically according to fitness (like a weighted wheel spin)[[12]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Besides%20this%2C%20you%20can%20use,Stochastic%20Universal%20Sampling%20and%20Tournament).

6.      **Tournament Selection:** randomly pit a few
individuals against each other and pick the best of each
"tournament," repeating until parent pool is filled[[12]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Besides%20this%2C%20you%20can%20use,Stochastic%20Universal%20Sampling%20and%20Tournament).

7.      **Stochastic Universal Sampling:** another
fitness-proportionate method ensuring spread-out picks across the wheel.

These are all compatible with binary
chromosomes – they only require fitness values. You can choose one based on
desired GA behavior. For instance, **elite selection** ensures the best
solutions are always carried forward (preserving top performers), whereas **tournament**
and **roulette** provide more exploratory pressure. In GeneticSharp, you
simply instantiate the selection class you want (e.g. new EliteSelection() or new TournamentSelection()) and pass it to
the GeneticAlgorithm. This selection step will decide which GraphChromosome individuals get to mate and create offspring for the next generation.

1.      **Crossover (Recombination):** After selecting
parents, the GA generates offspring through crossover – combining genes from
two (sometimes more) parents to form new solutions. Crossover is the primary
way the algorithm **recombines** existing good traits hoping to create even
better offspring. There are many crossover techniques, and GeneticSharp
provides a variety of them[[13]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=There%20are%20the%20ICrossover%20interface,Point%20%28C2%29%20and%20Uniform). For binary chromosomes, common crossover
operators include:

2.      **One-Point Crossover:** pick a random cut point
along the bit string; the first part of one parent is combined with the second
part of another to form a child, and vice versa for the second child.

3.      **Two-Point Crossover:** pick two cut points and
swap the middle segments between parents. This preserves head and tail segments
and swaps a middle portion.

4.      **Uniform Crossover:** treat each gene
independently, and for each position randomly decide which parent’s gene to
inherit (often with 50% probability from each parent)[[14]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Uniform%20Crossover%20enables%20the%20parent,other%20half%20from%20second%20parent). This effectively shuffles genes from
both parents into the offspring.

5.      **Custom crossover:** You can also design
domain-specific crossovers. In your case, you implemented UniqueCrossover for GraphChromosome, which appears to be a specialized uniform
crossover that ensures the offspring still represent valid connected graphs (it
likely swaps genes while checking connectivity via ReplaceGraphChromosomeGene). This was needed because some random combinations of edges could
break the connectivity constraint, so your crossover repairs or avoids invalid
gene transfers.

If connectivity or other constraints were
not a concern, **Uniform Crossover** is often a great default for binary
chromosomes – it tends to preserve a lot of diversity by mixing at the gene
level[[14]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Uniform%20Crossover%20enables%20the%20parent,other%20half%20from%20second%20parent). GeneticSharp will prevent you from using
a crossover that doesn’t make sense for the chromosome type (for example,
crossovers meant for ordered permutations like OX1/OX2 will reject a
BinaryChromosome)[[15]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=parent%20github.com%20%2C%20Two,and%20Uniform). In code, you instantiate a crossover
(e.g. new
UniformCrossover(mixProbability: 0.5f)) and provide
it to the GA. The GA will then apply it to each selected parent pair to produce
children. Typically, each pair of parents produces two children, but there are
also crossover variants that produce one or multiple offspring. For
GraphChromosome, using two parents at a time is standard. The outcome of
crossover is a set of new candidate solutions (bit strings) that combine
features of their parents.

1.      **Mutation:** After crossover, the GA applies
random **mutations** to the offspring. Mutation introduces new genetic
variations by tweaking genes randomly, which helps prevent the population from
converging too quickly to a local optimum (it maintains diversity)[[16]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=The%20mutation%20operator%20has%20the,never%20finding%20a%20better%20solution)[[17]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Flip,versa). For a binary chromosome, the natural
mutation operator is **bit-flip mutation**: choose a gene position at random
and flip its value (0→1 or 1→0)[[18]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Flip,versa). GeneticSharp has a built-in **FlipBitMutation**
operator specifically for chromosomes implementing IBinaryChromosome[[19]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Flip,versa). It will randomly pick one or more bits
(depending on a mutation rate/probability) and invert them. Typically you set a
mutation probability (e.g. 1% per gene or a few bits per chromosome) – enough
to introduce novelty but not so high that solutions become random again. In
code you might do var mutation = new FlipBitMutation(); and give it to the GA along with a mutation probability.

In your project, you created a custom GraphMutation class, which likely wraps a bit-flip but with additional checks.
Indeed, your GraphMutation chooses a random index and
calls GraphChromosome.TryMutate(index) before flipping the bit. This ensures, for example, that if the bit
corresponds to a critical edge (a bridge in the graph), the mutation will not
remove it (TryMutate returns false if that edge removal breaks the graph’s
connectivity). If the gene can be safely flipped, it then calls FlipGene(index) to actually toggle the 0/1[[20]](https://github.com/shtirlitsDva/Autocad-Civil3d-Tools/blob/42b21779bb1e2ad0baf3f177429dc57c05b8ceed/Acad-C3D-Tools/DimensioneringV2/Genetic/GraphMutation.cs#L36-L44)[[21]](https://github.com/shtirlitsDva/Autocad-Civil3d-Tools/blob/42b21779bb1e2ad0baf3f177429dc57c05b8ceed/Acad-C3D-Tools/DimensioneringV2/Genetic/GraphMutation.cs#L40-L43). So GraphMutation is a
domain-aware mutation operator. If you didn’t have such constraints, the
standard FlipBitMutation from GeneticSharp would suffice to flip random genes on any binary
chromosome (and it would integrate seamlessly because GraphChromosome implements IBinaryChromosome). The
key idea is that mutation should be a low-probability event that maintains the
presence of some new 0/1 combinations in the population over time[[22]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=The%20mutation%20operator%20has%20the,never%20finding%20a%20better%20solution).

1. **Replacement (New Generation Formation):** Once offspring are created and mutated, they form the basis of
   the **next generation**. There are different strategies for how to form
   the next population (sometimes called **reinsertion** or survivor
   selection). The simplest approach is generational: replace the entire old
   population with the new offspring (often used when population size is
   fixed and each pair of parents produces two children). Some GAs use
   elitism – carrying over a few top parents unchanged into the next
   generation to preserve their excellent genomes. GeneticSharp supports
   various **reinsertion** strategies through the IReinsertor interface, but by default it may replace the population entirely
   or include elitism depending on selection chosen. In your GA setup, if you
   use EliteSelection for example, typically the elite individuals are already chosen
   to breed or survive. The **Population** class in GeneticSharp will
   handle adding the new chromosomes and (unless you specify otherwise)
   discarding the old ones once a new generation is formed. The result is that
   the population “moves” into a new state: hopefully with better average
   fitness than the previous generation, thanks to selection pressure and
   recombination of good traits.
2. **Termination Check:** After each generation, the GA checks if a stopping condition is
   met. Common **termination criteria** include: reaching a maximum number
   of generations, exceeding a certain fitness threshold, lack of improvement
   (fitness stagnation) for a number of generations, or a time limit[[23]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=If%20you%20have%20some%20special,allows%20combine%20others%20terminations). For example, you might stop after
   100 generations, or stop early if your best fitness reaches a known
   optimum or hasn’t improved for 20 generations. GeneticSharp provides
   built-in termination conditions that you can use – e.g. GenerationNumberTermination, FitnessThresholdTermination, FitnessStagnationTermination, etc.[[23]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=If%20you%20have%20some%20special,allows%20combine%20others%20terminations). You can also combine conditions
   (with an OrTermination or AndTermination) or implement a custom ITermination.
   In code, you would set ga.Termination = new
   YourChosenTerminationCondition(...). If the
   termination criterion isn’t met, the GA will loop back and perform another
   cycle of selection → crossover → mutation to produce the next
   generation, and so on. The loop continues until termination, at which
   point the GA ends and you can take the best solution found.

Throughout this evolutionary loop, the GA
drives the population toward higher fitness. Thanks to selection, good
solutions tend to reproduce more, and thanks to crossover and mutation, the
algorithm explores new combinations of gene values. Over many generations, this
balances **exploitation** of known good gene patterns with **exploration**
of new ones, often yielding a near-optimal solution.

## Using GeneticSharp with a BinaryGraphChromosome

Now that we have the big picture, let’s
outline how you can implement your specific GA using GeneticSharp and the GraphChromosome class:

* **Chromosome Implementation:** You already have GraphChromosome
  inheriting from BinaryChromosomeBase.
  This custom chromosome defines how to generate a random valid solution (CreateGenes in the base class, customized in your constructor) and how to
  create a new blank instance (CreateNew is
  overridden to return a new GraphChromosome). It also contains
  domain-specific methods like TryMutate and
  ReplaceGraphChromosomeGene to enforce graph constraints during mutation and crossover.
  Because GraphChromosome implements IChromosome (via the base class), it can plug into GeneticSharp’s algorithm
  seamlessly. In other words, GeneticSharp will treat it like any other
  chromosome during GA operations. The binary nature means you conceptually
  have a vector of 0/1 genes – which, aside from your added checks, behaves
  like a typical binary string.
* **Fitness Function:** Develop a class that implements IFitness (or
  use FuncFitness) to evaluate GraphChromosome. For instance, GraphFitness might compute the total cost of the chosen edges, or some penalty
  for removed edges, etc., depending on your optimization goal. Return
  higher fitness for better solutions (remember GeneticSharp by default
  maximizes fitness). This fitness evaluator will be used by the GA to rank
  chromosomes each generation.
* **GA Configuration:** Set up the GeneticSharp GeneticAlgorithm object with all the components:

·      
Create a **Population** with your
GraphChromosome. For example: var population = new Population(minSize: 50, maxSize:
50, adamChromosome: new GraphChromosome(coherencyManager));. The “Adam” chromosome is a prototype needed so the GA knows how to
create other individuals (GraphChromosome’s CreateNew will be
used to generate the initial pool)[[9]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=In%20GeneticSharp%20a%20population%20is,directly%20use%20the%20Population%20class). The population size can be adjusted based
on problem complexity – larger populations explore more at the cost of
computation.

·      
Choose a **Selection** operator. You could start
with something like EliteSelection or TournamentSelection. For example: var selection = new EliteSelection(); which will favor the best graphs for mating[[12]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Besides%20this%2C%20you%20can%20use,Stochastic%20Universal%20Sampling%20and%20Tournament). (EliteSelection ensures the fittest
individuals are selected as parents, and often GeneticSharp will also carry
some elites directly to next generation).

·      
Choose a **Crossover** operator. Given
GraphChromosome is binary and order of genes is not inherently meaningful
beyond representing edges, a **UniformCrossover** is a robust choice. For
example: var
crossover = new UniformCrossover(0.5f); uses 50%
mix rate (each gene has 50% chance of coming from parent A vs parent B)[[14]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Uniform%20Crossover%20enables%20the%20parent,other%20half%20from%20second%20parent). This is exactly how your custom UniqueCrossover behaves (shuffling gene indices and randomly taking from one parent or
the other). Uniform crossover tends to produce diverse combinations of edges
from both parent graphs. Alternatively, you could use OnePointCrossover or TwoPointCrossover for simplicity – those
will cut the bit array and exchange segments, which also works since all
chromosomes share the same length. (Just avoid crossovers meant for ordered
permutations like PMX or OX; GeneticSharp would throw an error if you tried, as
noted in the docs[[15]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=parent%20github.com%20%2C%20Two,and%20Uniform).) If your custom UniqueCrossover is already working to ensure valid offspring, you can plug that in by
extending CrossoverBase as you did, and passing an instance of UniqueCrossover to the GA instead.

·      
Choose a **Mutation** operator. If you trust
GeneticSharp’s built-in flip-bit, you can do var mutation = new FlipBitMutation(); which will flip a random bit in a chromosome with a given probability[[18]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Flip,versa). Since GraphChromosome
implements IBinaryChromosome, FlipBitMutation will recognize it and flip one of its gene values (0↔1).
However, using it blindly might occasionally produce an invalid graph (e.g.,
remove a critical edge). That’s why you implemented GraphMutation, which essentially wraps the flip-bit but skips illegal flips. You
should use your GraphMutation operator in the GA for
safety. In GeneticSharp, custom mutations are integrated by inheriting MutationBase (which you did) and overriding PerformMutate. You
then instantiate it: var mutation = new GraphMutation(coherencyManager);. This ensures each mutation operation on a GraphChromosome respects
the graph’s coherency rules.

·      
Set the **Termination** condition. Decide when
the GA should stop. For example, ga.Termination = new GenerationNumberTermination(100) to run for 100 generations, or a FitnessStagnationTermination(20) to stop if the best fitness hasn’t improved in 20 generations[[24]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=If%20you%20have%20some%20special,allows%20combine%20others%20terminations). Choose what makes sense for your
scenario – if you have a time limit or you observe that after N generations
improvement slows down, use that. You can also combine criteria, e.g., stop if
fitness > X **OR** generations = Y (using OrTermination).

With these, you create the
GeneticAlgorithm:

var ga = new GeneticAlgorithm(population, fitness, selection,
crossover, mutation);  
ga.Termination
= new GenerationNumberTermination(100);

This ties together the population and all
operators into a GA instance.

* **Running the GA:**
  Call ga.Start() to begin the evolutionary loop. GeneticSharp will handle the
  generation-by-generation process as described: evaluate all chromosomes
  with your fitness, select parents, apply crossover and mutation to create
  offspring, form the next generation, and repeat. It’s multithreaded by
  default, evaluating fitness in parallel if possible, which is convenient
  given each GraphChromosome’s fitness evaluation is independent. During the
  run, you can hook into events like ga.GenerationRan to get updates after each generation[[25]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=So%20the%20GA%20ran%2C%20but,BestChromosome%20property). For example, you might log the best
  fitness or the best solution’s details each generation (this is often
  useful to see progress or to update a UI). But since the UI aspect will be
  handled separately, you mainly need to ensure the GA logic runs correctly.
  After termination, you can retrieve the best solution found via ga.BestChromosome property[[25]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=So%20the%20GA%20ran%2C%20but,BestChromosome%20property). This will be a GraphChromosome – you can then extract the gene array (with GetGenes()) or the graph it represents (perhaps your GraphChromosome holds a
  reference to its internal graph) and present or use that result.
* **Compatible GeneticSharp Classes:** Because GraphChromosome is
  built on BinaryChromosomeBase, it works with
  any GeneticSharp operator that handles binary or general chromosomes.
  We’ve already mentioned several: **FlipBitMutation** is specifically
  for IBinaryChromosome (flips a randomly chosen bit)[[19]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Flip,versa); **UniformCrossover** and **OnePointCrossover**,
  etc., can all take your GraphChromosome as long as it’s castable to IChromosome (the library will internally cast and operate on gene arrays).
  All **selection** strategies (elite, roulette, etc.) work because they
  only rely on fitness comparisons, not on chromosome internals. The **Population**
  and **GeneticAlgorithm** classes are likewise agnostic to the
  chromosome representation – they use the interfaces. In summary, any
  GeneticSharp class that doesn’t explicitly require an ordering or numeric
  chromosome will work with a bit-string chromosome:

·      
Selection: *EliteSelection*, *RouletteWheelSelection*,
*TournamentSelection*, *StochasticUniversalSampling*, etc.[[12]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Besides%20this%2C%20you%20can%20use,Stochastic%20Universal%20Sampling%20and%20Tournament).

·      
Crossover: *OnePointCrossover*, *TwoPointCrossover*,
*UniformCrossover* (ideal for binary)[[13]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=There%20are%20the%20ICrossover%20interface,Point%20%28C2%29%20and%20Uniform)[[26]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Uniform%20Crossover%20enables%20the%20parent,other%20half%20from%20second%20parent), or even *ThreeParentCrossover*.
(Avoid crossovers like Order or PMX which demand ordered sequences –
GeneticSharp will warn or throw an exception if used with a binary chromosome[[15]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=parent%20github.com%20%2C%20Two,and%20Uniform)).

·      
Mutation: *FlipBitMutation* (ideal for binary)[[19]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Flip,versa), or *UniformMutation* (which can set
a gene to a random value – for binary that’s effectively another form of bit
flip). Mutations like *Twors* or *ReverseSequenceMutation* are meant
for permutations or sequences, not applicable to unordered bits, so those
aren’t used here.

·      
Reinsertion: *ElitistReinsertion*, *PureRandomReinsertion*,
etc., if you want custom survivor policies – although GeneticSharp uses a
sensible default if you don’t specify it (often keeping best individuals).

·      
Terminations: *GenerationNumberTermination*, *FitnessThresholdTermination*,
*FitnessStagnationTermination*, *TimeEvolvingTermination*, etc., all
of which are independent of chromosome type[[23]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=If%20you%20have%20some%20special,allows%20combine%20others%20terminations).

In your specific implementation, you’ve
wisely provided custom crossover (UniqueCrossover)
and mutation (GraphMutation) to respect graph constraints
during those operations. These integrate by extending GeneticSharp’s base
classes (CrossoverBase, MutationBase) and thus plug into the GeneticAlgorithm just like built-in operators.
The rest of the GA can use standard components: for example, you might use EliteSelection to ensure the best graph designs are always preserved, and perhaps FitnessStagnationTermination to stop when no further improvement is seen.

## Summary and Best Practices

By leveraging the GeneticSharp framework
with your GraphChromosome, you get the benefit of a well-tested GA engine handling the loop and
multi-threading, while you focus on problem-specific logic (graph validity and
evaluating solution quality). To recap the big picture:

·      
**GA Concept:** Simulate
evolution – keep a pool of graph solutions, evaluate their fitness, breed the
good ones, mutate occasionally, and iterate. Over time, solutions improve.

·      
**Binary Chromosome:**
Represent solutions as binary gene sequences. This is compatible with classic
GA operators. Your GraphChromosome is a tailored binary chromosome that encodes
a network graph’s edges as bits.

·      
**Components in Use:**
Define your population size and initial solutions, implement a robust fitness
calculation, select a suitable selection strategy (e.g. elite or tournament),
use a crossover that mixes edge-inclusion bits from parent graphs (ensuring
offspring are valid), apply bit-flip mutations to explore new combinations
(again ensuring validity), and set a termination condition for the algorithm to
stop. GeneticSharp provides ready-made classes for all these components[[7]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=GeneticSharp%20implements%20all%20the%20classic,your%20solution%E2%80%99s%20chromosome%20and%20fitness), which can work with your binary
chromosome. Customizations are only needed to respect any special constraints
(which you’ve done with custom crossover/mutation).

·      
**Execution:** Once
configured, run the GA and monitor its progress. The output will be the best GraphChromosome found – you can then extract the solution (e.g. the set of edges to
remove or keep).

By understanding these steps and
components, you can effectively harness the GA to solve your graph optimization
problem. GAs may require tuning (population size, mutation rate, etc.) for
optimal performance, so don’t be afraid to experiment with those parameters.
The combination of GeneticSharp’s flexibility and your binary-encoded
GraphChromosome should allow you to explore a wide search space of network
designs and converge on a high-quality solution, all while ensuring the graph
remains coherent. **In short,** you have a powerful general genetic
algorithm engine, and with your bit-flipping chromosome representation, you can
apply well-known GA techniques to evolve solutions to your specific problem[[7]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=GeneticSharp%20implements%20all%20the%20classic,your%20solution%E2%80%99s%20chromosome%20and%20fitness)[[17]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Flip,versa). Good luck, and happy evolving!

---

[[1]](https://en.wikipedia.org/wiki/Genetic_algorithm#:~:text=In%20computer%20science%20%20and,164%20hyperparameter) [[2]](https://en.wikipedia.org/wiki/Genetic_algorithm#:~:text=The%20evolution%20usually%20starts%20from,Commonly%2C%20the%20algorithm) [[3]](https://en.wikipedia.org/wiki/Genetic_algorithm#:~:text=The%20next%20step%20is%20to,and%20mutation) [[4]](https://en.wikipedia.org/wiki/Genetic_algorithm#:~:text=fit%20individuals%20are%20stochastically%20,been%20reached%20for%20the%20population) [[5]](https://en.wikipedia.org/wiki/Genetic_algorithm#:~:text=In%20a%20genetic%20algorithm%2C%20a,5) [[6]](https://en.wikipedia.org/wiki/Genetic_algorithm#:~:text=1,to%20evaluate%20the%20solution%20domain) [[8]](https://en.wikipedia.org/wiki/Genetic_algorithm#:~:text=The%20population%20size%20depends%20on,6) Genetic algorithm - Wikipedia

<https://en.wikipedia.org/wiki/Genetic_algorithm>

[[7]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=GeneticSharp%20implements%20all%20the%20classic,your%20solution%E2%80%99s%20chromosome%20and%20fitness) [[9]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=In%20GeneticSharp%20a%20population%20is,directly%20use%20the%20Population%20class) [[10]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=The%20fitness%20function%20is%20where,a%20fast%20and%20optimum%20solution) [[11]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=The%20fitness%20function%20is%20where,a%20fast%20and%20optimum%20solution) [[12]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Besides%20this%2C%20you%20can%20use,Stochastic%20Universal%20Sampling%20and%20Tournament) [[13]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=There%20are%20the%20ICrossover%20interface,Point%20%28C2%29%20and%20Uniform) [[14]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Uniform%20Crossover%20enables%20the%20parent,other%20half%20from%20second%20parent) [[15]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=parent%20github.com%20%2C%20Two,and%20Uniform) [[16]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=The%20mutation%20operator%20has%20the,never%20finding%20a%20better%20solution) [[17]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Flip,versa) [[18]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Flip,versa) [[19]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Flip,versa) [[22]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=The%20mutation%20operator%20has%20the,never%20finding%20a%20better%20solution) [[23]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=If%20you%20have%20some%20special,allows%20combine%20others%20terminations) [[24]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=If%20you%20have%20some%20special,allows%20combine%20others%20terminations) [[25]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=So%20the%20GA%20ran%2C%20but,BestChromosome%20property) [[26]](https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/#:~:text=Uniform%20Crossover%20enables%20the%20parent,other%20half%20from%20second%20parent) Function optimization with GeneticSharp |
Diego Giacomelli | Senior .NET Developer | Azure | Web API | Framework | Unity
3D

<https://diegogiacomelli.com.br/function-optimization-with-geneticsharp/>