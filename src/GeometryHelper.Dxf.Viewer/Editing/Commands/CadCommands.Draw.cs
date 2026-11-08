using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The draw commands: LINE, PLINE, CIRCLE, ARC, RECTANG, ELLIPSE, POINT, TEXT, MTEXT, MLEADER, MVIEW.</summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> Line(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify first point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            var points = new List<GeoPoint2> { c.Input.Point };
            var made = new List<string>();

            while (true)
            {
                GeoPoint2 last = points[points.Count - 1];
                string[] keywords = made.Count >= 2 ? new[] { "Close", "Undo" } : new[] { "Undo" };
                yield return CadPrompt.ForPoint("Specify next point", last, keywords);

                if (c.Input.IsPoint)
                {
                    GeoPoint2 next = c.Input.Point;
                    string handle = AddedHandle(c.Apply(null, s => s.Append(c.Space, Current(c, new DxfLine(P3(last), P3(next))))));

                    if (handle != null)
                    {
                        made.Add(handle);
                        points.Add(next);
                    }
                }
                else if (c.Input.Is("Undo"))
                {
                    if (made.Count == 0)
                    {
                        c.Write("All segments already undone.");
                        continue;
                    }

                    EraseAll(c, new[] { made[made.Count - 1] });
                    made.RemoveAt(made.Count - 1);
                    points.RemoveAt(points.Count - 1);
                }
                else if (c.Input.Is("Close"))
                {
                    GeoPoint2 first = points[0];
                    c.Apply(null, s => s.Append(c.Space, Current(c, new DxfLine(P3(last), P3(first)))));
                    yield break;
                }
                else
                {
                    yield break;
                }
            }
        }

        private static IEnumerable<CadPrompt> Polyline(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify start point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            c.Write("Current line-width is 0.0000");
            var points = new List<GeoPoint2> { c.Input.Point };

            // The way the polyline leaves its last vertex, which an arc segment drawn next starts along.
            var tangents = new List<GeoVector2> { new GeoVector2(1.0, 0.0) };
            string handle = null;
            bool arcs = false;

            while (true)
            {
                GeoPoint2 last = points[points.Count - 1];
                int segments = points.Count - 1;

                if (arcs)
                {
                    yield return CadPrompt.ForPoint("Specify endpoint of arc", last, segments >= 1 ? new[] { "CLose", "Line", "Undo" } : new[] { "Line", "Undo" });
                }
                else
                {
                    yield return CadPrompt.ForPoint("Specify next point", last, segments >= 2 ? new[] { "Arc", "Close", "Undo" } : new[] { "Arc", "Undo" });
                }

                if (c.Input.Is("Arc"))
                {
                    arcs = true;
                    continue;
                }

                if (c.Input.Is("Line"))
                {
                    arcs = false;
                    continue;
                }

                if (c.Input.Is("Undo"))
                {
                    if (segments == 0)
                    {
                        c.Write("All segments already undone.");
                        continue;
                    }

                    string removed = handle;
                    int count = points.Count;

                    if (count == 2)
                    {
                        EraseAll(c, new[] { removed });
                        handle = null;
                    }
                    else
                    {
                        c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, removed) }, s =>
                        {
                            var p = (DxfPolyline)s.Document.FindEntity(removed);
                            p.RemoveVertexAt(count - 1);
                            p.SetBulgeAt(count - 2, 0.0);
                        });
                    }

                    points.RemoveAt(points.Count - 1);
                    tangents.RemoveAt(tangents.Count - 1);
                    continue;
                }

                bool close = c.Input.Is("Close") || c.Input.Is("CLose");

                if (!c.Input.IsPoint && !close)
                {
                    yield break;
                }

                GeoPoint2 next = close ? points[0] : c.Input.Point;
                GeoVector2 chord = next - last;

                if (chord.Length <= c.Session.Tolerance.EqualPoint)
                {
                    c.Write("Invalid point: a segment needs two points apart.");
                    continue;
                }

                double bulge = 0.0;
                GeoVector2 leaving = chord / chord.Length;

                if (arcs)
                {
                    // The arc that leaves the last vertex along the way the polyline came and ends at the point: its sweep is twice
                    // the angle between that way and the chord, and its bulge the tangent of a quarter of the sweep.
                    GeoVector2 along = tangents[tangents.Count - 1];
                    double turn = Math.Atan2(along.X * chord.Y - along.Y * chord.X, along.X * chord.X + along.Y * chord.Y);
                    bulge = Math.Tan(turn / 2.0);
                    double outAngle = Math.Atan2(chord.Y, chord.X) + turn;
                    leaving = new GeoVector2(Math.Cos(outAngle), Math.Sin(outAngle));
                }

                if (handle == null)
                {
                    GeoPoint2 start = last;
                    handle = AddedHandle(c.Apply(null, s =>
                    {
                        var made = new DxfPolyline();
                        made.AddVertexAt(0, start, bulge, 0.0, 0.0);
                        made.AddVertexAt(1, next, 0.0, 0.0, 0.0);
                        s.Append(c.Space, Current(c, made));
                    }));

                    if (handle == null)
                    {
                        yield break;
                    }
                }
                else if (close)
                {
                    string closing = handle;
                    int at = points.Count - 1;
                    c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, closing) }, s =>
                    {
                        var p = (DxfPolyline)s.Document.FindEntity(closing);
                        p.SetBulgeAt(at, bulge);
                        p.Closed = true;
                    });
                }
                else
                {
                    string growing = handle;
                    int at = points.Count - 1;
                    c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, growing) }, s =>
                    {
                        var p = (DxfPolyline)s.Document.FindEntity(growing);
                        p.SetBulgeAt(at, bulge);
                        p.AddVertexAt(at + 1, next, 0.0, 0.0, 0.0);
                    });
                }

                if (close)
                {
                    yield break;
                }

                points.Add(next);
                tangents.Add(leaving);
            }
        }

        private static IEnumerable<CadPrompt> Circle(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify center point for circle", null, new[] { "3P", "2P" });
            GeoPoint2 center;
            double radius;

            if (c.Input.Is("2P"))
            {
                yield return CadPrompt.ForPoint("Specify first end point of circle's diameter");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 a = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify second end point of circle's diameter", a);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 b = c.Input.Point;
                center = new GeoPoint2((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
                radius = (b - a).Length / 2.0;
            }
            else if (c.Input.Is("3P"))
            {
                yield return CadPrompt.ForPoint("Specify first point on circle");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 a = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify second point on circle", a);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 b = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify third point on circle", b);

                if (!c.Input.IsPoint || !CadGrips.CircleThrough(a, b, c.Input.Point, out center, out radius))
                {
                    c.Write("Circle does not exist.");
                    yield break;
                }
            }
            else if (c.Input.IsPoint)
            {
                center = c.Input.Point;
                double last = c.Remembered("circle", 0.0);
                string offered = last > 0.0 ? Length(last) : null;
                yield return CadPrompt.ForDistance("Specify radius of circle", center, new[] { "Diameter" }, offered);

                if (c.Input.Is("Diameter"))
                {
                    yield return CadPrompt.ForDistance("Specify diameter of circle", center, null, last > 0.0 ? Length(2.0 * last) : null);
                    radius = c.Input.Kind == CadInputKind.Number ? c.Input.Number / 2.0 : last;
                }
                else
                {
                    radius = c.Input.Kind == CadInputKind.Number ? c.Input.Number : last;
                }
            }
            else
            {
                yield break;
            }

            if (radius <= 0.0)
            {
                c.Write("Value must be positive and nonzero.");
                yield break;
            }

            c.Remember("circle", radius);
            GeoPoint2 at = center;
            double r = radius;
            c.Apply(null, s => s.Append(c.Space, Current(c, new DxfCircle(P3(at), Up, r))));
        }

        private static IEnumerable<CadPrompt> Arc(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify start point of arc");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 start = c.Input.Point;
            yield return CadPrompt.ForPoint("Specify second point of arc", start);

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 second = c.Input.Point;

            while (true)
            {
                yield return CadPrompt.ForPoint("Specify end point of arc", second);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                if (CadGrips.ArcThrough(start, second, c.Input.Point, out GeoPoint2 center, out double radius, out double from, out double till))
                {
                    c.Apply(null, s => s.Append(c.Space, Current(c, new DxfArc(P3(center), radius, from, till))));
                    yield break;
                }

                c.Write("*Invalid* The three points lie in a line.");
            }
        }

        private static IEnumerable<CadPrompt> Rectangle(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify first corner point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 a = c.Input.Point;
            GeoPoint2 b;

            while (true)
            {
                yield return CadPrompt.ForPoint("Specify other corner point", a);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                b = c.Input.Point;

                if (Math.Abs(b.X - a.X) > c.Session.Tolerance.EqualPoint && Math.Abs(b.Y - a.Y) > c.Session.Tolerance.EqualPoint)
                {
                    break;
                }

                c.Write("*Invalid* The corners lie in a line.");
            }

            c.Apply(null, s =>
            {
                var rectangle = new DxfPolyline();
                rectangle.AddVertexAt(0, a, 0.0, 0.0, 0.0);
                rectangle.AddVertexAt(1, new GeoPoint2(b.X, a.Y), 0.0, 0.0, 0.0);
                rectangle.AddVertexAt(2, b, 0.0, 0.0, 0.0);
                rectangle.AddVertexAt(3, new GeoPoint2(a.X, b.Y), 0.0, 0.0, 0.0);
                rectangle.Closed = true;
                s.Append(c.Space, Current(c, rectangle));
            });
        }

        /// <summary>ELLIPSE, whole, by the ends of one axis or by its centre and one end, then the other axis's half or a rotation.</summary>
        private static IEnumerable<CadPrompt> Ellipse(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify axis endpoint of ellipse", null, new[] { "Center" });
            GeoPoint2 center;
            GeoVector2 half;

            if (c.Input.Is("Center"))
            {
                yield return CadPrompt.ForPoint("Specify center of ellipse");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                center = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify endpoint of axis", center);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                half = c.Input.Point - center;
            }
            else if (c.Input.IsPoint)
            {
                GeoPoint2 a = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify other endpoint of axis", a);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                center = new GeoPoint2((a.X + c.Input.Point.X) / 2.0, (a.Y + c.Input.Point.Y) / 2.0);
                half = c.Input.Point - center;
            }
            else
            {
                yield break;
            }

            if (half.Length <= c.Session.Tolerance.EqualPoint)
            {
                c.Write("*Invalid* The ends of the axis are one point.");
                yield break;
            }

            yield return CadPrompt.ForDistance("Specify distance to other axis", center, new[] { "Rotation" });
            double other;

            if (c.Input.Is("Rotation"))
            {
                yield return CadPrompt.ForAngle("Specify rotation around major axis", center);

                // AutoCAD takes a rotation below 89.4 degrees, the other half the first's times its cosine.
                if (c.Input.Kind != CadInputKind.Number || Math.Abs(Math.Cos(c.Input.Number)) < Math.Cos(89.4 * Math.PI / 180.0))
                {
                    c.Write("*Invalid* Requires an angle from 0 to 89.4 degrees.");
                    yield break;
                }

                other = half.Length * Math.Abs(Math.Cos(c.Input.Number));
            }
            else if (c.Input.Kind == CadInputKind.Number)
            {
                other = Math.Abs(c.Input.Number);
            }
            else
            {
                yield break;
            }

            if (other <= c.Session.Tolerance.EqualPoint)
            {
                c.Write("*Invalid* The other axis has no length.");
                yield break;
            }

            // The longer half is the major axis; the shorter lies square to it.
            GeoVector2 major = other > half.Length ? new GeoVector2(-half.Y, half.X) / half.Length * other : half;
            double ratio = Math.Min(other, half.Length) / Math.Max(other, half.Length);
            c.Apply(null, s => s.Append(c.Space, Current(c, new DxfEllipse(P3(center), new GeoVector3(0.0, 0.0, 1.0), new GeoVector3(major.X, major.Y, 0.0), ratio, 0.0, 2.0 * Math.PI))));
        }

        /// <summary>POINT: one point object where the point is given, drawn as the drawing's PDMODE says.</summary>
        private static IEnumerable<CadPrompt> Point(CadCommandContext c)
        {
            c.Write("Current point modes:  PDMODE=" + c.Database.Pdmode.ToString(CultureInfo.InvariantCulture) + "  PDSIZE=" + Length(c.Database.Pdsize));
            yield return CadPrompt.ForPoint("Specify a point");

            if (c.Input.IsPoint)
            {
                GeoPoint2 at = c.Input.Point;
                c.Apply(null, s => s.Append(c.Space, Current(c, new DxfDBPoint(P3(at)))));
            }
        }

        private static IEnumerable<CadPrompt> Text(CadCommandContext c)
        {
            string style = TextStyle(c);
            double height = c.Remembered("text.height", TextSize(c));
            c.Write("Current text style:  \"" + (style ?? "Standard") + "\"  Text height:  " + Length(height) + "  Annotative:  No  Justify:  Left");
            yield return CadPrompt.ForPoint("Specify start point of text");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 at = c.Input.Point;
            yield return CadPrompt.ForDistance("Specify height", at, null, Length(height));

            if (c.Input.Kind == CadInputKind.Number)
            {
                if (c.Input.Number <= 0.0)
                {
                    c.Write("Value must be positive and nonzero.");
                    yield break;
                }

                height = c.Input.Number;
            }

            double rotation = c.Remembered("text.rotation", 0.0);
            yield return CadPrompt.ForAngle("Specify rotation angle of text", at, null, Number(rotation * 180.0 / Math.PI));

            if (c.Input.Kind == CadInputKind.Number)
            {
                rotation = c.Input.Number;
            }

            c.Remember("text.height", height);
            c.Remember("text.rotation", rotation);
            double widthFactor = c.Remembered("text.widthFactor", 1.0);

            // Line after line until an empty one, each a TEXT of its own five thirds of the height below the one before, as DTEXT
            // goes on; the lines of one run are one undo step.
            var down = new GeoVector2(Math.Sin(rotation), -Math.Cos(rotation)) * (height * 5.0 / 3.0);

            while (true)
            {
                yield return CadPrompt.ForString("Enter text");

                if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Length == 0)
                {
                    yield break;
                }

                string words = c.Input.Text;
                GeoPoint2 line = at;
                c.Apply(null, s =>
                {
                    var text = new DxfDBText { Position = P3(line), Height = height, Rotation = rotation, TextString = words, WidthFactor = widthFactor };

                    if (style != null)
                    {
                        text.TextStyleName = style;
                    }

                    s.Append(c.Space, Current(c, text));
                });
                at += down;
            }
        }

        private static IEnumerable<CadPrompt> MText(CadCommandContext c)
        {
            string style = TextStyle(c);
            double height = c.Remembered("text.height", TextSize(c));
            c.Write("Current text style:  \"" + (style ?? "Standard") + "\"  Text height:  " + Length(height) + "  Annotative:  No");
            yield return CadPrompt.ForPoint("Specify first corner");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 a = c.Input.Point;

            while (true)
            {
                yield return CadPrompt.ForPoint("Specify opposite corner", a, new[] { "Height" });

                if (c.Input.Is("Height"))
                {
                    yield return CadPrompt.ForDistance("Specify height", a, null, Length(height));

                    if (c.Input.Kind == CadInputKind.Number && c.Input.Number > 0.0)
                    {
                        height = c.Input.Number;
                    }

                    continue;
                }

                break;
            }

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 b = c.Input.Point;
            yield return CadPrompt.ForString("Enter text");

            if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Length == 0)
            {
                yield break;
            }

            string words = c.Input.Text;
            double widthFactor = c.Remembered("text.widthFactor", 1.0);

            // Letters of another width open the words with the code AutoCAD writes for them, \W0.8000; for 0.8.
            if (Math.Abs(widthFactor - 1.0) > 1E-9)
            {
                words = DxfMText.WidthChange + widthFactor.ToString("0.0000", CultureInfo.InvariantCulture) + ";" + words;
            }

            double width = Math.Abs(b.X - a.X);
            var corner = new GeoPoint2(Math.Min(a.X, b.X), Math.Max(a.Y, b.Y));
            c.Remember("text.height", height);
            c.Apply(null, s =>
            {
                var text = new DxfMText { Location = P3(corner), TextHeight = height, Width = width, Attachment = DxfAttachmentPoint.TopLeft, Contents = words };

                if (style != null)
                {
                    text.TextStyleName = style;
                }

                s.Append(c.Space, Current(c, text));
            });
        }

        private static IEnumerable<CadPrompt> MLeader(CadCommandContext c)
        {
            if (!HasMLeaderStyle(c.Database))
            {
                c.Write("The drawing has no multileader style to draw in: one made by AutoCAD's MLEADERSTYLE is needed.");
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify leader arrowhead location");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 arrow = c.Input.Point;
            yield return CadPrompt.ForPoint("Specify leader landing location", arrow);

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 landing = c.Input.Point;
            yield return CadPrompt.ForString("Enter text");

            if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Length == 0)
            {
                yield break;
            }

            string words = c.Input.Text;
            double height = TextSize(c);
            c.Apply(null, s =>
            {
                var mleader = new DxfMLeader { MText = new DxfMText { Contents = words, Location = P3(landing), TextHeight = height } };
                mleader.AddLeaderLine(P3(arrow));
                s.Append(c.Space, Current(c, mleader));
            });
        }

        private static IEnumerable<CadPrompt> MView(CadCommandContext c)
        {
            if (c.Space.IsModel)
            {
                c.Write("** Command not allowed in Model Tab **");
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify corner of viewport", null, new[] { "Fit" }, "Fit");
            DisplayBox frame;

            if (c.Input.IsPoint)
            {
                GeoPoint2 a = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify opposite corner", a);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 b = c.Input.Point;
                frame = new DisplayBox(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
            }
            else if (c.Input.Is("Fit"))
            {
                frame = c.Space.PaperBounds;
            }
            else
            {
                yield break;
            }

            if (frame.Width <= 0.0 || frame.Height <= 0.0)
            {
                c.Write("*Invalid* The corners lie in a line.");
                yield break;
            }

            // The viewport shows all of model space, as AutoCAD's MVIEW zooms a new viewport to the extents.
            DisplayBox model = c.Document.GetDisplay(c.Document.ModelSpace).Bounds;
            double viewHeight = 1.0;
            var viewCenter = new GeoPoint2(0.0, 0.0);

            if (!model.IsEmpty)
            {
                viewCenter = new GeoPoint2((model.MinX + model.MaxX) / 2.0, (model.MinY + model.MaxY) / 2.0);
                viewHeight = Math.Max(model.Height, model.Width * frame.Height / frame.Width);

                if (viewHeight <= 0.0)
                {
                    viewHeight = 1.0;
                }
            }

            CadSpace space = c.Space;
            c.Apply(null, s =>
            {
                // A layout never shown has no viewport 1 for its sheet yet; AutoCAD gives it one before the first of its own.
                int before = space.Layout.GetViewports().Count;
                DxfViewport sheet = space.Layout.Initialize();

                if (space.Layout.GetViewports().Count > before)
                {
                    s.Added(space, sheet);
                }

                var viewport = new DxfViewport();
                s.Append(space, Current(c, viewport));
                viewport.CenterPoint = new GeoPoint3((frame.MinX + frame.MaxX) / 2.0, (frame.MinY + frame.MaxY) / 2.0, 0.0);
                viewport.Width = frame.Width;
                viewport.Height = frame.Height;
                viewport.ViewCenter = viewCenter;
                viewport.ViewHeight = viewHeight;
                viewport.On = true;
            });
        }

        /// <summary>Whether the drawing has the multileader style a new MULTILEADER takes, which the library reads but does not write.</summary>
        private static bool HasMLeaderStyle(DxfDatabase database)
        {
            try
            {
                DxfDBDictionary styles = database.MLeaderStyleDictionary;
                string name = database.MLeaderstyle;
                return styles != null && !string.IsNullOrEmpty(name) && styles.Contains(name);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string TextStyle(CadCommandContext c)
        {
            string name;

            try
            {
                name = c.Database.Textstyle;
            }
            catch (Exception)
            {
                return null;
            }

            return !string.IsNullOrEmpty(name) && c.Database.TextStyleTable.Has(name) ? name : null;
        }

        private static double TextSize(CadCommandContext c)
        {
            try
            {
                double size = c.Database.Textsize;
                return size > 0.0 ? size : 2.5;
            }
            catch (Exception)
            {
                return 2.5;
            }
        }
    }
}
