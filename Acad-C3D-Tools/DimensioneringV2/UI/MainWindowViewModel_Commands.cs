using Autodesk.AutoCAD.Geometry;

using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.MapCommands;

using IntersectUtilities.UtilsCommon;

using Mapsui.Extensions;

using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

using System.Linq;

using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DimensioneringV2.UI
{
    internal partial class MainWindowViewModel
    {
        #region MapCommands
        public RelayCommand CollectFeaturesCommand => new RelayCommand(CollectFeatures.Execute);
        public RelayCommand LoadElevationsCommand => new RelayCommand(LoadElevations.Execute);
        public RelayCommand PerformCalculationsSPDCommand => new(async () => await new CalculateSPD().Execute());
        public RelayCommand PerformCalculationsBFCommand => new(async () => await new CalculateBF().Execute());
        public AsyncRelayCommand PerformCalculationsGAOptimizedCommand => new(new CalculateGA().Execute);
        public RelayCommand PerformPriceCalc => new RelayCommand(() => new CalculatePrice().Execute(Features));
        public RelayCommand ShowForbrugereCommand => new RelayCommand(() => new ShowForbrugere().Execute(Features));
        public RelayCommand Dim2ImportDimsCommand => new RelayCommand(() => new Dim2ImportDims().Execute());
        public RelayCommand SaveResultCommand => new RelayCommand(() => new SaveResult().Execute());
        public RelayCommand LoadResultCommand => new RelayCommand(() => new LoadResult().Execute());
        public RelayCommand WriteToDwgCommand => new RelayCommand(() => new MapCommands.Write2Dwg().Execute());
        public RelayCommand WriteStikOgVejklasserCommand => new RelayCommand(() => new WriteStikOgVejklasser().Execute());
        public AsyncRelayCommand TestElevationsCommand => new AsyncRelayCommand(new TestElevations().Execute);
        public AsyncRelayCommand SampleGridCommand => new AsyncRelayCommand(new SampleGrid().Execute);
        public AsyncRelayCommand TestCacheCommand => new AsyncRelayCommand(new TestCache().Execute);
        public RelayCommand OpenCalcManagerCommand => new RelayCommand(() =>
        {
            var window = new CalcManager.CalcManagerWindow();
            window.Owner = System.Windows.Application.Current?.MainWindow;
            window.ShowDialog();
        });
        public RelayCommand OpenBbrDataCommand => new RelayCommand(() =>
        {
            var window = new BBRData.Views.BbrDataWindow();
            window.Show();
        });
        #endregion

        #region ZoomToExtents
        public RelayCommand PerformZoomToExtents => new RelayCommand(ZoomToExtents, () => true);
        private void ZoomToExtents()
        {
            var map = Mymap;
            if (map == null) return;

            var layer = map.Layers.FirstOrDefault(x => x.Name == "Features");
            if (layer == null) return;

            var extent = layer.Extent!.Grow(100);

            map.Navigator.ZoomToBox(extent);
        }
        #endregion

        #region SyncACWindow
        public RelayCommand SyncACWindowCommand => new RelayCommand(SyncACWindow, () => true);
        private void SyncACWindow()
        {
            var vp = Mymap.Navigator.Viewport;
            var mapExtent = vp.ToExtent();
            var minX = mapExtent.MinX;
            var minY = mapExtent.MinY;
            var maxX = mapExtent.MaxX;
            var maxY = mapExtent.MaxY;

            var trans = new CoordinateTransformationFactory().CreateFromCoordinateSystems(
                ProjectedCoordinateSystem.WebMercator,
                ProjectedCoordinateSystem.WGS84_UTM(32, true));

            var minPT = trans.MathTransform.Transform([minX, minY]);
            var maxPT = trans.MathTransform.Transform([maxX, maxY]);

            var minPt = new Point3d(minPT[0], minPT[1], 0);
            var maxPt = new Point3d(maxPT[0], maxPT[1], 0);

            var docs = AcAp.DocumentManager;
            var ed = docs.MdiActiveDocument.Editor;

            ed.Zoom(new Autodesk.AutoCAD.DatabaseServices.Extents3d(minPt, maxPt));
        }
        #endregion
    }
}
