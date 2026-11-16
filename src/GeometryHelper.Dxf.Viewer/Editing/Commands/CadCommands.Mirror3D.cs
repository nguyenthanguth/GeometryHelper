using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    internal static partial class CadCommands
    {
        private static readonly string[] Mirror3DPlaneKeywords =
        {
            "Object",
            "Last",
            "Zaxis",
            "View",
            "XY",
            "YZ",
            "ZX",
            "3points",
        };

        /// <summary>
        /// MIRROR3D, as AutoCAD 2021's console asks it (probed: the prompts, the words that refuse a point, and every object
        /// mirrored with each option): objects, then the mirror plane by three points (the default, the first given as the answer
        /// itself), Object (the plane of a circle, an arc or a 2D polyline), Last, Zaxis (a point on the plane and one on its
        /// normal), View (a point on the plane parallel to the view, the plan here) or XY, YZ and ZX (a point on the plane parallel
        /// to that plane of the UCS, <c>0,0,0</c> by default), then "Delete source objects? [Yes/No] &lt;N&gt;:". Each object moves
        /// as its <c>TransformBy</c> mirrors it, a text, a circle or a hatch turned over where the plane is the drawing's own, its
        /// normal (0, 0, -1) and the picture on the sheet the same: a circle at (50, 50) mirrored across the XY plane is at (-50, 50)
        /// of the normal (0, 0, -1), where it stood. A hatch follows MIRRHATCH, as MIRROR does. The points of a plan have no Z, so
        /// three of them lie in the XY plane, or in none where they are collinear ("The three points are collinear.").
        /// </summary>
        private static IEnumerable<CadPrompt> Mirror3D(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            Tolerance tolerance = c.Session.Tolerance;
            CadUcs ucs = c.Session.Ucs;
            GeoPlane3? plane = null;

            while (plane == null)
            {
                yield return CadPrompt.ForPoint(
                    "Specify first point of mirror plane (3 points)",
                    null,
                    Mirror3DPlaneKeywords,
                    "3points"
                );

                if (!c.Input.IsPoint && c.Input.Kind != CadInputKind.Keyword)
                {
                    yield break;
                }

                GeoPoint2 first = c.Input.Point;
                bool given = c.Input.IsPoint;
                string way = given ? "3points" : c.Input.Text;

                switch (way)
                {
                    case "Object":
                        while (true)
                        {
                            yield return CadPrompt.ForEntity("Select a circle, arc, or 2D-polyline segment");

                            if (c.Input.Kind != CadInputKind.Selection)
                            {
                                yield break;
                            }

                            plane = PlaneOf(c.Document.FindEntity(c.Input.Handles[0]));

                            if (plane != null)
                            {
                                break;
                            }

                            c.Write("Improper type of object picked.");
                        }

                        break;
                    case "Last":
                        plane = LastMirror3DPlane(c);
                        break;
                    case "Zaxis":
                        yield return CadPrompt.ForPoint("Specify point on mirror plane");

                        if (!c.Input.IsPoint)
                        {
                            yield break;
                        }

                        GeoPoint2 onPlane = c.Input.Point;

                        while (true)
                        {
                            yield return CadPrompt.ForPoint(
                                "Specify point on Z-axis (normal) of mirror plane",
                                onPlane
                            );

                            if (!c.Input.IsPoint)
                            {
                                yield break;
                            }

                            if ((c.Input.Point - onPlane).Length > tolerance.EqualPoint)
                            {
                                break;
                            }

                            c.Write("The two points coincide.");
                        }

                        GeoVector2 across = c.Input.Point - onPlane;
                        plane = new GeoPlane3(P3(onPlane), new GeoVector3(across.X, across.Y, 0.0).Normalize());
                        break;
                    case "View":
                        yield return CadPrompt.ForPoint("Specify point on view plane", null, null, "0,0,0");

                        // The view of a plan looks down the Z axis: the plane parallel to it is the XY plane.
                        plane = new GeoPlane3(
                            c.Input.IsPoint ? P3(c.Input.Point) : P3(ucs.ToWorld(new GeoPoint2(0.0, 0.0))),
                            new GeoVector3(0.0, 0.0, 1.0)
                        );
                        break;
                    case "XY":
                    case "YZ":
                    case "ZX":
                        yield return CadPrompt.ForPoint("Specify point on " + way + " plane", null, null, "0,0,0");

                        // The planes are parallel to those of the UCS: YZ is square to its x axis, ZX to its y axis.
                        GeoPoint2 through = c.Input.IsPoint ? c.Input.Point : ucs.ToWorld(new GeoPoint2(0.0, 0.0));
                        double along = ucs.AngleToWorld(way == "ZX" ? Math.PI / 2.0 : 0.0);
                        plane = new GeoPlane3(
                            P3(through),
                            way == "XY"
                                ? new GeoVector3(0.0, 0.0, 1.0)
                                : new GeoVector3(Math.Cos(along), Math.Sin(along), 0.0)
                        );
                        break;
                    default:
                        // Three points: the first given, or asked as the default asks it; Z of a point of a plan is 0.
                        if (!given)
                        {
                            yield return CadPrompt.ForPoint("Specify first point on mirror plane");

                            if (!c.Input.IsPoint)
                            {
                                yield break;
                            }

                            first = c.Input.Point;
                        }

                        GeoPoint2 second;

                        while (true)
                        {
                            yield return CadPrompt.ForPoint("Specify second point on mirror plane", first);

                            if (!c.Input.IsPoint)
                            {
                                yield break;
                            }

                            second = c.Input.Point;

                            if ((second - first).Length > tolerance.EqualPoint)
                            {
                                break;
                            }

                            c.Write("The 1st and 2nd point coincide.");
                        }

                        GeoPoint2 third;

                        while (true)
                        {
                            yield return CadPrompt.ForPoint("Specify third point on mirror plane", second);

                            if (!c.Input.IsPoint)
                            {
                                yield break;
                            }

                            third = c.Input.Point;
                            GeoVector2 line = second - first;

                            if (
                                Math.Abs(line.X * (third.Y - first.Y) - line.Y * (third.X - first.X)) / line.Length
                                > tolerance.EqualPoint
                            )
                            {
                                break;
                            }

                            c.Write("The three points are collinear.");
                        }

                        plane = new GeoPlane3(P3(first), new GeoVector3(0.0, 0.0, 1.0));
                        break;
                }
            }

            yield return CadPrompt.ForKeyword("Delete source objects?", new[] { "Yes", "No" }, "N");

            GeoPlane3 mirrorPlane = plane.Value;
            RememberMirror3DPlane(c, mirrorPlane);
            GeoTransform3 mirror = GeoTransform3.Mirror(mirrorPlane);

            // As MIRROR: a hatch follows MIRRHATCH, 0 by default leaving the lines of its pattern as they were.
            bool mirrhatch = c.Remembered("mirrhatch", 0.0) >= 0.5;
            GeoPoint3 origin = mirrorPlane.Origin;
            GeoVector3 normal = mirrorPlane.Normal;

            Transform(
                c,
                c.Selected,
                mirror,
                !c.Input.Is("Yes"),
                "Mirror3D",
                Values(origin.X, origin.Y, origin.Z, normal.X, normal.Y, normal.Z),
                entity => entity is DxfHatch ? ReadableHatch(mirror, mirrhatch) : null
            );
        }

        /// <summary>The plane of a circle, an arc or a 2D polyline, which MIRROR3D's Object takes as the mirror plane; null for any other object.</summary>
        private static GeoPlane3? PlaneOf(DxfEntity entity)
        {
            switch (entity)
            {
                case DxfCircle circle:
                    return new GeoPlane3(circle.Center, circle.Normal);
                case DxfArc arc:
                    return new GeoPlane3(arc.Center, arc.Normal);
                case DxfPolyline polyline:
                    return polyline.GetPlane();
                case DxfPolyline2d polyline2d:
                    return polyline2d.GetPlane();
                default:
                    return null;
            }
        }

        /// <summary>The plane MIRROR3D last mirrored across; the XY plane through the origin where it has not been used, as AutoCAD 2021's console takes it.</summary>
        private static GeoPlane3 LastMirror3DPlane(CadCommandContext c) =>
            new GeoPlane3(
                new GeoPoint3(
                    c.Remembered("mirror3d.ox", 0.0),
                    c.Remembered("mirror3d.oy", 0.0),
                    c.Remembered("mirror3d.oz", 0.0)
                ),
                new GeoVector3(
                    c.Remembered("mirror3d.nx", 0.0),
                    c.Remembered("mirror3d.ny", 0.0),
                    c.Remembered("mirror3d.nz", 1.0)
                )
            );

        private static void RememberMirror3DPlane(CadCommandContext c, GeoPlane3 plane)
        {
            c.Remember("mirror3d.ox", plane.Origin.X);
            c.Remember("mirror3d.oy", plane.Origin.Y);
            c.Remember("mirror3d.oz", plane.Origin.Z);
            c.Remember("mirror3d.nx", plane.Normal.X);
            c.Remember("mirror3d.ny", plane.Normal.Y);
            c.Remember("mirror3d.nz", plane.Normal.Z);
        }
    }
}
