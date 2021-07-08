using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using MoreLinq;
using System.Text;
using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using System.Windows.Forms;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;

namespace IntersectUtilities
{
    /// <summary>
    /// Class for intersection tools.
    /// </summary>
    public class PolylineCurvingDev : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
        }

        public void Terminate()
        {
        }
        #endregion

        [CommandMethod("CALCULATESLOPE")]
        public void calculateslope()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                HashSet<Polyline> polies = localDb.HashSetOfType<Polyline>(tx);

                double bandWidth = 0.25;

                foreach (Polyline pline in polies)
                {
                    int numOfVert = pline.NumberOfVertices - 1;
                    LinkedList<SegmentProps> segmentsLinked = new LinkedList<SegmentProps>();

                    for (int i = 0; i < numOfVert; i++)
                    {
                        SegmentProps psp = new SegmentProps();

                        switch (pline.GetSegmentType(i))
                        {
                            case SegmentType.Line:
                                LineSegment2d seg = pline.GetLineSegment2dAt(i);
                                Point2d sp = seg.StartPoint;
                                Point2d ep = seg.EndPoint;
                                psp.Slope = (ep.Y - sp.Y) / (ep.X - sp.X);
                                psp.Index = i;
                                psp.Type = pline.GetSegmentType(i);
                                break;
                            case SegmentType.Arc:
                                psp.Type = pline.GetSegmentType(i);
                                break;
                        }
                        segmentsLinked.AddLast(psp);
                    }

                    for (LinkedListNode<SegmentProps> node = segmentsLinked.First; node != null; node = node.Next)
                    {
                        LinkedListNode<SegmentProps> previousNode = node.Previous;
                        if (previousNode == null)
                        {
                            node.Value.ChangeInSlopeFromPrevious = 0;
                            continue;
                        }

                        node.Value.ChangeInSlopeFromPrevious = previousNode.Value.Slope - node.Value.Slope;
                        node.Value.ChangeInChangeFromPrevious = previousNode.Value.ChangeInSlopeFromPrevious -
                            node.Value.ChangeInSlopeFromPrevious;
                    }

                    AcceptedSequences acceptedSequences = new AcceptedSequences();

                    for (LinkedListNode<SegmentProps> node = segmentsLinked.First; node != null; node = node.Next)
                    {
                        LinkedListNode<SegmentProps> previousNode = node.Previous;
                        if (previousNode == null)
                            continue;


                    }

                    #region Output
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Index;Slope;SlopeDelta;ChangeDelta;ExcelIndex");
                    for (LinkedListNode<SegmentProps> node = segmentsLinked.First; node != null; node = node.Next)
                    {
                        sb.AppendLine($"{node.Value.Index};{node.Value.Slope};{node.Value.ChangeInSlopeFromPrevious};" +
                            $"{node.Value.ChangeInChangeFromPrevious};{node.Value.Index + 1}");
                    }

                    string path = @"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\01 Intern\02 Tegninger\08 Net udvikling\Polylines\";
                    string fileName = $"{pline.Handle.ToString()}.csv";

                    Utils.ClrFile(path + fileName);
                    Utils.OutputWriter(path + fileName, sb.ToString());
                    #endregion
                }
                tx.Abort();
            }
        }
    }

    internal class SegmentProps
    {
        internal int Index { get; set; } = 0;
        internal double Slope { get; set; } = 0;
        internal double ChangeInSlopeFromPrevious { get; set; } = 0;
        internal double ChangeInChangeFromPrevious { get; set; } = 0;
        internal SegmentType Type { get; set; }
    }

    internal class AcceptedSequences
    {
        internal List<LinkedList<SegmentProps>> Sequences { get; set; } = new List<LinkedList<SegmentProps>>();
    }

}