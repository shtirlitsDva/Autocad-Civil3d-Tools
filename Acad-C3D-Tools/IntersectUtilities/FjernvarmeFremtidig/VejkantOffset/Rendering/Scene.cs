using System;
using System.Collections.Generic;

using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Rendering
{
	public sealed class Scene
	{
		public IReadOnlyList<IRenderable> Items { get; init; } = Array.Empty<IRenderable>();
	}

	public interface IRenderable
	{
		void Accept(IRenderVisitor v);
	}

	public interface IRenderVisitor
	{
		void Visit(Line2D line);
		void Visit(Arc2D arc);
		void Visit(PolyPath2D path);
	}

	public sealed record Style(double Width, short? ColorIndex);

	public sealed class Line2D : IRenderable
	{
		public Point2d A { get; init; }
		public Point2d B { get; init; }
		public Style? Style { get; init; }
		public void Accept(IRenderVisitor v) => v.Visit(this);
	}

	public sealed class Arc2D : IRenderable
	{
		public Point2d Center { get; init; }
		public double Radius { get; init; }
		public double StartAngle { get; init; }
		public double EndAngle { get; init; }
		public bool IsCCW { get; init; }
		public Style? Style { get; init; }
		public void Accept(IRenderVisitor v) => v.Visit(this);
	}

	public sealed class PolyPath2D : IRenderable
	{
		public IReadOnlyList<Point2d> Vertices { get; init; } = Array.Empty<Point2d>();
		public IReadOnlyList<double> Bulges { get; init; } = Array.Empty<double>();
		public Style? Style { get; init; }
		public void Accept(IRenderVisitor v) => v.Visit(this);
	}
}


