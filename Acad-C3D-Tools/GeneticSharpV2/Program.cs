using GeneticSharp;

namespace GeneticSharpV2
{
    internal class Program
    {
        static void Main(string[] args)
        {
            float maxWidth = 998f;
            float maxHeight = 680f;

            var chromosome = new FloatingPointChromosome(
                [0, 0, 0, 0],
                [maxWidth, maxHeight, maxWidth, maxHeight],
                [10, 10, 10, 10],
                [0, 0, 0, 0]);

            var population = new Population(50, 100, chromosome);

            var fitness = new FuncFitness((c) =>
            {
                var fc = c as FloatingPointChromosome;
                var values = fc.ToFloatingPoints();
                var x1 = values[0];
                var y1 = values[1];
                var x2 = values[2];
                var y2 = values[3];

                return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
            });

            var selection = new EliteSelection();
            var crossover = new UniformCrossover(0.5f);
            var mutation = new FlipBitMutation();
            var termination = new FitnessStagnationTermination(100);

            var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation)
            {
                Termination = termination
            };

            Console.WriteLine("Generation: (x1, y1), (x2, y2) = distance");

            var latestFitness = 0.0;
            ga.GenerationRan += (sender, e) =>
            {
                var bestChromosome = ga.BestChromosome as FloatingPointChromosome;
                var bestFitness = bestChromosome.Fitness.Value;
                if (bestFitness != latestFitness)
                {
                    latestFitness = bestFitness;
                    var phenotype = bestChromosome.ToFloatingPoints();
                    Console.WriteLine(
                        "Generation {0,2}: ({1},{2}),({3},{4}) = {5}",
                        ga.GenerationsNumber,
                        phenotype[0],
                        phenotype[1],
                        phenotype[2],
                        phenotype[3],
                        bestFitness
                    );
                }
            };
            ga.Start();
            Console.ReadKey();
        }
    }
}
