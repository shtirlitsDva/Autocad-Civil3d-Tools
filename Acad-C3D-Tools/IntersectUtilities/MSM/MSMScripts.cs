using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors; // For color
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry; // For Point3d
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices; // Added for ProfileView access
using IntersectUtilities.UtilsCommon;
using System.Linq;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>LABELPROFILEVIEWS</command>
        /// <summary>
        /// This command is numbering each Profile View with a label on the left side of each Profile View. 
        /// The color of the label should be yellow when the Profile View is not yet drawn with profiles or when the profiles needs to be edited. 
        /// The drawer should manually change the color to green when a Profile View is ready with updated profiles.
        /// </summary>
        /// <category>Utilities</category>
        [CommandMethod("LABELPROFILEVIEWS")]
        public void labelprofileviews()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                //Code starts here
                var profileViews = localDb.HashSetOfType<ProfileView>(tx);
                prdDbg($"Antal ProfileViews fundet: {profileViews.Count}");

                // Prepare current space for adding text
                var btr = (BlockTableRecord)tx.GetObject(localDb.CurrentSpaceId, OpenMode.ForWrite);

                // Ensure Arial text style exists
                TextStyleTable tst = (TextStyleTable)tx.GetObject(localDb.TextStyleTableId, OpenMode.ForRead);
                ObjectId arialStyleId = ObjectId.Null;
                const string styleName = "Arial";
                if (tst.Has(styleName))
                {
                    arialStyleId = tst[styleName];
                }
                else
                {
                    tst.UpgradeOpen();
                    TextStyleTableRecord tsRec = new TextStyleTableRecord
                    {
                        Name = styleName,
                        FileName = "arial.ttf"
                    };
                    arialStyleId = tst.Add(tsRec);
                    tx.AddNewlyCreatedDBObject(tsRec, true);
                }

                foreach (var pv in profileViews.OrderBy(x => x.Name))
                {
                    var loc = pv.Location; // Point3d
                    prdDbg($"ProfileView: {pv.Name} | Location: ({loc.X:0.###}, {loc.Y:0.###}, {loc.Z:0.###})");

                    // Insertion point 100 units to the left
                    Point3d insPt = new Point3d(loc.X - 100.0, loc.Y, loc.Z);

                    // Create text with pv name
                    DBText txt = new DBText
                    {
                        Position = insPt,
                        TextString = pv.Name.Replace("_PV", ""),
                        Height = 10,
                        Layer = "0"
                    };

                    if (!arialStyleId.IsNull)
                        txt.TextStyleId = arialStyleId;

                    txt.Color = Color.FromColorIndex(ColorMethod.ByAci, 2); // Yellow

                    btr.AppendEntity(txt);
                    tx.AddNewlyCreatedDBObject(txt, true);
                }
                //Code ends here
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }

            tx.Commit();
        }
    }
}
