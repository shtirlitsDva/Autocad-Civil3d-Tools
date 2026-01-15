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
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;

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

        public static void createltmethod(
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
                    // 12-dash limitation (three line dashes + forward text + reversed text).
                    const int maxTextSegments = 4;
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
                    //   1) Leading line dash (same half length as in linetypeX)
                    //   2) A dash per text segment (forward)
                    //   3) Middle line dash (double the leading one)
                    //   4) A dash per text segment (reversed)
                    //   5) Trailing line dash (same as leading)
                    int dashCount = 3 + 2 * segmentCount;
                    lttr.NumDashes = dashCount;

                    // Reverse characters within each subgroup to keep text readable after 180° rotation,
                    // but preserve subgroup ordering along the linetype
                    List<string> reverseSegments = forwardSegments
                        .Select(segment => new string(segment.Reverse().ToArray()))
                        .ToList();

                    // We need to measure each character to set dash lengths properly.
                    Oid textStyleId = tt[textStyleName];
                    var ts = new Autodesk.AutoCAD.GraphicsInterface.TextStyle();
                    ts.FromTextStyleTableRecord(textStyleId);
                    ts.TextSize = 0.9; // same size you used before

                    // We'll track total pattern length to finalize lttr.PatternLength
                    double totalPatternLen = 0;

                    // Leading line dash: same half-line length as used in linetypeX
                    string xTextForMeasure = "X";
                    var xExtForMeasure = ts.ExtentsBox(xTextForMeasure, true, false, null);
                    double xCharWidthForMeasure = xExtForMeasure.MaxPoint.X - xExtForMeasure.MinPoint.X;
                    double xDashLengthForMeasure = -(Math.Max(0.0, xCharWidthForMeasure) + 2 * textBuffer);
                    double firstLineLen = Math.Max(0.0, (dL - Math.Abs(xDashLengthForMeasure)) / 2.0);

                    lttr.SetDashLengthAt(0, firstLineLen);
                    totalPatternLen += firstLineLen;

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
                    double secondLineLen = 2 * firstLineLen;
                    lttr.SetDashLengthAt(lineDashIndex2, secondLineLen);
                    totalPatternLen += secondLineLen;

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

                    // Trailing line dash (same as leading)
                    int finalLineIndex = reverseStartIndex + segmentCount;
                    lttr.SetDashLengthAt(finalLineIndex, firstLineLen);
                    totalPatternLen += firstLineLen;

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

        public static void linetypeXsingle(
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
                    TextStyleTable tt = (TextStyleTable)
                        tx.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    LinetypeTable ltt = (LinetypeTable)
                        tx.GetObject(db.LinetypeTableId, OpenMode.ForWrite);
                    LayerTable lt = tx.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;

                    prdDbg($"Remember to create text style: {textStyleName}!!!");
                    if (!tt.Has(textStyleName))
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

                    LinetypeTableRecord lttr = new LinetypeTableRecord();
                    lttr.Name = lineTypeName;
                    lttr.AsciiDescription = $"{text} ---- {text} ---- {text} ----";
                    lttr.PatternLength = 0.9;
                    lttr.IsScaledToFit = true;

                    // Base line dash length used by the pattern in the original method
                    double dL = 5;

                    // Exactly one text segment per group (no grouping)
                    lttr.NumDashes = 8; // half | text | half | X | half | text(rot) | half | X

                    // Measure text style
                    Oid textStyleId = tt[textStyleName];
                    var ts = new Autodesk.AutoCAD.GraphicsInterface.TextStyle();
                    ts.FromTextStyleTableRecord(textStyleId);
                    ts.TextSize = 0.9;

                    // Buffers
                    double textBuffer = 0.05;

                    // Compute X element size and half-line lengths like in linetypeX
                    string xText = "X";
                    var xExt = ts.ExtentsBox(xText, true, false, null);
                    double xCharWidth = xExt.MaxPoint.X - xExt.MinPoint.X;
                    double xDashLength = -(Math.Max(0.0, xCharWidth) + 2 * textBuffer);
                    double availableForLines = Math.Max(0.0, dL - Math.Abs(xDashLength));
                    double halfLineLength = availableForLines / 2.0;

                    double totalPatternLen = 0;

                    // Leading half-line
                    lttr.SetDashLengthAt(0, halfLineLength);
                    totalPatternLen += halfLineLength;

                    // Forward text segment (index 1)
                    var fwdExt = ts.ExtentsBox(text, true, false, null);
                    double fwdWidth = fwdExt.MaxPoint.X - fwdExt.MinPoint.X;
                    double fwdDashLen = -(fwdWidth + 2 * textBuffer);
                    lttr.SetDashLengthAt(1, fwdDashLen);
                    lttr.SetShapeStyleAt(1, textStyleId);
                    lttr.SetShapeNumberAt(1, 0);
                    lttr.SetShapeScaleAt(1, 0.9);
                    lttr.SetShapeIsUcsOrientedAt(1, false);
                    lttr.SetShapeRotationAt(1, 0);
                    lttr.SetTextAt(1, text);
                    lttr.SetShapeOffsetAt(1, new Vector2d(-(fwdWidth - textBuffer), -0.45));
                    totalPatternLen += (fwdWidth + 2 * textBuffer);

                    // Half-line after forward group (index 2)
                    lttr.SetDashLengthAt(2, halfLineLength);
                    totalPatternLen += halfLineLength;

                    // X between groups (index 3)
                    lttr.SetDashLengthAt(3, xDashLength);
                    lttr.SetShapeStyleAt(3, textStyleId);
                    lttr.SetShapeNumberAt(3, 0);
                    lttr.SetShapeScaleAt(3, 0.9);
                    lttr.SetShapeIsUcsOrientedAt(3, false);
                    lttr.SetShapeRotationAt(3, 0);
                    lttr.SetTextAt(3, xText);
                    lttr.SetShapeOffsetAt(3, new Vector2d(-(xCharWidth + textBuffer), -0.45));
                    totalPatternLen += Math.Abs(xDashLength);

                    // Half-line before reversed group (index 4)
                    lttr.SetDashLengthAt(4, halfLineLength);
                    totalPatternLen += halfLineLength;

                    // Reversed text segment (rotated 180°, index 5)
                    var revExt = ts.ExtentsBox(text, true, false, null);
                    double revWidth = revExt.MaxPoint.X - revExt.MinPoint.X;
                    double revDashLen = -(revWidth + 2 * textBuffer);
                    lttr.SetDashLengthAt(5, revDashLen);
                    lttr.SetShapeStyleAt(5, textStyleId);
                    lttr.SetShapeNumberAt(5, 0);
                    lttr.SetShapeScaleAt(5, 0.9);
                    lttr.SetShapeIsUcsOrientedAt(5, false);
                    lttr.SetShapeRotationAt(5, Math.PI);
                    lttr.SetTextAt(5, text);
                    lttr.SetShapeOffsetAt(5, new Vector2d(0, 0.45));
                    totalPatternLen += (revWidth + 2 * textBuffer);

                    // Trailing half-line (index 6)
                    lttr.SetDashLengthAt(6, halfLineLength);
                    totalPatternLen += halfLineLength;

                    // Trailing X (index 7)
                    lttr.SetDashLengthAt(7, xDashLength);
                    lttr.SetShapeStyleAt(7, textStyleId);
                    lttr.SetShapeNumberAt(7, 0);
                    lttr.SetShapeScaleAt(7, 0.9);
                    lttr.SetShapeIsUcsOrientedAt(7, false);
                    lttr.SetShapeRotationAt(7, 0);
                    lttr.SetTextAt(7, xText);
                    lttr.SetShapeOffsetAt(7, new Vector2d(-(xCharWidth + textBuffer), -0.45));
                    totalPatternLen += Math.Abs(xDashLength);

                    // Finalize
                    lttr.PatternLength = totalPatternLen;

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

        public static void createltmethodsingle(
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
                    TextStyleTable tt = (TextStyleTable)
                        tx.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    LinetypeTable ltt = (LinetypeTable)
                        tx.GetObject(db.LinetypeTableId, OpenMode.ForWrite);
                    LayerTable lt = tx.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;

                    prdDbg($"Remember to create text style: {textStyleName}!!!");
                    if (!tt.Has(textStyleName))
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

                    LinetypeTableRecord lttr = new LinetypeTableRecord();
                    lttr.Name = lineTypeName;
                    lttr.AsciiDescription = $"{text} ---- {text} ---- {text} ----";
                    lttr.PatternLength = 0.9;
                    lttr.IsScaledToFit = true;

                    double dL = 5;

                    // Exactly one text segment per group (no grouping)
                    lttr.NumDashes = 5; // leading | text | middle(2x) | text(rot) | trailing

                    Oid textStyleId = tt[textStyleName];
                    var ts = new Autodesk.AutoCAD.GraphicsInterface.TextStyle();
                    ts.FromTextStyleTableRecord(textStyleId);
                    ts.TextSize = 0.9;

                    double textBuffer = 0.05;
                    double totalPatternLen = 0;

                    // Leading line dash (same half length logic as linetypeX measurement)
                    string xTextForMeasure = "X";
                    var xExtForMeasure = ts.ExtentsBox(xTextForMeasure, true, false, null);
                    double xCharWidthForMeasure = xExtForMeasure.MaxPoint.X - xExtForMeasure.MinPoint.X;
                    double xDashLengthForMeasure = -(Math.Max(0.0, xCharWidthForMeasure) + 2 * textBuffer);
                    double firstLineLen = Math.Max(0.0, (dL - Math.Abs(xDashLengthForMeasure)) / 2.0);

                    lttr.SetDashLengthAt(0, firstLineLen);
                    totalPatternLen += firstLineLen;

                    // Forward single text segment (index 1)
                    var fwdExt = ts.ExtentsBox(text, true, false, null);
                    double fwdWidth = (fwdExt.MaxPoint.X - fwdExt.MinPoint.X) + 0.1;
                    double fwdDashLen = -(fwdWidth + 2 * textBuffer);
                    lttr.SetDashLengthAt(1, fwdDashLen);
                    lttr.SetShapeStyleAt(1, textStyleId);
                    lttr.SetShapeNumberAt(1, 0);
                    lttr.SetShapeScaleAt(1, 0.9);
                    lttr.SetShapeIsUcsOrientedAt(1, false);
                    lttr.SetShapeRotationAt(1, 0);
                    lttr.SetTextAt(1, text);
                    lttr.SetShapeOffsetAt(1, new Vector2d(-(fwdWidth - textBuffer), -0.45));
                    totalPatternLen += (fwdWidth + 2 * textBuffer);

                    // Middle line dash (double the leading one) (index 2)
                    double secondLineLen = 2 * firstLineLen;
                    lttr.SetDashLengthAt(2, secondLineLen);
                    totalPatternLen += secondLineLen;

                    // Reversed single text segment (index 3)
                    var revExt = ts.ExtentsBox(text, true, false, null);
                    double revWidth = (revExt.MaxPoint.X - revExt.MinPoint.X) + 0.1;
                    double revDashLen = -(revWidth + 2 * textBuffer);
                    lttr.SetDashLengthAt(3, revDashLen);
                    lttr.SetShapeStyleAt(3, textStyleId);
                    lttr.SetShapeNumberAt(3, 0);
                    lttr.SetShapeScaleAt(3, 0.9);
                    lttr.SetShapeIsUcsOrientedAt(3, false);
                    lttr.SetShapeRotationAt(3, Math.PI);
                    lttr.SetTextAt(3, text);
                    lttr.SetShapeOffsetAt(3, new Vector2d(-textBuffer, 0.45));
                    totalPatternLen += (revWidth + 2 * textBuffer);

                    // Trailing line dash (same as leading) (index 4)
                    lttr.SetDashLengthAt(4, firstLineLen);
                    totalPatternLen += firstLineLen;

                    lttr.PatternLength = totalPatternLen;

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

        /// <summary>
        /// Holds extracted properties from a linetype text/symbol segment.
        /// </summary>
        internal class LinetypeSegmentInfo
        {
            public string Text { get; set; } = "";
            public Oid TextStyleId { get; set; } = Oid.Null;
            public double Scale { get; set; } = 0.9;
            public double YOffset { get; set; } = -0.45;
            public double XOffset { get; set; } = 0;
        }

        /// <summary>
        /// Extracts the first non-empty text segment info from a linetype.
        /// </summary>
        internal static LinetypeSegmentInfo? ExtractTextInfoFromLinetype(LinetypeTableRecord lttr)
        {
            for (int i = 0; i < lttr.NumDashes; i++)
            {
                try
                {
                    Oid shapeStyleId = lttr.ShapeStyleAt(i);
                    if (shapeStyleId.IsNull) continue;

                    string text = lttr.TextAt(i);
                    if (!string.IsNullOrEmpty(text) && text != "X" && text != "\u26A1")
                    {
                        var offset = lttr.ShapeOffsetAt(i);
                        return new LinetypeSegmentInfo
                        {
                            Text = text,
                            TextStyleId = shapeStyleId,
                            Scale = lttr.ShapeScaleAt(i),
                            YOffset = offset.Y,
                            XOffset = offset.X
                        };
                    }
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    continue;
                }
            }
            return null;
        }

        /// <summary>
        /// Extracts symbol segment info (X or lightning bolt) from a linetype.
        /// </summary>
        internal static LinetypeSegmentInfo? ExtractSymbolInfoFromLinetype(LinetypeTableRecord lttr)
        {
            for (int i = 0; i < lttr.NumDashes; i++)
            {
                try
                {
                    Oid shapeStyleId = lttr.ShapeStyleAt(i);
                    if (shapeStyleId.IsNull) continue;

                    string text = lttr.TextAt(i);
                    if (text == "X" || text == "\u26A1")
                    {
                        var offset = lttr.ShapeOffsetAt(i);
                        return new LinetypeSegmentInfo
                        {
                            Text = text,
                            TextStyleId = shapeStyleId,
                            Scale = lttr.ShapeScaleAt(i),
                            YOffset = offset.Y,
                            XOffset = offset.X
                        };
                    }
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    continue;
                }
            }
            return null;
        }

        /// <summary>
        /// Holds all properties of a single dash in a linetype pattern.
        /// </summary>
        internal class LinetypeDashInfo
        {
            public int OriginalIndex { get; set; }
            public double DashLength { get; set; }
            public bool HasText { get; set; }
            public string Text { get; set; } = "";
            public Oid TextStyleId { get; set; } = Oid.Null;
            public double Scale { get; set; }
            public double Rotation { get; set; }
            public Vector2d Offset { get; set; }
            public bool IsUcsOriented { get; set; }
            public int ShapeNumber { get; set; }
            public bool IsTextSegment { get; set; } // true if text (not symbol)
            public bool IsSymbolSegment { get; set; } // true if symbol (X or ⚡)
        }

        /// <summary>
        /// Holds metadata about a linetype.
        /// </summary>
        internal class LinetypeInfo
        {
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public bool IsScaledToFit { get; set; }
            public List<LinetypeDashInfo> Dashes { get; set; } = new List<LinetypeDashInfo>();
        }

        /// <summary>
        /// Reads all linetype information into a data object.
        /// </summary>
        internal static LinetypeInfo ReadLinetypeInfo(LinetypeTableRecord lttr)
        {
            var info = new LinetypeInfo
            {
                Name = lttr.Name,
                Description = lttr.AsciiDescription,
                IsScaledToFit = lttr.IsScaledToFit,
                Dashes = ReadLinetypePattern(lttr)
            };
            return info;
        }

        /// <summary>
        /// Prints linetype definition details for debugging.
        /// </summary>
        internal static void PrintLinetypeDefinition(LinetypeTableRecord lttr)
        {
            prdDbg($"=== Linetype: {lttr.Name} ===");
            prdDbg($"Description: {lttr.AsciiDescription}");
            prdDbg($"PatternLength: {lttr.PatternLength}");
            prdDbg($"NumDashes: {lttr.NumDashes}");
            prdDbg($"IsScaledToFit: {lttr.IsScaledToFit}");
            prdDbg("");

            for (int i = 0; i < lttr.NumDashes; i++)
            {
                prdDbg($"--- Dash [{i}] ---");
                prdDbg($"  DashLength: {lttr.DashLengthAt(i)}");

                try
                {
                    Oid styleId = lttr.ShapeStyleAt(i);
                    prdDbg($"  ShapeStyleAt: {(styleId.IsNull ? "NULL" : styleId.ToString())}");

                    if (!styleId.IsNull)
                    {
                        try { prdDbg($"  TextAt: \"{lttr.TextAt(i)}\""); }
                        catch { prdDbg($"  TextAt: [ERROR reading]"); }

                        try { prdDbg($"  ShapeNumberAt: {lttr.ShapeNumberAt(i)}"); }
                        catch { prdDbg($"  ShapeNumberAt: [ERROR reading]"); }

                        try { prdDbg($"  ShapeScaleAt: {lttr.ShapeScaleAt(i)}"); }
                        catch { prdDbg($"  ShapeScaleAt: [ERROR reading]"); }

                        try { prdDbg($"  ShapeRotationAt: {lttr.ShapeRotationAt(i)}"); }
                        catch { prdDbg($"  ShapeRotationAt: [ERROR reading]"); }

                        try
                        {
                            var offset = lttr.ShapeOffsetAt(i);
                            prdDbg($"  ShapeOffsetAt: ({offset.X}, {offset.Y})");
                        }
                        catch { prdDbg($"  ShapeOffsetAt: [ERROR reading]"); }

                        try { prdDbg($"  ShapeIsUcsOrientedAt: {lttr.ShapeIsUcsOrientedAt(i)}"); }
                        catch { prdDbg($"  ShapeIsUcsOrientedAt: [ERROR reading]"); }
                    }
                }
                catch
                {
                    prdDbg($"  [No shape/text at this dash]");
                }
            }
            prdDbg($"=== End Linetype ===");
        }

        /// <summary>
        /// Reads all dash information from an existing linetype.
        /// Skips zero-length dashes that have no shape/text (these are artifacts).
        /// </summary>
        internal static List<LinetypeDashInfo> ReadLinetypePattern(LinetypeTableRecord lttr)
        {
            var dashes = new List<LinetypeDashInfo>();
            for (int i = 0; i < lttr.NumDashes; i++)
            {
                double dashLen = lttr.DashLengthAt(i);

                // Check if this is a zero-length dash with no shape - skip it
                bool hasShape = false;
                try
                {
                    Oid styleId = lttr.ShapeStyleAt(i);
                    hasShape = !styleId.IsNull;
                }
                catch { }

                if (dashLen == 0 && !hasShape)
                {
                    // Skip zero-length dashes with no shape (artifacts)
                    continue;
                }

                var dash = new LinetypeDashInfo
                {
                    OriginalIndex = i,
                    DashLength = dashLen
                };

                try
                {
                    Oid styleId = lttr.ShapeStyleAt(i);
                    if (!styleId.IsNull)
                    {
                        dash.HasText = true;
                        dash.TextStyleId = styleId;
                        dash.Text = lttr.TextAt(i);
                        dash.Scale = lttr.ShapeScaleAt(i);
                        dash.Rotation = lttr.ShapeRotationAt(i);
                        dash.Offset = lttr.ShapeOffsetAt(i);
                        dash.IsUcsOriented = lttr.ShapeIsUcsOrientedAt(i);
                        dash.ShapeNumber = lttr.ShapeNumberAt(i);

                        // Classify as text or symbol
                        if (dash.Text == "X" || dash.Text == "\u26A1")
                            dash.IsSymbolSegment = true;
                        else if (!string.IsNullOrEmpty(dash.Text))
                            dash.IsTextSegment = true;
                    }
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    // No text/shape at this dash
                }

                dashes.Add(dash);
            }
            return dashes;
        }

        /// <summary>
        /// Updates an existing linetype with symbol to use upright rotation (U=0 and U=π).
        /// Preserves the exact structure of the original linetype, only duplicating the pattern
        /// with text at 0° in first half and text at 180° in second half.
        /// </summary>
        public static void UpdateLinetypeWithSymbolUprightRotation(
            LinetypeInfo originalInfo,
            Database? db = null
        )
        {
            // AutoCAD has a maximum of 12 dashes per linetype
            const int maxDashes = 12;
            if (originalInfo.Dashes.Count * 2 > maxDashes)
            {
                prdDbg($"ERROR: Cannot double linetype '{originalInfo.Name}' - would have {originalInfo.Dashes.Count * 2} dashes (max is {maxDashes})");
                prdDbg($"Original has {originalInfo.Dashes.Count} dashes. Consider simplifying the linetype first.");
                return;
            }

            db ??= Application.DocumentManager.MdiActiveDocument.Database;
            Transaction tx = db.TransactionManager.StartTransaction();
            using (tx)
            {
                try
                {
                    LinetypeTable ltt = (LinetypeTable)
                        tx.GetObject(db.LinetypeTableId, OpenMode.ForWrite);
                    LayerTable lt = tx.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;

                    // Use the pre-read pattern
                    var originalDashes = originalInfo.Dashes;
                    string lineTypeName = originalInfo.Name;

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

                    // Create new linetype with doubled pattern (first half U=0, second half U=π)
                    LinetypeTableRecord lttr = new LinetypeTableRecord();
                    lttr.Name = lineTypeName;
                    lttr.AsciiDescription = originalInfo.Description;
                    lttr.IsScaledToFit = originalInfo.IsScaledToFit;

                    // Double the pattern: original pattern twice
                    int originalCount = originalDashes.Count;
                    lttr.NumDashes = originalCount * 2;

                    double totalPatternLen = 0;

                    // First copy: text segments at U=0
                    for (int i = 0; i < originalCount; i++)
                    {
                        var dash = originalDashes[i];
                        lttr.SetDashLengthAt(i, dash.DashLength);
                        totalPatternLen += Math.Abs(dash.DashLength);

                        if (dash.HasText && !dash.TextStyleId.IsNull)
                        {
                            lttr.SetShapeStyleAt(i, dash.TextStyleId);
                            lttr.SetShapeNumberAt(i, dash.ShapeNumber);
                            lttr.SetShapeScaleAt(i, dash.Scale);
                            lttr.SetShapeIsUcsOrientedAt(i, dash.IsUcsOriented);
                            // Order must be: Rotation → Text → Offset
                            if (dash.IsTextSegment)
                                lttr.SetShapeRotationAt(i, 0);
                            else
                                lttr.SetShapeRotationAt(i, dash.Rotation);
                            lttr.SetTextAt(i, dash.Text ?? "");
                            lttr.SetShapeOffsetAt(i, dash.Offset);
                        }
                    }

                    // Second copy: text segments at U=π
                    for (int i = 0; i < originalCount; i++)
                    {
                        int newIndex = originalCount + i;
                        var dash = originalDashes[i];
                        lttr.SetDashLengthAt(newIndex, dash.DashLength);
                        totalPatternLen += Math.Abs(dash.DashLength);

                        if (dash.HasText && !dash.TextStyleId.IsNull)
                        {
                            lttr.SetShapeStyleAt(newIndex, dash.TextStyleId);
                            lttr.SetShapeNumberAt(newIndex, dash.ShapeNumber);
                            lttr.SetShapeScaleAt(newIndex, dash.Scale);
                            lttr.SetShapeIsUcsOrientedAt(newIndex, dash.IsUcsOriented);
                            // Order must be: Rotation → Text → Offset
                            if (dash.IsTextSegment && !string.IsNullOrEmpty(dash.Text))
                            {
                                try
                                {
                                    // Measure text width using graphics engine
                                    var ts = new Autodesk.AutoCAD.GraphicsInterface.TextStyle();
                                    ts.FromTextStyleTableRecord(dash.TextStyleId);
                                    ts.TextSize = dash.Scale > 0 ? dash.Scale : 0.9;
                                    var extBox = ts.ExtentsBox(dash.Text, true, false, null);
                                    double textWidth = extBox.MaxPoint.X - extBox.MinPoint.X;
                                    double textBuffer = 0.05;
                                    double rotatedX = (textWidth - textBuffer);

                                    lttr.SetShapeRotationAt(newIndex, Math.PI);
                                    lttr.SetTextAt(newIndex, dash.Text);
                                    lttr.SetShapeOffsetAt(newIndex, new Vector2d(rotatedX, -dash.Offset.Y));
                                }
                                catch
                                {
                                    // Fallback if text measurement fails
                                    lttr.SetShapeRotationAt(newIndex, Math.PI);
                                    lttr.SetTextAt(newIndex, dash.Text);
                                    lttr.SetShapeOffsetAt(newIndex, new Vector2d(0, -dash.Offset.Y));
                                }
                            }
                            else
                            {
                                lttr.SetShapeRotationAt(newIndex, dash.Rotation);
                                lttr.SetTextAt(newIndex, dash.Text ?? "");
                                lttr.SetShapeOffsetAt(newIndex, dash.Offset);
                            }
                        }
                    }

                    lttr.PatternLength = totalPatternLen;

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

        /// <summary>
        /// Updates an existing linetype without symbol to use upright rotation (U=0 and U=π).
        /// Preserves the exact structure of the original linetype, only duplicating the pattern
        /// with text at 0° in first half and text at 180° in second half.
        /// </summary>
        public static void UpdateLinetypeNoSymbolUprightRotation(
            LinetypeInfo originalInfo,
            Database? db = null
        )
        {
            // AutoCAD has a maximum of 12 dashes per linetype
            const int maxDashes = 12;
            if (originalInfo.Dashes.Count * 2 > maxDashes)
            {
                prdDbg($"ERROR: Cannot double linetype '{originalInfo.Name}' - would have {originalInfo.Dashes.Count * 2} dashes (max is {maxDashes})");
                prdDbg($"Original has {originalInfo.Dashes.Count} dashes. Consider simplifying the linetype first.");
                return;
            }

            db ??= Application.DocumentManager.MdiActiveDocument.Database;
            Transaction tx = db.TransactionManager.StartTransaction();
            using (tx)
            {
                try
                {
                    LinetypeTable ltt = (LinetypeTable)
                        tx.GetObject(db.LinetypeTableId, OpenMode.ForWrite);
                    LayerTable lt = tx.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;

                    // Use the pre-read pattern
                    var originalDashes = originalInfo.Dashes;
                    string lineTypeName = originalInfo.Name;

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

                    // Create new linetype with doubled pattern (first half U=0, second half U=π)
                    LinetypeTableRecord lttr = new LinetypeTableRecord();
                    lttr.Name = lineTypeName;
                    lttr.AsciiDescription = originalInfo.Description;
                    lttr.IsScaledToFit = originalInfo.IsScaledToFit;

                    // Double the pattern: original pattern twice
                    int originalCount = originalDashes.Count;
                    lttr.NumDashes = originalCount * 2;

                    double totalPatternLen = 0;

                    // First copy: text segments at U=0
                    for (int i = 0; i < originalCount; i++)
                    {
                        var dash = originalDashes[i];
                        lttr.SetDashLengthAt(i, dash.DashLength);
                        totalPatternLen += Math.Abs(dash.DashLength);

                        if (dash.HasText && !dash.TextStyleId.IsNull)
                        {
                            lttr.SetShapeStyleAt(i, dash.TextStyleId);
                            lttr.SetShapeNumberAt(i, dash.ShapeNumber);
                            lttr.SetShapeScaleAt(i, dash.Scale);
                            lttr.SetShapeIsUcsOrientedAt(i, dash.IsUcsOriented);
                            // Order must be: Rotation → Text → Offset
                            if (dash.IsTextSegment)
                                lttr.SetShapeRotationAt(i, 0);
                            else
                                lttr.SetShapeRotationAt(i, dash.Rotation);
                            lttr.SetTextAt(i, dash.Text ?? "");
                            lttr.SetShapeOffsetAt(i, dash.Offset);
                        }
                    }

                    // Second copy: text segments at U=π
                    for (int i = 0; i < originalCount; i++)
                    {
                        int newIndex = originalCount + i;
                        var dash = originalDashes[i];
                        lttr.SetDashLengthAt(newIndex, dash.DashLength);
                        totalPatternLen += Math.Abs(dash.DashLength);

                        if (dash.HasText && !dash.TextStyleId.IsNull)
                        {
                            lttr.SetShapeStyleAt(newIndex, dash.TextStyleId);
                            lttr.SetShapeNumberAt(newIndex, dash.ShapeNumber);
                            lttr.SetShapeScaleAt(newIndex, dash.Scale);
                            lttr.SetShapeIsUcsOrientedAt(newIndex, dash.IsUcsOriented);
                            // Order must be: Rotation → Text → Offset
                            if (dash.IsTextSegment && !string.IsNullOrEmpty(dash.Text))
                            {
                                try
                                {
                                    // Measure text width using graphics engine
                                    var ts = new Autodesk.AutoCAD.GraphicsInterface.TextStyle();
                                    ts.FromTextStyleTableRecord(dash.TextStyleId);
                                    ts.TextSize = dash.Scale > 0 ? dash.Scale : 0.9;
                                    var extBox = ts.ExtentsBox(dash.Text, true, false, null);
                                    double textWidth = extBox.MaxPoint.X - extBox.MinPoint.X;
                                    double textBuffer = 0.05;
                                    double rotatedX = (textWidth - textBuffer);

                                    lttr.SetShapeRotationAt(newIndex, Math.PI);
                                    lttr.SetTextAt(newIndex, dash.Text);
                                    lttr.SetShapeOffsetAt(newIndex, new Vector2d(rotatedX, -dash.Offset.Y));
                                }
                                catch
                                {
                                    // Fallback if text measurement fails
                                    lttr.SetShapeRotationAt(newIndex, Math.PI);
                                    lttr.SetTextAt(newIndex, dash.Text);
                                    lttr.SetShapeOffsetAt(newIndex, new Vector2d(0, -dash.Offset.Y));
                                }
                            }
                            else
                            {
                                lttr.SetShapeRotationAt(newIndex, dash.Rotation);
                                lttr.SetTextAt(newIndex, dash.Text ?? "");
                                lttr.SetShapeOffsetAt(newIndex, dash.Offset);
                            }
                        }
                    }

                    lttr.PatternLength = totalPatternLen;

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
    }
}
