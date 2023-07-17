using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;

namespace DRITBL
{
    internal abstract class IntersectResult
    {
        public IntersectType IntersectType { get; set; }
        public string Vejnavn { get; set; }
        public string Vejklasse { get; set; }
        public string Belægning { get; set; }
        public string Navn { get; set; }
        public string DN1 { get; set; }
        public string DN2 { get; set; }
        public string System { get; set; }
        public string Serie { get; set; }
        public override string ToString() =>
            $"{Vejnavn};Vejkl. {Vejklasse};{Belægning};{Navn};;;{DN1};{DN2};{System};{Serie};";
    }

    internal class IntersectResultPipe : IntersectResult
    {
        public IntersectResultPipe()
        {
            IntersectType = IntersectType.Pipe;
            Navn = "Rør præsioleret";
        }
        public double Length { get; set; }
        public override string ToString() => base.ToString() + $"{Length.ToString(new CultureInfo("da-DK"))}";
    }
    internal class IntersectResultComponent : IntersectResult
    {
        public IntersectResultComponent()
        {
            IntersectType = IntersectType.Component;
        }
        public int Count { get; set; }
        public override string ToString() => base.ToString() + $"{Count};";
    }
    public enum IntersectType
    {
        Unknown,
        Pipe,
        Component
    }
}
