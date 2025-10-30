using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;

namespace NTRExport.Ntr
{
    internal sealed class NtrDnCatalog
    {
        private readonly PipeSystemEnum _system;
        private readonly PipeTypeEnum _type;
        private readonly PipeSeriesEnum _series;
        private readonly bool _isTwin;
        private const string Norm = "EN 10217-2";

        public NtrDnCatalog(PipeSystemEnum system, PipeTypeEnum type, PipeSeriesEnum series, bool isTwin)
        {
            _system = system; _type = type; _series = series; _isTwin = isTwin;
        }        

        // Builds IS and DN lines with naming: DN{dn}.{x} (x = s for bonded, t for twin) and ISOTYP=FJV{dn}
        // Series suffix added: DN{dn}.{s/t}s{1/2/3} for S1/S2/S3, DN{dn}.{s/t} for Undefined
        public IEnumerable<string> BuildRecords(IEnumerable<int> dns)
        {
            var emittedIs = new HashSet<int>();
            var seriesSuffix = _series switch
            {
                PipeSeriesEnum.S1 => "s1",
                PipeSeriesEnum.S2 => "s2",
                PipeSeriesEnum.S3 => "s3",
                _ => string.Empty
            };
            
            foreach (var dn in dns.Distinct().OrderBy(x => x))
            {
                // IS type per DN
                var suffix = _type == PipeTypeEnum.Enkelt ? "s" : "t";
                var isName = $"FJV{dn}.{suffix}";
                var dnName = $"DN{dn}.{suffix}{seriesSuffix}";
                
                if (emittedIs.Add(dn))
                {
                    // Defaults per example
                    var gam = 61; 
                    var gambl = 944; 
                    double dickebl = PipeScheduleV2.GetPipekThk(
                        _system, dn, _type, _series);
                    yield return $"IS NAME={isName} GAM={gam} DICKEBL={dickebl:0.###} GAMBL={gambl}";
                }

                // Base dimensions
                var da = PipeScheduleV2.GetPipeOd(_system, dn);                
                var s = PipeScheduleV2.GetPipeThk(_system, dn);
                var kod = PipeScheduleV2.GetPipeKOd(_system, dn, _type, _series);

                // Single/bonded vs Twin
                if (!_isTwin)
                {
                    var isodickeS = Math.Max(0.0, (kod - da) / 2.0);
                    yield return 
                        $"DN " +
                        $"NAME={dnName} " +
                        $"DA={da:0.###} " +
                        $"S={s:0.###} " +
                        $"ISOTYP={isName} " +
                        $"ISODICKE={isodickeS:0.###} NORM='{Norm}'";
                }
                else
                {
                    var cTwin = Math.PI * kod;
                    var cSingle = cTwin / 2.0;
                    var odSingle = cSingle / Math.PI;
                    var isodickeT = Math.Max(0.0, (odSingle - da) / 2.0);
                    yield return $"DN NAME={dnName} DA={da:0.###} S={s:0.###} ISOTYP={isName} ISODICKE={isodickeT:0.###} NORM='{Norm}'";
                }
            }
        }        
    }
}