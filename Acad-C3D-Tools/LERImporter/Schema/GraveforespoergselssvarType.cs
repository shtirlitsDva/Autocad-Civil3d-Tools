using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using Autodesk.Civil.DataShortcuts;
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Data;
//using MoreLinq;
//using GroupByCluster;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

//using static IntersectUtilities.Enums;
//using static IntersectUtilities.HelperMethods;
//using static IntersectUtilities.Utils;
//using static IntersectUtilities.PipeSchedule;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Log = LERImporter.SimpleLogger;

namespace LERImporter.Schema
{
    public partial class GraveforespoergselssvarType
    {
        public Database Database { get; set; }
        public void test()
        {
            if (this.ledningMember == null) this.ledningMember =
                    new GraveforespoergselssvarTypeLedningMember[0];
            if (this.ledningstraceMember == null) this.ledningstraceMember =
                    new GraveforespoergselssvarTypeLedningstraceMember[0];
            if (this.ledningskomponentMember == null) this.ledningskomponentMember =
                    new GraveforespoergselssvarTypeLedningskomponentMember[0];

            Log.log($"Number of ledningMember -> {this.ledningMember?.Length.ToString()}");
            Log.log($"Number of ledningstraceMember -> {this.ledningstraceMember?.Length.ToString()}");
            Log.log($"Number of ledningskomponentMember -> {this.ledningskomponentMember?.Length.ToString()}");

            HashSet<string> names = new HashSet<string>();

            //prdDbg(ObjectDumper.Dump(ledningMember[0]));

            foreach (GraveforespoergselssvarTypeLedningMember member in ledningMember)
            {
                if (member.Item == null)
                {
                    Log.log($"ledningMember is null! Some enity has not been deserialized correct!");
                    continue;
                }
                
                ILerLedning ledning = member.Item as ILerLedning;
                ledning.DrawEntity2D(Database);

                names.Add(member.Item.ToString());
            }

            foreach (GraveforespoergselssvarTypeLedningstraceMember item in ledningstraceMember)
            {
                if (item.Ledningstrace == null)
                {
                    Log.log($"ledningstraceMember is null! Some enity has not been deserialized correct!");
                    continue;
                }
                names.Add(item.Ledningstrace.ToString());
            }

            foreach (GraveforespoergselssvarTypeLedningskomponentMember member in ledningskomponentMember)
            {
                if (member.Item == null)
                {
                    Log.log($"ledningskomponentMember is null! Some enity has not been deserialized correct!");
                    continue;
                }
                names.Add(member.Item.ToString());
            }

            foreach (string s in names)
            {
                prdDbg(s);
            }
        }

        #region Archive
        //public GraveforespoergselssvarTypeLedningMember[] getLedningMembers()
        //{
        //    if (this.ledningMember != null) 
        //        this.ledningMember?.Where(x => x != null && x.Item != null).ToArray();
        //    return new GraveforespoergselssvarTypeLedningMember[0];
        //}
        //public GraveforespoergselssvarTypeLedningstraceMember[] getLedningstraceMembers()
        //{
        //    if (this.ledningstraceMember != null) 
        //        return this.ledningstraceMember?.Where(x => x != null && x.Ledningstrace != null).ToArray();
        //    return new GraveforespoergselssvarTypeLedningstraceMember[0];
        //}
        //public GraveforespoergselssvarTypeLedningskomponentMember[] getLedningskomponentMembers()
        //{
        //    if (this.ledningskomponentMember != null)
        //        return this.ledningskomponentMember?.Where(x => x != null && x.Item != null).ToArray();
        //    return new GraveforespoergselssvarTypeLedningskomponentMember[0];
        //}
        #endregion
    }
}
