using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What dragging a grip does.</summary>
    public enum CadGripKind
    {
        /// <summary>An end of a line or an arc: moves that end.</summary>
        End,

        /// <summary>The middle of a line: moves the whole line. The middle of an arc: bends the arc through the point.</summary>
        Middle,

        /// <summary>The centre of a circle or an arc: moves it.</summary>
        Center,

        /// <summary>A quadrant of a circle: changes its radius.</summary>
        Quadrant,

        /// <summary>A vertex of a polyline: moves the vertex.</summary>
        Vertex,

        /// <summary>The middle of a polyline segment: moves a straight segment, bends an arc segment through the point.</summary>
        SegmentMiddle,

        /// <summary>The insertion point of a text, an MTEXT or a block reference: moves it.</summary>
        Insertion,

        /// <summary>
        /// A corner of a viewport's frame: resizes the frame, the corner across from it kept, the scale and the centre of the view
        /// kept. A corner of an MTEXT's box: takes its two edges of the box to the point, the width and the height of the MTEXT
        /// those of the box then.
        /// </summary>
        Corner,

        /// <summary>A point a dimension measures from (an extension line's origin, a chord point, a line point or the vertex of an angle, an ordinate's feature or leader end): moves that point.</summary>
        DefinitionPoint,

        /// <summary>An end of a dimension's line, or the arc of an angular one: moves the line, or the arc, through the point.</summary>
        DimensionLine,

        /// <summary>The words of a dimension: moves them, as DIMTEDIT does.</summary>
        Text,

        /// <summary>A custom grip of a dynamic block's point, polar or XY parameter, a square in the dynamic grip colour: dragged, sets its values.</summary>
        CustomPoint,

        /// <summary>A custom grip of a dynamic block's linear parameter, an arrow: dragged along the parameter, sets its distance.</summary>
        CustomLinear,

        /// <summary>A custom grip of a dynamic block's rotation parameter, a circle: dragged round its base point, sets its angle.</summary>
        CustomRotation,

        /// <summary>A custom grip of a dynamic block's flip parameter, a bent arrow across its line: clicked, flips the block.</summary>
        CustomFlip,

        /// <summary>A custom grip of a dynamic block's visibility or lookup parameter, a triangle pointing down: clicked, lists the values.</summary>
        CustomList,
    }

    /// <summary>One grip of an entity: where it is drawn and what dragging it does.</summary>
    public sealed class CadGrip
    {
        internal CadGrip(int index, CadGripKind kind, GeoPoint2 point)
            : this(index, kind, point, null)
        {
        }

        internal CadGrip(int index, CadGripKind kind, GeoPoint2 point, DxfDynamicBlockGrip custom)
        {
            Index = index;
            Kind = kind;
            Point = point;
            Custom = custom;
            Direction = custom == null ? new GeoVector2(0.0, 0.0) : new GeoVector2(custom.Direction.X, custom.Direction.Y);
        }

        /// <summary>
        /// Gets the custom grip of a dynamic block's parameter the grip stands for, what it drives and where its parameter
        /// measures from; null for a grip of any other kind.
        /// </summary>
        public DxfDynamicBlockGrip Custom { get; }

        /// <summary>
        /// Gets the way a custom grip's arrow points, in the plan: along a linear parameter, across a flip parameter's line; nought
        /// for a grip of no arrow.
        /// </summary>
        public GeoVector2 Direction { get; }

        /// <summary>Gets its place in the entity's list of grips, which <see cref="CadGrips.Drag"/> takes.</summary>
        public int Index { get; }

        /// <summary>Gets what dragging it does.</summary>
        public CadGripKind Kind { get; }

        /// <summary>Gets where it is drawn, in drawing units.</summary>
        public GeoPoint2 Point { get; }

        /// <inheritdoc/>
        public override string ToString() => Kind + " " + Index + " " + Point;
    }

    /// <summary>
    /// The grips of each kind of entity and what dragging each does, as AutoCAD's: a line's two ends and middle, a circle's
    /// centre and four quadrants, an arc's ends, middle and centre, a polyline's vertices and the middles of its segments, a
    /// text's and a block reference's insertion point, an MTEXT's location and the corners of its box, a viewport's four corners
    /// and middle, and a dimension's as AutoCAD 2021 gives them (see <see cref="Of"/>), its picture drawn anew after each drag.
    /// Entities of other kinds have none.
    /// </summary>
    public static partial class CadGrips
    {
        /// <summary>
        /// Gets the grips of an entity. A dimension's, as AutoCAD 2021's: a linear or aligned one's two extension line origins,
        /// the two ends of its dimension line, then its words; a radius's chord point and its words, a diameter's two chord
        /// points and its words; a three-point angle's vertex, its two line points, its arc and its words, an angle between two
        /// lines the two ends of each line, its arc and its words; an ordinate's feature and its leader's end. A dimension
        /// drawn in no plan, or with no picture of its own, has none. An MTEXT's, as AutoCAD 2021's: the four corners of its box
        /// (its defined width by its defined height, or with none the height of its lines) from the top left, the top right, the
        /// bottom left to the bottom right, the corner its location is at the grip of its insertion; its location first, a fifth
        /// grip, where it is at no corner (held at a middle); its location alone for an MTEXT of no width, in columns or out of
        /// the plan.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>The grips in the order of their indices; empty for an entity with none.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        public static IReadOnlyList<CadGrip> Of(DxfEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var grips = new List<CadGrip>();

            void Add(CadGripKind kind, double x, double y) => grips.Add(new CadGrip(grips.Count, kind, new GeoPoint2(x, y)));

            // A point of an arc's or a polyline's own plane, put in the world.
            void AddOwn(CadGripKind kind, GeoPoint2 own, bool below) => grips.Add(new CadGrip(grips.Count, kind, Own(own, below)));

            switch (entity)
            {
                case DxfLine line:
                    Add(CadGripKind.End, line.StartPoint.X, line.StartPoint.Y);
                    Add(CadGripKind.End, line.EndPoint.X, line.EndPoint.Y);
                    Add(CadGripKind.Middle, (line.StartPoint.X + line.EndPoint.X) / 2.0, (line.StartPoint.Y + line.EndPoint.Y) / 2.0);
                    break;
                case DxfCircle circle when IsPlan(circle.Normal):
                    Add(CadGripKind.Center, circle.Center.X, circle.Center.Y);
                    Add(CadGripKind.Quadrant, circle.Center.X + circle.Radius, circle.Center.Y);
                    Add(CadGripKind.Quadrant, circle.Center.X, circle.Center.Y + circle.Radius);
                    Add(CadGripKind.Quadrant, circle.Center.X - circle.Radius, circle.Center.Y);
                    Add(CadGripKind.Quadrant, circle.Center.X, circle.Center.Y - circle.Radius);
                    break;
                case DxfArc arc when IsPlan(arc.Normal):
                    {
                        bool below = arc.Normal.Z < 0.0;
                        GeoPoint2 c = Own(new GeoPoint2(arc.Center.X, arc.Center.Y), below);
                        double middle = arc.StartAngle + Sweep(arc.StartAngle, arc.EndAngle) / 2.0;
                        AddOwn(CadGripKind.End, new GeoPoint2(c.X + arc.Radius * Math.Cos(arc.StartAngle), c.Y + arc.Radius * Math.Sin(arc.StartAngle)), below);
                        AddOwn(CadGripKind.End, new GeoPoint2(c.X + arc.Radius * Math.Cos(arc.EndAngle), c.Y + arc.Radius * Math.Sin(arc.EndAngle)), below);
                        AddOwn(CadGripKind.Middle, new GeoPoint2(c.X + arc.Radius * Math.Cos(middle), c.Y + arc.Radius * Math.Sin(middle)), below);
                        Add(CadGripKind.Center, arc.Center.X, arc.Center.Y);
                        break;
                    }

                case DxfPolyline polyline when IsPlan(polyline.Normal):
                    {
                        int n = polyline.NumberOfVertices;

                        bool below = polyline.Normal.Z < 0.0;

                        for (int i = 0; i < n; i++)
                        {
                            AddOwn(CadGripKind.Vertex, polyline.GetPoint2dAt(i), below);
                        }

                        int segments = polyline.Closed && n > 2 ? n : n - 1;

                        for (int i = 0; i < segments; i++)
                        {
                            AddOwn(CadGripKind.SegmentMiddle, SegmentMiddle(polyline.GetPoint2dAt(i), polyline.GetPoint2dAt((i + 1) % n), polyline.GetBulgeAt(i)), below);
                        }

                        break;
                    }

                case DxfDBText text:
                    {
                        GeoPoint3 at = IsLeftOnBaseline(text) ? text.Position : text.AlignmentPoint;
                        Add(CadGripKind.Insertion, at.X, at.Y);
                        break;
                    }

                case DxfMText mtext:
                    MTextGrips(mtext, (kind, at) => Add(kind, at.X, at.Y));
                    break;
                case DxfBlockReference insert:
                    Add(CadGripKind.Insertion, insert.Position.X, insert.Position.Y);

                    foreach (DxfDynamicBlockGrip custom in CadDynamicBlocks.GripsOf(insert))
                    {
                        grips.Add(new CadGrip(grips.Count, CadDynamicBlocks.KindOf(custom), new GeoPoint2(custom.Position.X, custom.Position.Y), custom));
                    }

                    break;
                case DxfDimension dimension:
                    DimensionGrips(dimension, (kind, at) => Add(kind, at.X, at.Y));
                    break;
                case DxfViewport viewport when !(viewport.On && viewport.Number == 1):
                    {
                        // The four corners from the lower left round counter-clockwise, then the middle; the viewport of the sheet has none.
                        GeoPoint3 c = viewport.CenterPoint;
                        double w = viewport.Width / 2.0;
                        double h = viewport.Height / 2.0;
                        Add(CadGripKind.Corner, c.X - w, c.Y - h);
                        Add(CadGripKind.Corner, c.X + w, c.Y - h);
                        Add(CadGripKind.Corner, c.X + w, c.Y + h);
                        Add(CadGripKind.Corner, c.X - w, c.Y + h);
                        Add(CadGripKind.Center, c.X, c.Y);
                        break;
                    }
            }

            return grips;
        }

        /// <summary>
        /// Drags a grip of an entity to a point, as AutoCAD's stretch mode does: one undo step through
        /// <see cref="CadDocument.ApplyEdit"/>. An arc dragged by an end or its middle is the arc through the three points then
        /// (the moved one and the two kept); a quadrant makes a circle's radius the distance to the point. A dimension's grip
        /// rewrites its points of definition and its picture is drawn anew: an extension line's origin moved keeps the
        /// dimension line where it is (an aligned one's at the same distance from what it measures); an end of the dimension
        /// line moves the line through the point, words the user placed going with it; the words go where DIMTEDIT puts them,
        /// the dimension line with them where DIMTMOVE is 0; a radius's or a diameter's chord point goes round its circle
        /// towards the point, words the user placed turning with it. A corner of an MTEXT's box takes its two edges to the point,
        /// as AutoCAD 2021's does: the width of the MTEXT is the box's then and its words wrap anew, its defined height the box's
        /// (at least its text height; left as it is in a file before R2007, which holds none), and its location stays at the
        /// point of the box its attachment names: the top right corner of a box 60 wide dragged 5 right and 3 down makes it 65
        /// wide and moves a location at the top left 3 down.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The handle of the entity.</param>
        /// <param name="gripIndex">The index of the grip, as <see cref="Of"/> gives it.</param>
        /// <param name="to">The point it is dragged to, in drawing units.</param>
        /// <returns>What came of it: refused for an entity on a locked layer, a grip it has not, or a shape that cannot be (an
        /// arc through three points in a line, a circle of no radius, a chord point dragged to its centre); failed, the
        /// drawing as it was, for a dimension that would measure nothing.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult Drag(CadDocument document, string handle, int gripIndex, GeoPoint2 to)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            DxfEntity entity = document.FindEntity(handle);

            if (entity == null)
            {
                return CadEditResult.Refuse("The drawing has no entity " + handle + ".");
            }

            if (CadLayerEditor.IsLocked(document.Database, entity.Layer))
            {
                return CadEditResult.Refuse("The object is on a locked layer.");
            }

            if (entity is DxfViewport locked && locked.Locked)
            {
                return CadEditResult.Refuse("The viewport is locked.");
            }

            IReadOnlyList<CadGrip> grips = Of(entity);

            if (gripIndex < 0 || gripIndex >= grips.Count)
            {
                return CadEditResult.Refuse("The " + CadDocument.KindName(entity) + " has no grip " + gripIndex + ".");
            }

            CadGrip grip = grips[gripIndex];

            if (entity is DxfDimension)
            {
                return DragDimension(document, handle, gripIndex, grip.Point, to);
            }

            if (grip.Custom != null)
            {
                return CadDynamicBlocks.Drag(document, handle, grip, to);
            }

            GeoVector2 moved = to - grip.Point;
            var shift = new GeoVector3(moved.X, moved.Y, 0.0);
            string property = "Grip " + gripIndex;
            string was = CadCommands.Values(grip.Point.X, grip.Point.Y);
            string went = CadCommands.Values(to.X, to.Y);

            switch (grip.Kind)
            {
                case CadGripKind.Center when entity is DxfViewport:
                    return Reshape(document, handle, property, was, went, () =>
                    {
                        var v = (DxfViewport)document.FindEntity(handle);
                        v.CenterPoint = new GeoPoint3(to.X, to.Y, v.CenterPoint.Z);
                        return true;
                    });
                case CadGripKind.Corner when entity is DxfMText:
                    return ResizeMText(document, (DxfMText)entity, handle, property, was, went, gripIndex - (grips.Count - 4), to);
                case CadGripKind.Corner:
                    return Resize(document, (DxfViewport)entity, handle, property, was, went, grips[(gripIndex + 2) % 4].Point, to);
                case CadGripKind.Center:
                case CadGripKind.Insertion:
                    return Move(document, handle, property, shift, CadCommands.Values(shift.X, shift.Y));
                case CadGripKind.Middle when entity is DxfLine:
                    return Move(document, handle, property, shift, CadCommands.Values(shift.X, shift.Y));
            }

            switch (entity)
            {
                case DxfLine _:
                    return Reshape(document, handle, property, was, went, () => document.FindEntity(handle) is DxfLine l && Set(l, gripIndex == 0, to));
                case DxfCircle circle:
                    {
                        double radius = (to - new GeoPoint2(circle.Center.X, circle.Center.Y)).Length;

                        if (radius <= 0.0)
                        {
                            return CadEditResult.Refuse("A circle needs a radius above nought.");
                        }

                        return Reshape(document, handle, property, was, went, () =>
                        {
                            ((DxfCircle)document.FindEntity(handle)).Radius = radius;
                            return true;
                        });
                    }

                case DxfArc arc:
                    {
                        // Worked in the arc's own plane, where its angles run counter-clockwise, and put back in the world.
                        bool below = arc.Normal.Z < 0.0;
                        GeoPoint2 p1 = Own(gripIndex == 0 ? to : grips[0].Point, below);
                        GeoPoint2 p2 = Own(gripIndex == 2 ? to : grips[2].Point, below);
                        GeoPoint2 p3 = Own(gripIndex == 1 ? to : grips[1].Point, below);

                        if (!ArcThrough(p1, p2, p3, out GeoPoint2 center, out double radius, out double from, out double till))
                        {
                            return CadEditResult.Refuse("The three points of the arc lie in a line.");
                        }

                        return Reshape(document, handle, property, was, went, () =>
                        {
                            var a = (DxfArc)document.FindEntity(handle);
                            GeoPoint2 world = Own(center, below);
                            a.Center = new GeoPoint3(world.X, world.Y, a.Center.Z);
                            a.Radius = radius;
                            a.StartAngle = from;
                            a.EndAngle = till;
                            return true;
                        });
                    }

                case DxfPolyline polyline:
                    {
                        // Worked in the polyline's own plane, where its vertices and bulges are written.
                        int n = polyline.NumberOfVertices;
                        bool below = polyline.Normal.Z < 0.0;
                        GeoPoint2 own = Own(to, below);
                        GeoVector2 shifted = own - Own(grip.Point, below);

                        if (grip.Kind == CadGripKind.Vertex)
                        {
                            return Reshape(document, handle, property, was, went, () =>
                            {
                                ((DxfPolyline)document.FindEntity(handle)).SetPointAt(gripIndex, own);
                                return true;
                            });
                        }

                        int segment = gripIndex - n;
                        int next = (segment + 1) % n;
                        GeoPoint2 a = polyline.GetPoint2dAt(segment);
                        GeoPoint2 b = polyline.GetPoint2dAt(next);
                        double bulge = polyline.GetBulgeAt(segment);

                        if (Math.Abs(bulge) < 1E-12)
                        {
                            // A straight segment is moved whole, its two vertices with it, as AutoCAD's Stretch moves it.
                            return Reshape(document, handle, property, was, went, () =>
                            {
                                var p = (DxfPolyline)document.FindEntity(handle);
                                p.SetPointAt(segment, a + shifted);
                                p.SetPointAt(next, b + shifted);
                                return true;
                            });
                        }

                        if (!BulgeThrough(a, own, b, out double bent))
                        {
                            return CadEditResult.Refuse("The three points of the arc lie in a line.");
                        }

                        return Reshape(document, handle, property, was, went, () =>
                        {
                            ((DxfPolyline)document.FindEntity(handle)).SetBulgeAt(segment, bent);
                            return true;
                        });
                    }
            }

            return CadEditResult.Refuse("The grip cannot be dragged.");
        }

        /// <summary>A viewport's frame from a corner kept to a corner dragged, its scale and the centre of its view kept.</summary>
        private static CadEditResult Resize(CadDocument document, DxfViewport viewport, string handle, string property, string was, string went, GeoPoint2 kept, GeoPoint2 to)
        {
            double width = Math.Abs(to.X - kept.X);
            double height = Math.Abs(to.Y - kept.Y);
            double least = Tolerance.Default.EqualPoint;

            if (width <= least || height <= least || viewport.Height <= 0.0)
            {
                return CadEditResult.Refuse("A viewport needs a frame of some width and height.");
            }

            double viewHeight = viewport.ViewHeight * height / viewport.Height;
            var center = new GeoPoint2((kept.X + to.X) / 2.0, (kept.Y + to.Y) / 2.0);
            return Reshape(document, handle, property, was, went, () =>
            {
                var v = (DxfViewport)document.FindEntity(handle);
                v.CenterPoint = new GeoPoint3(center.X, center.Y, v.CenterPoint.Z);
                v.Width = width;
                v.Height = height;
                v.ViewHeight = viewHeight;
                return true;
            });
        }

        private static CadEditResult Move(CadDocument document, string handle, string property, GeoVector3 shift, string value)
        {
            var record = new CadEditRecord(CadEditRecordKind.Transformed, handle, property, null, value);
            return document.ApplyEdit("GRIP", new[] { record }, s => document.FindEntity(handle).TransformBy(GeoTransform3.Translation(shift)));
        }

        private static CadEditResult Reshape(CadDocument document, string handle, string property, string was, string went, Func<bool> write)
        {
            var record = new CadEditRecord(CadEditRecordKind.Reshaped, handle, property, was, went);
            return document.ApplyEdit("GRIP", new[] { record }, s => write());
        }

        private static bool Set(DxfLine line, bool start, GeoPoint2 to)
        {
            if (start)
            {
                line.StartPoint = new GeoPoint3(to.X, to.Y, line.StartPoint.Z);
            }
            else
            {
                line.EndPoint = new GeoPoint3(to.X, to.Y, line.EndPoint.Z);
            }

            return true;
        }

        /// <summary>Whether a normal is the plan's, seen from above or, as AutoCAD's MIRROR leaves an arc, from below.</summary>
        private static bool IsPlan(GeoVector3 normal) => Math.Abs(normal.Z) > 1E-12 && Math.Abs(normal.X) < 1E-12 && Math.Abs(normal.Y) < 1E-12;

        /// <summary>
        /// A point of the plan in the own plane of an entity seen from below (normal (0, 0, -1)), whose x runs against the
        /// world's, or back: the same map both ways. A point of an entity seen from above is its own.
        /// </summary>
        private static GeoPoint2 Own(GeoPoint2 point, bool below) => below ? new GeoPoint2(-point.X, point.Y) : point;

        private static bool IsLeftOnBaseline(DxfDBText text)
            => text.HorizontalMode == DxfTextHorizontalMode.TextLeft && text.VerticalMode == DxfTextVerticalMode.TextBase;

        /// <summary>The counter-clockwise sweep from one angle to another, above nought and at most a whole turn.</summary>
        internal static double Sweep(double from, double to)
        {
            double sweep = to - from;
            sweep -= Math.Floor(sweep / (2.0 * Math.PI)) * 2.0 * Math.PI;
            return sweep <= 0.0 ? 2.0 * Math.PI : sweep;
        }

        /// <summary>The middle of a polyline segment: of the chord, or of the arc its bulge draws.</summary>
        internal static GeoPoint2 SegmentMiddle(GeoPoint2 a, GeoPoint2 b, double bulge)
        {
            var middle = new GeoPoint2((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);

            if (Math.Abs(bulge) < 1E-12)
            {
                return middle;
            }

            // The arc's middle stands off the chord's by the sagitta, bulge times half the chord, to the right of a to b for a
            // bulge above nought (an arc counter-clockwise from a to b).
            GeoVector2 chord = b - a;
            double length = chord.Length;

            if (length <= 0.0)
            {
                return middle;
            }

            var right = new GeoVector2(chord.Y / length, -chord.X / length);
            return middle + right * (bulge * length / 2.0);
        }

        /// <summary>The bulge of the arc from a through q to b: the sagitta over half the chord, above nought where q is to the right of a to b.</summary>
        internal static bool BulgeThrough(GeoPoint2 a, GeoPoint2 q, GeoPoint2 b, out double bulge)
        {
            bulge = 0.0;

            if (!CircleThrough(a, q, b, out GeoPoint2 center, out double radius))
            {
                return false;
            }

            GeoVector2 chord = b - a;
            double length = chord.Length;
            var right = new GeoVector2(chord.Y / length, -chord.X / length);
            GeoVector2 toQ = q - a;
            double side = toQ.X * right.X + toQ.Y * right.Y;
            var middle = new GeoPoint2((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);

            // The arc's middle is the point of the circle on q's side of the chord, farthest from it.
            GeoVector2 off = middle - center;
            double offRight = off.X * right.X + off.Y * right.Y;
            double sagitta = side > 0.0 ? radius - offRight : radius + offRight;
            bulge = (side > 0.0 ? 1.0 : -1.0) * sagitta / (length / 2.0);
            return true;
        }

        /// <summary>The arc from p1 through p2 to p3, as a centre, a radius and the counter-clockwise angles of its start and end.</summary>
        internal static bool ArcThrough(GeoPoint2 p1, GeoPoint2 p2, GeoPoint2 p3, out GeoPoint2 center, out double radius, out double start, out double end)
        {
            start = 0.0;
            end = 0.0;

            if (!CircleThrough(p1, p2, p3, out center, out radius))
            {
                return false;
            }

            double cross = (p2.X - p1.X) * (p3.Y - p1.Y) - (p2.Y - p1.Y) * (p3.X - p1.X);
            double a1 = Math.Atan2(p1.Y - center.Y, p1.X - center.X);
            double a3 = Math.Atan2(p3.Y - center.Y, p3.X - center.X);

            // Counter-clockwise from p1 to p3 where p2 lies to the right of the way p1 to p3, as a counter-clockwise arc bulges; else from p3 to p1.
            start = cross > 0.0 ? a1 : a3;
            end = cross > 0.0 ? a3 : a1;
            start = Normalize(start);
            end = Normalize(end);
            return true;
        }

        private static double Normalize(double angle)
        {
            angle -= Math.Floor(angle / (2.0 * Math.PI)) * 2.0 * Math.PI;
            return angle >= 2.0 * Math.PI ? 0.0 : angle;
        }

        /// <summary>The circle through three points; false where they lie in a line or two are one.</summary>
        internal static bool CircleThrough(GeoPoint2 a, GeoPoint2 b, GeoPoint2 c, out GeoPoint2 center, out double radius)
        {
            double d = 2.0 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
            double scale = Math.Max((b - a).Length * (c - a).Length, 1E-300);

            if (Math.Abs(d) <= 1E-12 * scale)
            {
                center = default;
                radius = 0.0;
                return false;
            }

            double a2 = a.X * a.X + a.Y * a.Y;
            double b2 = b.X * b.X + b.Y * b.Y;
            double c2 = c.X * c.X + c.Y * c.Y;
            center = new GeoPoint2(
                (a2 * (b.Y - c.Y) + b2 * (c.Y - a.Y) + c2 * (a.Y - b.Y)) / d,
                (a2 * (c.X - b.X) + b2 * (a.X - c.X) + c2 * (b.X - a.X)) / d);
            radius = (a - center).Length;
            return radius > 0.0;
        }
    }
}
