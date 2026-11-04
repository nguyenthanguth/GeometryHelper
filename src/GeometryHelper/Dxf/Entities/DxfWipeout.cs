using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A WIPEOUT of a DXF file, as AutoCAD's <c>Wipeout</c>: a mask that hides what is drawn under it in the colour of the
    /// background, an image of one pixel with no picture, clipped by the polygon it hides. Under the words of a tag of a Tekla
    /// or AutoCAD drawing it is moved, copied and mirrored with them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Its record is that of an IMAGE, its only marker <c>AcDbWipeout</c> as AutoCAD 2021 writes it: the corner of its frame,
    /// the vectors of the frame's sides as those of its one pixel (11 and 12), and its boundary in -0.5 to 0.5 of the frame,
    /// x along it and y down; it shows no IMAGEDEF (340 of 0). A frame of side 40 placed at (-30, -10) holding the boundary
    /// (-0.5, 0.5), (0.5, 0.5), (0.5, -0.25), (-0.5, -0.25) hides the rectangle from (-30, -10) to (10, 20).
    /// </para>
    /// <para>
    /// What a <see cref="DxfRasterImage"/> has of a picture it has not: its brightness, contrast and fade and its display
    /// options are refused, as AutoCAD 2021 refuses or ignores them, and <see cref="DxfRasterImage.ImageDef"/>,
    /// <see cref="DxfRasterImage.Name"/> and <see cref="DxfRasterImage.Path"/> are null. <see cref="DxfEntity.TransformBy"/>
    /// takes any transformation; <see cref="DxfEntity.GetTransformedCopy"/> refuses one that scales more one way than another or
    /// shears, as AutoCAD 2021 answers eCannotScaleNonUniformly.
    /// </para>
    /// </remarks>
    public sealed class DxfWipeout : DxfRasterImage
    {
        private DxfWipeout(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Hides a polygon anew, as AutoCAD 2021's <c>SetFrom</c>: its corners in the plane of a normal, in the system of that
        /// normal (the arbitrary axis algorithm, the world's xy for (0, 0, 1)), the first again at the end. As AutoCAD 2021
        /// writes it, the frame is the square on the box of the corners and the origin of the plane, its side the box's longer:
        /// the rectangle (-30, -10) to (10, 20) has the frame of side 40 at (-30, -10, 0), the rectangle (200, 100) to (230,
        /// 110) the frame of side 230 at the origin. The corners are written in the frame as a polygon, the record said to be
        /// clipped (280 of 1).
        /// </summary>
        /// <param name="points">The corners, three or more, the first again at the end within the point tolerance of
        /// <see cref="Tolerance.Default"/>.</param>
        /// <param name="normal">The normal of the plane, of some length.</param>
        /// <exception cref="ArgumentNullException">Thrown when the corners are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a corner or the normal is not of finite numbers, the normal has no
        /// length, the last corner is not the first, there are fewer than four, or they close no area, the point tolerance times
        /// the longer side of their own box, wherever they stand: a square 10 by 10 at (100 000, 100 000) closes 100. AutoCAD
        /// 2021 writes a polygon not closed as a WIPEOUT of no boundary, which it then cannot open.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the wipeout is erased.</exception>
        public void SetFrom(IReadOnlyList<GeoPoint2> points, GeoVector3 normal)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            double length = normal.Length;
            Guard.Argument(normal.IsValid && length > 0.0 && !double.IsInfinity(length), nameof(normal), "The normal of a wipeout has to be a direction of finite numbers and some length: " + normal + " is not.");
            int count = points.Count;

            foreach (GeoPoint2 point in points)
            {
                Guard.Argument(Guard.IsFinite(point.X) && Guard.IsFinite(point.Y), nameof(points), "A corner of a wipeout has to be of finite numbers: " + point + " is not.");
            }

            Guard.Argument(count >= 4, nameof(points), "A wipeout hides a polygon of three corners or more, the first again at the end; " + count.ToString(CultureInfo.InvariantCulture) + " were given.");
            Guard.Argument(points[count - 1].IsEqualTo(points[0], Tolerance.Default), nameof(points), "The corners of a wipeout end on the first, as AutoCAD 2021 takes them: " + points[count - 1] + " is not " + points[0] + ".");

            // Twice the area the corners close, measured from the first so that a polygon far from the origin keeps its digits,
            // against the corners' own box: a square 10 by 10 at (100 000, 100 000) closes some, as one at the origin does.
            double area = 0.0;
            double ownMinX = points[0].X;
            double ownMinY = points[0].Y;
            double ownMaxX = points[0].X;
            double ownMaxY = points[0].Y;

            for (int i = 0; i + 1 < count; i++)
            {
                double ax = points[i].X - points[0].X;
                double ay = points[i].Y - points[0].Y;
                double bx = points[i + 1].X - points[0].X;
                double by = points[i + 1].Y - points[0].Y;
                area += ax * by - bx * ay;
                ownMinX = Math.Min(ownMinX, points[i].X);
                ownMinY = Math.Min(ownMinY, points[i].Y);
                ownMaxX = Math.Max(ownMaxX, points[i].X);
                ownMaxY = Math.Max(ownMaxY, points[i].Y);
            }

            double ownSide = Math.Max(ownMaxX - ownMinX, ownMaxY - ownMinY);
            double tolerance = Tolerance.Default.EqualPoint;
            Guard.Argument(Math.Abs(area) / 2.0 > tolerance * ownSide, nameof(points), "The corners of a wipeout close no area: they lie on one line.");

            // The frame as AutoCAD 2021 lays it: the square on the box of the corners and the origin of their plane.
            double minX = 0.0;
            double minY = 0.0;
            double maxX = 0.0;
            double maxY = 0.0;

            foreach (GeoPoint2 point in points)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            double side = Math.Max(maxX - minX, maxY - minY);

            // The frame in its pixels, one across and one up as AutoCAD writes a wipeout, more where the record holds more.
            double across = ImageWidth > 0.0 ? ImageWidth : 1.0;
            double up = ImageHeight > 0.0 ? ImageHeight : 1.0;
            GeoVector3 unit = Math.Abs(length - 1.0) <= 1E-12 ? normal : normal.Divide(length);
            OcsAxes(unit, out GeoVector3 x, out GeoVector3 y);
            GeoPoint3 corner = OcsToWorld(unit, new GeoPoint3(minX, minY, 0.0));
            var frame = new List<GeoPoint2>(count);

            for (int i = 0; i < count - 1; i++)
            {
                frame.Add(new GeoPoint2((points[i].X - minX) / side * across - 0.5, up - 0.5 - (points[i].Y - minY) / side * up));
            }

            frame.Add(frame[0]);
            WriteFrame(corner, x.Multiply(side / across), y.Multiply(side / up));
            SetClipBoundary(DxfClipBoundaryType.Poly, frame);
        }

        /// <summary>
        /// Moves a copy just made as AutoCAD 2021's <c>GetTransformedCopy</c> of a wipeout moves it: refused where the
        /// transformation scales more one way than another or shears, as AutoCAD answers eCannotScaleNonUniformly; moved as
        /// <see cref="DxfEntity.TransformBy"/> moves it otherwise, mirrored too.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the transformation is not one of a single scale.</exception>
        private protected override void MoveCopy(GeoTransform3 transform)
        {
            GeoVector3 x = transform.Transform(new GeoVector3(1.0, 0.0, 0.0));
            GeoVector3 y = transform.Transform(new GeoVector3(0.0, 1.0, 0.0));
            GeoVector3 z = transform.Transform(new GeoVector3(0.0, 0.0, 1.0));

            if (!DxfOcsTransform.IsUniform(x, y) || !DxfOcsTransform.IsUniform(y, z) || !DxfOcsTransform.IsUniform(x, z))
            {
                throw new ArgumentException("The transformation scales more one way than another or shears, and a copy of a wipeout is not made so, as AutoCAD 2021 answers eCannotScaleNonUniformly: move the wipeout itself by TransformBy.", nameof(transform));
            }

            base.MoveCopy(transform);
        }

        [DxfType("WIPEOUT")]
        internal static DxfEntity CreateWipeout(DxfItem item) => new DxfWipeout(item);
    }
}
