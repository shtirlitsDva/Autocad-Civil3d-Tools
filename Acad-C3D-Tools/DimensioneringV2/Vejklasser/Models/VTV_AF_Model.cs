using CommunityToolkit.Mvvm.ComponentModel;

using DimensioneringV2.GraphFeatures;

using IntersectUtilities;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Vejklasser.Models
{
    public class VTV_AF_Model : VejnavnTilVejklasseBase
    {        
        public List<AnalysisFeature> AFs { get; set; }
        public VTV_AF_Model(string vejnavn, List<AnalysisFeature> afs) : base(vejnavn)
        {
            AFs = afs;

            //Determine if vejklasse exists
            var existVejklasser = afs
                .Select(x => x["Vejklasse"])
                .Distinct();            

            if (existVejklasser.Where(x => x != null).Count() == 1)
                Vejklasse = existVejklasser.Cast<int>().First();
            else Vejklasse = 0;
        }
    }
}
