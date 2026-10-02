#if BRICSCAD
using Teigha.Colors;
using Teigha.DatabaseServices;
using Bricscad.EditorInput;
using Teigha.Geometry;
using Teigha.Runtime;
using AcRx = Teigha.Runtime;
using Application = Bricscad.ApplicationServices.Application;
using BlockReference = Teigha.DatabaseServices.BlockReference;
using Color = Teigha.Colors.Color;
using DBObject = Teigha.DatabaseServices.DBObject;
using Entity = Teigha.DatabaseServices.Entity;
using ErrorStatus = Teigha.Runtime.ErrorStatus;
using ObjectId = Teigha.DatabaseServices.ObjectId;
using ObjectIdCollection = Teigha.DatabaseServices.ObjectIdCollection;
using Oid = Teigha.DatabaseServices.ObjectId;
using OpenMode = Teigha.DatabaseServices.OpenMode;
#else
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcRx = Autodesk.AutoCAD.Runtime;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using Color = Autodesk.AutoCAD.Colors.Color;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ErrorStatus = Autodesk.AutoCAD.Runtime.ErrorStatus;
using ObjectId = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
#endif

using IntersectUtilities.UtilsCommon.Enums;

using MoreLinq;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Automation;
using System.Xml.Serialization;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.UtilsCommon.Utils;
using FjvDynamicComponents = IntersectUtilities.UtilsCommon.DataManager.CsvData.FjvDynamicComponents;

using DataTable = System.Data.DataTable;

namespace IntersectUtilities.UtilsCommon
{
    public static partial class Utils
    {

        /// <summary>
        /// Print to the command line, if there is one.
        /// </summary>
        /// <remarks>
        /// MdiActiveDocument is null whenever no drawing is current — during
        /// IExtensionApplication.Initialize, and any time the AutoCAD Start tab
        /// holds focus. Dereferencing it there threw a NullReferenceException that
        /// took the whole plugin load down, from a debug printer. There is nowhere
        /// to print in that state, so the text goes to the debug output instead of
        /// disappearing entirely.
        /// </remarks>
        public static void prdDbg(string msg = "")
        {
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            if (ed != null) ed.WriteMessage("\n" + msg);
            else System.Diagnostics.Debug.WriteLine("[prdDbg] " + msg);
        }

        public static void prdDbg(object obj)
        {
            if (obj is SystemException ex1)
                prdDbg(obj.ToString().WrapThis(70));
            else if (obj is System.Exception ex2)
                prdDbg(obj.ToString().WrapThis(70));
            else
                prdDbg(obj.ToString());
        }

        /// <summary>
        /// Returns a list of strings no larger than the max length sent in.
        /// </summary>
        /// <remarks>useful function used to wrap string text for reporting.</remarks>
        /// <param name="text">Text to be wrapped into of List of Strings</param>
        /// <param name="maxLength">Max length you want each line to be.</param>
        /// <returns>List of Strings</returns>
        public static string WrapThis(this string s, int maxLength)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            var lines = s.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            var wrappedLines = new List<string>();

            foreach (var line in lines)
            {
                if (line.Length <= maxLength)
                {
                    wrappedLines.Add(line);
                }
                else
                {
                    int start = 0;
                    while (start < line.Length)
                    {
                        int length = Math.Min(maxLength, line.Length - start);
                        wrappedLines.Add(line.Substring(start, length));
                        start += length;
                    }
                }
            }
            return string.Join("\n", wrappedLines);
        }

