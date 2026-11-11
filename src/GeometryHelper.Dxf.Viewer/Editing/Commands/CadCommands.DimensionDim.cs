using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>DIM, AutoCAD's one dimension command since 2016: the kind of dimension follows what is picked.</summary>
    /// <remarks>
    /// A click on a line dimensions the line, a click on another line then the angle of the two; a click on a circle its
    /// diameter, on an arc its radius; a click on nothing (or a point typed or snapped) starts a linear dimension from that point. The
    /// options run the commands of their names: Angular DIMANGULAR, Baseline DIMBASELINE, Continue DIMCONTINUE, Ordinate
    /// DIMORDINATE; aLign puts dimensions on the line of a base one, Distribute spaces them evenly or an offset apart, LAyer
    /// sets the layer new dimensions go on (AutoCAD's DIMLAYER) and Undo takes back the last dimension made. DIM asks again
    /// after each one until Enter; the whole run is one undo step.
    /// </remarks>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> Dim(CadCommandContext c)
        {
            DxfDimStyleTableRecord style = DimStyle(c);

            if (style == null)
            {
                c.Write("The drawing has no dimension style to draw in.");
                yield break;
            }

            var made = new List<string>();
            string[] keywords = { "Angular", "Baseline", "Continue", "Ordinate", "aLign", "Distribute", "LAyer", "Undo" };

            while (true)
            {
                GeoPoint2 lastBefore = c.Session.LastPoint;
                yield return CadPrompt.ForPoint("Select objects or specify first extension line origin", null, keywords).SnappingFrom(lastBefore);
                IEnumerable<CadPrompt> step;

                if (c.Input.IsPoint)
                {
                    GeoPoint2 at = c.Input.Point;
                    // A point an object snap gave is an origin, as AutoCAD's DIM takes it, not a pick of the object under it.
                    bool snapped = c.Input.Snap != null && c.Input.Snap.Kind != CadSnapKind.None;
                    DxfEntity picked = c.Input.Text == null && !snapped ? Dimensionable(c, at) : null;
                    step = picked == null ? DimLine(c, false, at, lastBefore) : DimObject(c, picked);
                }
                else if (c.Input.Is("Angular"))
                {
                    step = DimAngular(c);
                }
                else if (c.Input.Is("Baseline") || c.Input.Is("Continue"))
                {
                    step = DimChain(c, c.Input.Is("Baseline"));
                }
                else if (c.Input.Is("Ordinate"))
                {
                    step = DimOrdinate(c);
                }
                else if (c.Input.Is("aLign") || c.Input.Is("Distribute"))
                {
                    step = DimAlign(c, c.Input.Is("Distribute"));
                }
                else if (c.Input.Is("LAyer"))
                {
                    step = DimLayerOption(c);
                }
                else if (c.Input.Is("Undo"))
                {
                    if (made.Count == 0)
                    {
                        c.Write("Nothing to undo.");
                    }
                    else
                    {
                        EraseAll(c, new[] { made[made.Count - 1] });
                        made.RemoveAt(made.Count - 1);
                    }

                    continue;
                }
                else
                {
                    yield break;
                }

                var before = new HashSet<string>(Dimensions(c), StringComparer.OrdinalIgnoreCase);

                foreach (CadPrompt prompt in step)
                {
                    yield return prompt;
                }

                // The dimensions the step made, in the order the space holds them, that Undo takes back one at a time.
                foreach (string handle in Dimensions(c))
                {
                    if (!before.Contains(handle))
                    {
                        made.Add(handle);
                    }
                }
            }
        }

        /// <summary>The handles of the dimensions of the current space, in the order it holds them.</summary>
        private static List<string> Dimensions(CadCommandContext c)
            => c.Space.Block.OfType<DxfDimension>().Where(d => !d.IsErased).Select(d => d.Handle).ToList();

        /// <summary>The line, circle or arc under a point picked, as DIM highlights one under the cursor; null for none.</summary>
        private static DxfEntity Dimensionable(CadCommandContext c, GeoPoint2 at)
        {
            foreach (string handle in c.Document.GetPickIndex(c.Space).Pick(at.X, at.Y, c.Session.PickAperture))
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity is DxfLine || entity is DxfCircle || (entity is DxfArc arc && arc.Normal.Z > 0.0))
                {
                    return entity;
                }

                return null;
            }

            return null;
        }

        /// <summary>
        /// DIM on an object picked: a line, then where its dimension goes (linear, or aligned where the point lies beside a
        /// slanting line) or a second line for the angle; a circle's diameter (Radius for its radius); an arc's radius
        /// (Diameter for its diameter, Angular for the angle it spans). Mtext and Text give the words, Text angle turns them, Undo
        /// goes back to DIM's prompt.
        /// </summary>
        private static IEnumerable<CadPrompt> DimObject(CadCommandContext c, DxfEntity picked)
        {
            DxfDimStyleTableRecord style = DimStyle(c);
            string text = null;
            double? textAngle = null;
            bool diameter = picked is DxfCircle;

            while (true)
            {
                yield return picked is DxfLine
                    ? CadPrompt.ForPoint("Specify dimension line location or second line for angle", null, new[] { "Mtext", "Text", "text aNgle", "Undo" })
                    : CadPrompt.ForPoint(diameter ? "Specify diameter dimension location" : "Specify radius dimension location", Center(picked), new[] { diameter ? "Radius" : "Diameter", "Angular", "Mtext", "Text", "text aNgle", "Undo" });

                if (c.Input.Is("Mtext") || c.Input.Is("Text"))
                {
                    yield return CadPrompt.ForString("Enter dimension text", "<>");

                    if (c.Input.Kind == CadInputKind.Text)
                    {
                        text = c.Input.Text;
                    }

                    continue;
                }

                if (c.Input.Is("text aNgle"))
                {
                    yield return CadPrompt.ForAngle("Specify angle of dimension text");

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        textAngle = TextAngle(c, c.Input.Number);
                    }

                    continue;
                }

                if (c.Input.Is("Radius") || c.Input.Is("Diameter"))
                {
                    diameter = c.Input.Is("Diameter");
                    continue;
                }

                if (c.Input.Is("Angular"))
                {
                    if (picked is DxfCircle)
                    {
                        c.Write("The angle of a circle is not made: pick an arc or two lines.");
                        continue;
                    }

                    foreach (CadPrompt prompt in DimAngularFrom(c, picked, null))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                break;
            }

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 at = c.Input.Point;

            if (picked is DxfLine line)
            {
                DxfLine second = c.Input.Text == null ? Dimensionable(c, at) as DxfLine : null;

                if (second != null && !ReferenceEquals(second, line))
                {
                    foreach (CadPrompt prompt in DimAngularFrom(c, line, second))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                GeoPoint2 p1 = P2(line.StartPoint);
                GeoPoint2 p2 = P2(line.EndPoint);

                if ((p2 - p1).Length <= c.Session.Tolerance.EqualPoint)
                {
                    c.Write("*Invalid* The line has no length.");
                    yield break;
                }

                CadDimAssociation.Tie(c, MakeLinear(c, style, Beside(c, p1, p2, at), p1, p2, at, text, null, textAngle), CadDimAssociation.LineEnds(line));
                yield break;
            }

            GeoPoint2 center = Center(picked);
            double radius = picked is DxfCircle circle ? circle.Radius : ((DxfArc)picked).Radius;
            string handle = MakeRadial(c, style, center, radius, diameter, at, text, textAngle);
            CadDimAssociation.Tie(c, handle, CadDimAssociation.CenterOf(picked));

            if (handle != null)
            {
                c.Write("Dimension text = " + ((DxfDimension)c.Document.FindEntity(handle)).DimBlock.OfType<DxfMText>().Select(m => m.Contents).FirstOrDefault());
            }
        }

        private static GeoPoint2 Center(DxfEntity entity) => entity is DxfCircle circle ? P2(circle.Center) : P2(((DxfArc)entity).Center);

        /// <summary>
        /// Whether DIM measures a line along itself: where it slants, in the UCS, and the point lies beside it, square to some
        /// point of it; a level or upright line, or a point past its ends, is measured level or upright, as DIMLINEAR measures.
        /// </summary>
        private static bool Beside(CadCommandContext c, GeoPoint2 p1, GeoPoint2 p2, GeoPoint2 at)
        {
            GeoVector2 way = c.Session.Ucs.ToUcs(p2 - p1);
            double length = way.Length;

            if (Math.Abs(way.X) <= c.Session.Tolerance.EqualVector * length || Math.Abs(way.Y) <= c.Session.Tolerance.EqualVector * length)
            {
                return false;
            }

            double t = ((at - p1).X * (p2 - p1).X + (at - p1).Y * (p2 - p1).Y) / (length * length);
            return t >= 0.0 && t <= 1.0;
        }

        /// <summary>
        /// DIM's aLign and Distribute: a base dimension, then the dimensions to put on its line (aLign), or to space evenly out to
        /// the farthest of them (Equal) or an offset apart (Offset), as DIMSPACE spaces them.
        /// </summary>
        private static IEnumerable<CadPrompt> DimAlign(CadCommandContext c, bool distribute)
        {
            bool equal = true;
            double offset = 0.0;

            if (distribute)
            {
                yield return CadPrompt.ForKeyword("Specify method of distribution", new[] { "Equal", "Offset" }, "Equal");

                if (!c.Input.Is("Equal") && !c.Input.Is("Offset"))
                {
                    yield break;
                }

                equal = c.Input.Is("Equal");
            }

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

            yield return CadPrompt.ForSelection(distribute ? "Select dimensions to distribute" : "Select dimensions to align");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            IReadOnlyList<string> handles = c.Input.Handles;

            if (distribute && !equal)
            {
                yield return CadPrompt.ForDistance("Specify offset distance");

                if (c.Input.Kind != CadInputKind.Number || c.Input.Number < 0.0)
                {
                    yield break;
                }

                offset = c.Input.Number;
            }
            else if (distribute)
            {
                // Evenly out to the farthest: the farthest's distance from the base shared by as many as stand on its side.
                var lined = new SortedDictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                Spaced(c, baseDimension, handles, 0.0, lined);
                double farthest = lined.Count == 0 ? 0.0 : lined.Values.Max(v => Math.Abs(v));
                int above = lined.Values.Count(v => v < 0.0);
                int below = lined.Values.Count(v => v > 0.0);
                offset = farthest / Math.Max(1, Math.Max(above, below));
            }

            var moves = new SortedDictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            int apart = Spaced(c, baseDimension, handles, offset, moves);

            if (apart > 0)
            {
                c.Write(apart.ToString(CultureInfo.InvariantCulture) + (apart == 1 ? " dimension is" : " dimensions are") + " not parallel to the base dimension and stays where it is.");
            }

            List<DxfDimension> dimensions = Changeable(c, moves.Keys.ToList(), "Dimension line position", d => true);
            ChangeDimensions(c, dimensions, "Dimension line position", Values(offset), d => ShiftLine(d, baseDimension.Handle, moves[d.Handle], c.Document));
        }

        /// <summary>DIM's LAyer: the layer the dimensions made next go on, a layer of the drawing by its name, or Use current (".").</summary>
        private static IEnumerable<CadPrompt> DimLayerOption(CadCommandContext c)
        {
            string now = c.Session.DimLayer ?? "use current";
            yield return CadPrompt.ForKeywordOrText("Enter layer name", new[] { "Use current" }, now);

            if (c.Input.Is("Use current") || (c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim() == "."))
            {
                c.Session.DimLayer = null;
                c.Write("Dimensions go on the current layer.");
            }
            else if (c.Input.Kind == CadInputKind.Text)
            {
                string name = c.Input.Text.Trim();

                if (!c.Database.LayerTable.Has(name))
                {
                    c.Write("Layer \"" + name + "\" not found.");
                    yield break;
                }

                c.Session.DimLayer = c.Database.LayerTable[name].Name;
                c.Write("Dimensions go on layer " + c.Session.DimLayer + ".");
            }
        }
    }
}
