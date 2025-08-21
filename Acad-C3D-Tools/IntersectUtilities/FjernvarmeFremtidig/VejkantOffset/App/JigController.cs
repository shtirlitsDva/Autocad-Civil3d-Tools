using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts;
using IntersectUtilities.Jigs;

using System.Collections.Generic;

using ADB = Autodesk.AutoCAD.DatabaseServices;
using Dreambuild.AutoCAD;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App
{
    // Generic controller: analysis + inspector model + context type
    internal sealed class JigController<TAnalysis, TInspectorModel, TContext>
    {
        private readonly IAnalyzer<Line, TAnalysis> _analyzer;
        private readonly IRenderer _renderer;
        private readonly IVisualizer<TInspectorModel> _visualizer;
        private readonly ISceneComposer<TAnalysis> _sceneComposer;
        private readonly IInspectorMapper<TAnalysis, TInspectorModel> _inspectorMapper;

        public JigController(
            IAnalyzer<Line, TAnalysis> analyzer,
            IRenderer renderer,
            IVisualizer<TInspectorModel> visualizer,
            ISceneComposer<TAnalysis> sceneComposer,
            IInspectorMapper<TAnalysis, TInspectorModel> inspectorMapper)
        {
            _analyzer = analyzer;
            _renderer = renderer;
            _visualizer = visualizer;
            _sceneComposer = sceneComposer;
            _inspectorMapper = inspectorMapper;
        }

        public void Run(IEnumerable<LineJigKeyword<TContext>> keywords, TContext context)
        {
            var callbacks = new JigCallbacksAdapter<TAnalysis, TInspectorModel, TContext>(this);

            LineJigWithKeywords<TContext>.RunContinuous(
                keywords,
                context,
                callbacks,
                acquireStartPoint: () =>
                {
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
        }

        public void OnCancelLevel2()
        {
            _renderer.Clear();
            _visualizer.Hide();
        }
    }
}
