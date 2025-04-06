﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.PhysarumAlgorithm
{
    internal class PhyNode
    {
        public string Id { get; set; }
        public bool IsSource { get; set; }
        public bool IsTerminal { get; set; }
        public double ExternalDemand { get; set; } = 0.0;
        public double Pressure { get; set; } = 0.0;

        public PhyNode(string id)
        {
            Id = id;
        }

        public override string ToString() => Id;
    }
}
