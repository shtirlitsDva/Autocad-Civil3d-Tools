using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

using System;

namespace IntersectUtilities.Jigs
{
	public sealed class LineJigKeyword<TContext>
	{
		public string Global { get; }
		public string Local { get; }

		/// Handler runs when the keyword is chosen.
		/// Return PromptStatus.OK to continue, or Cancel/Error to abort.
		public Func<Editor, Line, TContext, PromptStatus> Handler { get; }

		public bool Visible { get; set; }
		public bool Enabled { get; set; }

		public LineJigKeyword(
			string global, string local,
			Func<Editor, Line, TContext, PromptStatus> handler,
			bool visible = true, bool enabled = true)
		{
			Global = global; Local = local;
			Handler = handler ?? throw new ArgumentNullException(nameof(handler));
			Visible = visible; Enabled = enabled;
		}
	}
}