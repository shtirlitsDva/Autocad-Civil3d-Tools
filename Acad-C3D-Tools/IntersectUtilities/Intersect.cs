using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Aec.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using static IntersectUtilities.HelperMethods;

namespace IntersectUtilities
{
    /// <summary>
    /// Class for intersection tools.
    /// </summary>
    public class Intersect : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            //doc.Editor.WriteMessage("\n-> Command: intut");
            doc.Editor.WriteMessage("\n-> Intersect alignment with XREF: INTAL");
        }

        public void Terminate()
        {
        }
        #endregion

        /// <summary>
        /// Finds all intersections between a selected polyline and all lines.
        /// Creates a point object at the intersection.
        /// </summary>
        
        //[CommandMethod("intut")]
        public void intersectutilities()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database db = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            CivilDocument doc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select a polyline : ");
                    promptEntityOptions1.SetRejectMessage("\n Not a polyline");
                    promptEntityOptions1.AddAllowedClass(typeof(Polyline), true);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId plObjId = entity1.ObjectId;
                    Polyline polyline = tx.GetObject(plObjId, OpenMode.ForRead, false) as Polyline;

                    List<Line> lines = db.ListOfType<Line>(tx);
                    List<Point3d> p3dList = new List<Point3d>();

                    foreach (Line line in lines)
                    {
                        using (Point3dCollection p3dcol = new Point3dCollection())
                        {
                            polyline.IntersectWith(line, 0, p3dcol, new IntPtr(0), new IntPtr(0));

                            foreach (Point3d p3d in p3dcol) p3dList.Add(p3d);
                        }
                    }

                    // Get the block table for the current database
                    var blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);

                    // Get the model space block table record
                    var modelSpace = (BlockTableRecord)tx.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    foreach (Point3d p3d in p3dList)
                    {
                        using (DBPoint acPoint = new DBPoint(p3d))
                        {
                            modelSpace.AppendEntity(acPoint);
                            tx.AddNewlyCreatedDBObject(acPoint, true);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                }
                tx.Commit();
            }
        }

        [CommandMethod("intal")]
        public void intersectalignment()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database db = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Select XREF
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select a LER XREF : ");
                    promptEntityOptions1.SetRejectMessage("\n Not a XREF");
                    promptEntityOptions1.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.BlockReference), true);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId blkObjId = entity1.ObjectId;
                    Autodesk.AutoCAD.DatabaseServices.BlockReference blkRef
                        = tx.GetObject(blkObjId, OpenMode.ForRead, false)
                        as Autodesk.AutoCAD.DatabaseServices.BlockReference; 
                    #endregion

                    // open the block definition?
                    BlockTableRecord blockDef = tx.GetObject(blkRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    // is not from external reference, exit
                    if (!blockDef.IsFromExternalReference) return;

                    // open the xref database
                    Database xRefDB = new Database(false, true);
                    editor.WriteMessage($"\nPathName of the blockDef -> {blockDef.PathName}");

                    //Relative path handling
                    //I
                    string curPathName = blockDef.PathName;
                    bool isFullPath = IsFullPath(curPathName);
                    if (isFullPath == false)
                    {
                        string sourcePath = Path.GetDirectoryName(doc.Name);
                        editor.WriteMessage($"\nSourcePath -> {sourcePath}");
                        curPathName = GetAbsolutePath(sourcePath, blockDef.PathName);
                        editor.WriteMessage($"\nTargetPath -> {curPathName}");
                    }

                    xRefDB.ReadDwgFile(curPathName, System.IO.FileShare.Read, false, string.Empty);

                    //Transaction from Database of the Xref
                    Transaction xrefTx = xRefDB.TransactionManager.StartTransaction();

                    List<Line> lines = xRefDB.ListOfType<Line>(xrefTx);
                    editor.WriteMessage($"\nNr. of lines: {lines.Count}");
                    List<Polyline> plines = xRefDB.ListOfType<Polyline>(xrefTx);
                    editor.WriteMessage($"\nNr. of plines: {plines.Count}");

                    #region Select Alignment
                    //Get alignment
                    PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions("\n Select alignment to intersect: ");
                    promptEntityOptions2.SetRejectMessage("\n Not an alignment");
                    promptEntityOptions2.AddAllowedClass(typeof(Alignment), true);
                    PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                    if (((PromptResult)entity2).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId alObjId = entity2.ObjectId;
                    Alignment alignment = tx.GetObject(alObjId, OpenMode.ForRead, false) as Alignment; 
                    #endregion

                    //Create a plane to project all intersections on
                    //Needed to avoid missing objects with non zero Z values
                    Plane plane = new Plane();

                    //Access CivilDocument cogopoints manager
                    CogoPointCollection cogoPoints = civilDoc.CogoPoints;

                    int count = 1;

                    foreach (Line line in lines)
                    {
                        LayerTableRecord layer = (LayerTableRecord)xrefTx.GetObject(line.LayerId, OpenMode.ForRead);
                        if (layer.IsFrozen) continue;

                        using (Point3dCollection p3dcol = new Point3dCollection())
                        {
                            alignment.IntersectWith(line, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                            foreach (Point3d p3d in p3dcol)
                            {
                                oid pointId = cogoPoints.Add(p3d, true);
                                CogoPoint cogoPoint = pointId.GetObject(OpenMode.ForWrite) as CogoPoint;
                                //var layer = xrefTx.GetObject(line.LayerId, OpenMode.ForRead) as SymbolTableRecord;

                                cogoPoint.PointName = layer.Name + " " + count;
                                cogoPoint.RawDescription = "Udfyld RAW DESCRIPTION";

                                count++;
                            }
                        }
                    }
                    foreach (Polyline pline in plines)
                    {
                        LayerTableRecord layer = (LayerTableRecord)xrefTx.GetObject(pline.LayerId, OpenMode.ForRead);
                        if (layer.IsFrozen) continue;

                        using (Point3dCollection p3dcol = new Point3dCollection())
                        {
                            alignment.IntersectWith(pline, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                            foreach (Point3d p3d in p3dcol)
                            {
                                oid pointId = cogoPoints.Add(p3d, true);
                                CogoPoint cogoPoint = pointId.GetObject(OpenMode.ForWrite) as CogoPoint;
                                //var layer = xrefTx.GetObject(pline.LayerId, OpenMode.ForRead) as SymbolTableRecord;

                                cogoPoint.PointName = layer.Name + " " + count;
                                cogoPoint.RawDescription = "Udfyld RAW DESCRIPTION";

                                count++;
                            }
                        }
                    }

                    xrefTx.Dispose();
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                }
                tx.Commit();
            }
        }
    }

    public static class ExtensionMethods
    {
        public static T Go<T>(this oid Oid, Transaction tx, OpenMode openMode = OpenMode.ForRead) where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            return (T) tx.GetObject(Oid, openMode, false);
        }
        public static void ForEach<T>(this Database database, Action<T> action, Transaction tr) where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            //using (var tr = database.TransactionManager.StartTransaction())
            //{
            // Get the block table for the current database
            var blockTable = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);

            // Get the model space block table record
            var modelSpace = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            RXClass theClass = RXObject.GetClass(typeof(T));

            // Loop through the entities in model space
            foreach (oid objectId in modelSpace)
            {
                // Look for entities of the correct type
                if (objectId.ObjectClass.IsDerivedFrom(theClass))
                {
                    var entity = (T)tr.GetObject(objectId, OpenMode.ForRead);
                    action(entity);
                }
            }
            //tr.Commit();
            //}
        }

        public static List<T> ListOfType<T>(this Database database, Transaction tr) where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            //using (var tr = database.TransactionManager.StartTransaction())
            //{

            //Init the list of the objects
            List<T> objs = new List<T>();

            // Get the block table for the current database
            var blockTable = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);

            // Get the model space block table record
            var modelSpace = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            RXClass theClass = RXObject.GetClass(typeof(T));

            // Loop through the entities in model space
            foreach (oid objectId in modelSpace)
            {
                // Look for entities of the correct type
                if (objectId.ObjectClass.IsDerivedFrom(theClass))
                {
                    var entity = (T)tr.GetObject(objectId, OpenMode.ForRead);
                    objs.Add(entity);
                }
            }
            return objs;
            //tr.Commit();
            //}
        }
    }

    public static class HelperMethods
    {
        public static bool IsFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(Path.GetInvalidPathChars()) != -1 || !Path.IsPathRooted(path))
                return false;

            var pathRoot = Path.GetPathRoot(path);
            if (pathRoot.Length <= 2 && pathRoot != "/") // Accepts X:\ and \\UNC\PATH, rejects empty string, \ and X:, but accepts / to support Linux
                return false;

            return !(pathRoot == path && pathRoot.StartsWith("\\\\") && pathRoot.IndexOf('\\', 2) == -1); // A UNC server name without a share name (e.g "\\NAME") is invalid
        }

        public static string GetAbsolutePath(String basePath, String path)
        {
            if (path == null)
                return null;
            if (basePath == null)
                basePath = Path.GetFullPath("."); // quick way of getting current working directory
            else
                basePath = GetAbsolutePath(null, basePath); // to be REALLY sure ;)
            string finalPath;
            // specific for windows paths starting on \ - they need the drive added to them.
            // I constructed this piece like this for possible Mono support.
            if (!Path.IsPathRooted(path) || "\\".Equals(Path.GetPathRoot(path)))
            {
                if (path.StartsWith(Path.DirectorySeparatorChar.ToString()))
                    finalPath = Path.Combine(Path.GetPathRoot(basePath), path.TrimStart(Path.DirectorySeparatorChar));
                else
                    finalPath = Path.Combine(basePath, path);
            }
            else
                finalPath = path;
            // resolves any internal "..\" to get the true full path.
            return Path.GetFullPath(finalPath);
        }
    }
}
