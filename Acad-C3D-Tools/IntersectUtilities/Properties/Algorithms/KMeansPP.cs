using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.Properties.Algorithms
{
    public class KMeans
    {
        public int K;  // number clusters (use lower k for indexing)
        public double[][] data;  // data to be clustered
        public int N;  // number data items
        public int dim;  // number values in each data item
        public string initMethod;  // "plusplus", "forgy" "random"
        public int maxIter;  // max per single clustering attempt
        public int[] clustering;  // final cluster assignments
        public double[][] means;  // final cluster means aka centroids
        public double wcss;  // final total within-cluster sum of squares (inertia??)
        public int[] counts;  // final num items in each cluster
        public Random rnd;  // for initialization

        public KMeans(int K, double[][] data, string initMethod, int maxIter, int seed)
        {
            this.K = K;
            this.data = data;  // reference copy
            this.initMethod = initMethod;
            this.maxIter = maxIter;

            N = data.Length;
            dim = data[0].Length;

            means = new double[K][];  // one mean per cluster
            for (int k = 0; k < K; ++k)
                means[k] = new double[dim];
            clustering = new int[N];  // cell val is cluster ID, index is data item
            counts = new int[K];  // one cell per cluster
            wcss = double.MaxValue;  // smaller is better

            rnd = new Random(seed);
        } // ctor

        public void Cluster(int trials)
        {
            for (int trial = 0; trial < trials; ++trial)
                Cluster();  // find a clustering and update bests
        }

        public void Cluster()
        {
            // init clustering[] and means[][] 
            // loop at most maxIter times
            //   update means using curr clustering
            //   update clustering using new means
            // end-loop
            // if clustering is new best, update clustering, means, counts, wcss

            int[] currClustering = new int[N];  // [0, 0, 0, 0, .. ]

            double[][] currMeans = new double[K][];
            for (int k = 0; k < K; ++k)
                currMeans[k] = new double[dim];

            if (initMethod == "plusplus")
                InitPlusPlus(data, currClustering, currMeans, rnd);
            else
                throw new Exception("not supported");

            bool changed;  //  result from UpdateClustering (to exit loop)
            int iter = 0;
            while (iter < maxIter)
            {
                UpdateMeans(currMeans, data, currClustering);
                changed = UpdateClustering(currClustering,
                  data, currMeans);
                if (changed == false)
                    break;  // need to stop iterating
                ++iter;
            }

            double currWCSS = ComputeWithinClusterSS(data,
              currMeans, currClustering);
            if (currWCSS < wcss)  // new best clustering found
            {
                // copy the clustering, means; compute counts; store WCSS
                for (int i = 0; i < N; ++i)
                    clustering[i] = currClustering[i];

                for (int k = 0; k < K; ++k)
                    for (int j = 0; j < dim; ++j)
                        means[k][j] = currMeans[k][j];

                counts = ComputeCounts(K, currClustering);
                wcss = currWCSS;
            }

        } // Cluster()

        private static void InitPlusPlus(double[][] data,
          int[] clustering, double[][] means, Random rnd)
        {
            //  k-means++ init using roulette wheel selection
            // clustering[] and means[][] exist
            int N = data.Length;
            int dim = data[0].Length;
            int K = means.Length;

            // select one data item index at random as 1st meaan
            int idx = rnd.Next(0, N); // [0, N)
            for (int j = 0; j < dim; ++j)
                means[0][j] = data[idx][j];

            for (int k = 1; k < K; ++k) // find each remaining mean
            {
                double[] dSquareds = new double[N]; // from each item to its closest mean

                for (int i = 0; i < N; ++i) // for each data item
                {
                    // compute distances from data[i] to each existing mean (to find closest)
                    double[] distances = new double[k]; // we currently have k means

                    for (int ki = 0; ki < k; ++ki)
                        distances[ki] = EucDistance(data[i], means[ki]);

                    int mi = ArgMin(distances);  // index of closest mean to curr item
                                                 // save the associated distance-squared
                    dSquareds[i] = distances[mi] * distances[mi];  // sq dist from item to its closest mean
                } // i

                // select an item far from its mean using roulette wheel
                // if an item has been used as a mean its distance will be 0
                // so it won't be selected

                int newMeanIdx = ProporSelect(dSquareds, rnd);
                for (int j = 0; j < dim; ++j)
                    means[k][j] = data[newMeanIdx][j];
            } // k remaining means

            //Console.WriteLine("");
            //ShowMatrix(means, 4, 10);
            //Console.ReadLine();

            UpdateClustering(clustering, data, means);
        } // InitPlusPlus

        static int ProporSelect(double[] vals, Random rnd)
        {
            // roulette wheel selection
            // on the fly technique
            // vals[] can't be all 0.0s
            int n = vals.Length;

            double sum = 0.0;
            for (int i = 0; i < n; ++i)
                sum += vals[i];

            double cumP = 0.0;  // cumulative prob
            double p = rnd.NextDouble();

            for (int i = 0; i < n; ++i)
            {
                cumP += vals[i] / sum;
                if (cumP > p) return i;
            }
            return n - 1;  // last index
        }

        private static int[] ComputeCounts(int K, int[] clustering)
        {
            int[] result = new int[K];
            for (int i = 0; i < clustering.Length; ++i)
            {
                int cid = clustering[i];
                ++result[cid];
            }
            return result;
        }

        private static void UpdateMeans(double[][] means,
          double[][] data, int[] clustering)
        {
            // compute the K means using data and clustering
            // assumes no empty clusters in clustering

            int K = means.Length;
            int N = data.Length;
            int dim = data[0].Length;

            int[] counts = ComputeCounts(K, clustering);  // needed for means

            for (int k = 0; k < K; ++k)  // make sure no empty clusters
                if (counts[k] == 0)
                    throw new Exception("empty cluster passed to UpdateMeans()");

            double[][] result = new double[K][];  // new means
            for (int k = 0; k < K; ++k)
                result[k] = new double[dim];

            for (int i = 0; i < N; ++i)  // each data item
            {
                int cid = clustering[i];  // which cluster ID?
                for (int j = 0; j < dim; ++j)
                    result[cid][j] += data[i][j];  // accumulate
            }

            // divide accum sums by counts to get means
            for (int k = 0; k < K; ++k)
                for (int j = 0; j < dim; ++j)
                    result[k][j] /= counts[k];

            // no 0-count clusters so update the means
            for (int k = 0; k < K; ++k)
                for (int j = 0; j < dim; ++j)
                    means[k][j] = result[k][j];
        }

        private static bool UpdateClustering(int[] clustering,
          double[][] data, double[][] means)
        {
            // update existing cluster clustering using data and means
            // proposed clustering would have an empty cluster: return false - no change to clustering
            // proposed clustering would be no change: return false, no change to clustering
            // proposed clustering is different and has no empty clusters: return true, clustering is changed

            int K = means.Length;
            int N = data.Length;

            int[] result = new int[N];  // proposed new clustering (cluster assignments)
            bool change = false;  // is there a change to the existing clustering?
            int[] counts = new int[K];  // check if new clustering makes an empty cluster

            for (int i = 0; i < N; ++i)  // make of copy of existing clustering
                result[i] = clustering[i];

            for (int i = 0; i < data.Length; ++i)  // each data item
            {
                double[] dists = new double[K];  // dist from curr item to each mean
                for (int k = 0; k < K; ++k)
                    dists[k] = EucDistance(data[i], means[k]);

                int cid = ArgMin(dists);  // index of the smallest distance
                result[i] = cid;
                if (result[i] != clustering[i])
                    change = true;  // the proposed clustering is different for at least one item
                ++counts[cid];
            }

            if (change == false)
                return false;  // no change to clustering -- clustering has converged

            for (int k = 0; k < K; ++k)
                if (counts[k] == 0)
                    return false;  // no change to clustering because would have an empty cluster

            // there was a change and no empty clusters so update clustering
            for (int i = 0; i < N; ++i)
                clustering[i] = result[i];

            return true;  // successful change to clustering so keep looping
        }

        private static double EucDistance(double[] item, double[] mean)
        {
            // Euclidean distance from item to mean
            // used to determine cluster assignments
            double sum = 0.0;
            for (int j = 0; j < item.Length; ++j)
                sum += (item[j] - mean[j]) * (item[j] - mean[j]);
            return Math.Sqrt(sum);
        }

        private static int ArgMin(double[] v)
        {
            int minIdx = 0;
            double minVal = v[0];
            for (int i = 0; i < v.Length; ++i)
            {
                if (v[i] < minVal)
                {
                    minVal = v[i];
                    minIdx = i;
                }
            }
            return minIdx;
        }

        private static double ComputeWithinClusterSS(double[][] data,
          double[][] means, int[] clustering)
        {
            // compute total within-cluster sum of squared differences between 
            // cluster items and their cluster means
            // this is actually the objective function, not distance
            double sum = 0.0;
            for (int i = 0; i < data.Length; ++i)
            {
                int cid = clustering[i];  // which cluster does data[i] belong to?
                sum += SumSquared(data[i], means[cid]);
            }
            return sum;
        }

        private static double SumSquared(double[] item, double[] mean)
        {
            // squared distance between vectors
            // surprisingly, k-means minimizes this, not distance
            double sum = 0.0;
            for (int j = 0; j < item.Length; ++j)
                sum += (item[j] - mean[j]) * (item[j] - mean[j]);
            return sum;
        }

        // display functions for debugging

        //private static void ShowVector(int[] vec, int wid)  // debugging use
        //{
        //  int n = vec.Length;
        //  for (int i = 0; i < n; ++i)
        //    Console.Write(vec[i].ToString().PadLeft(wid));
        //  Console.WriteLine("");
        //}

        //private static void ShowMatrix(double[][] m, int dec, int wid)  // debugging
        //{
        //  for (int i = 0; i < m.Length; ++i)
        //  {
        //    for (int j = 0; j < m[0].Length; ++j)
        //    {
        //      double x = m[i][j];
        //      if (Math.Abs(x) < 1.0e-5) x = 0.0;
        //      Console.Write(x.ToString("F" + dec).PadLeft(wid));
        //    }
        //    Console.WriteLine("");
        //  }
        //}

        //private static int[] PickDistinct(int n, int N, Random rnd)  // for Forgy init
        //{
        //  // pick n distict integers from  [0 .. N)
        //  int[] indices = new int[N];
        //  int[] result = new int[n];
        //  for (int i = 0; i < N; ++i)
        //    indices[i] = i;
        //  Shuffle(indices, rnd);
        //  for (int i = 0; i < n; ++i)
        //    result[i] = indices[i];
        //  return result;
        //}

    } // class KMeans
}
