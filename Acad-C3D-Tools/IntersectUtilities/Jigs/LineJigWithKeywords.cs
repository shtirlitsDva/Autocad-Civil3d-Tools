using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using Dreambuild.AutoCAD;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.Jigs
{
    public sealed class LineJigWithKeywords<TContext> : EntityJig
    {
        private readonly IReadOnlyList<LineJigKeyword<TContext>> _keywords;
        private readonly Dictionary<string, LineJigKeyword<TContext>> _keywordMap;
        private readonly TContext _context;
        private readonly ILineJigCallbacks? _callbacks;
        private Point3d _end; // dynamic (rubber-banded) point

        public LineJigWithKeywords(Line line,
            IEnumerable<LineJigKeyword<TContext>>? keywords,
            TContext context)
            : base(line)
        {
            _keywords = (keywords ?? Array.Empty<LineJigKeyword<TContext>>()).ToList();
            _keywordMap = new Dictionary<string, LineJigKeyword<TContext>>(StringComparer.OrdinalIgnoreCase);
            if (keywords != null)
                foreach (var key in keywords)
                {
                    _keywordMap[key.Global] = key;
                    if (!string.Equals(key.Local, key.Global, StringComparison.OrdinalIgnoreCase))
                        _keywordMap[key.Local] = key;
                }
            _context = context;
            _callbacks = null;
            _end = line.EndPoint;
        }

        public LineJigWithKeywords(Line line,
            IEnumerable<LineJigKeyword<TContext>>? keywords,
            TContext context, ILineJigCallbacks callbacks)
            : this(line, keywords, context)
        {
            _callbacks = callbacks;
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            Line line = (Line)Entity;

            var display = string.Join("|", _keywords.Select(k => k.StatusDisplay.Invoke(_context)));
            var opts = new JigPromptPointOptions($"\nSpecify end point [{display}]: ")
            {
                BasePoint = line.StartPoint,
                UseBasePoint = true
            };

            // Configure keywords (only those visible and enabled)
            foreach (var k in _keywords)
            {
                //if (!k.Visible || !k.Enabled) continue;
                opts.Keywords.Add(k.Global, k.Local, k.Local, k.Visible, k.Enabled);
            }

            // Allow keywords during point acquisition
            opts.UserInputControls = UserInputControls.GovernedByOrthoMode |
                                     UserInputControls.AcceptOtherInputString;

            var res = prompts.AcquirePoint(opts);

            if (res.Status == PromptStatus.Keyword)
            {
                // Do NOT change geometry here—just keep the jig alive.
                // The command loop will see PromptStatus.Keyword and invoke the handler.
                return SamplerStatus.OK;
            }

            if (_end.IsEqualTo(res.Value, Tolerance.Global))
                return SamplerStatus.NoChange;

            _end = res.Value;            
            return SamplerStatus.OK;
        }

        protected override bool Update()
        {
            Line line = (Line)Entity;
            line.EndPoint = _end;
            _callbacks?.OnSamplerPointChanged(line);
            return true;
        }

        /// Look up and run the matching keyword handler.
        public bool TryHandleKeyword(
            string keyword, Editor ed,
            out PromptStatus status)
        {
            if (_keywordMap.TryGetValue(keyword, out var k))
            {
                status = k.Handler(ed, (Line)Entity, _context);
                _callbacks?.OnKeyword(keyword);
                return true;
            }

            status = PromptStatus.OK;
            return false;
        }

        public static Line? GetLine(IEnumerable<LineJigKeyword<TContext>>? keywords, TContext context)
        {
            return GetLine(keywords, context, callbacks: null);
        }

        public static Line? GetLine(
            IEnumerable<LineJigKeyword<TContext>>? keywords,
            TContext context,
            ILineJigCallbacks? callbacks)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            Point3d sp = Interaction.GetPoint("Select start location: ");
            if (sp.IsNull()) return null;

            Line ln = new Line(sp, Point3d.Origin);

            var jig = callbacks == null
                ? new LineJigWithKeywords<TContext>(ln, keywords, context)
                : new LineJigWithKeywords<TContext>(ln, keywords, context, callbacks);

            while (true)
            {
                var drag = ed.Drag(jig);

                if (drag.Status == PromptStatus.Keyword)
                {
                    if (!jig.TryHandleKeyword(drag.StringResult, ed, out var ps))
                        ed.WriteMessage($"\nUnknown option: {drag.StringResult}");

                    if (ps == PromptStatus.Cancel || ps == PromptStatus.Error)
                    {
                        ln.Dispose();
                        return null;
                    }
                    continue; // resume jigging
                }
                else if (drag.Status == PromptStatus.OK)
                {
                    callbacks?.OnCommit(ln);
                    return ln;
                }
                else
                {
                    callbacks?.OnCancelLevel1();
                    ln.Dispose();
                    return null;
                }
            }
        }
    }
}
