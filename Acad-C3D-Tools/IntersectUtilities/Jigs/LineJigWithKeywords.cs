using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using Dreambuild.AutoCAD;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.Jigs
{
    public sealed class LineJigWithKeywords : EntityJig
    {
        private readonly Line _line;
        private readonly IReadOnlyList<LineJigKeyword> _keywords;
        private readonly Dictionary<string, LineJigKeyword> _keywordMap;
        private Point3d _end; // dynamic (rubber-banded) point

        public LineJigWithKeywords(Line line, IEnumerable<LineJigKeyword>? keywords)
            : base(line)
        {
            _line = line ?? throw new ArgumentNullException(nameof(line));
            _keywords = (keywords ?? Array.Empty<LineJigKeyword>()).ToList();
            _keywordMap = new Dictionary<string, LineJigKeyword>(StringComparer.OrdinalIgnoreCase);
            if (keywords != null)
                foreach (var key in keywords)
                    _keywordMap.Add(key.Global, key);
            _end = line.EndPoint;
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var display = string.Join("/", _keywords.Select(k => k.Local));
            var globals = string.Join(" ", _keywords.Select(k => k.Global));
            var msg = $"\nSpecify end point [{display}]";

            var opts = new JigPromptPointOptions("\nSpecify end point or choose an option:")
            {
                BasePoint = _line.StartPoint,
                UseBasePoint = true
            };

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
            _line.EndPoint = _end;
            return true;
        }

        /// Look up and run the matching keyword handler.
        public bool TryHandleKeyword(
            string keyword, Editor ed,
            out PromptStatus status)
        {
            if (_keywordMap.TryGetValue(keyword, out var k))
            {
                status = k.Handler(ed, _line);
                return true;
            }

            status = PromptStatus.OK;
            return false;
        }

        public static Line? GetLine(IEnumerable<LineJigKeyword>? keywords)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;            

            Point3d sp = Interaction.GetPoint("Select start location: ");
            if (sp.IsNull()) return null;

            Line ln = new Line(sp, Point3d.Origin);

            var jig = new LineJigWithKeywords(ln, keywords);

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
                    return ln;
                }
                else
                {
                    ln.Dispose();
                    return ln;
                }
            }
        }
    }
}
