using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRExport.Ntr
{
    internal class NtrLast
    {
        internal string Name { get; init; }
        internal string PB { get; init; }
        internal string TB { get; init; }
        internal string Gammed { get; init; }

        public NtrLast(string name, string pb, string tb, string gammed) 
        { 
            Name = name;
            PB = pb;
            TB = tb;
            Gammed = gammed;
        }

        public override string ToString() =>
            $"LAST NAME={Name} PB={PB} TB={TB} GAMMED={Gammed}";        
    }
}
