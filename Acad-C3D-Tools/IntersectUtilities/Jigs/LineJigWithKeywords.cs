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
	public sealed class LineJigWithKeywords<TContext> : EntityJig
	{
		private readonly Line _line;
		private readonly IReadOnlyList<LineJigKeyword<TContext>> _keywords;
		private readonly Dictionary<string, LineJigKeyword<TContext>> _keywordMap;
		private readonly TContext _context;
		private Point3d _end; // dynamic (rubber-banded) point

		public LineJigWithKeywords(Line line, IEnumerable<LineJigKeyword<TContext>>? keywords, TContext context)
			: base(line)
		{
			_line = line ?? throw new ArgumentNullException(nameof(line));
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
			_end = line.EndPoint;
		}

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {            
            var display = string.Join("|", _keywords.Select(k => k.StatusDisplay.Invoke(_context)));
            var opts = new JigPromptPointOptions($"\nSpecify end point [{display}]: ")
            {
                BasePoint = _line.StartPoint,
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
                status = k.Handler(ed, _line, _context);
                return true;
            }

            status = PromptStatus.OK;
            return false;
        }

        public static Line? GetLine(IEnumerable<LineJigKeyword<TContext>>? keywords, TContext context)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            Point3d sp = Interaction.GetPoint("Select start location: ");
            if (sp.IsNull()) return null;

            Line ln = new Line(sp, Point3d.Origin);

            var jig = new LineJigWithKeywords<TContext>(ln, keywords, context);

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
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets multiple lines continuously, where each subsequent line starts at the end point of the previous line.
        /// First Escape returns to start point selection, second Escape exits the command.
        /// </summary>
        public static List<Line> GetLinesContinuous(IEnumerable<LineJigKeyword<TContext>>? keywords, TContext context)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            var lines = new List<Line>();
            Point3d? previousEndPoint = null;
            bool isFirstLine = true;

            while (true)
            {
                Point3d sp;
                
                if (isFirstLine)
                {
                    sp = Interaction.GetPoint("Select start location: ");
                    if (sp.IsNull()) return lines; // Exit command
                    isFirstLine = false;
                }
                else
                {
                    if (previousEndPoint.HasValue)
                    {
                        sp = previousEndPoint.Value;
                    }
                    else
                    {
                        sp = Interaction.GetPoint("Select start location: ");
                        if (sp.IsNull()) return lines; // Exit command
                    }
                }

                Line ln = new Line(sp, Point3d.Origin);

                var jig = new LineJigWithKeywords<TContext>(ln, keywords, context);

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
                            return lines;
                        }
                        continue; // resume jigging
                    }
                    else if (drag.Status == PromptStatus.OK)
                    {
                        // Store the end point for the next line and add current line to collection
                        previousEndPoint = ln.EndPoint;
                        lines.Add(ln);
                        
                        // Ask if user wants to continue
                        var continuePrompt = new PromptKeywordOptions("\nContinue with next line? ");
                        continuePrompt.Keywords.Add("Yes", "Yes", "Yes", true, true);
                        continuePrompt.Keywords.Add("No", "No", "No", true, true);
                        continuePrompt.Keywords.Add("Restart", "Restart", "Restart", true, true);
                        
                        var continueResult = ed.GetKeywords(continuePrompt);
                        if (continueResult.Status == PromptStatus.Keyword)
                        {
                            if (continueResult.StringResult == "No")
                            {
                                return lines; // Exit with collected lines
                            }
                            else if (continueResult.StringResult == "Restart")
                            {
                                // Clear all lines and start over
                                foreach (var line in lines)
                                {
                                    line.Dispose();
                                }
                                lines.Clear();
                                isFirstLine = true;
                                previousEndPoint = null;
                                break; // Break out of inner while loop to restart
                            }
                            else // Yes
                            {
                                break; // Break out of inner while loop to continue with next line
                            }
                        }
                        else if (continueResult.Status == PromptStatus.Cancel)
                        {
                            // First Escape - return to "Select First Point" prompt
                            ed.WriteMessage("\nReturning to start point selection. Press Escape again to exit.");
                            isFirstLine = true;
                            previousEndPoint = null;
                            break; // Break out of inner while loop to restart with new start point
                        }
                        else
                        {
                            return lines; // Exit with collected lines
                        }
                    }
                    else if (drag.Status == PromptStatus.Cancel)
                    {
                        // First Escape - return to "Select First Point" prompt
                        ln.Dispose();
                        ed.WriteMessage("\nReturning to start point selection. Press Escape again to exit.");
                        isFirstLine = true;
                        previousEndPoint = null;
                        break; // Break out of inner while loop to restart with new start point
                    }
                    else
                    {
                        ln.Dispose();
                        return lines;
                    }
                }
            }
        }
    }
}
