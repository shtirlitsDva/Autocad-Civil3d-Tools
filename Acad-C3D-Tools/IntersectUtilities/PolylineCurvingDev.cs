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
                LinkedList<PolylineSegmentProps> segmentsLinked = new LinkedList<PolylineSegmentProps>();
                HashSet<Polyline> polies = localDb.HashSetOfType<Polyline>(tx);

                foreach (Polyline pline in polies)
                {
                    int numOfVert = pline.NumberOfVertices - 1;

                    for (int i = 0; i < numOfVert; i++)
                    {
                        PolylineSegmentProps psp = new PolylineSegmentProps();

                        switch (pline.GetSegmentType(i))
                        {
                            case SegmentType.Line:
                                LineSegment2d seg = pline.GetLineSegment2dAt(i);
                                Point2d sp = seg.StartPoint;
                                Point2d ep = seg.EndPoint;
                                psp.Slope = (ep.Y - sp.Y) / (ep.X - sp.X);
                                psp.Index = i;
                                break;
                        }
                        segmentsLinked.AddLast(psp);
                    }

                    for (LinkedListNode<PolylineSegmentProps> node = segmentsLinked.First; node != null; node = node.Next)
                    {
                        LinkedListNode<PolylineSegmentProps> previousNode = node.Previous;
                        if (previousNode == null)
                        {
                            node.Value.ChangeInSlopeFromPrevious = 0;
                            continue;
                        }

                        node.Value.ChangeInSlopeFromPrevious = previousNode.Value.Slope - node.Value.Slope;
                    }

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Index;Slope;SlopeDelta");
                    for (LinkedListNode<PolylineSegmentProps> node = segmentsLinked.First; node != null; node = node.Next)
                    {
                        sb.AppendLine($"{node.Value.Index};{node.Value.Slope};{node.Value.ChangeInSlopeFromPrevious}");
                    }

                    string path = @"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\01 Intern\02 Tegninger\08 Net udvikling\Polylines\";
                    string fileName = $"{pline.Handle.ToString()}.csv";

                    Utils.ClrFile(path + fileName);
                    Utils.OutputWriter(path + fileName, sb.ToString());
                }
                tx.Abort();
            }
        }
    }

    internal class PolylineSegmentProps
    {
        internal int Index { get; set; } = 0;
        internal double Slope { get; set; } = 0;
        internal double ChangeInSlopeFromPrevious { get; set; } = 0;

        internal PolylineSegmentProps() { }
    }
}