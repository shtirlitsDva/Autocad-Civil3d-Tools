using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Models;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.Models;
using IntersectUtilities.Jigs;

using System.Collections.Generic;

using Dreambuild.AutoCAD;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App
{
    // Specific controller for Vejkant analysis with intersection visualization
    internal sealed class JigController
    {
        private readonly IAnalyzer<Line, VejkantAnalysis> _analyzer;
        private readonly IRenderer _renderer;
        private readonly IVisualizer<IntersectionVisualizationModel> _visualizer;
        private readonly ISceneComposer<VejkantAnalysis> _sceneComposer;
        private readonly IInspectorMapper<VejkantAnalysis, IntersectionVisualizationModel> _inspectorMapper;
        private bool _ignoreNextLevel2;

        public JigController(
            IAnalyzer<Line, VejkantAnalysis> analyzer,
            IRenderer renderer,
            IVisualizer<IntersectionVisualizationModel> visualizer,
            ISceneComposer<VejkantAnalysis> sceneComposer,
            IInspectorMapper<VejkantAnalysis, IntersectionVisualizationModel> inspectorMapper)
        {
            _analyzer = analyzer;
            _renderer = renderer;
            _visualizer = visualizer;
            _sceneComposer = sceneComposer;
            _inspectorMapper = inspectorMapper;
            _ignoreNextLevel2 = false;
        }

        public void Run(IEnumerable<LineJigKeyword<VejkantOffsetSettings>> keywords, VejkantOffsetSettings context)
        {
            var callbacks = new JigCallbacksAdapter(this);
            // Revert: show the palette immediately (original behavior)
            _visualizer.Show();
            _ignoreNextLevel2 = false;

            LineJigWithKeywords<VejkantOffsetSettings>.RunContinuous(
                keywords,
                context,
                callbacks,
                acquireStartPoint: () =>
                {
                    // Ensure palette is shown but do not steal focus
                    IntersectUtilities.UtilsCommon.Utils.prdDbg("[Jig] acquireStartPoint");
                    var sp = Interaction.GetPoint("Select start location: ");
                    if (sp.IsNull()) return null;
                    return sp;
                }
            );
        }

        public void OnSamplerPointChanged(Line line)
        {
            var analysis = _analyzer.Analyze(line);
            _renderer.Show(_sceneComposer.Compose(analysis, line));
            _visualizer.Update(_inspectorMapper.Map(analysis, line));
            //IntersectUtilities.UtilsCommon.Utils.prdDbg("[Jig] Sampler tick -> Visualizer.Update called");
        }

        public void OnCommit(Line line)
        {
            var analysis = _analyzer.Analyze(line);
            _renderer.Clear();
            _visualizer.Update(_inspectorMapper.Map(analysis, line));
            _analyzer.Commit(analysis);
        }

        public void OnCancelLevel1()
        {
            _renderer.Clear();
            // Keep the visualizer open; user can continue to select first point
            IntersectUtilities.UtilsCommon.Utils.prdDbg("[Jig] OnCancelLevel1");
            // Some jigs emit a Level2 immediately after Level1. Ignore the next Level2 once.
            _ignoreNextLevel2 = true;
        }

        public void OnCancelLevel2()
        {
            _renderer.Clear();
            if (_ignoreNextLevel2)
            {
                // Ignore the first Level2 that follows Level1 automatically
                _ignoreNextLevel2 = false;
                IntersectUtilities.UtilsCommon.Utils.prdDbg("[Jig] OnCancelLevel2 ignored (paired with Level1)");
            }
            else
            {
                _visualizer.Hide();
                IntersectUtilities.UtilsCommon.Utils.prdDbg("[Jig] OnCancelLevel2 (visualizer hidden)");
            }
        }
    }
}
