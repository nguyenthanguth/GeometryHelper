using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// OFFSET as AutoCAD 2021 asks it: a distance or Through, Erase and Layer, then an object and the side, again and again,
    /// with Multiple and Undo inside. The curve is the library's <c>GetOffsetCurves</c>, which keeps arcs, runs the corners
    /// of a polyline on until they meet (OFFSETGAPTYPE 0) and cuts away what comes nearer than the distance.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly GeoVector3 PlanUp = new GeoVector3(0.0, 0.0, 1.0);

        private static IEnumerable<CadPrompt> Offset(CadCommandContext c)
        {
            bool erase = c.Remembered("offset.erase", 0.0) != 0.0;
            bool current = c.Remembered("offset.layer", 0.0) != 0.0;
            double distance = c.Remembered("offset.distance", -1.0);
            c.Write("Current settings: Erase source=" + (erase ? "Yes" : "No") + "  Layer=" + (current ? "Current" : "Source") + "  OFFSETGAPTYPE=0");

            while (true)
            {
                yield return CadPrompt.ForPoint("Specify offset distance", null, new[] { "Through", "Erase", "Layer" }, distance < 0.0 ? "Through" : Length(distance), true);

                if (c.Input.Is("Through"))
                {
                    distance = -1.0;
                    break;
                }

                if (c.Input.Is("Erase"))
                {
                    yield return CadPrompt.ForKeyword("Erase source object after offsetting?", new[] { "Yes", "No" }, erase ? "Yes" : "No");
                    erase = c.Input.Is("Yes") || (c.Input.IsNone && erase);
                    c.Remember("offset.erase", erase ? 1.0 : 0.0);
                    continue;
                }

                if (c.Input.Is("Layer"))
                {
                    yield return CadPrompt.ForKeyword("Enter layer option for offset objects", new[] { "Current", "Source" }, current ? "Current" : "Source");
                    current = c.Input.Is("Current") || (c.Input.IsNone && current);
                    c.Remember("offset.layer", current ? 1.0 : 0.0);
                    continue;
                }

                if (c.Input.IsNone)
                {
                    break;
                }

                double typed;

                if (c.Input.IsPoint)
                {
                    GeoPoint2 first = c.Input.Point;
                    yield return CadPrompt.ForPoint("Specify second point", first);

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    typed = (c.Input.Point - first).Length;
                }
                else if (c.Input.Kind != CadInputKind.Text || !double.TryParse(c.Input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out typed) || double.IsNaN(typed) || double.IsInfinity(typed))
                {
                    c.Write("Requires numeric distance, two points, or option keyword.");
                    continue;
                }

                if (!(typed > c.Session.Tolerance.EqualPoint))
                {
                    c.Write("Value must be positive and nonzero.");
                    continue;
                }

                distance = typed;
                break;
            }

            c.Remember("offset.distance", distance);
            bool through = distance < 0.0;
            var marks = new List<CadGroupMark>();

            while (true)
            {
                yield return CadPrompt.ForEntity("Select object to offset", new[] { "Exit", "Undo" }, "Exit");

                if (c.Input.Is("Undo"))
                {
                    TakeBackLast(c, marks);
                    continue;
                }

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                string source = c.Input.Handles[0];

                if (!Offsettable(c, source))
                {
                    continue;
                }

                bool multiple = false;
                bool erased = false;

                while (true)
                {
                    string ask = through ? "Specify through point" : "Specify point on side to offset";
                    yield return multiple
                        ? CadPrompt.ForPoint(ask, null, new[] { "Exit", "Undo" }, "next object")
                        : CadPrompt.ForPoint(ask, null, new[] { "Exit", "Multiple", "Undo" }, "Exit");

                    if (c.Input.Is("Exit"))
                    {
                        yield break;
                    }

                    if (c.Input.Is("Multiple"))
                    {
                        multiple = true;
                        continue;
                    }

                    if (c.Input.Is("Undo"))
                    {
                        TakeBackLast(c, marks);

                        if (!multiple)
                        {
                            break;
                        }

                        continue;
                    }

                    if (!c.Input.IsPoint)
                    {
                        break;
                    }

                    c.OpenUndoStep();
                    CadGroupMark mark = c.Document.MarkGroup();
                    string made = OffsetOne(c, source, c.Input.Point, distance, erase && !erased, current);

                    if (made == null)
                    {
                        if (!multiple)
                        {
                            break;
                        }

                        continue;
                    }

                    marks.Add(mark);
                    erased = erased || erase;

                    if (!multiple)
                    {
                        break;
                    }

                    // Multiple offsets each new curve from the one made last, as AutoCAD's does: 10, 20, 30 from the first.
                    source = made;
                }
            }
        }

        /// <summary>Reads the drawing back to before the last pick of the command, or says there is nothing to undo.</summary>
        private static void TakeBackLast(CadCommandContext c, List<CadGroupMark> marks)
        {
            if (marks.Count == 0)
            {
                c.Write("Everything has been undone");
                return;
            }

            c.Document.BackTo(marks[marks.Count - 1]);
            marks.RemoveAt(marks.Count - 1);
        }

        /// <summary>Whether an object can be offset: a curve of the plan on a layer not locked.</summary>
        private static bool Offsettable(CadCommandContext c, string handle)
        {
            DxfEntity entity = c.Document.FindEntity(handle);

            if (!(entity is DxfLine || entity is DxfArc || entity is DxfCircle || entity is DxfEllipse || entity is DxfPolyline || entity is DxfSpline
                || entity is DxfXline || entity is DxfRay))
            {
                c.Write("Cannot offset that object.");
                return false;
            }

            if (CadLayerEditor.IsLocked(c.Database, entity.Layer))
            {
                c.Write("Object is on a locked layer.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Offsets one curve to the side of a point, or through it, as one pick of OFFSET: the side is that of the point from the
        /// curve where it comes nearest, a line's own way along it, the rest outwards or to the right. Gives the handle of the
        /// first curve made, or null where none was.
        /// </summary>
        private static string OffsetOne(CadCommandContext c, string handle, GeoPoint2 side, double distance, bool erase, bool currentLayer)
        {
            var curve = (DxfCurve)c.Document.FindEntity(handle);
            GeoPoint3 at = P3(side);
            IReadOnlyList<DxfCurve> made;

            try
            {
                bool straight = curve is DxfLine || curve is DxfXline || curve is DxfRay;
                GeoPoint3 near = curve.GetClosestPointTo(at, straight);
                var away = new GeoVector2(side.X - near.X, side.Y - near.Y);
                double length = distance < 0.0 ? away.Length : distance;

                if (!(length > c.Session.Tolerance.EqualPoint))
                {
                    c.Write("Cannot offset that object.");
                    return null;
                }

                switch (curve)
                {
                    case DxfArc arc:
                        made = arc.GetOffsetCurves(Outside(arc.Center, side, arc.Radius) ? length : -length);
                        break;
                    case DxfCircle circle:
                        made = circle.GetOffsetCurves(Outside(circle.Center, side, circle.Radius) ? length : -length);
                        break;
                    case DxfEllipse ellipse:
                        made = ellipse.GetOffsetCurves(OutsideEllipse(ellipse, side) ? length : -length);
                        break;
                    default:
                        // A line goes to the left of its way above nought, a polyline and a spline to the right, seen from above.
                        GeoVector3 way = curve.GetFirstDerivative(near);
                        double left = way.X * away.Y - way.Y * away.X;
                        double signed = straight ? (left > 0.0 ? length : -length) : (left < 0.0 ? length : -length);
                        made = curve.GetOffsetCurvesGivenPlaneNormal(PlanUp, signed);
                        break;
                }
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is NotSupportedException)
            {
                GeometryHelperLog.Debug("OFFSET: " + handle + " has no offset there: " + exception.Message);
                made = null;
            }

            if (made == null || made.Count == 0)
            {
                c.Write("Cannot offset that object.");
                return null;
            }

            IReadOnlyList<CadEditRecord> planned = erase ? new[] { new CadEditRecord(CadEditRecordKind.Erased, handle) } : null;
            string layer = c.Database.Clayer;
            CadEditResult result = c.Apply(planned, s =>
            {
                CadSpace space = s.Document.FindSpace(handle);

                foreach (DxfCurve piece in made)
                {
                    DxfCurve copy = (DxfCurve)piece.Clone();
                    CadOwnerTag.Remove(copy);

                    if (currentLayer)
                    {
                        copy.Layer = layer;
                    }

                    s.Append(space, copy);
                }

                if (erase && !s.IsHandled(handle))
                {
                    s.Document.FindEntity(handle).Erase();
                }
            });

            return result.Succeeded ? AddedHandle(result) : null;
        }

        private static bool Outside(GeoPoint3 center, GeoPoint2 point, double radius)
            => new GeoVector2(point.X - center.X, point.Y - center.Y).Length > radius;

        /// <summary>Whether a point of the plan lies outside an ellipse, measured in the ellipse's own axes.</summary>
        private static bool OutsideEllipse(DxfEllipse ellipse, GeoPoint2 point)
        {
            GeoVector3 major = ellipse.MajorAxis;
            GeoVector3 minor = ellipse.MinorAxis;
            var from = new GeoVector3(point.X - ellipse.Center.X, point.Y - ellipse.Center.Y, 0.0);
            double u = from.DotProduct(major) / major.DotProduct(major);
            double v = from.DotProduct(minor) / minor.DotProduct(minor);
            return u * u + v * v > 1.0;
        }
    }
}
