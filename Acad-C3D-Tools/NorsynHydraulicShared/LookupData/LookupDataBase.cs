using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace NorsynHydraulicCalc.LookupData
{
    internal abstract class LookupDataBase : ILookupData
    {
        private double? tryLookUp(int T, Dictionary<int, double> dict)
        {
            if (dict.TryGetValue(T, out double value)) return value;
            else if (T >= LowT && T <= HighT)
            {
                int lowerkey = dict.Keys.Where(k => k < T).Max();
                int upperkey = dict.Keys.Where(k => k > T).Min();
                double lowerValue = dict[lowerkey];
                double upperValue = dict[upperkey];
                //Interpolate
                return lowerValue + (upperValue - lowerValue) * ((T - lowerkey) / (double)(upperkey - lowerkey));
            }
            return null;
        }
        public virtual double rho(int T)
        {
            double? result = tryLookUp(T, rhoD);
            if (result.HasValue) return result.Value * 1000;
            throw new ArgumentException($"Temperature out of range for \"rho\": {T}, allowed values: {LowT} - {HighT}.");
        }
        public virtual double cp(int T)
        {
            var result = tryLookUp(T, cpD);
            if (result.HasValue) return result.Value;
            throw new ArgumentException($"Temperature out of range for \"cp\": {T}, allowed values: {LowT} - {HighT}.");
        }
        public virtual double mu(int T)
        {
            var result = tryLookUp(T, muD);
            if (result.HasValue) return result.Value;
            throw new ArgumentException($"Temperature out of range for \"mu\": {T}, allowed values: {LowT} - {HighT}.");
        }
        public virtual double nu(int T) => throw new NotImplementedException();        

        protected abstract Dictionary<int, double> rhoD { get; }
        protected abstract Dictionary<int, double> cpD { get; }
        protected abstract Dictionary<int, double> nuD { get; }
        protected abstract Dictionary<int, double> muD { get; }

        protected abstract int LowT { get; }
        protected abstract int HighT { get; }
    }
}