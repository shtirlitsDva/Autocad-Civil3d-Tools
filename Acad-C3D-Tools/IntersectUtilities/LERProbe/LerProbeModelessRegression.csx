// Load the current models, reader, window, graphic builder, highlight and session.
// The fixture database is disposable; preview geometry is transient. View changes
// used to exercise navigation are restored, and the modeless window is closed.
var lerModelessTests = new List<object>();
void ModelessTest(string name,Func<bool> check)
{
    try{lerModelessTests.Add(new{name,pass=check(),error=""});}
    catch(System.Exception ex){lerModelessTests.Add(new{name,pass=false,error=ex.Message});}
}
using(var fixtureDb=new Database(true,true))
using(var read=fixtureDb.TransactionManager.StartTransaction())
{
    var blocks=(BlockTable)read.GetObject(fixtureDb.BlockTableId,OpenMode.ForWrite);
    var model=(BlockTableRecord)read.GetObject(blocks[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
    var layers=(LayerTable)read.GetObject(fixtureDb.LayerTableId,OpenMode.ForWrite);
    using var redLayer=new LayerTableRecord{Name="LERPROBE_test_red",Color=CadColor.FromColorIndex(ColorMethod.ByAci,1)};
    layers.Add(redLayer);read.AddNewlyCreatedDBObject(redLayer,true);
    using var greenLayer=new LayerTableRecord{Name="LERPROBE_test_green",Color=CadColor.FromColorIndex(ColorMethod.ByAci,3)};
    layers.Add(greenLayer);read.AddNewlyCreatedDBObject(greenLayer,true);
    using var outer=new BlockTableRecord{Name="LERPROBE_test_outer"};blocks.Add(outer);read.AddNewlyCreatedDBObject(outer,true);
    using var inner=new BlockTableRecord{Name="LERPROBE_test_inner"};blocks.Add(inner);read.AddNewlyCreatedDBObject(inner,true);
    using var pipe=new Polyline();pipe.SetDatabaseDefaults(fixtureDb);
    pipe.AddVertexAt(0,new Point2d(0,0),0.5,0.2,0.2);pipe.AddVertexAt(1,new Point2d(10,0),0,0,0);
    inner.AppendEntity(pipe);read.AddNewlyCreatedDBObject(pipe,true);
    using var child=new BlockReference(Point3d.Origin,inner.ObjectId);child.SetDatabaseDefaults(fixtureDb);outer.AppendEntity(child);read.AddNewlyCreatedDBObject(child,true);
    using var first=new BlockReference(Point3d.Origin,outer.ObjectId);first.SetDatabaseDefaults(fixtureDb);model.AppendEntity(first);read.AddNewlyCreatedDBObject(first,true);
    using var second=new BlockReference(new Point3d(50,0,0),outer.ObjectId);second.SetDatabaseDefaults(fixtureDb);model.AppendEntity(second);read.AddNewlyCreatedDBObject(second,true);
    var paths=new List<LerProbeHighlightPath>();
    LerProbeHighlight.Resolve(read,pipe.ObjectId,new[]{first.ObjectId,child.ObjectId}).Match(path=>{paths.Add(path);return true;},error=>{Console.WriteLine(error);return false;});
    var path=paths.Single();
    ModelessTest("nested path orders complete selected pipe",()=>path.Path.GetObjectIds().SequenceEqual(new[]{first.ObjectId,child.ObjectId,pipe.ObjectId}));
    ModelessTest("container order is independent",()=>LerProbeHighlight.Resolve(read,pipe.ObjectId,new[]{child.ObjectId,first.ObjectId}).Match(
        p=>p.Path==path.Path,_=>false));
    ModelessTest("second instance has a distinct root",()=>LerProbeHighlight.Resolve(read,pipe.ObjectId,new[]{child.ObjectId,second.ObjectId}).Match(
        p=>p.RootId==second.ObjectId,_=>false));
    bool GraphicCheck(bool blue,Matrix3d transform,Func<LerProbeGraphic,bool> extra) =>
        LerProbeGraphicBuilder.Read(read,fixtureDb,path,transform).Match(graphic=>{
            using(graphic)return graphic.Blue==blue&&graphic.Entities.Count>0&&graphic.Entities.All(entity=>{
                using var color=entity.Color;
                return color.Red==(blue?0:255)&&color.Green==0&&color.Blue==(blue?255:0)&&entity.LayerId==fixtureDb.LayerZero;
            })&&extra(graphic);
        },error=>{Console.WriteLine(error);return false;});
    pipe.Color=CadColor.FromColorIndex(ColorMethod.ByAci,5);
    ModelessTest("blue source receives bright RGB red",()=>GraphicCheck(false,Matrix3d.Identity,_=>true));
    pipe.Color=CadColor.FromColorIndex(ColorMethod.ByAci,1);
    ModelessTest("ACI red source receives bright RGB blue",()=>GraphicCheck(true,Matrix3d.Identity,_=>true));
    pipe.Color=CadColor.FromRgb(255,0,0);
    ModelessTest("true-colour red source receives blue",()=>GraphicCheck(true,Matrix3d.Identity,_=>true));
    pipe.Color=CadColor.FromRgb(128,0,0);
    ModelessTest("dark red source receives blue",()=>GraphicCheck(true,Matrix3d.Identity,_=>true));
    pipe.Color=CadColor.FromRgb(255,0,255);
    ModelessTest("magenta source receives red",()=>GraphicCheck(false,Matrix3d.Identity,_=>true));
    pipe.LayerId=redLayer.ObjectId;pipe.Color=CadColor.FromColorIndex(ColorMethod.ByLayer,256);
    ModelessTest("ByLayer red receives blue",()=>GraphicCheck(true,Matrix3d.Identity,_=>true));
    pipe.LayerId=greenLayer.ObjectId;
    ModelessTest("ByLayer green receives red",()=>GraphicCheck(false,Matrix3d.Identity,_=>true));
    pipe.Color=CadColor.FromColorIndex(ColorMethod.ByBlock,0);child.Color=CadColor.FromColorIndex(ColorMethod.ByAci,1);
    ModelessTest("ByBlock red receives blue",()=>GraphicCheck(true,Matrix3d.Identity,_=>true));
    pipe.Color=CadColor.FromColorIndex(ColorMethod.ByLayer,256);pipe.LayerId=fixtureDb.LayerZero;
    child.Color=CadColor.FromColorIndex(ColorMethod.ByLayer,256);child.LayerId=fixtureDb.LayerZero;
    first.LayerId=redLayer.ObjectId;
    ModelessTest("nested layer zero inherits the red insertion layer",()=>GraphicCheck(true,Matrix3d.Identity,_=>true));
    pipe.Color=CadColor.FromColorIndex(ColorMethod.ByAci,3);
    var transform=Matrix3d.Displacement(new Vector3d(100,200,0))*Matrix3d.Scaling(2,Point3d.Origin);
    ModelessTest("scaled and translated arc retains exact bulge and endpoints",()=>GraphicCheck(false,transform,graphic=>{
        var p=(Polyline)graphic.Entities.Single();
        return p.GetPoint3dAt(0).DistanceTo(new Point3d(100,200,0))<1e-8&&p.GetPoint3dAt(1).DistanceTo(new Point3d(120,200,0))<1e-8&&Math.Abs(p.GetBulgeAt(0)-0.5)<1e-8;
    }));
    ModelessTest("nonuniform arc becomes an exact ellipse",()=>GraphicCheck(false,
        Matrix3d.AlignCoordinateSystem(Point3d.Origin,Vector3d.XAxis,Vector3d.YAxis,Vector3d.ZAxis,Point3d.Origin,new Vector3d(2,0,0),Vector3d.YAxis,Vector3d.ZAxis),
        graphic=>graphic.Entities.Single() is Ellipse));
    ModelessTest("source geometry and colour are unchanged by builder",()=>pipe.GetPoint3dAt(0)==Point3d.Origin&&pipe.GetPoint3dAt(1)==new Point3d(10,0,0)&&pipe.Color.ColorIndex==3);
    var preview=new List<LerProbeGraphic>();
    LerProbeGraphicBuilder.Read(read,fixtureDb,path,Matrix3d.Identity).Match(g=>{preview.Add(g);return true;},error=>{Console.WriteLine(error);return false;});
    var owned=preview.Single();
    using(var highlight=new LerProbeHighlight(Doc,owned))
    {
        ModelessTest("transient overlay displays in active drawing",()=>highlight.Show().Match(shown=>shown&&highlight.IsVisible,_=>false));
        highlight.Hide();
        ModelessTest("hide removes overlay without destroying cached geometry",()=>!highlight.IsVisible&&owned.Entities.All(e=>!e.IsDisposed));
        ModelessTest("show restores cached overlay",()=>highlight.Show().Match(shown=>shown&&highlight.IsVisible,_=>false));
    }
    ModelessTest("disposal releases preview geometry",()=>owned.Entities.All(e=>e.IsDisposed));
    var snapshot=new LerProbeSnapshot("LER fixture","fixture.dwg","Water","1",0,Array.Empty<LerProbeProperty>());
    var modelessGraphic=LerProbeGraphicBuilder.Read(read,fixtureDb,path,Matrix3d.Identity);
    LerProbeSession.Open(Doc,snapshot,modelessGraphic);
    var windows=System.Windows.Forms.Application.OpenForms.Cast<Form>().OfType<LerProbeWindow>().ToArray();
    ModelessTest("window is visible and modeless after Open returns",()=>windows.Length==1&&windows[0].Visible&&!windows[0].Modal);
    using(var originalView=Ed.GetCurrentView())
    using(var movedView=Ed.GetCurrentView())
    {
        try
        {
            movedView.Width=originalView.Width*0.8;movedView.Height=originalView.Height*0.8;
            movedView.CenterPoint=originalView.CenterPoint+new Vector2d(1,2);
            Ed.SetCurrentView(movedView);
            using(var actual=Ed.GetCurrentView())
                ModelessTest("pan and zoom remain available with probe open",()=>Math.Abs(actual.Width-movedView.Width)<1e-7&&actual.CenterPoint.GetDistanceTo(movedView.CenterPoint)<1e-7);
        }
        finally{Ed.SetCurrentView(originalView);}
    }
    var buttons=windows[0].Controls.Cast<Control>().SelectMany(c=>c.Controls.Cast<Control>()).OfType<TableLayoutPanel>().SelectMany(c=>c.Controls.Cast<Control>()).OfType<Button>().ToArray();
    buttons.Single(b=>b.Text=="Close").PerformClick();
    ModelessTest("Close button disposes the modeless window",()=>windows[0].IsDisposed);
    ModelessTest("Close button releases its preview",()=>modelessGraphic.Match(g=>g.Entities.All(e=>e.IsDisposed),_=>false));
    var replacement=LerProbeGraphicBuilder.Read(read,fixtureDb,path,Matrix3d.Identity);
    LerProbeSession.Open(Doc,snapshot,replacement);
    var oldWindow=System.Windows.Forms.Application.OpenForms.Cast<Form>().OfType<LerProbeWindow>().Single();
    var next=LerProbeGraphicBuilder.Read(read,fixtureDb,path,Matrix3d.Identity);
    LerProbeSession.Open(Doc,snapshot,next);
    ModelessTest("probing again closes the previous window",()=>oldWindow.IsDisposed&&System.Windows.Forms.Application.OpenForms.Cast<Form>().OfType<LerProbeWindow>().Count()==1);
    ModelessTest("probing again releases the previous overlay",()=>replacement.Match(g=>g.Entities.All(e=>e.IsDisposed),_=>false));
    LerProbeSession.Reset();
    ModelessTest("plugin reset closes windows and releases overlays",()=>!System.Windows.Forms.Application.OpenForms.Cast<Form>().OfType<LerProbeWindow>().Any()&&next.Match(g=>g.Entities.All(e=>e.IsDisposed),_=>false));
}
new{tests=lerModelessTests,caseCount=lerModelessTests.Count}