        public static Dictionary<string, PipelineElementType> PipelineElementTypeDict =
        new Dictionary<string, PipelineElementType>()
        {
                { "Pipe", PipelineElementType.Pipe },
                { "Afgrening med spring", PipelineElementType.AfgreningMedSpring },
                { "Afgrening, parallel", PipelineElementType.AfgreningParallel },
                { "Afgreningsstuds", PipelineElementType.Afgreningsstuds },
                { "Endebund", PipelineElementType.Endebund },
                { "Engangsventil", PipelineElementType.Engangsventil },
                { "F-Model", PipelineElementType.F_Model },
                { "Kedelrørsbøjning", PipelineElementType.Kedelrørsbøjning },
                { "Kedelrørsbøjning, vertikal", PipelineElementType.Kedelrørsbøjning },
                { "Lige afgrening", PipelineElementType.LigeAfgrening },
                { "Parallelafgrening", PipelineElementType.AfgreningParallel },
                { "Præisoleret bøjning, 90gr", PipelineElementType.PræisoleretBøjning90gr },
                { "Præisoleret bøjning, 45gr", PipelineElementType.PræisoleretBøjning45gr },
                { "Bøjning, 45gr", PipelineElementType.Bøjning45gr },
                { "Bøjning, 30gr", PipelineElementType.Bøjning30gr },
                { "Bøjning, 15gr", PipelineElementType.Bøjning15gr },
                { "$Præisoleret bøjning, L {$L1}x{$L2} m, V {$V}°", PipelineElementType.PræisoleretBøjningVariabel },
                { "$Præisoleret bøjning, 90gr, L {$L1}x{$L2} m", PipelineElementType.PræisoleretBøjningVariabel },
                { "Præisoleret bøjning, L {$L1}x{$L2} m, V {$V}°", PipelineElementType.PræisoleretBøjningVariabel },
                { "Præisoleret ventil", PipelineElementType.PræisoleretVentil },
                { "Præventil med udluftning", PipelineElementType.PræventilMedUdluftning },
                { "Reduktion", PipelineElementType.Reduktion },
                { "Svanehals", PipelineElementType.Svanehals },
                { "Svejsetee", PipelineElementType.Svejsetee },
                { "Svejsning", PipelineElementType.Svejsning },
                { "Y-Model", PipelineElementType.Y_Model },
                { "H-Model", PipelineElementType.H_Model },
                { "$Buerør V{$Vinkel}° R{$R} L{$L}", PipelineElementType.Buerør },
                { "Buerør V{$Vinkel}° R{$R} L{$L}", PipelineElementType.Buerør },
                { "Stikafgrening", PipelineElementType.Stikafgrening },
                { "Muffetee", PipelineElementType.Muffetee },
                { "Preskobling tee", PipelineElementType.Muffetee },
                { "Materialeskift {#M1}{#DN1}x{#M2}{#DN2}", PipelineElementType.Materialeskift },
        };

    }
}
namespace IntersectUtilities.UtilsCommon
{
    public static partial class Extensions
    {
        public static bool IsNoE(this string s) => string.IsNullOrEmpty(s);

        public static bool IsNotNoE(this string s) => !string.IsNullOrEmpty(s);

        public static bool Equalz(this double a, double b, double tol) => Math.Abs(a - b) <= tol;

        public static bool HorizontalEqualz(this Point3d a, Point3d b, double tol = 0.01) =>
            null != a && null != b && a.X.Equalz(b.X, tol) && a.Y.Equalz(b.Y, tol);

        public static void CheckOrOpenForWrite(this DBObject dbObject)
        {
            if (dbObject.IsWriteEnabled == false)
            {
                if (dbObject.IsReadEnabled == true)
                {
                    dbObject.UpgradeOpen();
                }
                else if (dbObject.IsReadEnabled == false)
                {
                    dbObject.UpgradeOpen();
                    dbObject.UpgradeOpen();
                }
            }
        }

        public static double DistanceHorizontalTo(this Point3d sourceP3d, Point3d targetP3d)
        {
            double X1 = sourceP3d.X;
            double Y1 = sourceP3d.Y;
            double X2 = targetP3d.X;
            double Y2 = targetP3d.Y;
            return Math.Sqrt(Math.Pow((X2 - X1), 2) + Math.Pow((Y2 - Y1), 2));
        }

        public static BlockTableRecord GetModelspaceForWrite(this Database db) =>
            db
                .BlockTableId.Go<BlockTable>(db.TransactionManager.TopTransaction)[
                    BlockTableRecord.ModelSpace
                ]
                .Go<BlockTableRecord>(db.TransactionManager.TopTransaction, OpenMode.ForWrite);

        public static string RealName(this BlockReference br)
        {
            Transaction tx = br.Database.TransactionManager.TopTransaction;
            return br.IsDynamicBlock
                ? (
                    (BlockTableRecord)tx.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead)
                ).Name
                : br.Name;
        }

        public static string GetAttributeStringValue(this BlockReference br, string attributeName)
        {
            Database db = br.Database;
            Transaction tx = db.TransactionManager.TopTransaction;
            foreach (Oid oid in br.AttributeCollection)
            {
                AttributeReference ar = oid.Go<AttributeReference>(tx);
                if (string.Equals(ar.Tag, attributeName, StringComparison.OrdinalIgnoreCase))
                {
                    return ar.TextString;
                }
            }

            BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);
            foreach (Oid oid in btr)
            {
                if (oid.IsDerivedFrom<AttributeDefinition>())
                {
                    AttributeDefinition attDef = oid.Go<AttributeDefinition>(tx);
                    if (
                        attDef.Constant
                        && string.Equals(
                            attDef.Tag,
                            attributeName,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        return attDef.TextString;
                    }
                }
            }

            return "";
        }

