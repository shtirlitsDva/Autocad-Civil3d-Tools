using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphFeatures
{
    internal interface IInfoForFeature
    {
        public IEnumerable<PropertyItem> PropertiesToDataGrid();
    }
}
