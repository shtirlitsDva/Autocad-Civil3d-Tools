using CommunityToolkit.Mvvm.ComponentModel;

using IntersectUtilities;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Vejklasser.Models
{
    public partial class VejnavnTilVejklasseModel : ObservableObject
    {
        [ObservableProperty] private string _vejnavn;
        [ObservableProperty] private int _vejklasse = 0;
        public List<BBR> BBRs { get; set; }
        public VejnavnTilVejklasseModel(string vejnavn, List<BBR> bBRs)
        {
            Vejnavn = vejnavn;
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