        public static Transaction GetTopTx(this Entity ent) =>
            ent.Database.TransactionManager.TopTransaction;

        public static Point2d To2d(this Point3d p3d) => new Point2d(p3d.X, p3d.Y);

        public static HashSet<Point3d> GetAllEndPoints(this BlockReference br)
        {
            return ComponentPorts.Read(br, GetTopTx(br)).Select(p => p.Position).ToHashSet();
        }

        public static HashSet<Point3d> GetAllEndPoints(this Entity ent)
        {
            switch (ent)
            {
                case Polyline pl:
                    return new HashSet<Point3d> { pl.StartPoint, pl.EndPoint };
                case BlockReference br:
                    return br.GetAllEndPoints();
                default:
                    throw new System.Exception(
                        $"Entity is not a Polyline or BlockReference! {ent.GetType()}"
                    );
            }
        }

        /// <summary>
        /// Remember that the grouped objects need to have Equals and GetHashCode implemented
        /// </summary>
        /// <returns>List of lists, hvere each list contains connected objects</returns>
        public static List<List<T>> GroupConnected<T>(
            this IEnumerable<T> itemsToGroup,
            Func<T, T, bool> predicateIsConnected
        )
        {
            var visited = new HashSet<T>();
            var groups = new List<List<T>>();

            foreach (var item in itemsToGroup)
            {
                if (!visited.Contains(item))
                {
                    var group = new List<T>();
                    Stack<T> stack = new Stack<T>();
                    stack.Push(item);

                    while (stack.Count > 0)
                    {
                        var current = stack.Pop();

                        if (!visited.Contains(current))
                        {
                            visited.Add(current);
                            group.Add(current);

                            foreach (var neighbor in itemsToGroup)
                            {
                                if (
                                    !visited.Contains(neighbor)
                                    && predicateIsConnected(current, neighbor)
                                )
                                {
                                    stack.Push(neighbor);
                                }
                            }
                        }
                    }

                    groups.Add(group);
                }
            }

            return groups;
        }

    }
}
namespace IntersectUtilities.UtilsCommon
{
    public static partial class ExtensionMethods
    {
        public static T? Go<T>(
            this Oid oid,
            Transaction tx,
            OpenMode openMode =
                OpenMode.ForRead
        )
            where T : DBObject
        {
            var obj = tx.GetObject(oid, openMode, false);
            return obj as T;
        }

        public static T? Go<T>(this Handle handle, Database database)
            where T : DBObject
        {
            Oid id = database.GetObjectId(false, handle, 0);
            if (database.TransactionManager.TopTransaction == null)
                throw new System.Exception(
                    "Handle.Go<DBObject> -> no top transaction found! Call inside transaction."
                );
            return id.Go<T>(database.TransactionManager.TopTransaction);
        }

        public static Oid AddEntityToDbModelSpace<T>(this T entity, Database db)
            where T : Entity
        {
            if (db.TransactionManager.TopTransaction == null)
            {
                using (Transaction tx = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        BlockTableRecord modelSpace = db.GetModelspaceForWrite();
                        Oid id = modelSpace.AppendEntity(entity);
                        tx.AddNewlyCreatedDBObject(entity, true);
                        tx.Commit();
                        return id;
                    }
                    catch (System.Exception)
                    {
                        prdDbg("Adding element to database failed!");
                        tx.Abort();
                        return Oid.Null;
                    }
                }
            }
            else
            {
                Transaction tx = db.TransactionManager.TopTransaction;

                BlockTableRecord modelSpace = db.GetModelspaceForWrite();
                Oid id = modelSpace.AppendEntity(entity);
                tx.AddNewlyCreatedDBObject(entity, true);
                return id;
            }
        }

