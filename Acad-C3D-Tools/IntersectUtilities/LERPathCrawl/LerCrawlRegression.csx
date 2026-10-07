// Run in ACD-MCP after loading the five LerCrawl core files as script declarations
// (remove file-scoped namespaces and put using directives first). All fixtures use
// disposable databases/transient curves; the user's drawing is never modified.
var lerCrawlTests = new List<object>();
string lerFixtureFolder=Path.Combine(Path.GetTempPath(),"IntersectUtilities-LerCrawl-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(lerFixtureFolder);
string lerFixturePath=Path.Combine(lerFixtureFolder,"fixture_ler.dwg");
void LerTest(string name, Func<bool> check)
{
    try { lerCrawlTests.Add(new { name, pass = check(), error = "" }); }
    catch (System.Exception ex) { lerCrawlTests.Add(new { name, pass = false, error = ex.Message }); }
}
LerCrawlSegment LerSeg(double x1, double y1, double x2, double y2, double width = 0.6, double bulge = 0)
    => new(new Point2d(x1,y1),new Point2d(x2,y2),bulge,width,width,ObjectId.Null);
bool LerNear(double a, double b) => Math.Abs(a-b) < 1e-6;
bool LerPointNear(Point2d a, double x, double y) => a.GetDistanceTo(new Point2d(x,y)) < 1e-6;
LerCrawlResult<LerCrawlRoute> LerRoute(LerCrawlSegment[] segments, Point3d start, Point3d end)
{
    using var graph = new LerCrawlGraph();
    graph.Read(segments);
    return LerCrawlSession.Create(graph,ObjectId.Null,start).Match(
        source => source.Session.Route(end), error => LerCrawlResult<LerCrawlRoute>.Fault(error));
}
LerTest("centreline preserves arc and zero-width sections",()=>{
    using var p=LerCrawlPolylineBuilder.Centerline(new[]{LerSeg(5,0,-5,0,0,1),LerSeg(-5,0,-10,0)});
    return p.NumberOfVertices==3 && LerNear(p.GetBulgeAt(0),1) && LerNear(p.Length,5*Math.PI+5);
});
LerTest("start and end cut a straight interior",()=>LerRoute(new[]{LerSeg(0,0,10,0)},new Point3d(2,0,0),new Point3d(8,0,0)).Match(r=>
    r.Segments.Count==1 && LerPointNear(r.Segments[0].Start,2,0) && LerPointNear(r.Segments[0].End,8,0),_=>false));
LerTest("reverse interior arc retains exact bulge",()=>{
    var segment=LerSeg(5,0,-5,0,0.6,1);using var p=segment.CreateCurve();
    return LerRoute(new[]{segment},p.GetPointAtParameter(0.75),p.GetPointAtParameter(0.25)).Match(r=>
        r.Segments.Count==1 && LerNear(r.Segments[0].Bulge,-Math.Tan(Math.PI/8)) &&
        LerNear(r.Segments[0].StartWidth,0.6),_=>false);
});
LerTest("shortest route through loop",()=>LerRoute(new[]{LerSeg(0,0,10,0),LerSeg(10,0,10,10),LerSeg(0,0,0,10),LerSeg(0,10,10,10)},
    new Point3d(1,0,0),new Point3d(10,9,0)).Match(r=>{
        using var p=LerCrawlPolylineBuilder.Centerline(r.Segments);return r.Segments.Count==2 && LerNear(p.Length,18);},_=>false));
LerTest("endpoint to interior T-junction",()=>LerRoute(new[]{LerSeg(0,0,10,0),LerSeg(5,0,5,5)},new Point3d(1,0,0),new Point3d(5,4,0)).Match(r=>{
    using var p=LerCrawlPolylineBuilder.Centerline(r.Segments);return LerNear(p.Length,8);},_=>false));
LerTest("crossing interiors remain disconnected",()=>LerRoute(new[]{LerSeg(0,0,10,0),LerSeg(5,-5,5,5)},new Point3d(1,0,0),new Point3d(5,4,0)).Match(_=>false,_=>true));
LerTest("10 mm gap connects and is preserved",()=>LerRoute(new[]{LerSeg(0,0,5,0),LerSeg(5.01,0,10.01,0)},new Point3d(1,0,0),new Point3d(9,0,0)).Match(r=>{
    using var p=LerCrawlPolylineBuilder.Centerline(r.Segments);return LerNear(p.Length,8) && p.NumberOfVertices==4;},_=>false));
LerTest("30 mm gap does not connect",()=>LerRoute(new[]{LerSeg(0,0,5,0),LerSeg(5.03,0,10.03,0)},new Point3d(1,0,0),new Point3d(9,0,0)).Match(_=>false,_=>true));
LerTest("same start and end rejects empty route",()=>LerRoute(new[]{LerSeg(0,0,10,0)},new Point3d(2,0,0),new Point3d(2,0,0)).Match(_=>false,_=>true));
LerTest("closed circle chooses an arc route",()=>LerRoute(new[]{LerSeg(5,0,-5,0,0.6,1),LerSeg(-5,0,5,0,0.6,1)},new Point3d(5,0,0),new Point3d(-5,0,0)).Match(r=>{
    using var p=LerCrawlPolylineBuilder.Centerline(r.Segments);return LerNear(p.Length,5*Math.PI);},_=>false));
LerTest("xref scale doubles physical width",()=>{
    using var p=new Polyline();p.AddVertexAt(0,new Point2d(0,0),0,0.6,0.6);p.AddVertexAt(1,new Point2d(10,0),0,0,0);
    return LerCrawlReader.ReadPolyline(p,Matrix3d.Scaling(2,Point3d.Origin)).Match(r=>
        r.Items.Count==1 && LerNear(r.Items[0].StartWidth,1.2) && LerPointNear(r.Items[0].End,20,0),_=>false);
});
LerTest("mirror keeps arc in host coordinates",()=>{
    using var p=LerSeg(5,0,-5,0,0.6,1).CreateCurve();p.SetStartWidthAt(0,0.6);p.SetEndWidthAt(0,0.6);
    using var plane=new Plane(Point3d.Origin,Vector3d.XAxis);var matrix=Matrix3d.Mirroring(plane);
    return LerCrawlReader.ReadPolyline(p,matrix).Match(r=>{
        using var q=r.Items[0].CreateCurve();return LerNear(r.Items[0].Bulge,-1) &&
            q.GetPointAtParameter(0.5).DistanceTo(p.GetPointAtParameter(0.5).TransformBy(matrix))<1e-6;
    },_=>false);
});
LerTest("nonuniform xref scale rejects misleading arcs",()=>{
    using var p=new Polyline();return LerCrawlReader.ReadPolyline(p,
    Matrix3d.AlignCoordinateSystem(Point3d.Origin,Vector3d.XAxis,Vector3d.YAxis,Vector3d.ZAxis,
    Point3d.Origin,new Vector3d(2,0,0),Vector3d.YAxis,Vector3d.ZAxis)).Match(_=>false,_=>true);
});
LerTest("tilted polylines reject elliptical projection",()=>{
    using var p=LerSeg(0,0,10,0).CreateCurve();p.TransformBy(Matrix3d.Rotation(0.5,Vector3d.XAxis,Point3d.Origin));
    return LerCrawlReader.ReadPolyline(p,Matrix3d.Identity).Match(_=>false,_=>true);
});
LerTest("qualified layer names are exact leaf names",()=>LerCrawlReader.LocalLayer("root|nested|Vandledning_L2")=="Vandledning_L2" &&
    LerCrawlReader.LocalLayer("root|Vandledning_L20")!="Vandledning_L2");

// Real DWG/xref fixture: includes a nested ordinary block and two similarly named
// utility layers. It is created in the workspace, never in the user's drawing.
LerTest("real xref filters layer, reads nested blocks and scales width",()=>{
    string path=lerFixturePath;
    using(var sourceDb=new Database(true,true)){
        using(var tr=sourceDb.TransactionManager.StartTransaction()){
            var layers=(LayerTable)tr.GetObject(sourceDb.LayerTableId,OpenMode.ForWrite);
            foreach(string name in new[]{"Vandledning_L2","Vandledning_L20"}){
                using var l=new LayerTableRecord{Name=name};layers.Add(l);tr.AddNewlyCreatedDBObject(l,true);
            }
            var bt=(BlockTable)tr.GetObject(sourceDb.BlockTableId,OpenMode.ForWrite);
            using var nested=new BlockTableRecord{Name="LER_nested"};var nestedId=bt.Add(nested);tr.AddNewlyCreatedDBObject(nested,true);
            var model=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
            foreach(var data in new[]{(Record:model,Segment:LerSeg(0,0,10,0),Layer:"Vandledning_L2"),
                (Record:model,Segment:LerSeg(0,10,10,10),Layer:"Vandledning_L20"),
                (Record:nested,Segment:LerSeg(10,0,20,0,1.2),Layer:"Vandledning_L2")}){
                using var p=data.Segment.CreateCurve();p.SetDatabaseDefaults(sourceDb);p.ConstantWidth=data.Segment.StartWidth;p.Layer=data.Layer;
                data.Record.AppendEntity(p);tr.AddNewlyCreatedDBObject(p,true);
            }
            using var instance=new BlockReference(Point3d.Origin,nestedId);instance.SetDatabaseDefaults(sourceDb);model.AppendEntity(instance);tr.AddNewlyCreatedDBObject(instance,true);
            tr.Commit();
        }
        sourceDb.SaveAs(path,DwgVersion.Current);
    }
    using var hostDb=new Database(true,true);
    var xref=hostDb.AttachXref(path,"LER_fixture");hostDb.ResolveXrefs(false,false);
    using var hostTr=hostDb.TransactionManager.StartTransaction();
    var transform=Matrix3d.Displacement(new Vector3d(100,200,0))*Matrix3d.Scaling(2,Point3d.Origin);
    return LerCrawlReader.ReadRecord(hostTr,xref,transform,"Vandledning_L2").Match(r=>
        r.Items.Count==2 && LerPointNear(r.Items[0].Start,100,200) && LerNear(r.Items[0].StartWidth,1.2) &&
        LerNear(r.Items[1].StartWidth,2.4),_=>false);
});
bool LerFixturePick(bool nestedPick, bool rotatedUcs, bool mirrored, Func<LerCrawlSource,bool> check)
{
    using var hostDb=new Database(true,true);
    var xref=hostDb.AttachXref(lerFixturePath,"LER_fixture");
    hostDb.ResolveXrefs(false,false);
    using var tr=hostDb.TransactionManager.StartTransaction();
    var bt=(BlockTable)tr.GetObject(hostDb.BlockTableId,OpenMode.ForRead);
    var model=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
    using var instance=new BlockReference(Point3d.Origin,xref);instance.SetDatabaseDefaults(hostDb);
    Matrix3d transform=Matrix3d.Displacement(new Vector3d(100,200,0))*Matrix3d.Scaling(2,Point3d.Origin);
    if(mirrored){using var plane=new Plane(Point3d.Origin,Vector3d.XAxis);transform=transform*Matrix3d.Mirroring(plane);}
    instance.TransformBy(transform);var instanceId=model.AppendEntity(instance);tr.AddNewlyCreatedDBObject(instance,true);
    var root=(BlockTableRecord)tr.GetObject(xref,OpenMode.ForRead);
    var ids=root.Cast<ObjectId>().ToArray();
    ObjectId selected;
    ObjectId[] containers;
    if(nestedPick){
        var block=(BlockReference)tr.GetObject(ids.Single(id=>tr.GetObject(id,OpenMode.ForRead) is BlockReference),OpenMode.ForRead);
        var child=(BlockTableRecord)tr.GetObject(block.BlockTableRecord,OpenMode.ForRead);
        selected=child.Cast<ObjectId>().First();containers=new[]{instanceId,block.ObjectId};
    }else{selected=ids.First(id=>tr.GetObject(id,OpenMode.ForRead) is Polyline);containers=new[]{instanceId};}
    var source=(Polyline)tr.GetObject(selected,OpenMode.ForRead);
    var start=source.StartPoint.TransformBy(transform);
    Matrix3d ucs=rotatedUcs ? Matrix3d.Displacement(new Vector3d(20,30,0))*Matrix3d.Rotation(0.3,Vector3d.ZAxis,Point3d.Origin) : Matrix3d.Identity;
    var constructor=typeof(PromptNestedEntityResult).GetConstructors(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
        .Single(c=>c.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(PromptStatus),typeof(string),typeof(ObjectId),typeof(Point3d),typeof(Matrix3d),typeof(ObjectId[])}));
    var prompt=(PromptNestedEntityResult)constructor.Invoke(new object[]{PromptStatus.OK,"",selected,start.TransformBy(ucs.Inverse()),transform,containers});
    return LerCrawlReader.Read(tr,prompt,ucs).Match(check,_=>false);
}
LerTest("selected entity resolves through real xref owner IDs",()=>LerFixturePick(false,false,false,(LerCrawlSource s)=>
    s.SourceLayer=="Vandledning_L2" && s.Segments.Count==2 && LerNear(s.StartPick.X,100) && LerNear(s.StartPick.Y,200)));
