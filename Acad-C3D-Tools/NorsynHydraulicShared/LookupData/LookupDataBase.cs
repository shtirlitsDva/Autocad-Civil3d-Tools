using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace NorsynHydraulicCalc.LookupData
{
    internal abstract class LookupDataBase : ILookupData
    {
        private double? tryLookUp(double T, Dictionary<int, double> dict)
        {
            // First, try exact match if T is a whole number
            int intT = (int)T;
            if (T == intT && dict.TryGetValue(intT, out double exactValue))
            {
                return exactValue;
            }
            
            // Check if T is within valid range
            if (T >= LowT && T <= HighT)
            {
                // Find the keys that bracket T
                var lowerKeys = dict.Keys.Where(k => k < T);
                var upperKeys = dict.Keys.Where(k => k > T);
                
                if (lowerKeys.Any() && upperKeys.Any())
                {
                    double lowerkey = lowerKeys.Max();
                    double upperkey = upperKeys.Min();
                    double lowerValue = dict[(int)lowerkey];
                    double upperValue = dict[(int)upperkey];
                    // Interpolate using double arithmetic
                    return lowerValue + (upperValue - lowerValue) * ((T - lowerkey) / (upperkey - lowerkey));
                }
            }
            return null;
        }
        public virtual double rho(double T)
        {
            double? result = tryLookUp(T, rhoD);
            if (result.HasValue) return result.Value * 1000;
            throw new ArgumentException($"Temperature out of range for \"rho\": {T}, allowed values: {LowT} - {HighT}.");
        }
        public virtual double cp(double T)
        {
            var result = tryLookUp(T, cpD);
            if (result.HasValue) return result.Value;
            throw new ArgumentException($"Temperature out of range for \"cp\": {T}, allowed values: {LowT} - {HighT}.");
        }
        public virtual double mu(double T)
        {
            var result = tryLookUp(T, muD);
            if (result.HasValue) return result.Value;
            throw new ArgumentException($"Temperature out of range for \"mu\": {T}, allowed values: {LowT} - {HighT}.");
        }
        public virtual double nu(double T) => throw new NotImplementedException();        

        protected abstract Dictionary<int, double> rhoD { get; }
        protected abstract Dictionary<int, double> cpD { get; }
        protected abstract Dictionary<int, double> nuD { get; }
        protected abstract Dictionary<int, double> muD { get; }

        protected abstract int LowT { get; }
        protected abstract int HighT { get; }
    }
}