        public static bool CheckOrCreateLayer(
            this Database db,
            string layerName,
            short colorIdx = -1,
            bool isPlottable = true
        )
        {
            bool newTx = false;
            if (db.TransactionManager.TopTransaction == null)
                newTx = true;

            Transaction txLag;
            if (newTx)
                txLag = db.TransactionManager.StartTransaction();
            else
                txLag = db.TransactionManager.TopTransaction;
            try
            {
                LayerTable lt = txLag.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                if (!lt.Has(layerName))
                {
                    LayerTableRecord ltr = new LayerTableRecord();
                    ltr.Name = layerName;
                    ltr.IsPlottable = isPlottable;
                    if (colorIdx != -1)
                    {
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIdx);
                    }

                    //Make layertable writable
                    lt.CheckOrOpenForWrite();

                    //Add the new layer to layer table
                    Oid ltId = lt.Add(ltr);
                    txLag.AddNewlyCreatedDBObject(ltr, true);
                    if (newTx)
                    {
                        txLag.Commit();
                        txLag.Dispose();
                    }
                    return true;
                }
                else
                {
                    if (colorIdx == -1)
                        return true;
                    LayerTableRecord ltr = lt[layerName]
                        .Go<LayerTableRecord>(txLag, OpenMode.ForWrite);
                    if (ltr.Color.ColorIndex != colorIdx)
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIdx);
                    if (newTx)
                    {
                        txLag.Commit();
                        txLag.Dispose();
                    }
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                txLag.Abort();
                if (newTx)
                    txLag.Dispose();
                throw;
            }
        }

        public static bool IsDerivedFrom<T>(this Oid oid)
        {
            return oid.ObjectClass.IsDerivedFrom(RXObject.GetClass(typeof(T)));
        }

        public static List<T> ListOfType<T>(
            this Database database,
            Transaction tr,
            bool discardFrozen = false
        )
            where T : Entity
        {
            //Init the list of the objects
            List<T> objs = new List<T>();

            // Get the block table for the current database
            var blockTable = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);

            // Get the model space block table record
            var modelSpace = (BlockTableRecord)
                tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            RXClass theClass = RXObject.GetClass(typeof(T));

            // Loop through the entities in model space
            foreach (Oid oid in modelSpace)
            {
                // Look for entities of the correct type
                if (oid.ObjectClass.IsDerivedFrom(theClass))
                {
                    var entity = (T)tr.GetObject(oid, OpenMode.ForRead);
                    if (discardFrozen)
                    {
                        LayerTableRecord layer = (LayerTableRecord)
                            tr.GetObject(entity.LayerId, OpenMode.ForRead);
                        if (layer.IsFrozen)
                            continue;
                    }

                    objs.Add(entity);
                }
            }
            return objs;
        }

        public static HashSet<Entity> GetFjvEntities(
            this Database db,
            Transaction tr,
            bool discardWelds = true,
            bool discardStikBlocks = true,
            bool discardFrozen = false
        )
        {
            var fk = DataManager.CsvData.Csv.FjvDynamicComponents;

            HashSet<Entity> entities = new HashSet<Entity>();

            var rawPlines = db.ListOfType<Polyline>(tr, discardFrozen);
            var plineQuery = rawPlines.Where(pline =>
                GetPipeSystem(pline) != PipeSystemEnum.Ukendt
            );

            var rawBrefs = db.ListOfType<BlockReference>(tr, discardFrozen);
            var brQuery = rawBrefs.Where(x => fk.HasNavn(x.RealName()));

            HashSet<string> weldingBlocks = new HashSet<string>()
            {
                "SVEJSEPUNKT",
                "SVEJSEPUNKT-NOTXT",
                "SVEJSEPUNKT-V2",
            };

            HashSet<string> stikBlocks = new HashSet<string>() { "STIKAFGRENING", "STIKTEE" };

            if (discardWelds)
                brQuery = brQuery.Where(x => !weldingBlocks.Contains(x.RealName()));
            if (discardStikBlocks)
                brQuery = brQuery.Where(x => !stikBlocks.Contains(x.RealName()));

            //prdDbg($"FJV Entities > Polyline(s): {plineQuery.Count()}, BlockReference(s): {brQuery.Count()}");

            entities.UnionWith(brQuery);
            entities.UnionWith(plineQuery);
            return entities;
        }

        public static HashSet<BlockReference> GetFjvBlocks(
            this Database db,
            Transaction tr,
            bool discardWelds = true,
            bool discardStikBlocks = true,
            bool discardFrozen = false
        )
        {
            var fk = DataManager.CsvData.Csv.FjvDynamicComponents;

            HashSet<BlockReference> entities = new HashSet<BlockReference>();

            var rawBrefs = db.ListOfType<BlockReference>(tr, discardFrozen);
            var brQuery = rawBrefs.Where(x => fk.HasNavn(x.RealName()));

            HashSet<string> weldingBlocks = new HashSet<string>()
            {
                "SVEJSEPUNKT",
                "SVEJSEPUNKT-NOTXT",
                "SVEJSEPUNKT-V2"
            };

            HashSet<string> stikBlocks = new HashSet<string>() { "STIKAFGRENING", "STIKTEE" };

            if (discardWelds)
                brQuery = brQuery.Where(x => !weldingBlocks.Contains(x.RealName()));
            if (discardStikBlocks)
                brQuery = brQuery.Where(x => !stikBlocks.Contains(x.RealName()));

            entities.UnionWith(brQuery);
            return entities;
        }

        public static HashSet<Polyline> GetFjvPipes(
            this Database db,
            Transaction tr,
            bool discardFrozen = false
        )
        {
            HashSet<Polyline> entities = new HashSet<Polyline>();

            var rawPlines = db.ListOfType<Polyline>(tr, discardFrozen);
            entities = rawPlines
                .Where(pline =>
                    PipeScheduleV2.PipeScheduleV2.GetPipeSystem(pline) != PipeSystemEnum.Ukendt
                )
                .ToHashSet();

            return entities;
        }

        public static double ToDeg(this double radians) => (180 / Math.PI) * radians;

    }
}
namespace IntersectUtilities.UtilsCommon
{