LerTest("nested pick resolves regardless of container array order",()=>LerFixturePick(true,false,false,(LerCrawlSource s)=>
    s.Segments.Count==2 && LerNear(s.StartPick.X,120) && s.Segments[1].SourceId==s.SelectedId));
LerTest("xref pick in rotated UCS returns host WCS",()=>LerFixturePick(true,true,false,(LerCrawlSource s)=>
    LerNear(s.StartPick.X,120) && LerNear(s.StartPick.Y,200)));
LerTest("mirrored xref pick reads correct plan coordinates",()=>LerFixturePick(true,false,true,(LerCrawlSource s)=>
    LerNear(s.StartPick.X,80) && LerPointNear(s.Segments[1].End,60,200) && LerNear(s.Segments[1].StartWidth,2.4)));
LerTest("zero and tapered source widths produce a zero-width centreline",()=>{
    using var p=LerCrawlPolylineBuilder.Centerline(new[]{
        LerSeg(0,0,5,0,0),LerSeg(5,0,10,0,2) with{EndWidth=4},LerSeg(10,0,15,0,8)});
    return p.NumberOfVertices==4 && LerNear(p.Length,15) &&
        Enumerable.Range(0,p.NumberOfVertices).All(i=>LerNear(p.GetStartWidthAt(i),0) && LerNear(p.GetEndWidthAt(i),0));
});
LerTest("large pipe widths do not reject small centreline arcs",()=>{
    using var p=LerCrawlPolylineBuilder.Centerline(new[]{LerSeg(1,0,-1,0,10,1)});
    return LerNear(p.Length,Math.PI) && LerNear(p.GetBulgeAt(0),1);
});
LerTest("map-coordinate bend produces only original centreline vertices",()=>{
    var data=new[]{LerSeg(722000,6189000,722010,6189000,2),LerSeg(722010,6189000,722010,6189010,0.6)};
    using var p=LerCrawlPolylineBuilder.Centerline(data);
    return p.NumberOfVertices==3 && LerNear(p.Length,20) && LerPointNear(p.GetPoint2dAt(1),722010,6189000);
});
LerTest("real xref route writes its centreline independently of widths",()=>LerFixturePick(false,false,false,(LerCrawlSource s)=>{
    using var graph=new LerCrawlGraph();graph.Read(s.Segments);
    return LerCrawlSession.Create(graph,s.SelectedId,new Point3d(102,200,0)).Match(start=>
        start.Session.Route(new Point3d(134,200,0)).Match(r=>{
            using var p=LerCrawlPolylineBuilder.Centerline(r.Segments);
            return LerNear(p.Length,32) && LerPointNear(p.GetPoint2dAt(0),102,200) &&
                LerPointNear(p.GetPoint2dAt(p.NumberOfVertices-1),134,200) && LerNear(p.ConstantWidth,0);
        },_=>false),_=>false);
}));
bool LerWriteFixture(bool existing, bool current, bool off, bool frozen, bool paper, bool zeroWidth=false)
{
    using var db=new Database(true,true);
    if(paper)db.TileMode=false;
    using(var setup=db.TransactionManager.StartTransaction()){
        if(existing){
            var setupLayers=(LayerTable)setup.GetObject(db.LayerTableId,OpenMode.ForWrite);
            using var setupLayer=new LayerTableRecord{Name=LerCrawlSettings.Layer,
                Color=Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci,4)};
            var setupId=setupLayers.Add(setupLayer);setup.AddNewlyCreatedDBObject(setupLayer,true);
            if(current)db.Clayer=setupId;
            if(off)setupLayer.IsOff=true;
            if(frozen)setupLayer.IsFrozen=true;
        }
        setup.Commit();
    }
    ObjectId originalCurrent=db.Clayer;
    var data=zeroWidth ? new[]{LerSeg(0,0,5,0),LerSeg(5,0,10,0,0),LerSeg(10,0,15,0) with{EndWidth=2}} :
        new[]{LerSeg(722000,6189000,722010,6189000,2),LerSeg(722010,6189000,722020,6189010,0.6)};
    using var centre=LerCrawlPolylineBuilder.Centerline(data);
    ObjectId id;
    using(var write=db.TransactionManager.StartTransaction()){
        id=LerCrawlDrawingWriter.Append(db,write,centre);
        write.Commit();
    }
    using var read=db.TransactionManager.StartTransaction();
    var layers=(LayerTable)read.GetObject(db.LayerTableId,OpenMode.ForRead);
    var layer=(LayerTableRecord)read.GetObject(layers[LerCrawlSettings.Layer],OpenMode.ForRead);
    var blocks=(BlockTable)read.GetObject(db.BlockTableId,OpenMode.ForRead);
    var model=(BlockTableRecord)read.GetObject(blocks[BlockTableRecord.ModelSpace],OpenMode.ForRead);
    var p=(Polyline)read.GetObject(id,OpenMode.ForRead);
    return model.Cast<ObjectId>().Count()==1 && db.Clayer==originalCurrent &&
        !layer.IsOff && !layer.IsFrozen && layer.Color.ColorIndex==(existing?4:2) &&
        p.OwnerId==model.ObjectId && p.LayerId==layer.ObjectId && LerNear(p.ConstantWidth,0) &&
        p.Color.ColorMethod==Autodesk.AutoCAD.Colors.ColorMethod.ByLayer &&
        p.NumberOfVertices==(zeroWidth?4:3);
}
LerTest("writer creates missing reference layer and one centreline",()=>LerWriteFixture(false,false,false,false,false));
LerTest("writer reuses existing layer and preserves its colour",()=>LerWriteFixture(true,false,false,false,false));
LerTest("writer retains current reference layer",()=>LerWriteFixture(true,true,false,false,false));
LerTest("writer turns on current reference layer without changing it",()=>LerWriteFixture(true,true,true,false,false));
LerTest("writer turns on non-current reference layer",()=>LerWriteFixture(true,false,true,false,false));
LerTest("writer thaws frozen non-current reference layer",()=>LerWriteFixture(true,false,false,true,false));
LerTest("writer turns on and thaws non-current reference layer",()=>LerWriteFixture(true,false,true,true,false));
LerTest("writer uses model space while current space is paper space",()=>LerWriteFixture(true,true,false,false,true));
LerTest("writer creates one centreline across zero and tapered widths",()=>LerWriteFixture(true,true,false,false,false,true));
File.Delete(lerFixturePath);
Directory.Delete(lerFixtureFolder);
new{tests=lerCrawlTests,caseCount=lerCrawlTests.Count}

