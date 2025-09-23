using DimensioneringV2.UI;

using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapStyles
{
    internal class StyleManager
    {
        private static bool _labelsOn = true;

        private IMapStyle? _styleLabelsOn;
        private IMapStyle? _styleLabelsOff;
        private IMapStyle? _currentStyle;
        public IMapStyle CurrentStyle => _currentStyle!;

        public StyleManager(MapPropertyEnum propName)
        {
            IMapStyle? sOn = null;
            IMapStyle? sOff = null;
            switch (propName)
            {
                case MapPropertyEnum.Default:
                    sOn = new StyleDefault();
                    sOff = new StyleDefault();
                    break;
                case MapPropertyEnum.Basic:
                    sOn = new StyleBasic();
                    sOff = new StyleBasic();
                    break;
                case MapPropertyEnum.Bygninger:
                    sOn = new StyleMapProperty_WithLabels<int>(f => f.NumberOfBuildingsSupplied);
                    sOff = new StyleMapProperty_NoLabels<int>(f => f.NumberOfBuildingsSupplied);
                    break;
                case MapPropertyEnum.Units:
                    sOn = new StyleMapProperty_WithLabels<int>(f => f.NumberOfUnitsSupplied);
                    sOff = new StyleMapProperty_NoLabels<int>(f => f.NumberOfUnitsSupplied);
                    break;
                case MapPropertyEnum.HeatingDemand:
                    sOn = new StyleMapProperty_WithLabels<double>(f => f.HeatingDemandSupplied);
                    sOff = new StyleMapProperty_NoLabels<double>(f => f.HeatingDemandSupplied);
                    break;
                case MapPropertyEnum.FlowSupply:
                    sOn = new StyleMapProperty_WithLabels<double>(f => f.FlowSupply);
                    sOff = new StyleMapProperty_NoLabels<double>(f => f.FlowSupply);
                    break;
                case MapPropertyEnum.FlowReturn:
                    sOn = new StyleMapProperty_WithLabels<double>(f => f.FlowReturn);
                    sOff = new StyleMapProperty_NoLabels<double>(f => f.FlowReturn);
                    break;
                case MapPropertyEnum.PressureGradientSupply:
                    sOn = new StyleMapProperty_WithLabels<double>(f => f.PressureGradientSupply);
                    sOff = new StyleMapProperty_NoLabels<double>(f => f.PressureGradientSupply);
                    break;
                case MapPropertyEnum.PressureGradientReturn:
                    sOn = new StyleMapProperty_WithLabels<double>(f => f.PressureGradientReturn);
                    sOff = new StyleMapProperty_NoLabels<double>(f => f.PressureGradientReturn);
                    break;
                case MapPropertyEnum.VelocitySupply:
                    sOn = new StyleMapProperty_WithLabels<double>(f => f.VelocitySupply);
                    sOff = new StyleMapProperty_NoLabels<double>(f => f.VelocitySupply);
                    break;
                case MapPropertyEnum.VelocityReturn:
                    sOn = new StyleMapProperty_WithLabels<double>(f => f.VelocityReturn);
                    sOff = new StyleMapProperty_NoLabels<double>(f => f.VelocityReturn);
                    break;
                case MapPropertyEnum.UtilizationRate:
                    sOn = new StyleMapProperty_WithLabels<double>(f => f.UtilizationRate);
                    sOff = new StyleMapProperty_NoLabels<double>(f => f.UtilizationRate);
                    break;
                case MapPropertyEnum.Pipe:
                    sOn = new StyleMapPipeSize_WithLabels(f => f.Dim);
                    sOff = new StyleMapPipeSize_NoLabels(f => f.Dim);
                    break;
                case MapPropertyEnum.Bridge:
                    sOn = new StyleMapBridge_NoLabels(f => f.IsBridge);
                    sOff = new StyleMapBridge_NoLabels(f => f.IsBridge);
                    break;
                case MapPropertyEnum.SubGraphId:
                    sOn = new StyleMapProperty_WithLabels<int>(f => f.SubGraphId);
                    sOff = new StyleMapProperty_NoLabels<int>(f => f.SubGraphId);
                    break;
                case MapPropertyEnum.CriticalPath:
                    //sOn = new StyleMapCriticalPath_WithLabels(f => f.IsCriticalPath);
                    sOff = new StyleMapCriticalPath_NoLabels(f => f.IsCriticalPath);
                    break;
                default:
                    throw new Exception("Unknown property name!");
            }

            _styleLabelsOn = sOn;
            _styleLabelsOff = sOff;
            _currentStyle = _labelsOn ? _styleLabelsOn : _styleLabelsOff;
        }

        public void Switch()
        {
            _currentStyle = _currentStyle == _styleLabelsOn ? _styleLabelsOff : _styleLabelsOn;
            _labelsOn = !_labelsOn;
        }
    }
}