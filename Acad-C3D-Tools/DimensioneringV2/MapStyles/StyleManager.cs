using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapStyles
{
    internal class StyleManager
    {
        private IMapStyle _styleLabelsOn;
        private IMapStyle _styleLabelsOff;
        private IMapStyle _currentStyle;
        public IMapStyle CurrentStyle => _currentStyle;

        public StyleManager(IMapStyle styleLabelsOn, IMapStyle styleLabelsOff)
        {
            _styleLabelsOn = styleLabelsOn;
            _styleLabelsOff = styleLabelsOff;
            _currentStyle = _styleLabelsOn;
        }

        public void Switch()
        {
            _currentStyle = _currentStyle == _styleLabelsOn ? _styleLabelsOff : _styleLabelsOn;
        }
    }
}
