using CommunityToolkit.Mvvm.ComponentModel;

using DimensioneringV2.Vejklasser.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Vejklasser.Models
{
    public abstract partial class VejnavnTilVejklasseBase :
        ObservableObject, IVejnavnTilVejklasse
    {
        [ObservableProperty] private string _vejnavn;
        [ObservableProperty] private int _vejklasse = 0;

        protected VejnavnTilVejklasseBase(string vejnavn)
        {
            Vejnavn = vejnavn;
        }
    }
}
