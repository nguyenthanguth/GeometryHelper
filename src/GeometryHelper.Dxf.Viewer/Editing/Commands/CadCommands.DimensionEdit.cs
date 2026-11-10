using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The commands that change dimensions already drawn: DIMEDIT, DIMTEDIT, DIMSPACE and DIMBREAK.</summary>
    /// <remarks>
    /// Each writes into the dimension's record what AutoCAD writes (its text, the place and turn of its words, the oblique angle
    /// of its extension lines, the place of its dimension line), then draws its picture anew from it, as AutoCAD draws a
    /// dimension again; a picture another dimension draws with too is first given to this one as a block of its own. A
    /// dimension of a model drawing whose owner does not let it be changed is left as it is, with the owner's words. Each run is
    /// one undo step.
    /// </remarks>
    internal static partial class CadCommands
    {
        /// <summary>
        /// DIMEDIT: Home puts the words of the dimensions selected back where their style puts them, level with the line; New
        /// gives them a text (&lt;&gt; for the measurement); Rotate turns their words to an angle (0 for the style's); Oblique
        /// slants the extension lines of the linear ones to an angle from the x axis, as for an isometric view, and keeps it as
        /// AutoCAD keeps it, from the dimension line: 30 for a dimension rotated 30 slanted to 60.
        /// </summary>
        private static IEnumerable<CadPrompt> DimEdit(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword("Enter type of dimension editing", new[] { "Home", "New", "Rotate", "Oblique" }, "Home");
            string kind = c.Input.Kind == CadInputKind.Keyword ? c.Input.Text : null;

            if (kind == null)
            {
                yield break;
            }

            string text = null;
            double angle = 0.0;
            bool slanted = false;

            if (kind == "New")
            {
                yield return CadPrompt.ForString("Enter dimension text", "<>");
                text = c.Input.Kind == CadInputKind.Text ? c.Input.Text : "<>";
            }
            else if (kind == "Rotate")
            {
                yield return CadPrompt.ForAngle("Specify angle for dimension text");

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                angle = TextAngle(c, c.Input.Number);
            }

            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (kind == "Oblique")
            {
                yield return CadPrompt.ForAngle("Enter obliquing angle (press ENTER for none)").Plain();

                if (c.Input.Kind == CadInputKind.Number)
                {
                    angle = Turned(c.Session.Ucs.AngleToWorld(c.Input.Number));
                    slanted = true;
                }
                else if (!c.Input.IsNone)
                {
                    yield break;
                }
            }

            string property = kind == "Home" ? "Text position" : kind == "New" ? "Text override" : kind == "Rotate" ? "Text rotation" : "Oblique";
            string value = kind == "New" ? text : kind == "Home" ? "Home" : Values(angle);
            List<DxfDimension> dimensions = Changeable(c, c.Selected, property, d => kind != "Oblique" || d is DxfRotatedDimension || d is DxfAlignedDimension);

            ChangeDimensions(c, dimensions, property, value, d =>
            {
                switch (kind)
                {
                    case "Home":
                        d.UsingDefaultTextPosition = true;
                        d.TextRotation = 0.0;
                        break;
                    case "New":
                        d.DimensionText = text;
                        break;
                    case "Rotate":
                        d.TextRotation = angle;
                        break;
                    default:
                        double oblique = slanted ? Turned(angle - LineAngleOf(d)) : 0.0;

                        if (d is DxfRotatedDimension rotated)
                        {
                            rotated.Oblique = oblique;
                        }
                        else
                        {
                            ((DxfAlignedDimension)d).Oblique = oblique;
                        }

                        break;
                }
            });
        }

        /// <summary>
        /// DIMTEDIT: moves the words of a dimension to a point, the dimension line going with them where the dimension's DIMTMOVE
        /// is 0, a leader drawn to them where it is 1; Left and Right put them along the line next to the extension line on that
        /// side, Center and Home back in the middle; Angle turns them.
        /// </summary>
        private static IEnumerable<CadPrompt> DimTextEdit(CadCommandContext c)
        {
            yield return CadPrompt.ForEntity("Select dimension");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            if (!(c.Document.FindEntity(c.Input.Handles[0]) is DxfDimension picked))
            {
                c.Write("Object selected is not a dimension.");
                yield break;
            }

            if (Changeable(c, new[] { picked.Handle }, "Text position", d => true).Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify new location for dimension text", null, new[] { "Left", "Right", "Center", "Home", "Angle" });
            var only = new List<DxfDimension> { picked };

            if (c.Input.IsPoint)
            {
                GeoPoint2 at = c.Input.Point;
                ChangeDimensions(c, only, "Text position", Values(at.X, at.Y), d => MoveWords(d, at));
            }
            else if (c.Input.Is("Center") || c.Input.Is("Home"))
            {
                ChangeDimensions(c, only, "Text position", c.Input.Text, d => d.UsingDefaultTextPosition = true);
            }
            else if (c.Input.Is("Left") || c.Input.Is("Right"))
            {
                bool left = c.Input.Is("Left");

                if (!(picked is DxfRotatedDimension) && !(picked is DxfAlignedDimension))
                {
                    c.Write("Left and Right place the text of linear dimensions only.");
                    yield break;
                }

                ChangeDimensions(c, only, "Text position", c.Input.Text, d => WordsToSide(d, left));
            }
            else if (c.Input.Is("Angle"))
            {
                yield return CadPrompt.ForAngle("Specify angle for dimension text");

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                double angle = TextAngle(c, c.Input.Number);
                ChangeDimensions(c, only, "Text rotation", Values(angle), d => d.TextRotation = angle);
            }
        }

        /// <summary>
        /// DIMSPACE: the linear dimensions selected that are parallel to a base one, or the angular ones about its vertex, put at
        /// even spacing from it, each side of it in the order they stand: a value apart (0 lines them up with it), or Auto,
        /// twice the height of the base's text.
        /// </summary>
        private static IEnumerable<CadPrompt> DimSpace(CadCommandContext c)
        {
            yield return CadPrompt.ForEntity("Select base dimension");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            DxfDimension baseDimension = c.Document.FindEntity(c.Input.Handles[0]) as DxfDimension;

            if (baseDimension == null || !IsChained(baseDimension))
            {
                c.Write("The base dimension must be linear or angular.");
                yield break;
            }

            yield return CadPrompt.ForSelection("Select dimensions to space");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            IReadOnlyList<string> handles = c.Input.Handles;
            yield return CadPrompt.ForDistance("Enter value", null, new[] { "Auto" }, "Auto");
            double spacing;

            if (c.Input.Is("Auto"))
            {
                DxfDimStyleTableRecord data = baseDimension.GetDimstyleData();
                spacing = 2.0 * data.Dimtxt * (data.Dimscale > 0.0 ? data.Dimscale : 1.0);
            }
            else if (c.Input.Kind == CadInputKind.Number && c.Input.Number >= 0.0)
            {
                spacing = c.Input.Number;
            }
            else
            {
                yield break;
            }

            var moves = new SortedDictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            int apart = Spaced(c, baseDimension, handles, spacing, moves);

            if (apart > 0)
            {
                c.Write(apart.ToString(CultureInfo.InvariantCulture) + (apart == 1 ? " dimension is" : " dimensions are") + " not parallel to the base dimension and stays where it is.");
            }

            List<DxfDimension> dimensions = Changeable(c, moves.Keys.ToList(), "Dimension line position", d => true);
            ChangeDimensions(c, dimensions, "Dimension line position", Values(spacing), d => ShiftLine(d, baseDimension.Handle, moves[d.Handle], c.Document));
        }

        /// <summary>
        /// DIMBREAK: breaks the extension lines and the dimension line of a dimension where other objects cross them, a gap of
        /// the break size wide (DIMBREAK, from the style's extended data, else one and a half arrowheads); Auto at every line,
        /// polyline, circle and arc of the space, or at the objects picked one by one; Manual between two points; Remove takes
        /// the breaks out. Multiple breaks or restores several at once.
        /// </summary>
        /// <remarks>
        /// The break is drawn into the dimension's picture and kept as AutoCAD 2021 keeps it, a BREAKDATA under ACAD_BREAKDATA in
        /// the dimension's extension dictionary, each object breaking it named there and naming the BREAKDATA among its
        /// reactors, so that a dimension drawn anew (by DIMEDIT, a change of its style, or AutoCAD) is broken again; Remove
        /// takes the BREAKDATA out, and the extension dictionary where nothing else is in it.
        /// </remarks>
        private static IEnumerable<CadPrompt> DimBreak(CadCommandContext c)
        {
            yield return CadPrompt.ForEntity("Select dimension to add/remove break", new[] { "Multiple" });

            if (c.Input.Is("Multiple"))
            {
                yield return CadPrompt.ForSelection("Select dimensions");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                IReadOnlyList<string> handles = c.Input.Handles;
                yield return CadPrompt.ForKeyword("Enter an option", new[] { "Break", "Remove" }, "Break");

                if (!c.Input.Is("Break") && !c.Input.Is("Remove"))
                {
                    yield break;
                }

                bool remove = c.Input.Is("Remove");
                List<DxfDimension> many = Changeable(c, handles, "Dimension break", d => true);
                CadEditResult result = ChangeDimensions(c, many, "Dimension break", remove ? "Remove" : "Auto", d =>
                {
                    if (remove)
                    {
                        ForgetBreaks(d);
                    }
                    else
                    {
                        KeepObjectBreaks(d, BreakAt(d, Cutters(c.Document, c.Space, d, null), null));
                    }
                }, remove);

                if (result != null && result.Succeeded)
                {
                    c.Write(Modified(many.Count));
                }

                yield break;
            }

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            if (!(c.Document.FindEntity(c.Input.Handles[0]) is DxfDimension dimension))
            {
                c.Write("Object selected is not a dimension.");
                yield break;
            }

            if (Changeable(c, new[] { dimension.Handle }, "Dimension break", d => true).Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForEntity("Select object to break dimension", new[] { "Auto", "Manual", "Remove" }, "Auto");
            var only = new List<DxfDimension> { dimension };
            CadEditResult done = null;

            if (c.Input.Is("Auto"))
            {
                done = ChangeDimensions(c, only, "Dimension break", "Auto", d => KeepObjectBreaks(d, BreakAt(d, Cutters(c.Document, c.Space, d, null), null)), false);
            }
            else if (c.Input.Is("Remove"))
            {
                done = ChangeDimensions(c, only, "Dimension break", "Remove", ForgetBreaks);
            }
            else if (c.Input.Is("Manual"))
            {
                yield return CadPrompt.ForPoint("Specify first break point");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 first = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify second break point", first);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 second = c.Input.Point;
                done = ChangeDimensions(c, only, "Dimension break", Values(first.X, first.Y, second.X, second.Y), d =>
                {
                    BreakAt(d, null, new[] { first, second });
                    KeepManualBreak(d, first, second);
                }, false);
            }
            else if (c.Input.Kind == CadInputKind.Selection)
            {
                var picked = new List<string> { c.Input.Handles[0] };

                while (true)
                {
                    yield return CadPrompt.ForEntity("Select object to break dimension");

                    if (c.Input.Kind != CadInputKind.Selection)
                    {
                        break;
                    }

                    picked.Add(c.Input.Handles[0]);
                }

                done = ChangeDimensions(c, only, "Dimension break", string.Join(",", picked), d => KeepObjectBreaks(d, BreakAt(d, Cutters(c.Document, c.Space, d, picked), null)), false);
            }

            if (done != null && done.Succeeded)
            {
                c.Write(Modified(1));
            }
        }

        private static string Modified(int count) => count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " object modified" : " objects modified");

        /// <summary>An angle typed or picked for the words, in the UCS, as the turn of the text in the world; 0 stays 0, the style's own.</summary>
        private static double TextAngle(CadCommandContext c, double ucsAngle) => ucsAngle == 0.0 ? 0.0 : Turned(c.Session.Ucs.AngleToWorld(ucsAngle));

        /// <summary>
        /// The dimensions of a list that are of a kind an edit takes and whose owner lets them be changed, in the order given; a
        /// refusal written as the owner gives it, the dimension called a dimension.
        /// </summary>
        private static List<DxfDimension> Changeable(CadCommandContext c, IEnumerable<string> handles, string property, Func<DxfDimension, bool> takes)
        {
            var dimensions = new List<DxfDimension>();

            foreach (string handle in handles)
            {
                if (!(c.Document.FindEntity(handle) is DxfDimension dimension) || !takes(dimension))
                {
                    continue;
                }

                var record = new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, property);

                if (!c.Document.EditGate.Allows(c.Document, dimension, record, out string reason))
                {
                    c.Write(reason == null
                        ? "The dimension " + handle + " may not be changed."
                        : reason.Replace("This " + CadOwnerEditGate.Describe(dimension) + " ", "This dimension "));
                    continue;
                }

                dimensions.Add(dimension);
            }

            return dimensions;
        }

        /// <summary>
        /// Changes dimensions as one edit, each one's property planned with its value for an owner that takes its own edits, then
        /// each drawn anew, or, for a break, its picture cut as it stands; null where there are none to change.
        /// </summary>
        private static CadEditResult ChangeDimensions(CadCommandContext c, List<DxfDimension> dimensions, string property, string value, Action<DxfDimension> change, bool redraw = true)
        {
            if (dimensions.Count == 0)
            {
                return null;
            }

            var planned = new List<CadEditRecord>();

            foreach (DxfDimension dimension in dimensions)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.PropertyChanged, dimension.Handle, property, null, value));
            }

            return c.Apply(planned, s =>
            {
                foreach (CadEditRecord record in planned)
                {
                    if (s.IsHandled(record.Handle))
                    {
                        continue;
                    }

                    var dimension = (DxfDimension)s.Document.FindEntity(record.Handle);
                    OwnPicture(s.Database, dimension);
                    change(dimension);

                    if (redraw)
                    {
                        Redraw(dimension);
                    }
                }
            });
        }

        /// <summary>Gives a dimension a picture block of its own where another dimension draws with the same one, as a copy does.</summary>
        internal static void OwnPicture(DxfDatabase database, DxfDimension dimension)
        {
            DxfBlockTableRecord picture = dimension.DimBlock;

            if (picture != null && picture.IsAnonymous && CadPropertyEditor.SharesPicture(database, dimension, picture))
            {
                var own = new DxfBlockTableRecord { Name = "*D" };
                database.BlockTable.Add(own);
                dimension.DimBlock = own;
            }
        }

        /// <summary>
        /// Appends a copy of an entity moved by a transformation to the space of the entity, and gives it back, its owner's tag
        /// taken off. A dimension drawn by an anonymous block of its own is copied as AutoCAD 2021's COPY copies one: its
        /// picture copied into a block of the copy's own, which moves with it, so that the copy is drawn where it stands and a
        /// move of the one leaves the other's picture where it is.
        /// </summary>
        internal static DxfEntity AppendCopy(CadEditScope s, string handle, GeoTransform3 transform)
        {
            DxfEntity entity = s.Document.FindEntity(handle);
            CadSpace space = s.Document.FindSpace(handle);
            DxfBlockTableRecord picture = (entity as DxfDimension)?.DimBlock;

            if (picture == null || !picture.IsAnonymous)
            {
                DxfEntity copied = entity.GetTransformedCopy(transform);
                CadOwnerTag.Remove(copied);
                return s.Append(space, copied);
            }

            var copy = (DxfDimension)entity.Clone();
            CadOwnerTag.Remove(copy);
            s.Append(space, copy);
            var own = new DxfBlockTableRecord { Name = "*D" };
            s.Database.BlockTable.Add(own);

            foreach (DxfEntity part in picture)
            {
                if (!part.IsErased)
                {
                    own.AppendEntity(part.Clone());
                }
            }

            copy.DimBlock = own;
            copy.TransformBy(transform);
            return copy;
        }

        /// <summary>
        /// Moves the words of a dimension to a point, as AutoCAD 2021's DIMTEDIT moves them: a linear one's dimension line moved
        /// to run through the point where its DIMTMOVE is 0, the words above it there (on it, the line broken, for DIMTAD 0),
        /// and for 1 and 2 its words by the point, the line staying (see <see cref="PlaceMoved"/>); an angular one's arc through the point where it is 0, the words standing on
        /// the arc there, above it as they read; a radius's or a diameter's words slid along its line, which stays, to where the
        /// point falls on it for DIMTMOVE 0 and 1, and put at the point itself for 2. A radius of 20 at 30 degrees whose words
        /// are moved to 50 out at 60 degrees has them 43.3 out along its line.
        /// </summary>
        internal static void MoveWords(DxfDimension dimension, GeoPoint2 at)
        {
            bool withLine = dimension.Dimtmove == 0;

            switch (dimension)
            {
                case DxfPoint3AngularDimension three when withLine:
                    three.ArcPoint = P3(ArcUnder(P2(three.CenterPoint), P2(three.ArcPoint), at, 0.0));
                    break;
                case DxfLineAngularDimension2 two when withLine:
                    if (Meet(new DxfLine(two.XLine1Start, two.XLine1End), new DxfLine(two.XLine2Start, two.XLine2End), new Tolerance(1E-9, 1E-9), out GeoPoint2 vertex))
                    {
                        two.ArcPoint = P3(ArcUnder(vertex, P2(two.ArcPoint), at, 0.0));
                    }

                    break;
                case DxfRadialDimension radial:
                    at = dimension.Dimtmove == 2 ? at : OnLine(P2(radial.Center), P2(radial.ChordPoint), at);
                    break;
                case DxfDiametricDimension diametric:
                    at = dimension.Dimtmove == 2 ? at : OnLine(P2(diametric.FarChordPoint), P2(diametric.ChordPoint), at);
                    break;
                default:
                    DragWords(dimension, at);
                    return;
            }

            dimension.TextPosition = P3(at);
        }

        /// <summary>Where a point falls on the line through two points, square to it; the first point where they are one.</summary>
        private static GeoPoint2 OnLine(GeoPoint2 from, GeoPoint2 to, GeoPoint2 point)
        {
            GeoVector2 way = to - from;
            double length = way.Length;

            if (length <= 1E-12)
            {
                return from;
            }

            GeoVector2 unit = way / length;
            return from + unit * ((point - from).X * unit.X + (point - from).Y * unit.Y);
        }

        /// <summary>
        /// Drags the words of a dimension to a point, as their grip drags them: a linear one's dimension line moved to run through
        /// the point where its DIMTMOVE is 0, the point kept as its text's, about which its picture stands the words as AutoCAD
        /// 2021's DIMTEDIT does (above the line for DIMTAD 1); an angular one's arc to the radius under them as they read, so
        /// that they stand at the point; a radius's or a diameter's line turned toward them.
        /// </summary>
        internal static void DragWords(DxfDimension dimension, GeoPoint2 at)
        {
            DxfDimStyleTableRecord data = dimension.GetDimstyleData();
            bool withLine = dimension.Dimtmove == 0;

            switch (dimension)
            {
                case DxfRotatedDimension rotated when withLine:
                    rotated.DimLinePoint = P3(LineUnder(P2(rotated.XLine2Point), rotated.Rotation, ObliqueOf(rotated), at));
                    break;
                case DxfAlignedDimension aligned when withLine:
                    GeoPoint2 a = P2(aligned.XLine1Point);
                    GeoPoint2 b = P2(aligned.XLine2Point);
                    aligned.DimLinePoint = P3(LineUnder(b, Math.Atan2(b.Y - a.Y, b.X - a.X), ObliqueOf(aligned), at));
                    break;
                case DxfPoint3AngularDimension three when withLine:
                    at = OnArcUnder(dimension, data, P2(three.CenterPoint), at);
                    three.ArcPoint = P3(ArcUnder(P2(three.CenterPoint), P2(three.ArcPoint), at, 0.0));
                    break;
                case DxfLineAngularDimension2 two when withLine:
                    if (Meet(new DxfLine(two.XLine1Start, two.XLine1End), new DxfLine(two.XLine2Start, two.XLine2End), new Tolerance(1E-9, 1E-9), out GeoPoint2 vertex))
                    {
                        at = OnArcUnder(dimension, data, vertex, at);
                        two.ArcPoint = P3(ArcUnder(vertex, P2(two.ArcPoint), at, 0.0));
                    }

                    break;
                case DxfRadialDimension radial:
                    GeoPoint2 center = P2(radial.Center);
                    double radius = (P2(radial.ChordPoint) - center).Length;

                    if ((at - center).Length > 1E-12)
                    {
                        GeoPoint2 chord = center + (at - center) / (at - center).Length * radius;
                        radial.ChordPoint = P3(chord);
                        radial.LeaderLength = (at - chord).Length;
                    }

                    break;
                case DxfDiametricDimension diametric:
                    GeoPoint2 near = P2(diametric.ChordPoint);
                    GeoPoint2 far = P2(diametric.FarChordPoint);
                    var middle = new GeoPoint2((near.X + far.X) / 2.0, (near.Y + far.Y) / 2.0);
                    double half = (near - middle).Length;

                    if ((at - middle).Length > 1E-12)
                    {
                        GeoVector2 way = (at - middle) / (at - middle).Length;
                        diametric.ChordPoint = P3(middle + way * half);
                        diametric.FarChordPoint = P3(middle - way * half);
                        diametric.LeaderLength = (at - (middle + way * half)).Length;
                    }

                    break;
            }

            dimension.TextPosition = P3(at);
        }

        /// <summary>The point of a linear dimension's line, where its second extension line meets it, once the line runs through a point.</summary>
        private static GeoPoint2 LineUnder(GeoPoint2 second, double angle, double oblique, GeoPoint2 at)
            => Foot(second, at, new GeoVector2(Math.Cos(angle), Math.Sin(angle)), oblique);

        /// <summary>
        /// The point of the arc of an angular dimension its words stand on where they are to stand at a point, as its picture puts
        /// them about a point of the arc: below them as they read by DIMGAP and half their height, on them for DIMTAD 0.
        /// </summary>
        private static GeoPoint2 OnArcUnder(DxfDimension dimension, DxfDimStyleTableRecord data, GeoPoint2 vertex, GeoPoint2 at)
        {
            if ((at - vertex).Length <= 1E-12)
            {
                return at;
            }

            double scale = data.Dimscale > 0.0 ? data.Dimscale : 1.0;
            double gap = Math.Abs(data.Dimgap) * scale;
            string words = dimension.DimBlock?.OfType<DxfMText>().FirstOrDefault(m => !m.IsErased)?.Contents ?? string.Empty;
            WordsWidth(data, words, data.Dimtxt * scale, out double tall);
            int tad = data.Dimtad == 0 && Math.Abs(data.Dimtvp) >= 0.7 ? 1 : data.Dimtad;
            double lift = tad != data.Dimtad ? data.Dimtvp * tall : tad == 0 ? data.Dimtvp * tall : (gap + tall / 2.0 + (data.Dimgap < 0.0 ? gap : 0.0)) * (tad == 4 ? -1.0 : 1.0);
            double tangent = Readable(Math.Atan2(at.Y - vertex.Y, at.X - vertex.X) + Math.PI / 2.0);
            return at - new GeoVector2(-Math.Sin(tangent), Math.Cos(tangent)) * lift;
        }

        /// <summary>The arc point of an angular dimension, kept on its ray, once the arc runs under words at a point.</summary>
        private static GeoPoint2 ArcUnder(GeoPoint2 vertex, GeoPoint2 arc, GeoPoint2 at, double lift)
        {
            double radius = (at - vertex).Length - lift;

            if (radius <= 1E-12 || (arc - vertex).Length <= 1E-12)
            {
                return arc;
            }

            return vertex + (arc - vertex) / (arc - vertex).Length * radius;
        }

        /// <summary>
        /// Puts the words of a linear dimension along its line next to the extension line on the left or the right as they
        /// read (the top of a vertical line): an arrowhead and a gap in from it, plus half their width and a gap, above the line
        /// by the gap where DIMTAD puts them there; level under DIMTIH, centred on the line.
        /// </summary>
        private static void WordsToSide(DxfDimension dimension, bool left)
        {
            GeoPoint2 p1;
            GeoPoint2 p2;
            GeoPoint2 onLine;
            double angle;

            if (dimension is DxfRotatedDimension rotated)
            {
                p1 = P2(rotated.XLine1Point);
                p2 = P2(rotated.XLine2Point);
                onLine = P2(rotated.DimLinePoint);
                angle = rotated.Rotation;
            }
            else
            {
                var aligned = (DxfAlignedDimension)dimension;
                p1 = P2(aligned.XLine1Point);
                p2 = P2(aligned.XLine2Point);
                onLine = P2(aligned.DimLinePoint);
                angle = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);
            }

            DxfDimStyleTableRecord data = dimension.GetDimstyleData();
            double scale = data.Dimscale > 0.0 ? data.Dimscale : 1.0;
            double height = data.Dimtxt * scale;
            var along = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
            double oblique = ObliqueOf(dimension);
            GeoPoint2 q1 = Foot(p1, onLine, along, oblique);
            GeoPoint2 q2 = Foot(p2, onLine, along, oblique);
            double readable = Readable(angle);
            var reads = new GeoVector2(Math.Cos(readable), Math.Sin(readable));
            // Left of a vertical line is its top, as AutoCAD 2021 takes it, though the words read up it.
            GeoVector2 leftward = Math.Abs(readable - Math.PI / 2.0) <= 1E-9 ? -reads : reads;
            bool firstLeft = (q2 - q1).X * leftward.X + (q2 - q1).Y * leftward.Y >= 0.0;
            GeoPoint2 end = left == firstLeft ? q1 : q2;
            GeoPoint2 other = left == firstLeft ? q2 : q1;
            GeoVector2 inward = (other - end).Length > 1E-12 ? (other - end) / (other - end).Length : reads;
            List<DxfEntity> picture = dimension.DimBlock?.ToList() ?? new List<DxfEntity>();
            string words = picture.OfType<DxfMText>().FirstOrDefault()?.Contents ?? dimension.DimensionText;

            // An arrowhead and a gap in from the extension line, then the words with a gap about them, measured along the line
            // (level words under DIMTIH across it): "100" of Arial on a line at 30 degrees stands 6.733 in, 6.895 kept level.
            double gap = Math.Abs(data.Dimgap) * scale;
            double width = WordsWidth(data, words, height, out double tall);
            double inset = data.Dimasz * scale + gap + Chord(width, tall, gap, (data.Dimtih ? 0.0 : readable) - readable);
            var above = new GeoVector2(-reads.Y, reads.X);

            // The text's point on the line, about which the picture stands the words as DIMTAD says (above it for 1), but under
            // DIMTMOVE 2, where the point is the words' middle itself.
            double lift = data.Dimtmove == 2 ? gap + tall / 2.0 : 0.0;
            dimension.TextPosition = P3(end + inward * inset + above * lift);
        }

        /// <summary>The angle words along a direction are turned to, so that they read from the left or from below, as AutoCAD turns them.</summary>
        private static double Readable(double angle)
        {
            double readable = Math.Atan2(Math.Sin(angle), Math.Cos(angle));

            if (readable > Math.PI / 2.0 + 1E-9 || readable <= -Math.PI / 2.0 + 1E-9)
            {
                readable += Math.PI;
            }

            return readable;
        }

        /// <summary>
        /// Works out for DIMSPACE how far each dimension's line is to move, by handle, into <paramref name="moves"/>; the
        /// number that are not parallel to the base (or, angular, not about its vertex).
        /// </summary>
        private static int Spaced(CadCommandContext c, DxfDimension baseDimension, IReadOnlyList<string> handles, double spacing, SortedDictionary<string, double> moves)
        {
            Tolerance tolerance = c.Session.Tolerance;
            bool angular = !(baseDimension is DxfRotatedDimension || baseDimension is DxfAlignedDimension);
            GeoPoint2 vertex = default;
            double baseRadius = 0.0;
            double baseAngle = 0.0;
            GeoPoint2 baseLine = default;

            if (angular)
            {
                if (!AngularRays(baseDimension, out vertex, out _, out _, out baseRadius))
                {
                    return handles.Count;
                }
            }
            else
            {
                Chain chain = ChainOf(baseDimension, null, false);
                baseAngle = chain.Angle;
                baseLine = chain.Line;
            }

            var square = new GeoVector2(-Math.Sin(baseAngle), Math.Cos(baseAngle));
            var sides = new[] { new List<KeyValuePair<double, string>>(), new List<KeyValuePair<double, string>>() };
            int apart = 0;

            foreach (string handle in handles)
            {
                if (string.Equals(handle, baseDimension.Handle, StringComparison.OrdinalIgnoreCase) || !(c.Document.FindEntity(handle) is DxfDimension other))
                {
                    continue;
                }

                double offset;

                if (angular)
                {
                    if (!AngularRays(other, out GeoPoint2 otherVertex, out _, out _, out double radius) || (otherVertex - vertex).Length > tolerance.EqualPoint)
                    {
                        apart++;
                        continue;
                    }

                    offset = radius - baseRadius;
                }
                else
                {
                    Chain chain = other is DxfRotatedDimension || other is DxfAlignedDimension ? ChainOf(other, null, false) : null;
                    double turn = chain == null ? double.NaN : Math.Abs(Math.Sin(chain.Angle - baseAngle));

                    if (chain == null || !(turn <= tolerance.EqualVector))
                    {
                        apart++;
                        continue;
                    }

                    offset = (chain.Line - baseLine).X * square.X + (chain.Line - baseLine).Y * square.Y;
                }

                sides[offset < 0.0 ? 1 : 0].Add(new KeyValuePair<double, string>(Math.Abs(offset), handle));
            }

            for (int side = 0; side < 2; side++)
            {
                sides[side].Sort((a, b) =>
                {
                    int byDistance = a.Key.CompareTo(b.Key);
                    return byDistance != 0 ? byDistance : string.CompareOrdinal(a.Value, b.Value);
                });

                for (int k = 0; k < sides[side].Count; k++)
                {
                    double wanted = spacing * (k + 1);
                    moves[sides[side][k].Value] = (wanted - sides[side][k].Key) * (side == 0 ? 1.0 : -1.0);
                }
            }

            return apart;
        }

        /// <summary>
        /// Moves a dimension's line by a distance square to the base's (away from it where positive on its first side), or an
        /// angular one's arc out by it; words it was given a place for move with the line.
        /// </summary>
        private static void ShiftLine(DxfDimension dimension, string baseHandle, double by, CadDocument document)
        {
            var baseDimension = (DxfDimension)document.FindEntity(baseHandle);
            GeoVector2 shift;

            if (dimension is DxfRotatedDimension || dimension is DxfAlignedDimension)
            {
                double angle = ChainOf(baseDimension, null, false).Angle;
                shift = new GeoVector2(-Math.Sin(angle), Math.Cos(angle)) * by;

                if (dimension is DxfRotatedDimension rotated)
                {
                    rotated.DimLinePoint = P3(P2(rotated.DimLinePoint) + shift);
                }
                else
                {
                    var aligned = (DxfAlignedDimension)dimension;
                    aligned.DimLinePoint = P3(P2(aligned.DimLinePoint) + shift);
                }
            }
            else
            {
                AngularRays(dimension, out GeoPoint2 vertex, out _, out _, out double radius);
                GeoPoint2 arc = dimension is DxfPoint3AngularDimension three ? P2(three.ArcPoint) : P2(((DxfLineAngularDimension2)dimension).ArcPoint);
                GeoVector2 way = (arc - vertex) / (arc - vertex).Length;
                GeoPoint2 moved = vertex + way * (radius + by);
                shift = moved - arc;

                if (dimension is DxfPoint3AngularDimension angular)
                {
                    angular.ArcPoint = P3(moved);
                }
                else
                {
                    ((DxfLineAngularDimension2)dimension).ArcPoint = P3(moved);
                }
            }

            if (!dimension.UsingDefaultTextPosition)
            {
                dimension.TextPosition = P3(P2(dimension.TextPosition) + shift);
            }
        }

        /// <summary>
        /// The objects that break a dimension, sorted by handle: those picked, or every line, polyline, circle and arc of the
        /// space but the dimension itself.
        /// </summary>
        private static List<DxfEntity> Cutters(CadDocument document, CadSpace space, DxfDimension dimension, IReadOnlyList<string> picked)
        {
            var cutters = new List<DxfEntity>();
            IEnumerable<DxfEntity> from = picked == null ? space.Block.Cast<DxfEntity>() : picked.Select(document.FindEntity);

            foreach (DxfEntity entity in from)
            {
                if (entity != null && !ReferenceEquals(entity, dimension) && !entity.IsErased && (entity is DxfLine || entity is DxfPolyline || entity is DxfCircle || entity is DxfArc))
                {
                    cutters.Add(entity);
                }
            }

            cutters.Sort((a, b) => string.CompareOrdinal(a.Handle, b.Handle));
            return cutters;
        }

        /// <summary>
        /// Breaks the lines of a dimension's picture (its extension lines and dimension line, not its points of definition): a
        /// gap of the break size about each place an object crosses one, or between where two points fall on the line nearest
        /// them. Gives the objects that cross a line, in the order given; none for a break between two points.
        /// </summary>
        private static List<DxfEntity> BreakAt(DxfDimension dimension, List<DxfEntity> cutters, GeoPoint2[] manual)
        {
            DxfBlockTableRecord picture = dimension.DimBlock;
            var crossed = new List<DxfEntity>();

            if (picture == null)
            {
                return crossed;
            }

            GeoVector2 shift = new GeoVector2(dimension.DimBlockPosition.X, dimension.DimBlockPosition.Y);
            var lines = picture.OfType<DxfLine>().Where(l => !l.IsErased && !string.Equals(l.Layer, "Defpoints", StringComparison.OrdinalIgnoreCase)).ToList();
            double size = BreakSize(dimension);

            if (manual != null)
            {
                var middle = new GeoPoint2((manual[0].X + manual[1].X) / 2.0, (manual[0].Y + manual[1].Y) / 2.0);
                DxfLine nearest = null;
                double best = double.MaxValue;

                foreach (DxfLine line in lines)
                {
                    double distance = SegmentDistance(P2(line.StartPoint) + shift, P2(line.EndPoint) + shift, middle);

                    if (distance < best)
                    {
                        best = distance;
                        nearest = line;
                    }
                }

                if (nearest != null)
                {
                    GeoPoint2 a = P2(nearest.StartPoint) + shift;
                    GeoPoint2 b = P2(nearest.EndPoint) + shift;
                    GeoVector2 way = (b - a) / (b - a).Length;
                    double t1 = (manual[0] - a).X * way.X + (manual[0] - a).Y * way.Y;
                    double t2 = (manual[1] - a).X * way.X + (manual[1] - a).Y * way.Y;
                    Cut(picture, nearest, new List<KeyValuePair<double, double>> { new KeyValuePair<double, double>(Math.Min(t1, t2), Math.Max(t1, t2)) });
                }

                return crossed;
            }

            foreach (DxfLine line in lines)
            {
                GeoPoint2 a = P2(line.StartPoint) + shift;
                GeoPoint2 b = P2(line.EndPoint) + shift;
                var gaps = new List<KeyValuePair<double, double>>();

                foreach (DxfEntity cutter in cutters)
                {
                    List<double> at = Crossings(a, b, new List<DxfEntity> { cutter });

                    if (at.Count > 0 && !crossed.Contains(cutter))
                    {
                        crossed.Add(cutter);
                    }

                    foreach (double t in at)
                    {
                        gaps.Add(new KeyValuePair<double, double>(t - size / 2.0, t + size / 2.0));
                    }
                }

                if (gaps.Count > 0)
                {
                    gaps.Sort((x, y) => x.Key.CompareTo(y.Key));
                    Cut(picture, line, gaps);
                }
            }

            crossed.Sort((x, y) => cutters.IndexOf(x).CompareTo(cutters.IndexOf(y)));
            return crossed;
        }

        /// <summary>The break size of a dimension: DIMBREAK from its style's extended data (ACAD_DSTYLE_DIMBREAK), else 1.5 arrowheads, by DIMSCALE.</summary>
        private static double BreakSize(DxfDimension dimension)
        {
            DxfDimStyleTableRecord data = dimension.GetDimstyleData();
            double scale = data.Dimscale > 0.0 ? data.Dimscale : 1.0;
            double size = 1.5 * data.Dimasz;

            try
            {
                IReadOnlyList<DxfTypedValue> stored = dimension.DimensionStyle?.GetXDataForApplication("ACAD_DSTYLE_DIMBREAK");

                for (int i = 0; stored != null && i + 1 < stored.Count; i++)
                {
                    if (stored[i].TypeCode == 1070 && stored[i].Value.Trim() == "391" && stored[i + 1].TypeCode == 1040
                        && double.TryParse(stored[i + 1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && value >= 0.0)
                    {
                        size = value;
                    }
                }
            }
            catch (Exception)
            {
                size = 1.5 * data.Dimasz;
            }

            return size * scale;
        }

        /// <summary>Where along a segment, from its start, the objects cross it, in order.</summary>
        private static List<double> Crossings(GeoPoint2 a, GeoPoint2 b, List<DxfEntity> cutters)
        {
            var found = new List<double>();
            double length = (b - a).Length;

            if (length <= 1E-12)
            {
                return found;
            }

            GeoVector2 way = (b - a) / length;

            foreach (DxfEntity cutter in cutters)
            {
                switch (cutter)
                {
                    case DxfLine line:
                        SegmentCrossing(a, way, length, P2(line.StartPoint), P2(line.EndPoint), found);
                        break;
                    case DxfPolyline polyline:
                        int n = polyline.NumberOfVertices;

                        for (int i = 0; i + 1 < n || (polyline.Closed && i < n && n > 2); i++)
                        {
                            SegmentCrossing(a, way, length, polyline.GetPoint2dAt(i), polyline.GetPoint2dAt((i + 1) % n), found);
                        }

                        break;
                    case DxfCircle circle:
                        CircleCrossing(a, way, length, P2(circle.Center), circle.Radius, 0.0, 2.0 * Math.PI, found);
                        break;
                    case DxfArc arc:
                        CircleCrossing(a, way, length, P2(arc.Center), arc.Radius, arc.StartAngle, CadGrips.Sweep(arc.StartAngle, arc.EndAngle), found);
                        break;
                }
            }

            found.Sort();
            return found;
        }

        private static void SegmentCrossing(GeoPoint2 a, GeoVector2 way, double length, GeoPoint2 p, GeoPoint2 q, List<double> found)
        {
            GeoVector2 other = q - p;
            double across = Cross(way, other);

            if (Math.Abs(across) <= 1E-12 * Math.Max(1.0, other.Length))
            {
                return;
            }

            double t = Cross(p - a, other) / across;
            double u = Cross(p - a, way) / across;

            if (t >= 0.0 && t <= length && u >= 0.0 && u <= 1.0)
            {
                found.Add(t);
            }
        }

        private static void CircleCrossing(GeoPoint2 a, GeoVector2 way, double length, GeoPoint2 center, double radius, double start, double sweep, List<double> found)
        {
            GeoVector2 from = a - center;
            double b = from.X * way.X + from.Y * way.Y;
            double c = from.X * from.X + from.Y * from.Y - radius * radius;
            double d = b * b - c;

            if (d < 0.0)
            {
                return;
            }

            double root = Math.Sqrt(d);

            foreach (double t in d == 0.0 ? new[] { -b } : new[] { -b - root, -b + root })
            {
                if (t < 0.0 || t > length)
                {
                    continue;
                }

                GeoPoint2 at = a + way * t;

                if (CadGrips.Sweep(start, Math.Atan2(at.Y - center.Y, at.X - center.X)) <= sweep)
                {
                    found.Add(t);
                }
            }
        }

        private static double SegmentDistance(GeoPoint2 a, GeoPoint2 b, GeoPoint2 p)
        {
            GeoVector2 way = b - a;
            double length2 = way.X * way.X + way.Y * way.Y;
            double t = length2 <= 0.0 ? 0.0 : Math.Max(0.0, Math.Min(1.0, ((p - a).X * way.X + (p - a).Y * way.Y) / length2));
            return (p - (a + way * t)).Length;
        }

        /// <summary>Replaces a line of a picture by the pieces of it the gaps (distances from its start) leave, each with its look.</summary>
        private static void Cut(DxfBlockTableRecord picture, DxfLine line, List<KeyValuePair<double, double>> gaps)
        {
            double length = line.StartPoint.DistanceTo(line.EndPoint);
            gaps.Sort((x, y) => x.Key != y.Key ? x.Key.CompareTo(y.Key) : x.Value.CompareTo(y.Value));
            var pieces = new List<KeyValuePair<double, double>>();
            double from = 0.0;

            foreach (KeyValuePair<double, double> gap in gaps)
            {
                if (gap.Key > from)
                {
                    pieces.Add(new KeyValuePair<double, double>(from, Math.Min(gap.Key, length)));
                }

                from = Math.Max(from, gap.Value);
            }

            if (from < length)
            {
                pieces.Add(new KeyValuePair<double, double>(from, length));
            }

            GeoVector3 way = line.StartPoint.GetVectorTo(line.EndPoint).Multiply(1.0 / length);

            foreach (KeyValuePair<double, double> piece in pieces)
            {
                if (piece.Value - piece.Key <= 1E-9)
                {
                    continue;
                }

                var part = new DxfLine(line.StartPoint.Add(way.Multiply(piece.Key)), line.StartPoint.Add(way.Multiply(piece.Value)))
                {
                    Layer = line.Layer,
                    Color = line.Color,
                    LineWeight = line.LineWeight,
                };
                picture.AppendEntity(part);
            }

            line.Erase();
        }
    }
}
