using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    internal static partial class CadCenterMarks
    {
        /// <summary>A straight piece of the drawing a center line is drawn between: a line, or one segment of a polyline.</summary>
        internal readonly struct Segment
        {
            internal Segment(DxfEntity entity, GeoPoint3 a, GeoPoint3 b, int marker)
            {
                Entity = entity;
                A = a;
                B = b;
                Marker = marker;
            }

            internal DxfEntity Entity { get; }

            internal GeoPoint3 A { get; }

            internal GeoPoint3 B { get; }

            /// <summary>The segment of a polyline, 0 for the first; -1 for a line.</summary>
            internal int Marker { get; }
        }

        /// <summary>The straight piece an entity gives where it is picked at a point: the line, or the straight segment of a polyline nearest the point; false for any other entity.</summary>
        internal static bool TryGetSegment(DxfEntity entity, GeoPoint2 picked, out Segment segment)
        {
            segment = default;

            if (entity is DxfLine line)
            {
                segment = new Segment(entity, line.StartPoint, line.EndPoint, -1);
                return true;
            }

            if (!(entity is DxfPolyline polyline))
            {
                return false;
            }

            int count = polyline.NumberOfVertices;
            int segments = polyline.Closed ? count : count - 1;

            if (segments < 1)
            {
                return false;
            }

            GeoPoint3 near = polyline.GetClosestPointTo(new GeoPoint3(picked.X, picked.Y, polyline.Elevation), false);
            int marker = Math.Max(0, Math.Min(segments - 1, (int)Math.Floor(polyline.GetParameterAtPoint(near))));

            if (polyline.GetBulgeAt(marker) != 0.0)
            {
                return false;
            }

            GeoPoint2 a = polyline.GetPoint2dAt(marker);
            GeoPoint2 b = polyline.GetPoint2dAt((marker + 1) % count);
            segment = new Segment(
                entity,
                new GeoPoint3(a.X, a.Y, polyline.Elevation),
                new GeoPoint3(b.X, b.Y, polyline.Elevation),
                marker
            );
            return true;
        }

        /// <summary>Whether two segments are one: the same line, or the same segment of the same polyline.</summary>
        internal static bool Same(Segment a, Segment b) =>
            string.Equals(a.Entity.Handle, b.Entity.Handle, StringComparison.OrdinalIgnoreCase) && a.Marker == b.Marker;

        /// <summary>The layer a mark or a line goes on: the current one for <c>.</c>, else the layer of the name, made where the drawing has none, as AutoCAD 2021 made the one CENTERLAYER named.</summary>
        private static string LayerFor(DxfDatabase database, string setting)
        {
            if (setting == ".")
            {
                return database.Clayer;
            }

            foreach (DxfLayerTableRecord layer in database.LayerTable)
            {
                if (string.Equals(layer.Name, setting, StringComparison.OrdinalIgnoreCase))
                {
                    return layer.Name;
                }
            }

            try
            {
                database.LayerTable.Add(new DxfLayerTableRecord { Name = setting });
                return setting;
            }
            catch (ArgumentException)
            {
                return database.Clayer;
            }
        }

        /// <summary>The name of the linetype in the drawing, loaded from the presenter's own set where the drawing has none of it; Continuous where it is in neither.</summary>
        private static string LinetypeFor(DxfDatabase database, string wanted)
        {
            foreach (DxfLinetypeTableRecord record in database.LinetypeTable)
            {
                if (string.Equals(record.Name, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return record.Name;
                }
            }

            CadLinetypeDefinition own = CadLinetypeFile.BuiltIn.FirstOrDefault(l =>
                string.Equals(l.Name, wanted, StringComparison.OrdinalIgnoreCase)
            );

            if (own == null)
            {
                return "Continuous";
            }

            CadLinetypes.Add(database, own);
            return own.Name;
        }

        /// <summary>The x axis AutoCAD gives an object of a normal (the arbitrary axis algorithm).</summary>
        private static GeoVector3 AxisOf(GeoVector3 normal)
        {
            var z = new GeoVector3(0.0, 0.0, 1.0);
            var y = new GeoVector3(0.0, 1.0, 0.0);
            GeoVector3 axis =
                Math.Abs(normal.X) < 1.0 / 64.0 && Math.Abs(normal.Y) < 1.0 / 64.0
                    ? y.CrossProduct(normal)
                    : z.CrossProduct(normal);
            return axis.Length <= 0.0 ? new GeoVector3(1.0, 0.0, 0.0) : axis / axis.Length;
        }

        /// <summary>
        /// Draws the block and the INSERT of a picture: the block anonymous, its cross ByBlock and Continuous and the other lines
        /// ByBlock in the linetype given, each on the layer; the INSERT of the linetype too, with the colour, lineweight and
        /// transparency new objects take. The lines of a mark that has no extensions are hidden (group 60 of 1), as AutoCAD 2021
        /// writes CENTERMARKEXE 0.
        /// </summary>
        private static DxfBlockReference Draw(
            CadCommandContext c,
            CadEditScope s,
            CadSpace space,
            GeoPoint3 position,
            GeoVector3 normal,
            IReadOnlyList<Stroke> strokes,
            Look look,
            string layer,
            string linetype,
            bool hideExtensions
        )
        {
            DxfDatabase database = c.Database;
            var block = new DxfBlockTableRecord { Name = "*U", MadeFlags = 1 };
            database.BlockTable.AddAnonymous(block);

            foreach (Stroke stroke in strokes)
            {
                var line = new DxfLine(stroke.A, stroke.B)
                {
                    Layer = layer,
                    Color = DxfColor.ByBlock,
                    Linetype = stroke.Cross ? "Continuous" : linetype,
                    Transparency = DxfTransparency.ByBlock,
                };

                if (database.Celweight != DxfLineWeight.ByLayer)
                {
                    line.LineWeight = database.Celweight;
                }

                if (look.LtScale != 1.0)
                {
                    line.LinetypeScale = look.LtScale;
                }

                if (!stroke.Cross && hideExtensions)
                {
                    line.Visible = false;
                }

                block.AppendEntity(line);
            }

            var insert = new DxfBlockReference(position, block);
            CadCommands.Current(c, insert);
            insert.Layer = layer;
            insert.Linetype = linetype;
            insert.Normal = normal;

            if (look.LtScale != 1.0)
            {
                insert.LinetypeScale = look.LtScale;
            }

            s.Append(space, insert);
            return insert;
        }

        /// <summary>Makes the center mark of a circle or an arc, with its associative action where the drawing can hold one; the INSERT.</summary>
        internal static DxfBlockReference MakeMark(
            CadCommandContext c,
            CadEditScope s,
            CadSpace space,
            DxfEntity target
        )
        {
            DxfDatabase database = c.Database;
            Look look = LookOf(database);
            GeoPoint3 centre;
            GeoVector3 normal;
            double radius;
            double start = 0.0;
            double end = 2.0 * Math.PI;

            if (target is DxfArc arc)
            {
                centre = arc.Center;
                normal = arc.Normal;
                radius = arc.Radius;
                start = arc.StartAngle;
                end = arc.EndAngle;
            }
            else
            {
                var circle = (DxfCircle)target;
                centre = circle.Center;
                normal = circle.Normal;
                radius = circle.Radius;
            }

            string layer = LayerFor(database, look.Layer);
            string linetype = LinetypeFor(database, look.Linetype);
            DxfBlockReference insert = Draw(
                c,
                s,
                space,
                centre,
                normal,
                MarkStrokes(look.Size, look.Gap, look.Exe, radius),
                look,
                layer,
                linetype,
                !look.Extensions
            );

            if (!CanTie(database))
            {
                return insert;
            }

            var values = new List<Value>();

            if (!look.Extensions)
            {
                foreach (string line in new[] { "eLeftLine", "eRightLine", "eBottomLine", "eTopLine" })
                {
                    values.Add(Real(line + "_visibility", 0.0));
                }
            }

            foreach (string way in new[] { "HorizontalStart", "HorizontalEnd", "VerticalStart", "VerticalEnd" })
            {
                values.Add(Real(way + "Extension", Unlimited));
            }

            foreach (string way in new[] { "HorizontalStart", "HorizontalEnd", "VerticalStart", "VerticalEnd" })
            {
                values.Add(Real(way + "Overshoot", look.Exe));
            }

            values.Add(Text("CrossHairLengthExpression", look.Size));
            values.Add(Text("CrossHairGapExpression", look.Gap));
            values.Add(new Value { Name = "CenterDepId", Kind = 'h' });
            values.Add(Real("CachedCircleRadius", radius));
            AddSubentValues(values, database, linetype, look);

            var input = new Input { Entity = target, EdgeType = 11 };
            GeoVector3 axis = AxisOf(normal);
            input.Subentity.G(1, "AcDbAssocSingleEdgePersSubentId");
            input.Subentity.G(290, 0);
            input.Edge.P(10, centre.X, centre.Y, centre.Z);
            input.Edge.P(10, normal.X, normal.Y, normal.Z);
            input.Edge.P(10, axis.X, axis.Y, axis.Z);
            input.Edge.G(40, radius);
            input.Edge.G(40, start);
            input.Edge.G(40, end);
            input.Edge.G(40, 0.0);
            Tie(database, space.Block, MarkBody, new[] { input }, insert, values);
            return insert;
        }

        /// <summary>The values every action ends with: the linetype, lineweight and linetype scale of its lines, and that it is associated.</summary>
        private static void AddSubentValues(List<Value> values, DxfDatabase database, string linetype, Look look)
        {
            values.Add(Text("SubentLineType", linetype));
            values.Add(Real("SubentLineWeight", (double)(int)database.Celweight));
            values.Add(Real("SubentLinetypeScale", look.LtScale));
            values.Add(Real("CachedAssociateStatus", 1.0));
        }

        /// <summary>
        /// Makes the center line between two segments; the INSERT, or null where the two have no way between them (one of no
        /// length, or running across the other).
        /// </summary>
        internal static DxfBlockReference MakeLine(
            CadCommandContext c,
            CadEditScope s,
            CadSpace space,
            Segment one,
            Segment two
        )
        {
            DxfDatabase database = c.Database;
            Look look = LookOf(database);
            var a0 = new GeoPoint2(one.A.X, one.A.Y);
            var a1 = new GeoPoint2(one.B.X, one.B.Y);
            var b0 = new GeoPoint2(two.A.X, two.A.Y);
            var b1 = new GeoPoint2(two.B.X, two.B.Y);

            if (
                !LineBetween(
                    a0,
                    a1,
                    b0,
                    b1,
                    out GeoPoint2 centre,
                    out GeoVector2 direction,
                    out double first,
                    out double last
                )
            )
            {
                return null;
            }

            string layer = LayerFor(database, look.Layer);
            string linetype = LinetypeFor(database, look.Linetype);
            double overshoot = Math.Max(0.0, look.Exe);
            double out0 = first <= last ? -overshoot : overshoot;
            GeoPoint3 p0 = new GeoPoint3(direction.X * (first + out0), direction.Y * (first + out0), 0.0);
            GeoPoint3 p1 = new GeoPoint3(direction.X * (last - out0), direction.Y * (last - out0), 0.0);
            DxfBlockReference insert = Draw(
                c,
                s,
                space,
                new GeoPoint3(centre.X, centre.Y, one.A.Z),
                new GeoVector3(0.0, 0.0, 1.0),
                new[] { new Stroke(p0, p1, false) },
                look,
                layer,
                linetype,
                false
            );

            if (!CanTie(database))
            {
                return insert;
            }

            var values = new List<Value>
            {
                Real("StartExtLength", Unlimited),
                Real("EndExtLength", Unlimited),
                Real("StartOvershoot", look.Exe),
                Real("EndOvershoot", look.Exe),
                new Value { Name = "EdgeRefDirectionEnding", Kind = 'e' },
                new Value { Name = "CenterDepId", Kind = 'h' },
                Real("CachedCenterLineLength", Math.Abs(last - first)),
                Real("CachedCenterLineDirectionX", direction.X),
                Real("CachedCenterLineDirectionY", direction.Y),
                Real("CachedCenterLineDirectionZ", 0.0),
            };
            AddSubentValues(values, database, linetype, look);
            Tie(database, space.Block, LineBody, new[] { LineInput(one), LineInput(two) }, insert, values);
            return insert;
        }

        /// <summary>The input of a segment: how its dependency names it, and its start and its vector as the edge.</summary>
        private static Input LineInput(Segment segment)
        {
            var input = new Input { Entity = segment.Entity, EdgeType = 23 };

            if (segment.Marker < 0)
            {
                input.Subentity.G(1, "AcDbAssocSingleEdgePersSubentId");
                input.Subentity.G(290, 0);
            }
            else
            {
                input.Subentity.G(1, "AcDbAssocEdgePersSubentId");
                input.Subentity.G(90, 0);
                input.Subentity.G(90, segment.Marker + 1);
                input.Subentity.G(90, segment.Marker + 2);
                input.Subentity.G(290, 0);
            }

            input.Edge.P(10, segment.A.X, segment.A.Y, segment.A.Z);
            input.Edge.P(10, segment.B.X - segment.A.X, segment.B.Y - segment.A.Y, segment.B.Z - segment.A.Z);
            return input;
        }
    }
}