    public class Point3dHorizontalComparer : IEqualityComparer<Point3d>
    {
        private readonly int _scale;

        public Point3dHorizontalComparer(int scale = 1000)
        {
            _scale = scale;
        }

        public bool Equals(Point3d p1, Point3d p2) =>
            (int)(p1.X * _scale) == (int)(p2.X * _scale)
            && (int)(p1.Y * _scale) == (int)(p2.Y * _scale);

        public int GetHashCode(Point3d a)
        {
            int xHash = ((int)(a.X * _scale)).GetHashCode();
            int yHash = ((int)(a.Y * _scale)).GetHashCode();
            return xHash ^ yHash;
        }
    }

    public class Result
    {
        private ResultStatus _status = ResultStatus.OK;
        internal ResultStatus Status { get { return _status; } set { if (_status != ResultStatus.FatalError) _status = value; } }
        private List<string> _errorMsg = new List<string>();
        internal void AddMsg(string msg)
        {
            if (msg.IsNotNoE()) _errorMsg.Add(msg);
        }
        internal List<string> GetMsg() { return _errorMsg; }
        internal string ErrorMsg
        {
            get
            {

                return string.Join("\n", _errorMsg);
            }
            set { if (value.IsNotNoE()) _errorMsg.Add(value); }
        }
        internal Result() { }
        internal Result(ResultStatus status, string errorMsg)
        {
            Status = status;
            AddMsg(errorMsg);
        }
        internal void Combine(Result input)
        {
            if (input._status > this._status) this._status = input._status;
            this.GetMsg().AddRange(input.GetMsg());
        }
        public override string ToString()
        {
            switch (this._status)
            {
                case ResultStatus.OK:
                    if (this._errorMsg.Count == 0) return "Operation completed OK";
                    else return "Operation completed OK with message: \n" + string.Join("\n", this._errorMsg);
                case ResultStatus.SoftError:
                    if (this._errorMsg.Count == 0) return "Operation completed with unknown Warnings.";
                    else return "Operation completed with Warnings: \n" + string.Join("\n", this._errorMsg);
                case ResultStatus.FatalError:
                    if (this._errorMsg.Count == 0) return "Operation aborted with Fatal Error!.";
                    else return "Operation aborted with Fatal Error! \n" + string.Join("\n", this._errorMsg);
                default:
                    throw new NotImplementedException($"ResultStatus {this._status} is not implemented!");
            }
        }
    }

    internal enum ResultStatus
    {
        OK,
        SoftError, //Exection may continue, changes to current drawing aborted
        FatalError, //Execution of processing must stop
    }

    public class DebugEntityException : System.Exception
    {
        public List<Entity> DebugEntities { get; }

        public DebugEntityException(string message, List<Entity>? debugEntities = null)
            : base(message)
        {
            DebugEntities = debugEntities ?? new List<Entity>();
            if (DebugEntities.Count > 0)
            {
                this.Data[nameof(DebugEntities)] = DebugEntities;
            }
        }
    }

}
