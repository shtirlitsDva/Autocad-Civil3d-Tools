using DimensioneringV2.BruteForceOptimization;
using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphUtilities
{
    internal class SpanningTreeCount
    {
        /// <summary>
        /// Computes the number of spanning trees in the given undirected graph 
        /// using Kirchhoff's Matrix-Tree Theorem, but stops if it exceeds 'threshold'.
        /// 
        /// Returns:
        ///   - If the number of spanning trees <= threshold, returns that exact count.
        ///   - If it exceeds the threshold, returns (threshold + 1) as a sentinel value.
        /// </summary>
        public static BigInteger CountSpanningTrees(
            UndirectedGraph<BFNode, BFEdge> graph,
            BigInteger threshold,
            Action<BigInteger>? progressCallback = null)
        {
            int n = graph.VertexCount;
            if (n <= 1)
            {
                // A graph with 0 or 1 vertices trivially has 1 spanning tree 
                // (the graph itself or empty).
                return 1;
            }

            // 1) Get an ordering of vertices
            var vertices = graph.Vertices.ToList();
            var indexOf = new Dictionary<BFNode, int>();
            for (int i = 0; i < vertices.Count; i++)
            {
                indexOf[vertices[i]] = i;
            }

            // 2) Build adjacency matrix
            BigInteger[,] adjacency = new BigInteger[n, n];
            foreach (var edge in graph.Edges)
            {
                int i = indexOf[edge.Source];
                int j = indexOf[edge.Target];
                // If parallel edges are allowed, you can accumulate them:
                adjacency[i, j] += 1;
                adjacency[j, i] += 1;
            }

            // 3) Build Laplacian
            var laplacian = new BigInteger[n, n];
            for (int i = 0; i < n; i++)
            {
                BigInteger degree = 0;
                for (int j = 0; j < n; j++)
                {
                    degree += adjacency[i, j];
                }
                laplacian[i, i] = degree;  // diagonal
                for (int j = 0; j < n; j++)
                {
                    if (i != j)
                    {
                        laplacian[i, j] = -adjacency[i, j];
                    }
                }
            }

            // 4) Form submatrix by removing last row/column
            int size = n - 1;
            BigInteger[,] submatrix = new BigInteger[size, size];
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    submatrix[i, j] = laplacian[i, j];
                }
            }

            // 5) Compute determinant with threshold check
            BigInteger det = DeterminantWithThreshold(submatrix, threshold,
                progressCallback);

            // If determinant is returned as threshold+1, it means "exceeded".
            return det;
        }

        /// <summary>
        /// Computes the determinant of a square matrix of BigIntegers using a 
        /// variant of LU decomposition with partial pivoting. 
        /// 
        /// If at any point intermediate values exceed 'threshold', 
        /// it returns (threshold + 1) as a sentinel for "too large".
        /// </summary>
        private static BigInteger DeterminantWithThreshold(BigInteger[,] matrix, BigInteger threshold,
            Action<BigInteger>? progressCallback)
        {
            int n = matrix.GetLength(0);
            // Clone so as not to modify the original
            var mat = (BigInteger[,])matrix.Clone();
            BigInteger det = BigInteger.One;
            BigInteger sentinel = threshold + 1; // returned if we exceed threshold

            for (int i = 0; i < n; i++)
            {
                // Partial pivoting
                BigInteger maxAbsVal = BigInteger.Zero;
                int pivotRow = i;
                for (int r = i; r < n; r++)
                {
                    BigInteger absVal = BigInteger.Abs(mat[r, i]);
                    if (absVal > maxAbsVal)
                    {
                        maxAbsVal = absVal;
                        pivotRow = r;
                    }
                }

                // If pivot is zero => determinant is zero
                if (maxAbsVal.IsZero)
                    return BigInteger.Zero;

                // Swap pivot row if needed
                if (pivotRow != i)
                {
                    for (int col = 0; col < n; col++)
                    {
                        (mat[i, col], mat[pivotRow, col]) = (mat[pivotRow, col], mat[i, col]);
                    }
                    // Swapping rows flips the sign of the determinant
                    det = -det;
                }

                // Multiply pivot
                BigInteger pivot = mat[i, i];
                det *= pivot;

                // Progress callback
                progressCallback?.Invoke(det);

                // Check threshold
                if (BigInteger.Abs(det) > threshold)
                    return sentinel;  // Exceeds threshold => short-circuit

                // Eliminate below pivot
                for (int r = i + 1; r < n; r++)
                {
                    BigInteger factor = mat[r, i];
                    if (!factor.IsZero)
                    {
                        // Row operation:
                        //   mat[r, c] = (mat[r, c]*pivot - factor*mat[i, c]) / pivot
                        // for each column c from i..n-1
                        for (int c = i; c < n; c++)
                        {
                            mat[r, c] = (mat[r, c] * pivot - factor * mat[i, c]) / pivot;
                        }
                    }
                }
            }

            // Final check
            if (BigInteger.Abs(det) > threshold)
                return sentinel;

            return det;
        }
    }
}
