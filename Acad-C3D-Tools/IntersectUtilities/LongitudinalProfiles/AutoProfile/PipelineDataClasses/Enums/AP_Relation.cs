using IntersectUtilities.PipeScheduleV2;

using static IntersectUtilities.UtilsCommon.Utils;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{    
    internal enum Relation
    {
        Unknown,
        Inside,
        Outside,
        Overlaps
    }
}