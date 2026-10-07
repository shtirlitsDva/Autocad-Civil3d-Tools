// Run after loading the crawl core, LerProbeModels, LerProbeReader, and LerProbeWindow.
// All property sets and xrefs are created in disposable databases/temp files.
var lerProbeTests = new List<object>();
void ProbeTest(string name, Func<bool> check)
{
    try { lerProbeTests.Add(new { name, pass=check(), error="" }); }
    catch(System.Exception ex) { lerProbeTests.Add(new { name, pass=false, error=ex.Message }); }
}
string probeFixtureFolder=Path.Combine(Path.GetTempPath(),"IntersectUtilities-LerProbe-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(probeFixtureFolder);
string probeFixtureFile=Path.Combine(probeFixtureFolder,"ler_properties.dwg");
using(var sourceDb=new Database(true,true))
{
    Database previousWorkingDb=HostApplicationServices.WorkingDatabase;
    try
    {
        HostApplicationServices.WorkingDatabase=sourceDb;
        using(var setup=sourceDb.TransactionManager.StartTransaction())
        {
            var layers=(LayerTable)setup.GetObject(sourceDb.LayerTableId,OpenMode.ForWrite);
            using var layer=new LayerTableRecord{Name="Vandledning_L2"};layers.Add(layer);setup.AddNewlyCreatedDBObject(layer,true);
            var blocks=(BlockTable)setup.GetObject(sourceDb.BlockTableId,OpenMode.ForWrite);
            var model=(BlockTableRecord)setup.GetObject(blocks[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
            using var nested=new BlockTableRecord{Name="LER_nested"};var nestedId=blocks.Add(nested);setup.AddNewlyCreatedDBObject(nested,true);
            using var pipe=new Polyline();pipe.SetDatabaseDefaults(sourceDb);pipe.Layer="Vandledning_L2";
            pipe.AddVertexAt(0,new Point2d(0,0),0,0,0);pipe.AddVertexAt(1,new Point2d(10,0),0,0,0);
            nested.AppendEntity(pipe);setup.AddNewlyCreatedDBObject(pipe,true);
            foreach(string setName in new[]{"WaterInfo","OtherInfo"})
            {
                using var definition=new PropertySetDefinition();definition.SetToStandard(sourceDb);definition.SubSetDatabaseDefaults(sourceDb);
                definition.SetAppliesToFilter(new System.Collections.Specialized.StringCollection{"AcDbPolyline"},false);
                var text=new PropertyDefinition();text.SetToStandard(sourceDb);text.SubSetDatabaseDefaults(sourceDb);
                text.Name="Owner";text.Description="Ledningsejer ÆØÅ";text.DataType=Autodesk.Aec.PropertyData.DataType.Text;
                text.DefaultData="";definition.Definitions.Add(text);
                if(setName=="WaterInfo")
                {
                    var number=new PropertyDefinition();number.SetToStandard(sourceDb);number.SubSetDatabaseDefaults(sourceDb);
                    number.Name="Diameter";number.DataType=Autodesk.Aec.PropertyData.DataType.Real;number.DefaultData=0.0;definition.Definitions.Add(number);
                    var automatic=new PropertyDefinition();automatic.SetToStandard(sourceDb);automatic.SubSetDatabaseDefaults(sourceDb);
                    automatic.Name="Length";automatic.SetAutomaticData("AcDbPolyline","Length");definition.Definitions.Add(automatic);
                }
                var dictionary=new DictionaryPropertySetDefinitions(sourceDb);dictionary.AddNewRecord(setName,definition);setup.AddNewlyCreatedDBObject(definition,true);
                PropertyDataServices.AddPropertySet(pipe,definition.ObjectId);
                var set=(PropertySet)setup.GetObject(PropertyDataServices.GetPropertySet(pipe,definition.ObjectId),OpenMode.ForWrite);
                set.SetAt(set.PropertyNameToId("Owner"),setName=="WaterInfo"?"Vandværk ÆØÅ":"Second owner");
                if(setName=="WaterInfo")set.SetAt(set.PropertyNameToId("Diameter"),0.09);
            }
            using var nestedInstance=new BlockReference(Point3d.Origin,nestedId);model.AppendEntity(nestedInstance);setup.AddNewlyCreatedDBObject(nestedInstance,true);
            using var empty=new Polyline();empty.AddVertexAt(0,new Point2d(0,10),0,0,0);empty.AddVertexAt(1,new Point2d(10,10),0,0,0);
            model.AppendEntity(empty);setup.AddNewlyCreatedDBObject(empty,true);
            using var poly3d=new Polyline3d(Poly3dType.SimplePoly,new Point3dCollection(new[]{new Point3d(0,20,0),new Point3d(10,20,5)}),false);
            model.AppendEntity(poly3d);setup.AddNewlyCreatedDBObject(poly3d,true);
            setup.Commit();
        }
        sourceDb.SaveAs(probeFixtureFile,DwgVersion.Current);
    }
    finally{HostApplicationServices.WorkingDatabase=previousWorkingDb;}
}
using(var hostDb=new Database(true,true))
{
    var xrefId=hostDb.AttachXref(probeFixtureFile,"LER_probe_fixture");hostDb.ResolveXrefs(false,false);
    using var hostRead=hostDb.TransactionManager.StartTransaction();
    var blocks=(BlockTable)hostRead.GetObject(hostDb.BlockTableId,OpenMode.ForRead);
    var model=(BlockTableRecord)hostRead.GetObject(blocks[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
    using var instance=new BlockReference(Point3d.Origin,xrefId);var instanceId=model.AppendEntity(instance);hostRead.AddNewlyCreatedDBObject(instance,true);
    var root=(BlockTableRecord)hostRead.GetObject(xrefId,OpenMode.ForRead);
    var nested=(BlockReference)root.Cast<ObjectId>().Select(id=>hostRead.GetObject(id,OpenMode.ForRead)).OfType<BlockReference>().Single();
    var nestedRecord=(BlockTableRecord)hostRead.GetObject(nested.BlockTableRecord,OpenMode.ForRead);
    ObjectId selectedId=nestedRecord.Cast<ObjectId>().Single();
    var containers=new[]{instanceId,nested.ObjectId};
    var snapshot=new List<LerProbeSnapshot>();
    ProbeTest("nested source polyline reads both property sets",()=>LerProbeReader.Read(hostRead,selectedId,containers).Match(value=>{
        snapshot.Add(value);return value.PropertySetCount==2&&value.Properties.Count==4&&value.Properties.All(p=>!p.Unavailable);
    },_=>false));
    ProbeTest("container order does not affect probing",()=>LerProbeReader.Read(hostRead,selectedId,containers.Reverse().ToArray()).Match(
        value=>value.Properties.SequenceEqual(snapshot[0].Properties),_=>false));
    ProbeTest("xref metadata identifies source file, handle and leaf layer",()=>snapshot[0].SourceFile.EndsWith("ler_properties.dwg")&&
        snapshot[0].XrefNames=="LER_probe_fixture"&&snapshot[0].Layer=="Vandledning_L2"&&snapshot[0].Handle.Length>0);
    ProbeTest("duplicate property names stay separated by property set",()=>snapshot[0].Properties.Count(p=>p.Property=="Owner")==2&&
        snapshot[0].Properties.Single(p=>p.PropertySet=="WaterInfo"&&p.Property=="Owner").Value=="Vandværk ÆØÅ"&&
        snapshot[0].Properties.Single(p=>p.PropertySet=="OtherInfo"&&p.Property=="Owner").Value=="Second owner");
    ProbeTest("numeric property retains value and type",()=>{
        var row=snapshot[0].Properties.Single(p=>p.Property=="Diameter");
        return row.DataType=="Real"&&Math.Abs(double.Parse(row.Value,CultureInfo.CurrentCulture)-0.09)<1e-9;
    });
    ProbeTest("automatic length evaluates on the source polyline",()=>{
        var row=snapshot[0].Properties.Single(p=>p.Property=="Length");
        return row.Automatic&&!row.Unavailable&&Math.Abs(double.Parse(row.Value,CultureInfo.CurrentCulture)-10)<1e-9;
    });
    ObjectId emptyId=root.Cast<ObjectId>().First(id=>hostRead.GetObject(id,OpenMode.ForRead) is Polyline);
    ObjectId poly3dId=root.Cast<ObjectId>().Single(id=>hostRead.GetObject(id,OpenMode.ForRead) is Polyline3d);
    ProbeTest("no attached property sets is an explicit empty result",()=>LerProbeReader.Read(hostRead,emptyId,new[]{instanceId}).Match(
        value=>value.PropertySetCount==0&&value.Properties.Count==0,_=>false));
    ProbeTest("3D polyline is accepted for inspection",()=>LerProbeReader.Read(hostRead,poly3dId,new[]{instanceId}).Match(
        value=>value.PropertySetCount==0&&value.Properties.Count==0,_=>false));
    using var hostPipe=new Polyline();hostPipe.AddVertexAt(0,new Point2d(0,0),0,0,0);hostPipe.AddVertexAt(1,new Point2d(10,0),0,0,0);
    var hostPipeId=model.AppendEntity(hostPipe);hostRead.AddNewlyCreatedDBObject(hostPipe,true);
    ProbeTest("host polyline is rejected without an xref container",()=>LerProbeReader.Read(hostRead,hostPipeId,Array.Empty<ObjectId>()).Match(_=>false,_=>true));
    ProbeTest("non-polyline selection is rejected",()=>LerProbeReader.Read(hostRead,instanceId,Array.Empty<ObjectId>()).Match(_=>false,_=>true));
    ProbeTest("empty-property dialog explains absence",()=>{
        using var window=new LerProbeWindow(snapshot[0] with{PropertySetCount=0,Properties=Array.Empty<LerProbeProperty>()});
        window.CreateControl();var handle=window.Handle;window.PerformLayout();
        var label=window.Controls.Cast<Control>().SelectMany(c=>c.Controls.Cast<Control>()).OfType<TableLayoutPanel>()
            .SelectMany(c=>c.Controls.Cast<Control>()).OfType<Label>().Single(l=>l.Text.Contains("no attached"));
        return label.Text=="This polyline has no attached property sets.";
    });
}
File.Delete(probeFixtureFile);Directory.Delete(probeFixtureFolder);
new{tests=lerProbeTests,caseCount=lerProbeTests.Count}
