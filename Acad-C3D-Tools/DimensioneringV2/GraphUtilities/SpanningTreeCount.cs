using DimensioneringV2.BruteForceOptimization;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphUtilities
{
    internal class SpanningTreeCount
    {
        public static BigInteger CountSpanningTrees<TVertex, TEdge>(
        UndirectedGraph<TVertex, TEdge> graph,
        TimeSpan timelimit
            ) where TEdge : IEdge<TVertex>
        {
            var sw = Stopwatch.StartNew();

            int n = graph.VertexCount;

            // For 0 or 1 vertex, there's exactly 1 spanning tree
            if (n <= 1) return 1;

            // 1) Order vertices
            var vertices = graph.Vertices.ToList();
            var indexOf = new Dictionary<TVertex, int>(n);
            for (int i = 0; i < n; i++)
                indexOf[vertices[i]] = i;

            // 2) Build adjacency
            var adjacency = new BigInteger[n, n];
            foreach (var edge in graph.Edges)
            {
                int i = indexOf[edge.Source];
                int j = indexOf[edge.Target];
                adjacency[i, j] += 1;
                adjacency[j, i] += 1;
            }

            // 3) Build Laplacian
            var laplacian = new BigInteger[n, n];
            for (int i = 0; i < n; i++)
            {
                BigInteger degree = 0;
                for (int j = 0; j < n; j++)
                    degree += adjacency[i, j];

                laplacian[i, i] = degree;
                for (int j = 0; j < n; j++)
                {
                    if (i != j)
                    {
                        laplacian[i, j] = -adjacency[i, j];
                    }
                }
            }

            // 4) Build submatrix (n-1 x n-1), removing last row & column
            int size = n - 1;
            var subMatrix = new BigRational[size, size];
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    // Convert the BigInteger to fraction
                    subMatrix[i, j] = new BigRational(laplacian[i, j], BigInteger.One);
                }
            }

            // 5) Compute the determinant of that submatrix
            BigRational detFrac = DeterminantExact(subMatrix, sw, timelimit);

            if (detFrac.Equals(new BigRational(-1, 1))) { return -1; }
            
            // If it's zero, spanning-tree count is zero 
            if (detFrac.Equals(BigRational.Zero)) return 0;

            // Otherwise, it should be an integer
            return detFrac.ToBigInteger();
        }

        /// <summary>
        /// Computes the determinant of a square matrix of BigRational
        /// via an LU decomposition approach with partial pivoting.
        /// </summary>
        private static BigRational DeterminantExact(
            BigRational[,] mat,
            Stopwatch sw,
            TimeSpan timelimit
            )
        {
            int n = mat.GetLength(0);
            var local = (BigRational[,])mat.Clone();

            // We'll accumulate the determinant in a fraction
            BigRational det = BigRational.One;

            for (int i = 0; i < n; i++)
            {
                // 1) Partial pivoting: find pivot row
                int pivotRow = i;
                BigRational pivotValAbs = BigRational.Zero;
                for (int r = i; r < n; r++)
                {
                    BigRational absCandidate = Abs(local[r, i]);
                    if (Greater(absCandidate, pivotValAbs))
                    {
                        pivotValAbs = absCandidate;
                        pivotRow = r;
                    }
                }

                // If pivot is exactly 0 => determinant is 0
                if (pivotValAbs.Equals(BigRational.Zero))
                    return BigRational.Zero;

                // 2) Swap if needed
                if (pivotRow != i)
                {
                    // Swap the rows
                    for (int c = 0; c < n; c++)
                    {
                        (local[i, c], local[pivotRow, c]) = (local[pivotRow, c], local[i, c]);
                    }
                    // Flip sign of determinant
                    det = -det;
                }

                // 3) Multiply pivot into determinant
                BigRational pivot = local[i, i];
                det *= pivot;

                // Check if we're running out of time
                if (sw.Elapsed > timelimit)
                {
                    return new BigRational(-1, 1);
                }

                // 4) Eliminate below pivot
                //    Row r = Row r - ( (Row r pivot-col)/(Row i pivot-col) ) * Row i
                //    but in fraction form, carefully:
                for (int r2 = i + 1; r2 < n; r2++)
                {
                    BigRational factor = local[r2, i] / pivot;
                    // Subtract factor * row i from row r2
                    for (int c = i; c < n; c++)
                    {
                        local[r2, c] -= factor * local[i, c];
                    }
                }
            }

            return det;
        }

        // Utility: compare absolute values of BigRational
        private static bool Greater(BigRational a, BigRational b)
        {
            return CompareAbs(a, b) > 0;
        }
        private static BigRational Abs(BigRational x)
        {
            return x.Numerator.Sign < 0
                ? new BigRational(BigInteger.Negate(x.Numerator), x.Denominator)
                : x;
        }
        private static int CompareAbs(BigRational a, BigRational b)
        {
            // Compare |a| and |b| by cross multiplication
            // => compare a.Numerator^2 / a.Denominator^2 to b.Numerator^2 / b.Denominator^2
            // or we can do more direct approach:
            var lhs = BigInteger.Abs(a.Numerator) * BigInteger.Abs(b.Denominator);
            var rhs = BigInteger.Abs(b.Numerator) * BigInteger.Abs(a.Denominator);
            return lhs.CompareTo(rhs);
        }
    }
}
