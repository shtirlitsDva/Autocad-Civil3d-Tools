using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Drawing;
using System.Windows.Forms;

using static IntersectUtilities.UtilsCommon.Utils;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities.PlanDetailing.LineTypes
{
    internal static class LineTypes
    {
        public static void linetypeX(
            string lineTypeName,
            string text,
            string textStyleName,
            Database? db = null
        )
        {
            db ??= Application.DocumentManager.MdiActiveDocument.Database;
            Transaction tx = db.TransactionManager.StartTransaction();
            using (tx)
            {
                try
                {
                    // We'll use the textstyle table to access
                    // the "Standard" textstyle for our text segment
                    TextStyleTable tt = (TextStyleTable)
                        tx.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    // Get the linetype table from the drawing
                    LinetypeTable ltt = (LinetypeTable)
                        tx.GetObject(db.LinetypeTableId, OpenMode.ForWrite);
                    // Get layer table
                    LayerTable lt = tx.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                    //**************************************
                    //Change name of line type to create new and text value
                    //**************************************
                    prdDbg($"Remember to create text style: {textStyleName}!!!");
                    if (tt.Has(textStyleName))
                    {
                        prdDbg("Text style exists!");
                    }
                    else
                    {
                        prdDbg($"Text style {textStyleName} DOES NOT exist!");
                        tx.Abort();
                        return;
                    }
                    List<string> layersToChange = new List<string>();
                    if (ltt.Has(lineTypeName))
                    {
                        Oid existingId = ltt[lineTypeName];
                        Oid placeHolderId = ltt["Continuous"];
                        foreach (Oid oid in lt)
                        {
                            LayerTableRecord ltr = oid.Go<LayerTableRecord>(tx);
                            if (ltr.LinetypeObjectId == existingId)
                            {
                                ltr.CheckOrOpenForWrite();
                                ltr.LinetypeObjectId = placeHolderId;
                                layersToChange.Add(ltr.Name);
                            }
                        }
                        LinetypeTableRecord exLtr = existingId.Go<LinetypeTableRecord>(
                            tx,
                            OpenMode.ForWrite
                        );
                        exLtr.Erase(true);
                    }
                    // Create our new linetype table record...
                    LinetypeTableRecord lttr = new LinetypeTableRecord();
                    // ... and set its properties
                    lttr.Name = lineTypeName;
                    lttr.AsciiDescription = $"{text} ---- {text} ---- {text} ----";
                    lttr.PatternLength = 0.9;
                    //IsScaledToFit unsure what it does, can't see any difference
                    lttr.IsScaledToFit = true;

                    // A single line dash length
                    double dL = 5;

                    // Convert your text into segments that keep the total dash count within AutoCAD's
                    // 12-dash limitation. Pattern layout:
                    //   line dash | forward segments | half line | X | half line | reversed segments | X
                    const int maxDashCapacity = 12;
                    const int baseDashUsage = 6;
                    int maxTextSegments = Math.Max(1, Math.Min(text.Length, (maxDashCapacity - baseDashUsage) / 2));
                    List<string> forwardSegments = new List<string>();
                    int targetSegments = Math.Max(1, Math.Min(maxTextSegments, text.Length));
                    int baseSegmentLength = text.Length / targetSegments;
                    int remainder = text.Length % targetSegments;
                    int cursor = 0;
                    for (int i = 0; i < targetSegments; i++)
                    {
                        int segmentLength = baseSegmentLength + (i < remainder ? 1 : 0);
                        if (segmentLength <= 0)
                            continue;
                        forwardSegments.Add(text.Substring(cursor, segmentLength));
                        cursor += segmentLength;
                    }
                    if (cursor < text.Length && forwardSegments.Count > 0)
                    {
                        forwardSegments[^1] += text.Substring(cursor);
                    }

                    int segmentCount = forwardSegments.Count;
                    if (segmentCount == 0)
                        throw new System.Exception("Unable to generate text segments for linetype pattern.");

                    // A small horizontal buffer on each side of each character
                    double textBuffer = 0.05;
                    double innerSegmentBuffer = segmentCount > 1 ? 0.0 : textBuffer;

                    int dashCount = (2 * segmentCount) + baseDashUsage;
                    lttr.NumDashes = dashCount;

                    if (dashCount > maxDashCapacity)
                        throw new System.Exception("Computed dash count exceeds AutoCAD limit.");

                    List<string> reverseSegments = forwardSegments
                        .AsEnumerable()
                        .Reverse()
                        .ToList();

                    // We need to measure each character to set dash lengths properly.
                    Oid textStyleId = tt[textStyleName];
                    var ts = new Autodesk.AutoCAD.GraphicsInterface.TextStyle();
                    ts.FromTextStyleTableRecord(textStyleId);
                    ts.TextSize = 0.9; // same size you used before

                    // We'll track total pattern length to finalize lttr.PatternLength
                    double totalPatternLen = 0;
                    int firstHalfLineIndex = 1 + segmentCount;
                    string xText = "X";
                    var xExt = ts.ExtentsBox(xText, true, false, null);
                    double xCharWidth = xExt.MaxPoint.X - xExt.MinPoint.X;
                    double xDashLength = -(Math.Max(0.0, xCharWidth) + 2 * textBuffer);
                    double availableForLines = Math.Max(0.0, dL - Math.Abs(xDashLength));
                    double halfLineLength = availableForLines / 2.0;

                    lttr.SetDashLengthAt(0, halfLineLength);
                    totalPatternLen += halfLineLength;

                    // Half-line dash after forward text group (before the X)
                    lttr.SetDashLengthAt(firstHalfLineIndex, halfLineLength);
                    totalPatternLen += halfLineLength;

                    // -----------------------------
                    // FORWARD CHARACTERS (DASH #1+)
                    // -----------------------------
                    for (int i = 0; i < segmentCount; i++)
                    {
                        // This dash index is consecutive after dash 0
                        int dashIndex = i + 1;

                        string segment = forwardSegments[i];
                        var extBox = ts.ExtentsBox(segment, true, false, null);
                        double segmentWidth = extBox.MaxPoint.X - extBox.MinPoint.X;

                        double leftBuffer = i == 0 ? textBuffer : innerSegmentBuffer;
                        double rightBuffer = i == segmentCount - 1 ? textBuffer : innerSegmentBuffer;

                        double dashLen = -(segmentWidth + leftBuffer + rightBuffer);
                        lttr.SetDashLengthAt(dashIndex, dashLen);

                        lttr.SetShapeStyleAt(dashIndex, textStyleId);
                        lttr.SetShapeNumberAt(dashIndex, 0);
                        lttr.SetShapeScaleAt(dashIndex, 0.9);
                        lttr.SetShapeIsUcsOrientedAt(dashIndex, false);
                        lttr.SetShapeRotationAt(dashIndex, 0);
                        lttr.SetTextAt(dashIndex, segment);

                        lttr.SetShapeOffsetAt(
                            dashIndex,
                            new Vector2d(-(segmentWidth - rightBuffer), -0.45)
                        );

                        totalPatternLen += (segmentWidth + leftBuffer + rightBuffer);
                    }

                    int xBetweenIndex = firstHalfLineIndex + 1;
                    lttr.SetDashLengthAt(xBetweenIndex, xDashLength);
                    lttr.SetShapeStyleAt(xBetweenIndex, textStyleId);
                    lttr.SetShapeNumberAt(xBetweenIndex, 0);
                    lttr.SetShapeScaleAt(xBetweenIndex, 0.9);
                    lttr.SetShapeIsUcsOrientedAt(xBetweenIndex, false);
                    lttr.SetShapeRotationAt(xBetweenIndex, 0);
                    lttr.SetTextAt(xBetweenIndex, xText);
                    lttr.SetShapeOffsetAt(
                        xBetweenIndex,
                        new Vector2d(-(xCharWidth + textBuffer), -0.45)
                    );
                    totalPatternLen += Math.Abs(xDashLength);

                    int secondHalfLineIndex = xBetweenIndex + 1;
                    lttr.SetDashLengthAt(secondHalfLineIndex, halfLineLength);
                    totalPatternLen += halfLineLength;

                    // ------------------------------------
                    // ROTATED CHARACTERS (DASH #...)
                    // ------------------------------------
                    int reverseStartIndex = secondHalfLineIndex + 1;
                    for (int i = 0; i < segmentCount; i++)
                    {
                        int dashIndex = reverseStartIndex + i;

                        string segment = reverseSegments[i];
                        var extBox = ts.ExtentsBox(segment, true, false, null);
                        double segmentWidth = extBox.MaxPoint.X - extBox.MinPoint.X;

                        double leftBuffer = i == 0 ? textBuffer : innerSegmentBuffer;
                        double rightBuffer = i == segmentCount - 1 ? textBuffer : innerSegmentBuffer;

                        double dashLen = -(segmentWidth + leftBuffer + rightBuffer);
                        lttr.SetDashLengthAt(dashIndex, dashLen);

                        lttr.SetShapeStyleAt(dashIndex, textStyleId);
                        lttr.SetShapeNumberAt(dashIndex, 0);
                        lttr.SetShapeScaleAt(dashIndex, 0.9);
                        lttr.SetShapeIsUcsOrientedAt(dashIndex, false);

                        // Rotation 180° for reversed text
                        lttr.SetShapeRotationAt(dashIndex, Math.PI);
                        lttr.SetTextAt(dashIndex, segment);

                        // Offset is local to the dash. We'll place it near the
                        // "other end" of the dash: -textBuffer in X, etc.
                        lttr.SetShapeOffsetAt(
                            dashIndex,
                            new Vector2d(0, 0.45)
                        );

                        totalPatternLen += (segmentWidth + leftBuffer + rightBuffer);
                    }

                    // Trailing half-line before final X
                    int thirdHalfLineIndex = reverseStartIndex + segmentCount;
                    lttr.SetDashLengthAt(thirdHalfLineIndex, halfLineLength);
                    totalPatternLen += halfLineLength;

                    // ------------------------------------
                    // Trailing X for repeating pattern
                    // ------------------------------------
                    int finalXIndex = thirdHalfLineIndex + 1;
                    lttr.SetDashLengthAt(finalXIndex, xDashLength);
                    lttr.SetShapeStyleAt(finalXIndex, textStyleId);
                    lttr.SetShapeNumberAt(finalXIndex, 0);
                    lttr.SetShapeScaleAt(finalXIndex, 0.9);
                    lttr.SetShapeIsUcsOrientedAt(finalXIndex, false);
                    lttr.SetShapeRotationAt(finalXIndex, 0);
                    lttr.SetTextAt(finalXIndex, xText);
                    lttr.SetShapeOffsetAt(
                        finalXIndex,
                        new Vector2d(-(xCharWidth + textBuffer), -0.45)
                    );
                    totalPatternLen += Math.Abs(xDashLength);

                    // Finalize overall pattern length
                    lttr.PatternLength = totalPatternLen;

                    // Add the new linetype to the linetype table
                    Oid ltId = ltt.Add(lttr);
                    tx.AddNewlyCreatedDBObject(lttr, true);
                    foreach (string name in layersToChange)
                    {
                        Oid ltrId = lt[name];
                        LayerTableRecord ltr = ltrId.Go<LayerTableRecord>(tx, OpenMode.ForWrite);
                        ltr.LinetypeObjectId = ltId;
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                }
                tx.Commit();
            }
        }

        public static void createcomplexlinetypemethod(
            string lineTypeName,
            string text,
            string textStyleName,
            Database? db = null
        )
        {
            db ??= Application.DocumentManager.MdiActiveDocument.Database;
            Transaction tx = db.TransactionManager.StartTransaction();
            using (tx)
            {
                try
                {
                    // We'll use the textstyle table to access
                    // the "Standard" textstyle for our text segment
                    TextStyleTable tt = (TextStyleTable)
                        tx.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    // Get the linetype table from the drawing
                    LinetypeTable ltt = (LinetypeTable)
                        tx.GetObject(db.LinetypeTableId, OpenMode.ForWrite);
                    // Get layer table
                    LayerTable lt = tx.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                    //**************************************
                    //Change name of line type to create new and text value
                    //**************************************
                    prdDbg($"Remember to create text style: {textStyleName}!!!");
                    if (tt.Has(textStyleName))
                    {
                        prdDbg("Text style exists!");
                    }
                    else
                    {
                        prdDbg($"Text style {textStyleName} DOES NOT exist!");
                        tx.Abort();
                        return;
                    }
                    List<string> layersToChange = new List<string>();
                    if (ltt.Has(lineTypeName))
                    {
                        Oid existingId = ltt[lineTypeName];
                        Oid placeHolderId = ltt["Continuous"];
                        foreach (Oid oid in lt)
                        {
                            LayerTableRecord ltr = oid.Go<LayerTableRecord>(tx);
                            if (ltr.LinetypeObjectId == existingId)
                            {
                                ltr.CheckOrOpenForWrite();
                                ltr.LinetypeObjectId = placeHolderId;
                                layersToChange.Add(ltr.Name);
                            }
                        }
                        LinetypeTableRecord exLtr = existingId.Go<LinetypeTableRecord>(
                            tx,
                            OpenMode.ForWrite
                        );
                        exLtr.Erase(true);
                    }
                    // Create our new linetype table record...
                    LinetypeTableRecord lttr = new LinetypeTableRecord();
                    // ... and set its properties
                    lttr.Name = lineTypeName;
                    lttr.AsciiDescription = $"{text} ---- {text} ---- {text} ----";
                    lttr.PatternLength = 0.9;
                    //IsScaledToFit unsure what it does, can't see any difference
                    lttr.IsScaledToFit = true;

                    // A single line dash length
                    double dL = 5;

                    // Convert your text into segments that keep the total dash count within AutoCAD's
                    // 12-dash limitation (two line dashes + forward text + reversed text).
                    const int maxTextSegments = 5;
                    List<string> forwardSegments;
                    if (text.Length <= maxTextSegments)
                    {
                        forwardSegments = text.Select(c => c.ToString()).ToList();
                    }
                    else
                    {
                        forwardSegments = new List<string>();
                        int baseLength = text.Length / maxTextSegments;
                        int remainder = text.Length % maxTextSegments;
                        int cursor = 0;
                        for (int i = 0; i < maxTextSegments; i++)
                        {
                            int segmentLength = baseLength + (i < remainder ? 1 : 0);
                            if (segmentLength <= 0)
                                continue;
                            forwardSegments.Add(text.Substring(cursor, segmentLength));
                            cursor += segmentLength;
                        }
                        if (cursor < text.Length)
                        {
                            int lastIndex = forwardSegments.Count - 1;
                            forwardSegments[lastIndex] += text.Substring(cursor);
                        }
                    }

                    int segmentCount = forwardSegments.Count;
                    if (segmentCount == 0)
                        throw new System.Exception("Unable to generate text segments for linetype pattern.");

                    // A small horizontal buffer on each side of each character
                    double textBuffer = 0.05;
                    double innerSegmentBuffer = segmentCount > 1 ? 0.0 : textBuffer;

                    // We want:
                    //   1) One line dash
                    //   2) A dash per text segment (forward)
                    //   3) Another line dash
                    //   4) A dash per text segment (reversed)
                    int dashCount = 2 + 2 * segmentCount;
                    lttr.NumDashes = dashCount;

                    List<string> reverseSegments = forwardSegments
                        .Select(segment => new string(segment.Reverse().ToArray()))
                        .Reverse()
                        .ToList();

                    // We need to measure each character to set dash lengths properly.
                    Oid textStyleId = tt[textStyleName];
                    var ts = new Autodesk.AutoCAD.GraphicsInterface.TextStyle();
                    ts.FromTextStyleTableRecord(textStyleId);
                    ts.TextSize = 0.9; // same size you used before

                    // We'll track total pattern length to finalize lttr.PatternLength
                    double totalPatternLen = 0;

                    // -----------------------------
                    // FORWARD CHARACTERS (DASH #1+)
                    // -----------------------------
                    for (int i = 0; i < segmentCount; i++)
                    {
                        // This dash index is consecutive after dash 0
                        int dashIndex = i + 1;

                        string segment = forwardSegments[i];
                        var extBox = ts.ExtentsBox(segment, true, false, null);
                        double segmentWidth = (extBox.MaxPoint.X - extBox.MinPoint.X) + 0.1;
                        // +0.1 just like your original code

                        double leftBuffer = i == 0 ? textBuffer : 0;
                        double rightBuffer = i == segmentCount - 1 ? textBuffer : 0;

                        // Dash length is negative for text (shape) dash with buffers only at ends
                        double dashLen = -(segmentWidth + leftBuffer + rightBuffer);
                        lttr.SetDashLengthAt(dashIndex, dashLen);

                        // For shape-based text, you must set these:
                        lttr.SetShapeStyleAt(dashIndex, textStyleId);
                        lttr.SetShapeNumberAt(dashIndex, 0);
                        lttr.SetShapeScaleAt(dashIndex, 0.9);
                        lttr.SetShapeIsUcsOrientedAt(dashIndex, false);
                        lttr.SetShapeRotationAt(dashIndex, 0);
                        lttr.SetTextAt(dashIndex, segment);

                        // The shape offset must be local within the dash,
                        // not relative to the entire pattern.
                        // We place it near the left side, preserving right-side buffer when needed
                        lttr.SetShapeOffsetAt(
                            dashIndex,
                            new Vector2d(-(segmentWidth - rightBuffer), -0.45)
                        );

                        // Accumulate pattern length (absolute value of dashLen)
                        totalPatternLen += (segmentWidth + leftBuffer + rightBuffer);
                    }

                    // -----------------------
                    // Next line dash
                    // -----------------------
                    int lineDashIndex2 = 1 + segmentCount;
                    lttr.SetDashLengthAt(lineDashIndex2, dL);
                    totalPatternLen += dL;

                    // ------------------------------------
                    // ROTATED CHARACTERS (DASH #...)
                    // ------------------------------------
                    int reverseStartIndex = lineDashIndex2 + 1;
                    for (int i = 0; i < segmentCount; i++)
                    {
                        int dashIndex = reverseStartIndex + i;

                        string segment = reverseSegments[i];
                        var extBox = ts.ExtentsBox(segment, true, false, null);
                        double segmentWidth = (extBox.MaxPoint.X - extBox.MinPoint.X) + 0.1;

                        double leftBuffer = i == 0 ? textBuffer : innerSegmentBuffer;
                        double rightBuffer = i == segmentCount - 1 ? textBuffer : innerSegmentBuffer;

                        double dashLen = -(segmentWidth + leftBuffer + rightBuffer);
                        lttr.SetDashLengthAt(dashIndex, dashLen);

                        lttr.SetShapeStyleAt(dashIndex, textStyleId);
                        lttr.SetShapeNumberAt(dashIndex, 0);
                        lttr.SetShapeScaleAt(dashIndex, 0.9);
                        lttr.SetShapeIsUcsOrientedAt(dashIndex, false);

                        // Rotation 180° for reversed text
                        lttr.SetShapeRotationAt(dashIndex, Math.PI);
                        lttr.SetTextAt(dashIndex, segment);

                        // Offset is local to the dash. We'll place it near the
                        // “other end” of the dash: -textBuffer in X, etc.
                        lttr.SetShapeOffsetAt(dashIndex, new Vector2d(-textBuffer, 0.45));

                        totalPatternLen += (segmentWidth + leftBuffer + rightBuffer);
                    }

                    // Finalize overall pattern length
                    lttr.PatternLength = totalPatternLen;

                    // Add the new linetype to the linetype table
                    Oid ltId = ltt.Add(lttr);
                    tx.AddNewlyCreatedDBObject(lttr, true);
                    foreach (string name in layersToChange)
                    {
                        Oid ltrId = lt[name];
                        LayerTableRecord ltr = ltrId.Go<LayerTableRecord>(tx, OpenMode.ForWrite);
                        ltr.LinetypeObjectId = ltId;
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                }
                tx.Commit();
            }
        }

        private sealed class StringInputForm : Form
        {
            private readonly TextBox inputTextBox;

            public string InputText => inputTextBox.Text;

            public StringInputForm(string title, string prompt, string defaultValue)
            {
                Text = title;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterScreen;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                KeyPreview = true;

                int margin = 12;
                int spacing = 8;
                int textBoxWidth = 360;

                var promptLabel = new Label
                {
                    AutoSize = true,
                    Text = prompt,
                    Location = new Point(margin, margin)
                };

                inputTextBox = new TextBox
                {
                    Location = new Point(margin, promptLabel.Bottom + spacing),
                    Width = textBoxWidth,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Text = defaultValue
                };

                var okButton = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Size = new Size(90, 28),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right
                };

                var cancelButton = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Size = new Size(90, 28),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right
                };

                AcceptButton = okButton;
                CancelButton = cancelButton;

                int buttonsTop = inputTextBox.Bottom + spacing + 4;
                cancelButton.Location = new Point(margin + textBoxWidth - cancelButton.Width, buttonsTop);
                okButton.Location = new Point(cancelButton.Left - okButton.Width - spacing, buttonsTop);

                ClientSize = new Size(margin + textBoxWidth + margin, buttonsTop + okButton.Height + margin);

                Controls.Add(promptLabel);
                Controls.Add(inputTextBox);
                Controls.Add(okButton);
                Controls.Add(cancelButton);

                Shown += (sender, e) =>
                {
                    inputTextBox.SelectAll();
                    inputTextBox.Focus();
                };
            }
        }

        internal static string? PromptForString(string title, string prompt, string defaultValue = "")
        {
            using (var form = new StringInputForm(title, prompt, defaultValue))
            {
                return form.ShowDialog() == DialogResult.OK ? form.InputText : null;
            }
        }
    }
}
