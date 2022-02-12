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

namespace LERImporter.Schema
{
    public partial class GraveforespoergselssvarType
    {
        public Database Database { get; set; }
        public GraveforespoergselssvarTypeLedningMember[] getLedningMembers()
        {
            if (this.ledningMember != null) 
                this.ledningMember.Where(x => x != null && x.Item != null).ToArray();
            return new GraveforespoergselssvarTypeLedningMember[0];
        }
        public GraveforespoergselssvarTypeLedningstraceMember[] getLedningstraceMembers()
        {
            if (this.ledningstraceMember != null) 
                return this.ledningstraceMember?.Where(x => x != null && x.Ledningstrace != null).ToArray();
            return new GraveforespoergselssvarTypeLedningstraceMember[0];
        }
        public GraveforespoergselssvarTypeLedningskomponentMember[] getLedningskomponentMembers()
        {
            if (this.ledningskomponentMember != null)
                return this.ledningskomponentMember?.Where(x => x != null && x.Item != null).ToArray();
            return new GraveforespoergselssvarTypeLedningskomponentMember[0];
        }

        public void test()
        {
            foreach (GraveforespoergselssvarTypeLedningMember member in getLedningMembers())
            {
                prdDbg(member.Item.ToString());
            }
            
            //foreach (GraveforespoergselssvarTypeLedningstraceMember item in getLedningstraceMembers())
            //{
            //    prdDbg(item.Ledningstrace?.Item.ToString());
            //}

            //foreach (GraveforespoergselssvarTypeLedningskomponentMember member in getLedningskomponentMembers())
            //{
            //    prdDbg(member.Item.ToString());
            //}
        }
    }
}
