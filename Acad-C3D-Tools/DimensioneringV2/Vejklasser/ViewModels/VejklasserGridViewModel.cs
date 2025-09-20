using CommunityToolkit.Mvvm.ComponentModel;

using DimensioneringV2.Vejklasser.Interfaces;
using DimensioneringV2.Vejklasser.Models;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Vejklasser.ViewModels
{
    public partial class VejklasserGridViewModel : ObservableObject
    {
        public ObservableCollection<IVejnavnTilVejklasse> Models { get; } = new();        
        public VejklasserGridViewModel() {}
        public void ReceiveData(IEnumerable<IVejnavnTilVejklasse> models)
        {
            Models.Clear();
            foreach (var model in models)
            {
                Models.Add(model);
            }
        }            
    } 
}
