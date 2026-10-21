using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Viewer
{
    internal sealed partial class DiagnosticRun
    {
        private const int SphereSegments = 24;

        private int _spheresDrawn;
        private List<Vec3>[] _vertexOf;

        #region Gap

        private void GapCheck(List<int> targets)
        {
            if (targets.Count != 2)
            {
                Add(DiagnosticChecks.Gap, DiagnosticKind.Note, false, targets.Count > 0 ? targets[0] : -1, null, "the gap is measured between two items: select exactly two (" + Count(targets.Count, "item") + " are checked)", "viewer", default(Vec3), 0.0, 0.0);
                return;
            }

            SceneItem first = _scene.Items[targets[0]];
            SceneItem second = _scene.Items[targets[1]];
            string nameA = first.Name ?? first.Kind;
            string nameB = second.Name ?? second.Kind;
            LibraryDistance answer = LibraryMeasure.Between(_scene, targets[0], targets[1]);

            foreach (string note in answer.Notes)
            {
                Add(DiagnosticChecks.Gap, DiagnosticKind.Note, false, targets[0], nameA, note, "LibraryMeasure", default(Vec3), 0.0, 0.0);
            }

            if (!answer.Distance.HasValue)
            {
                return;
            }

            double distance = answer.Distance.Value;
            string source = first.Kind + ".DistanceTo(" + second.Kind + ")" + (answer.From.HasValue ? " and GetShortestLineTo" : string.Empty);
            string text = "the gap between '" + nameA + "' and '" + nameB + "' is " + N(distance);

            if (answer.From.HasValue && answer.To.HasValue)
            {
                text += "; the shortest segment runs from " + Where(answer.From.Value) + " to " + Where(answer.To.Value);
            }

            if (distance > 0.0 && distance <= _tol)
            {
                text += "; that is within EqualPoint " + N(_tol);
            }
            else if (distance == 0.0)
            {
                text += "; the two touch or cross";
            }

            Vec3 at = answer.From.HasValue && answer.To.HasValue ? (answer.From.Value + answer.To.Value) * 0.5 : default(Vec3);
            DiagnosticFinding f = Add(DiagnosticChecks.Gap, DiagnosticKind.Gap, false, targets[0], nameA, text, source, at, distance, Math.Max(distance * 3.0, 0.0));

            if (answer.From.HasValue && answer.To.HasValue)
            {
                f.Drawing.Line(DiagnosticColors.Gap, 3.5, answer.From.Value, answer.To.Value);
                f.Drawing.Dots.Add(new DiagnosticDot(answer.From.Value, DiagnosticColors.Gap, 9.0));
                f.Drawing.Dots.Add(new DiagnosticDot(answer.To.Value, DiagnosticColors.Gap, 9.0));
                _gap = new GapReading(targets[0], targets[1], distance, answer.From.Value, answer.To.Value, source);
            }
        }

        #endregion

        #region Spheres

        private void SpheresCheck(ItemModel m)
        {
            if (_vertexOf == null)
            {
                _vertexOf = new List<Vec3>[_scene.Items.Count];

                foreach (SceneCurve curve in Geometry().Curves)
                {
                    if (curve.ItemIndex >= 0 && curve.ItemIndex < _vertexOf.Length && curve.HasEnds)
                    {
                        List<Vec3> list = _vertexOf[curve.ItemIndex] ?? (_vertexOf[curve.ItemIndex] = new List<Vec3>());
                        list.Add(curve.Start);
                        list.Add(curve.End);
                    }
                }

                foreach (ScenePoint point in Geometry().Points)
                {
                    if (point.ItemIndex >= 0 && point.ItemIndex < _vertexOf.Length)
                    {
                        (_vertexOf[point.ItemIndex] ?? (_vertexOf[point.ItemIndex] = new List<Vec3>())).Add(point.At);
                    }
                }
            }

            List<Vec3> vertices = _vertexOf[m.Index];

            if (vertices == null || vertices.Count == 0)
            {
                return;
            }

            // One sphere to a place: the vertices in order of place, the same point once.
            vertices.Sort((a, b) =>
            {
                int c = a.X.CompareTo(b.X);
                c = c != 0 ? c : a.Y.CompareTo(b.Y);
                return c != 0 ? c : a.Z.CompareTo(b.Z);
            });

            var distinct = new List<Vec3>();

            foreach (Vec3 v in vertices)
            {
                if (distinct.Count == 0 || !distinct[distinct.Count - 1].Equals(v))
                {
                    distinct.Add(v);
                }
            }

            int room = Math.Max(0, _options.MaxSpheres - _spheresDrawn);
            int drawn = Math.Min(room, distinct.Count);
            var overlay = new DiagnosticOverlay();

            for (int s = 0; s < drawn; s++)
            {
                Sphere(overlay, distinct[s], _tol);
            }

            _spheresDrawn += drawn;
            string text = Count(distinct.Count, "vertex") + " (a sphere of radius EqualPoint " + N(_tol) + " round each; another vertex inside one is the same point to the library); "
                + (drawn < distinct.Count ? "the first " + drawn.ToString(System.Globalization.CultureInfo.InvariantCulture) + " in order of place are drawn, the cap being " + _options.MaxSpheres.ToString(System.Globalization.CultureInfo.InvariantCulture) : "all are drawn");
            DiagnosticFinding f = Add(m, DiagnosticChecks.Spheres, DiagnosticKind.ToleranceSpheres, false, text, "viewer: EqualPoint of the scene", distinct[0], _tol, 0.0);
            f.Drawing.Strokes.AddRange(overlay.Strokes);
        }

        /// <summary>
        /// Draws a sphere as its three great circles.
        /// </summary>
        internal static void Sphere(DiagnosticOverlay overlay, Vec3 centre, double radius)
        {
            for (int plane = 0; plane < 3; plane++)
            {
                var ring = new Vec3[SphereSegments];

                for (int k = 0; k < SphereSegments; k++)
                {
                    double t = 2.0 * Math.PI * k / SphereSegments;
                    double c = Math.Cos(t) * radius;
                    double s = Math.Sin(t) * radius;
                    ring[k] = plane == 0 ? centre + new Vec3(c, s, 0.0) : plane == 1 ? centre + new Vec3(0.0, c, s) : centre + new Vec3(s, 0.0, c);
                }

                overlay.Loop(DiagnosticColors.Sphere, 1.4, ring);
            }
        }

        #endregion

        #region Pieces

        private void PiecesCheck(List<int> targets)
        {
            // The pieces are the items of a layer, numbered in the order of the scene; the layers are taken in the order of their first piece.
            var layers = new List<string>();
            var counts = new List<int>();

            foreach (int index in targets)
            {
                _cancel.ThrowIfCancellationRequested();
                SceneItem item = _scene.Items[index];

                if (item.Kind == SceneKinds.Text || !SceneKinds.IsKnown(item.Kind))
                {
                    continue;
                }

                string measure;
                string name;
                double value;

                try
                {
                    object shape = _scene.Rebuild(item, out _);

                    if (!Measure(shape, out value, out name, out measure))
                    {
                        continue;
                    }
                }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is NotSupportedException || e is FormatException)
                {
                    continue;
                }

                int layerNumber = layers.IndexOf(item.Layer);

                if (layerNumber < 0)
                {
                    layers.Add(item.Layer);
                    counts.Add(0);
                    layerNumber = layers.Count - 1;
                }

                int number = ++counts[layerNumber];
                string color = SceneColor.FromCycle(number - 1);
                ItemPrimitives drawn = index < _primitives.Items.Count ? _primitives.Items[index] : null;
                string itemName = item.Name ?? item.Kind;
                Vec3 at = drawn != null && !drawn.Bounds.IsEmpty ? drawn.Bounds.Center : default(Vec3);
                string text = "piece " + number.ToString(System.Globalization.CultureInfo.InvariantCulture) + " of layer '" + item.Layer + "': " + name + " " + N(value);
                DiagnosticFinding f = Add(DiagnosticChecks.Pieces, DiagnosticKind.Piece, false, index, itemName, text, measure, at, value, drawn != null && !drawn.Bounds.IsEmpty ? drawn.Bounds.Radius * 1.1 : 0.0);

                if (drawn != null)
                {
                    foreach (DrawPolyline line in drawn.Polylines)
                    {
                        f.Drawing.Strokes.Add(new DiagnosticStroke(line, color, 3.5));
                    }

                    f.Drawing.Labels.Add(new DrawLabel(at, "#" + number.ToString(System.Globalization.CultureInfo.InvariantCulture) + "  " + name + " " + value.ToString("G6", System.Globalization.CultureInfo.InvariantCulture), 0.0));
                }

                // Listed in the order of the scene, not of the place: each piece is filed on its own.
                Flush();
            }
        }

        /// <summary>
        /// Measures a piece with the library: the area of a loop or a face, the volume of a body, the length of a chain.
        /// </summary>
        private bool Measure(object shape, out double value, out string name, out string source)
        {
            value = 0.0;
            name = "area";
            source = null;

            switch (shape)
            {
                case GeoPolygon2 s: value = s.Area; source = "GeoPolygon2.Area"; return true;
                case GeoPolygon3 s: value = s.Area; source = "GeoPolygon3.Area"; return true;
                case GeoPolygonArc2 s: value = s.Area; source = "GeoPolygonArc2.Area"; return true;
                case GeoPolygonArc3 s: value = s.Area; source = "GeoPolygonArc3.Area"; return true;
                case GeoFace2 s: value = s.Area; source = "GeoFace2.Area"; return true;
                case GeoFace3 s: value = s.Area; source = "GeoFace3.Area"; return true;
                case GeoTriangle2 s: value = s.Area; source = "GeoTriangle2.Area"; return true;
                case GeoTriangle3 s: value = s.Area; source = "GeoTriangle3.Area"; return true;
                case GeoCircle2 s: value = s.Area; source = "GeoCircle2.Area"; return true;
                case GeoCircle3 s: value = s.Area; source = "GeoCircle3.Area"; return true;
                case GeoEllipse2 s: value = s.Area; source = "GeoEllipse2.Area"; return true;
                case GeoEllipse3 s: value = s.Area; source = "GeoEllipse3.Area"; return true;
                case GeoRectangle2 s: value = s.Area; source = "GeoRectangle2.Area"; return true;
                case GeoPolygon2[] s: value = s.Sum(p => p.Area); source = "GeoPolygon2.Area of each face"; return true;
                case GeoPolygon3[] s: value = s.Sum(p => p.Area); source = "GeoPolygon3.Area of each face"; return true;

                case GeoSolid3 s:
                    name = "volume";
                    source = "GeoSolid3.TryGetVolume";
                    return s.TryGetVolume(out value, _scene.Tolerance);

                case GeoSphere3 s:
                    name = "volume";
                    value = s.Volume;
                    source = "GeoSphere3.Volume";
                    return true;

                case GeoPolyline2 s: name = "length"; value = s.Length; source = "GeoPolyline2.Length"; return true;
                case GeoPolyline3 s: name = "length"; value = s.Length; source = "GeoPolyline3.Length"; return true;
                case GeoPolylineArc2 s: name = "length"; value = s.Length; source = "GeoPolylineArc2.Length"; return true;
                case GeoPolylineArc3 s: name = "length"; value = s.Length; source = "GeoPolylineArc3.Length"; return true;
                case GeoLine2 s: name = "length"; value = s.Length; source = "GeoLine2.Length"; return true;
                case GeoLine3 s: name = "length"; value = s.Length; source = "GeoLine3.Length"; return true;
                case GeoArc2 s: name = "length"; value = s.Length; source = "GeoArc2.Length"; return true;
                case GeoArc3 s: name = "length"; value = s.Length; source = "GeoArc3.Length"; return true;
                case GeoSpline2 s: name = "length"; value = s.Length; source = "GeoSpline2.Length"; return true;
                case GeoSpline3 s: name = "length"; value = s.Length; source = "GeoSpline3.Length"; return true;
                default: return false;
            }
        }

        #endregion
    }
}
