using CommunityToolkit.Mvvm.ComponentModel;

using IntersectUtilities;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Vejklasser.Models
{
    public class VTV_BBR_Model : VejnavnTilVejklasseBase
    {        
        public List<BBR> BBRs { get; set; }
        public VTV_BBR_Model(string vejnavn, List<BBR> bBRs) : base(vejnavn)
        {            
            BBRs = bBRs;

            //Determine if vejklasse exists
            var existVejklasser = bBRs
                .Select(x => x.Vejklasse)
                .Distinct();

            if (existVejklasser.Count() == 1) Vejklasse = existVejklasser.First();
            else Vejklasse = 0;
        }
    }
}
