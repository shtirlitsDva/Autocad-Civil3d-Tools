# AutoCAD API Class Reference

This document stores metadata for AutoCAD API classes used in this project.

---

## Table of Contents

- [Autodesk.AutoCAD.GraphicsInterface](#autodeskautocadgraphicsinterface)
  - [DrawableOverrule](#drawableoverrule)
  - [WorldDraw](#worlddraw)
  - [WorldGeometry](#worldgeometry)
  - [SubEntityTraits](#subentitytraits)
  - [Geometry (base class)](#geometry-base-class)
- [Autodesk.AutoCAD.Colors](#autodeskautocadcolors)
  - [Color](#color)

---

## Autodesk.AutoCAD.GraphicsInterface

### DrawableOverrule

```csharp
namespace Autodesk.AutoCAD.GraphicsInterface
{
    [Wrapper("AcGiDrawableOverrule")]
    public abstract class DrawableOverrule : Overrule
    {
        protected internal DrawableOverrule();

        public virtual int SetAttributes(Drawable drawable, DrawableTraits traits);
        public sealed override void SetCustomFilter();
        public sealed override void SetExtensionDictionaryEntryFilter(string entryName);
        public sealed override void SetIdFilter(ObjectId[] ids);
        public sealed override void SetNoFilter();
        public sealed override void SetXDataFilter(string registeredApplicationName);
        public virtual void ViewportDraw(Drawable drawable, ViewportDraw vd);
        public virtual int ViewportDrawLogicalFlags(Drawable drawable, ViewportDraw vd);
        public virtual bool WorldDraw(Drawable drawable, WorldDraw wd);
    }
}
```

**Key Methods:**
- `WorldDraw(Drawable drawable, WorldDraw wd)` - Override to customize world-coordinate drawing
- `ViewportDraw(Drawable drawable, ViewportDraw vd)` - Override to customize viewport-specific drawing
- `SetAttributes(Drawable drawable, DrawableTraits traits)` - Override to customize drawable attributes
- `SetCustomFilter()` - Call in constructor to enable `IsApplicable` filtering

---

### WorldDraw

```csharp
namespace Autodesk.AutoCAD.GraphicsInterface
{
    [Wrapper("AcGiWorldDraw")]
    public abstract class WorldDraw : CommonDraw
    {
        protected WorldDraw();
        protected WorldDraw(nint unmanagedPointer, bool autoDelete);

        public abstract WorldGeometry Geometry { get; }
    }
}
```

**Key Properties:**
- `Geometry` - Access to `WorldGeometry` for drawing primitives
- Inherits from `CommonDraw` which provides `SubEntityTraits` property

---

### WorldGeometry

```csharp
namespace Autodesk.AutoCAD.GraphicsInterface
{
    [Wrapper("AcGiWorldGeometry")]
    public abstract class WorldGeometry : Geometry
    {
        protected WorldGeometry();
        protected WorldGeometry(nint unmanagedPointer, bool autoDelete);

        public abstract void SetExtents(Extents3d extents);
        public abstract void StartAttributesSegment();
    }
}
```

**Note:** Inherits from `Geometry` base class which contains the actual drawing methods.

---

### Geometry (base class)

Contains all the drawing methods for graphics primitives.

```csharp
namespace Autodesk.AutoCAD.GraphicsInterface
{
    [Wrapper("AcGiGeometry")]
    public abstract class Geometry : RXObject
    {
        protected Geometry(nint unmanagedPointer, bool autoDelete);

        public abstract Matrix3d WorldToModelTransform { get; }
        public abstract Matrix3d ModelToWorldTransform { get; }

        // Circles and Arcs
        public abstract bool Circle(Point3d center, double radius, Vector3d normal);
        public abstract bool Circle(Point3d firstPoint, Point3d secondPoint, Point3d thirdPoint);
        public abstract bool CircularArc(Point3d center, double radius, Vector3d normal, Vector3d startVector, double sweepAngle, ArcType arcType);
        public abstract bool CircularArc(Point3d start, Point3d point, Point3d endingPoint, ArcType arcType);
        public abstract bool EllipticalArc(Point3d center, Vector3d normal, double majorAxisLength, double minorAxisLength, double startDegreeInRads, double endDegreeInRads, double tiltDegreeInRads, ArcType arcType);

        // Lines
        public abstract bool WorldLine(Point3d startPoint, Point3d endPoint);
        public abstract bool Ray(Point3d point1, Point3d point2);
        public abstract bool Xline(Point3d point1, Point3d point2);

        // Polylines
        public abstract bool Polyline(DatabaseServices.Polyline value, int fromIndex, int segments);
        public abstract bool Polyline(Polyline polylineObj);
        public abstract bool Polyline(Point3dCollection points, Vector3d normal, nint subEntityMarker);
        public abstract bool PolyPolyline(PolylineCollection polylineCollection);

        // Polygons
        public abstract bool Polygon(Point3dCollection points);
        public abstract bool PolyPolygon(UInt32Collection numPolygonPositions, Point3dCollection polygonPositions, UInt32Collection numPolygonPoints, Point3dCollection polygonPoints, EntityColorCollection outlineColors, LinetypeCollection outlineTypes, EntityColorCollection fillColors, TransparencyCollection fillOpacities);

        // Curves
        public abstract bool Curve(Curve3d curve3d);
        public abstract bool Edge(Curve2dCollection e);

        // Mesh and Shell
        public abstract bool Mesh(int rows, int columns, Point3dCollection points, EdgeData edgeData, FaceData faceData, VertexData vertexData, bool bAutoGenerateNormals);
        public abstract bool Shell(Point3dCollection points, IntegerCollection faces, EdgeData edgeData, FaceData faceData, VertexData vertexData, bool bAutoGenerateNormals);

        // Points
        public abstract bool Polypoint(Point3dCollection points, Vector3dCollection normals, IntPtrCollection subentityMarkers);
        public abstract bool RowOfDots(int count, Point3d start, Vector3d step);

        // Text
        public abstract bool Text(Point3d position, Vector3d normal, Vector3d direction, double height, double width, double oblique, string message);
        public abstract bool Text(Point3d position, Vector3d normal, Vector3d direction, string message, bool raw, TextStyle textStyle);

        // Images
        public abstract bool Image(ImageBGRA32 imageSource, Point3d position, Vector3d u, Vector3d v);
        public abstract bool Image(ImageBGRA32 imageSource, Point3d position, Vector3d u, Vector3d v, TransparencyMode transparencyMode);
        public abstract bool OwnerDraw(GdiDrawObject gdiDrawObject, Point3d position, Vector3d u, Vector3d v);

        // Drawing other drawables
        public abstract bool Draw(Drawable value);

        // Transform stack
        public abstract bool PushModelTransform(Vector3d normal);
        public abstract bool PushModelTransform(Matrix3d matrix);
        public abstract bool PopModelTransform();
        public abstract Matrix3d PushOrientationTransform(OrientationBehavior behavior);
        public abstract Matrix3d PushPositionTransform(PositionBehavior behavior, Point3d offset);
        public abstract Matrix3d PushPositionTransform(PositionBehavior behavior, Point2d offset);
        public abstract Matrix3d PushScaleTransform(ScaleBehavior behavior, Point3d extents);
        public abstract Matrix3d PushScaleTransform(ScaleBehavior behavior, Point2d extents);

        // Clipping
        public abstract bool PushClipBoundary(ClipBoundary boundary);
        public abstract void PopClipBoundary();
    }
}
```

**Key Drawing Methods:**
- `CircularArc(center, radius, normal, startVector, sweepAngle, arcType)` - Draw arc by center/radius/sweep
- `CircularArc(start, point, endingPoint, arcType)` - Draw arc through 3 points
- `Polyline(DatabaseServices.Polyline value, int fromIndex, int segments)` - **Draw specific segment(s) of a polyline with width!**
- `WorldLine(startPoint, endPoint)` - Draw a line
- `Circle(center, radius, normal)` - Draw a circle

---

### SubEntityTraits

Controls the current values of color, layer, linetype, fill type, and graphics system marker attributes for graphics primitives. Attribute settings are used for all graphics primitives drawn until the setting is changed or the end of the current `WorldDraw()` or `ViewportDraw()` execution.

```csharp
namespace Autodesk.AutoCAD.GraphicsInterface
{
    [Wrapper("AcGiSubEntityTraits")]
    public abstract class SubEntityTraits : RXObject
    {
        protected SubEntityTraits();

        public abstract double LineTypeScale { get; set; }
        public abstract double Thickness { get; set; }
        public abstract PlotStyleDescriptor PlotStyleDescriptor { get; set; }
        public abstract ObjectId Material { get; set; }
        public abstract Mapper Mapper { get; set; }
        public abstract bool Sectionable { get; set; }
        public abstract ObjectId VisualStyle { get; set; }
        public abstract int DrawFlags { get; set; }
        public abstract ShadowFlags ShadowFlags { get; set; }
        public abstract bool SelectionOnlyGeometry { get; set; }
        public abstract short Color { get; set; }
        public abstract EntityColor TrueColor { get; set; }
        public abstract Transparency Transparency { get; set; }
        public abstract ObjectId Layer { get; set; }
        public abstract ObjectId LineType { get; set; }
        public abstract Fill Fill { get; set; }
        public abstract LineWeight LineWeight { get; set; }
        public abstract FillType FillType { get; set; }

        public abstract void SetSelectionMarker(nint markerId);
    }
}
```

**Key Properties for Color:**
- `Color` (short) - ACI color index (0-256)
- `TrueColor` (EntityColor) - Full RGB color support

---

## Autodesk.AutoCAD.Colors

### Color

```csharp
namespace Autodesk.AutoCAD.Colors
{
    [TypeConverter(typeof(ColorConverter))]
    [Wrapper("AcCmColor")]
    [XmlType("AcColor")]
    public sealed class Color : DisposableWrapper, IComparable, ICloneable
    {
        public Color();

        // Properties
        public System.Drawing.Color ColorValue { get; }
        public string ColorName { get; }
        public string BookName { get; }
        public string ColorNameForDisplay { get; }
        public bool HasColorName { get; }
        public bool HasBookName { get; }
        public EntityColor EntityColor { get; }
        public int DictionaryKeyLength { get; }
        public string DictionaryKey { get; }
        public ColorMethod ColorMethod { get; }
        public byte Blue { get; }
        public byte Green { get; }
        public byte Red { get; }
        public short PenIndex { get; }
        public bool IsForeground { get; }
        public bool IsNone { get; }
        public bool IsByAci { get; }
        public bool IsByPen { get; }
        public string Explanation { get; }
        public bool IsByColor { get; }
        public string Description { get; }
        public bool IsByBlock { get; }
        public bool IsByLayer { get; }
        public short ColorIndex { get; }

        // Static Factory Methods
        public static Color DwgIn(DwgFiler inputFiler);
        public static Color DxfIn(DxfFiler inputFiler, int groupCodeOffset);
        public static Color FromColor(System.Drawing.Color value);
        public static Color FromColor(System.Windows.Media.Color value);
        public static Color FromColorIndex(ColorMethod colorMethod, short colorIndex);
        public static Color FromDictionaryName(string name);
        public static Color FromEntityColor(EntityColor eclr);
        public static Color FromNames(string colorName, string bookName);
        public static Color FromRgb(byte red, byte green, byte blue);
        public static System.Drawing.Color GetColorValue(short colorIndex, System.Drawing.Color backgroundColor);
        
        // Instance Methods
        public void Audit(AuditInfo auditInfo);
        public object Clone();
        public int CompareTo(object obj);
        public void DwgOut(DwgFiler outputFiler);
        public void DxfOut(DxfFiler outputFiler, int groupCodeOffset);
        public sealed override bool Equals(object obj);
        public sealed override int GetHashCode();
        public string ToString(IFormatProvider provider);
        public sealed override string ToString();
        protected sealed override void DeleteUnmanagedObject();

        // Operators
        public static bool operator ==(Color a, Color b);
        public static bool operator !=(Color a, Color b);
        public static int operator <(Color a, Color b);
        public static int operator >(Color a, Color b);
    }
}
```

**Key Factory Methods for Creating Colors:**
- `FromRgb(byte red, byte green, byte blue)` - Create from RGB values
- `FromColorIndex(ColorMethod colorMethod, short colorIndex)` - Create from ACI index
- `FromEntityColor(EntityColor eclr)` - Create from EntityColor

**Key Properties:**
- `Red`, `Green`, `Blue` - Individual RGB components
- `ColorIndex` - ACI color index
- `ColorMethod` - How the color is defined (ByLayer, ByBlock, ByAci, ByColor, etc.)

---

## Notes

- This file serves as a quick reference for AutoCAD API classes
- Metadata is extracted from AutoCAD .NET API documentation
- Use this as a reference when implementing overrules and custom drawing logic
