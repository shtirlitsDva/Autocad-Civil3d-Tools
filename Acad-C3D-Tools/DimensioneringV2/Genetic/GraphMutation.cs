﻿using GeneticSharp;

using Mapsui.Utilities;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Genetic
{
    internal class GraphMutation : MutationBase
    {
        private readonly CoherencyManager _chm;
        public GraphMutation(CoherencyManager coherencyManager)
        {
            _chm = coherencyManager;
            m_rnd = RandomizationProvider.Current;
        }

        #region Fields
        private readonly IRandomization m_rnd;
        #endregion

        protected override void PerformMutate(IChromosome chromosome, float probability)
        {
            var binaryChromosome = chromosome as GraphChromosome;

            if (binaryChromosome == null)
            {
                throw new MutationException(this, "Must be a GarphChromosome!");
            }

            if (m_rnd.GetDouble() <= probability)
            {
                BitArray bitArray;
                do
                {
                    var index = m_rnd.GetInt(0, chromosome.Length);
                    binaryChromosome.FlipGene(index);
                    bitArray = binaryChromosome.GetBitArray();

                } while (!_chm.IsUnique(bitArray));
            }
        }
    }
}