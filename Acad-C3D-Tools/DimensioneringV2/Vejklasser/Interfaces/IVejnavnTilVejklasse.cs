using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Vejklasser.Interfaces
{
    public interface IVejnavnTilVejklasse
    {
        public string Vejnavn { get; }
        public int Vejklasse { get; set; }
    }
}
