using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;
using GeometryHelper.Internal.Nurbs;

namespace GeometryHelper.Dxf
{
    public static partial class DxfReader
    {
        private sealed partial class Reading
        {
            private const string NotReadType = "of a type that is not read";
            private const string NotFiniteReason = "with a number that is not a finite number";
            private const string Incomplete = "without the groups that place it";

            private static readonly IReadOnlyList<DxfTypedValue> NoGroups = Array.AsReadOnly(new DxfTypedValue[0]);
            private readonly SortedDictionary<string, Failure> _failures = new SortedDictionary<string, Failure>(StringComparer.Ordinal);
            private Gathering _drawing;
            private int _fitSplines;
            private int _periodicSplines;
            private int _openedLoops;
            private int _droppedFaces;
            private int _looseFaces;
            private int _badLook;
            private int _refusedSplines;
            private int _loweredFitSplines;

            #region The entities in turn

            /// <summary>
            /// Takes a record of the drawing, read as soon as it is whole: a POLYLINE once its vertices are in.
            /// </summary>
            private void Entity(Record r)
            {
                if (_drawing == null)
                {
                    _drawing = new Gathering(item => Item(item, false));
                }

                _drawing.Add(r);
            }

            /// <summary>
            /// The records of a section taken whole: a POLYLINE gathers the VERTEX records after it up to its SEQEND, which
            /// it is handed on with, and any other record is handed on as it comes.
            /// </summary>
            private sealed class Gathering
            {
                private readonly Action<Record> _take;
                private Record _polyline;
                private List<Record> _vertices;

                internal Gathering(Action<Record> take)
                {
                    _take = take;
                }

                internal void Add(Record r)
                {
                    if (_polyline != null)
                    {
                        if (r.Type == "VERTEX")
                        {
                            _vertices.Add(r);
                            return;
                        }

                        bool end = r.Type == "SEQEND";
                        Close(end ? r : null);

                        if (end)
                        {
                            return;
                        }
                    }

                    if (r.Type == "POLYLINE")
                    {
                        _polyline = r;
                        _vertices = new List<Record>();
                        return;
                    }

                    // A VERTEX or SEQEND belongs to a POLYLINE, and a SEQEND also ends the attributes of an INSERT.
                    if (r.Type == "VERTEX" || r.Type == "SEQEND")
                    {
                        return;
                    }

                    _take(r);
                }

                /// <summary>Hands on the POLYLINE gathered, if any, its groups running on through its vertices and its SEQEND.</summary>
                internal void Close(Record end = null)
                {
                    if (_polyline == null)
                    {
                        return;
                    }

                    var following = new List<Record>(_vertices);

                    if (end != null)
                    {
                        following.Add(end);
                    }

                    Record polyline = _polyline;
                    polyline.Following = following;
                    _polyline = null;
                    _vertices = null;
                    _take(polyline);
                }
            }

            /// <summary>
            /// Reads a whole record: of the drawing, where one in paper space is counted and an INSERT kept to be placed once
            /// the blocks are all in, or of a block being placed.
            /// </summary>
            private void Item(Record r, bool inBlock)
            {
                if (r.Type == "POLYLINE")
                {
                    ReadPolylineRecord(r, inBlock);
                    return;
                }

                if (!inBlock && r.Int(67, 0) == 1)
                {
                    _paperEntities++;
                    return;
                }

                if (r.Type == "INSERT")
                {
                    if (inBlock)
                    {
                        Expand(r);
                    }
                    else
                    {
                        _pending.Add(new KeyValuePair<int, Record>(_into.Count, r));
                    }

                    return;
                }

                switch (r.Type)
                {
                    case "POINT":
                    case "LINE":
                    case "RAY":
                    case "XLINE":
                    case "CIRCLE":
                    case "ARC":
                    case "ELLIPSE":
                    case "SPLINE":
                    case "LWPOLYLINE":
                    case "3DFACE":
                    case "SOLID":
                    case "TRACE":
                    case "TEXT":
                    case "MTEXT":
                    case "LEADER":
                    case "MULTILEADER":
                        break;

                    default:
                        Skip(r.Type, NotReadType);
                        return;
                }

                if (r.Unreadable() != null)
                {
                    Skip(r.Type, NotFiniteReason);
                    return;
                }

                Guarded(r, () =>
                {
                    switch (r.Type)
                    {
                        case "POINT": ReadPoint(r); break;
                        case "LINE": ReadLine(r); break;
                        case "RAY":
                        case "XLINE": ReadRay(r); break;
                        case "CIRCLE": ReadCircle(r); break;
                        case "ARC": ReadArc(r); break;
                        case "ELLIPSE": ReadEllipse(r); break;
                        case "SPLINE": ReadSpline(r); break;
                        case "LWPOLYLINE": ReadLightweight(r); break;
                        case "3DFACE": ReadFace(r); break;
                        case "TEXT":
                        case "MTEXT": ReadText(r); break;
                        case "LEADER": ReadLeader(r); break;
                        case "MULTILEADER": ReadMLeader(r); break;
                        default: ReadSolid(r); break;
                    }
                });
            }

            private void EndOfEntities() => _drawing?.Close();

            /// <summary>
            /// Reads a POLYLINE gathered with its vertices, its groups running on through theirs and those of its SEQEND.
            /// </summary>
            private void ReadPolylineRecord(Record r, bool inBlock)
            {
                List<Record> vertices = r.Following.FindAll(v => v.Type == "VERTEX");

                if (!inBlock && r.Int(67, 0) == 1)
                {
                    _paperEntities++;
                    return;
                }

                if (r.Unreadable() != null || vertices.Exists(v => v.Unreadable() != null))
                {
                    Skip(r.Type, NotFiniteReason);
                    return;
                }

                int flags = r.Int(70, 0);

                Guarded(r, () =>
                {
                    if ((flags & 64) != 0)
                    {
                        ReadPolyface(r, vertices);
                    }
                    else if ((flags & 16) != 0)
                    {
                        ReadPolygonMesh(r, vertices, flags);
                    }
                    else
                    {
                        ReadPolyline(r, vertices, flags);
                    }
                });
            }

            private void EntityWarnings(bool log = true)
            {
                Warn(_fitSplines, " SPLINE given by its fit points alone was read as the spline GeoSpline.Interpolate passes through them, its parameters by chord length, which can differ from the curve the CAD program draws.", " SPLINEs given by their fit points alone were read as the splines GeoSpline.Interpolate passes through them, their parameters by chord length, which can differ from the curves the CAD program draws.");
                Warn(_loweredFitSplines, " SPLINE had fewer fit points than its degree asks for and was read at one degree less than its points, two points being a straight line.", " SPLINEs had fewer fit points than their degree asks for and were read at one degree less than their points, two points being a straight line.");
                Warn(_periodicSplines, " periodic SPLINE was not read: its knots could not be clamped to those of the library.", " periodic SPLINEs were not read: their knots could not be clamped to those of the library.");
                Warn(_refusedSplines, " SPLINE was not read: its degree, knots, weights or fit points do not make a spline the library takes.", " SPLINEs were not read: their degree, knots, weights or fit points do not make a spline the library takes.");
                Warn(_openedLoops, " closed polyline that encloses no area, or in space does not lie in one plane, was read as an open chain that ends on its first vertex.", " closed polylines that enclose no area, or in space do not lie in one plane, were read as open chains that end on their first vertex.");
                Warn(_droppedFaces, " face of a mesh was left out: it has fewer than three corners, names a vertex that is not there, or encloses no area.", " faces of meshes were left out: they have fewer than three corners, name a vertex that is not there, or enclose no area.");
                Warn(_looseFaces, " mesh of fewer than four faces was read as its faces, each an entity of its own, since a body has four at least.", " meshes of fewer than four faces were read as their faces, each an entity of its own, since a body has four at least.");
                Warn(_badLook, " group of the colour, lineweight, true colour or thickness of an entity could not be read and was left out, the entity read without it.", " groups of the colour, lineweight, true colour or thickness of entities could not be read and were left out, the entities read without them.");

                foreach (KeyValuePair<string, Failure> failed in _failures)
                {
                    _warnings.Add(Number(failed.Value.Count) + " " + failed.Key + (failed.Value.Count == 1 ? " could not be read and was" : " could not be read and were") + " left out: " + failed.Value.First.GetType().Name + ", " + failed.Value.First.Message);

                    // A placing for DxfFile logs its own line, once the placing is done.
                    if (log)
                    {
                        GeometryHelperLog.Warn("DxfReader: " + Number(failed.Value.Count) + " " + failed.Key + (failed.Value.Count == 1 ? " could not be read and was left out." : " could not be read and were left out."), failed.Value.First);
                    }
                }
            }

            /// <summary>The entities of one type that could not be read: how many, and the first exception.</summary>
            private sealed class Failure
            {
                internal int Count;
                internal Exception First;
            }

            /// <summary>
            /// Reads one entity, which never stops the reading: an exception from it leaves it out, takes back any shape of
            /// it already added, and counts it under its type.
            /// </summary>
            private void Guarded(Record r, Action read)
            {
                int before = _into.Count;

                try
                {
                    read();
                }
                catch (NotFinite)
                {
                    _into.RemoveRange(before, _into.Count - before);
                    Skip(r.Type, NotFiniteReason);
                }
                catch (Exception e) when (!(e is OutOfMemoryException) && !(e is PastTheBound))
                {
                    _into.RemoveRange(before, _into.Count - before);
                    Skip(r.Type, "that could not be read");

                    if (!_failures.TryGetValue(r.Type, out Failure failure))
                    {
                        failure = new Failure { First = e };
                        _failures.Add(r.Type, failure);
                    }

                    failure.Count++;
                }
            }

            /// <summary>
            /// Adds the shape of an entity, on the layer, in the colour and with the handle the record gives, those of the
            /// INSERTs that place it where it takes them, with the names of their blocks.
            /// </summary>
            private void Add(Record r, object shape, int hiddenEdges = 0)
            {
                Look look = LookOf(r);
                double thickness = look.Thickness;

                // The thickness stands along the extrusion, and is stretched as the placement stretches that; it is turned over
                // where the placed extrusion points against the entity's own, as a mirror along z leaves it.
                if (thickness != 0.0 && _place != null)
                {
                    GeoVector3 own = Extrusion(r);
                    GeoVector3 carried = _place.Linear(own);
                    double placed = thickness * carried.Length * (carried.DotProduct(own) < 0.0 ? -1.0 : 1.0);

                    if (double.IsNaN(placed) || double.IsInfinity(placed))
                    {
                        _badLook++;
                        thickness = 0.0;
                    }
                    else
                    {
                        thickness = placed;
                    }
                }

                string handle = r.Text(5)?.Trim();
                IReadOnlyList<DxfTypedValue> groups = _keepGroups ? r.Groups() : NoGroups;

                // Whatever the size worked out for its block, an INSERT places no more than the bound leaves.
                if (_approved && _into.Count - _treeStart >= _budget.Left)
                {
                    throw new PastTheBound();
                }

                _into.Add(new DxfShape(shape, r.Type, look.Layer, look.Colour, string.IsNullOrEmpty(handle) ? null : handle, look.Linetype, look.Lineweight, look.TrueColour, thickness, groups, hiddenEdges, _path, _handles));
            }

            /// <summary>
            /// The look of a record: its layer, colour, true colour, linetype, lineweight and thickness. Inside a block being
            /// placed, layer 0 is the INSERT's layer, and a colour, linetype or lineweight BYBLOCK the INSERT's, its true colour
            /// with its colour where the record gives none of its own; BYLAYER stays BYLAYER.
            /// </summary>
            private Look LookOf(Record r)
            {
                string layer = Decode((r.Text(8) ?? string.Empty).Trim());
                layer = LayerName(layer.Length == 0 ? "0" : layer);

                // A group of the look that cannot be read is left out, and the entity read without it.
                int? colour = null;
                int? lineweight = null;
                int? trueColour = null;
                double thickness = 0.0;
                bool colourByBlock = false;
                bool weightByBlock = false;

                if (r.Has(62))
                {
                    if (r.TryInt(62, out int index))
                    {
                        colour = index >= 1 && index <= 255 ? index : (int?)null;
                        colourByBlock = index == 0;
                    }
                    else
                    {
                        _badLook++;
                    }
                }

                if (r.Has(370))
                {
                    if (r.TryInt(370, out int weight) && (weight < 0 || DxfShape.IsLineweight(weight)))
                    {
                        lineweight = weight >= 0 ? weight : (int?)null;
                        weightByBlock = weight == -2;
                    }
                    else
                    {
                        _badLook++;
                    }
                }

                if (r.Has(420))
                {
                    if (r.TryInt(420, out int rgb))
                    {
                        trueColour = rgb & 0xFFFFFF;
                    }
                    else
                    {
                        _badLook++;
                    }
                }

                if (r.TryReal(39, out double raised))
                {
                    double scaled = raised * _scale;

                    if (double.IsNaN(scaled) || double.IsInfinity(scaled))
                    {
                        _badLook++;
                    }
                    else
                    {
                        thickness = _scale == 1.0 ? raised : scaled;
                    }
                }

                string linetype = Decode((r.Text(6) ?? string.Empty).Trim());

                if (linetype.Length == 0 || string.Equals(linetype, "BYLAYER", StringComparison.OrdinalIgnoreCase))
                {
                    linetype = null;
                }

                if (_inherit.HasValue)
                {
                    Look inherit = _inherit.Value;

                    if (layer == "0")
                    {
                        layer = inherit.Layer;
                    }

                    if (colourByBlock)
                    {
                        colour = inherit.Colour;
                        trueColour = trueColour ?? inherit.TrueColour;
                    }

                    if (linetype != null && string.Equals(linetype, "BYBLOCK", StringComparison.OrdinalIgnoreCase))
                    {
                        linetype = inherit.Linetype;
                    }

                    if (weightByBlock)
                    {
                        lineweight = inherit.Lineweight;
                    }
                }

                return new Look(layer, colour, trueColour, linetype, lineweight, thickness);
            }

            /// <summary>The look of an entity, or of an INSERT that its block's entities take theirs from.</summary>
            private readonly struct Look
            {
                internal Look(string layer, int? colour, int? trueColour, string linetype, int? lineweight, double thickness)
                {
                    Layer = layer;
                    Colour = colour;
                    TrueColour = trueColour;
                    Linetype = linetype;
                    Lineweight = lineweight;
                    Thickness = thickness;
                }

                internal string Layer { get; }

                internal int? Colour { get; }

                internal int? TrueColour { get; }

                internal string Linetype { get; }

                internal int? Lineweight { get; }

                internal double Thickness { get; }
            }

            #endregion

            #region Numbers of the file

            /// <summary>
            /// A coordinate or size scaled to millimetres where asked. One the scaling takes past the largest double (1E307
            /// metres is 1E310 millimetres) leaves its entity out, counted as one with a number that is not finite.
            /// </summary>
            private double Scaled(double value)
            {
                if (_scale == 1.0)
                {
                    return value;
                }

                double scaled = value * _scale;

                if (double.IsInfinity(scaled) || double.IsNaN(scaled))
                {
                    throw new NotFinite();
                }

                return scaled;
            }

            /// <summary>A number the scaling to millimetres took past the largest double.</summary>
            private sealed class NotFinite : Exception
            {
            }

            /// <summary>
            /// The point of a record held in the groups 10, 20, 30 (n = 0), 11, 21, 31 (n = 1) and so on, scaled to
            /// millimetres where asked: there where any of the three is, a coordinate the file leaves out being nought, as
            /// the DXF reference has it.
            /// </summary>
            private bool TryPoint(Record r, int n, out GeoPoint3 point)
            {
                point = default(GeoPoint3);

                if (!TryDirection(r, n, out GeoVector3 raw))
                {
                    return false;
                }

                point = new GeoPoint3(Scaled(raw.X), Scaled(raw.Y), Scaled(raw.Z));
                return true;
            }

            /// <summary>
            /// The direction of a record held in the groups 10 + n, 20 + n, 30 + n, not scaled: there where any of the three
            /// is, one the file leaves out being nought.
            /// </summary>
            private static bool TryDirection(Record r, int n, out GeoVector3 direction)
            {
                bool any = r.TryReal(10 + n, out double x) | r.TryReal(20 + n, out double y) | r.TryReal(30 + n, out double z);
                direction = new GeoVector3(x, y, z);
                return any;
            }

            /// <summary>
            /// The extrusion of a record: the normal of the plane its shape of the plane lies in, (0, 0, 1) where the file
            /// gives none, a part of it the file leaves out being nought. A vector within 1E-12 of unit length is kept as the
            /// file has it, since dividing it by its length would move its last digit; any other is made unit, and one of no
            /// length is read as (0, 0, 1).
            /// </summary>
            private static GeoVector3 Extrusion(Record r)
            {
                if (!TryDirection(r, 200, out GeoVector3 given))
                {
                    return new GeoVector3(0.0, 0.0, 1.0);
                }

                double x = given.X;
                double y = given.Y;
                double z = given.Z;

                double length = Math.Sqrt(x * x + y * y + z * z);

                if (!(length > 0.0) || double.IsInfinity(length))
                {
                    return new GeoVector3(0.0, 0.0, 1.0);
                }

                return Math.Abs(length - 1.0) <= 1E-12 ? new GeoVector3(x, y, z) : new GeoVector3(x / length, y / length, z / length);
            }

            private static bool IsUp(GeoVector3 normal) => normal.X == 0.0 && normal.Y == 0.0 && normal.Z == 1.0;

            /// <summary>
            /// The object coordinate system of an extrusion by the arbitrary axis algorithm of the DXF reference, as
            /// <see cref="DxfWriter"/> works it out: its x axis is the world y crossed with the normal where x and y of the
            /// normal are both less than 1/64 from nought, and the world z crossed with it otherwise; its y axis is the
            /// normal crossed with the x axis. Of the extrusion (0, 0, 1) it is the world's, and a point is taken as it is.
            /// </summary>
            private readonly struct Ocs
            {
                private readonly bool _world;
                private readonly double _ux;
                private readonly double _uy;
                private readonly double _uz;
                private readonly double _vx;
                private readonly double _vy;
                private readonly double _vz;

                internal Ocs(GeoVector3 normal)
                {
                    Normal = normal;
                    _world = IsUp(normal);

                    double nx = normal.X;
                    double ny = normal.Y;
                    double nz = normal.Z;
                    bool nearZ = Math.Abs(nx) < 1.0 / 64.0 && Math.Abs(ny) < 1.0 / 64.0;
                    double ax = nearZ ? nz : -ny;
                    double ay = nearZ ? 0.0 : nx;
                    double az = nearZ ? -nx : 0.0;
                    double length = Math.Sqrt(ax * ax + ay * ay + az * az);

                    _ux = ax / length;
                    _uy = ay / length;
                    _uz = az / length;

                    double bx = ny * _uz - nz * _uy;
                    double by = nz * _ux - nx * _uz;
                    double bz = nx * _uy - ny * _ux;
                    length = Math.Sqrt(bx * bx + by * by + bz * bz);

                    _vx = bx / length;
                    _vy = by / length;
                    _vz = bz / length;
                }

                internal GeoVector3 Normal { get; }

                /// <summary>Whether the system is the world's, the extrusion being (0, 0, 1) exactly.</summary>
                internal bool IsWorld => _world;

                /// <summary>The x axis of the system in the world, (1, 0, 0) where it is the world's.</summary>
                internal GeoVector3 U => new GeoVector3(_ux, _uy, _uz);

                /// <summary>The y axis of the system in the world, (0, 1, 0) where it is the world's.</summary>
                internal GeoVector3 V => new GeoVector3(_vx, _vy, _vz);

                /// <summary>
                /// The point of the system in the world. One the turn takes past the largest double, a coordinate of 1.7E308
                /// on a tilted extrusion, leaves its entity out as one with a number that is not finite.
                /// </summary>
                internal GeoPoint3 World(double x, double y, double z)
                {
                    if (_world)
                    {
                        return new GeoPoint3(x, y, z);
                    }

                    var p = new GeoPoint3(_ux * x + _vx * y + Normal.X * z, _uy * x + _vy * y + Normal.Y * z, _uz * x + _vz * y + Normal.Z * z);

                    if (!IsFinite(p))
                    {
                        throw new NotFinite();
                    }

                    return p;
                }

                internal GeoPoint3 World(GeoPoint3 p) => World(p.X, p.Y, p.Z);
            }

            private static bool IsFinite(GeoPoint3 p)
                => !double.IsNaN(p.X) && !double.IsInfinity(p.X) && !double.IsNaN(p.Y) && !double.IsInfinity(p.Y) && !double.IsNaN(p.Z) && !double.IsInfinity(p.Z);

            private static bool IsDown(GeoVector3 normal) => normal.X == 0.0 && normal.Y == 0.0 && normal.Z == -1.0;

            /// <summary>
            /// Whether an entity in the system of its extrusion's plane is read as a shape of the plane: asked for, its
            /// extrusion (0, 0, 1) or (0, 0, -1) exactly, and every height in that system nought. Of (0, 0, -1) the plane is
            /// the floor seen from below, its x the world's -x and its y the world's y, as AutoCAD writes a mirrored entity.
            /// </summary>
            private bool FlatInPlane(GeoVector3 normal, IReadOnlyList<GeoPoint3> points)
                => (IsUp(normal) || IsDown(normal)) && FlatInWorld(points);

            /// <summary>
            /// Whether an entity in world coordinates is read as a shape of the plane: asked for, and every height nought,
            /// whatever its extrusion, which for a POINT or a LINE says only which way its thickness stands.
            /// </summary>
            private bool FlatInWorld(IReadOnlyList<GeoPoint3> points)
            {
                if (!_planAs2D)
                {
                    return false;
                }

                for (int i = 0; i < points.Count; i++)
                {
                    if (points[i].Z != 0.0)
                    {
                        return false;
                    }
                }

                return true;
            }

            private static GeoPoint2 Plan(GeoPoint3 p) => new GeoPoint2(p.X, p.Y);

            #endregion

            #region Points, lines, rays, circles, arcs, ellipses

            private void ReadPoint(Record r)
            {
                if (!TryPoint(r, 0, out GeoPoint3 point))
                {
                    Skip(r.Type, Incomplete);
                    return;
                }

                point = Place(point);
                Add(r, FlatInWorld(new[] { point }) ? (object)Plan(point) : point);
            }

            private void ReadLine(Record r)
            {
                if (!TryPoint(r, 0, out GeoPoint3 start) || !TryPoint(r, 1, out GeoPoint3 end))
                {
                    Skip(r.Type, Incomplete);
                    return;
                }

                start = Place(start);
                end = Place(end);

                if (start.IsEqualTo(end, _tolerance))
                {
                    Skip(r.Type, "of no length");
                    return;
                }

                Add(r, FlatInWorld(new[] { start, end }) ? (object)new GeoLine2(Plan(start), Plan(end)) : new GeoLine3(start, end));
            }

            /// <summary>
            /// Reads a RAY or an XLINE: its origin, and its direction, kept as the file has it where it is within 1E-12 of unit
            /// length.
            /// </summary>
            private void ReadRay(Record r)
            {
                if (!TryPoint(r, 0, out GeoPoint3 origin) || !TryDirection(r, 1, out GeoVector3 direction))
                {
                    Skip(r.Type, Incomplete);
                    return;
                }

                double length = direction.Length;

                if (!(length > _tolerance.EqualVector) || double.IsInfinity(length))
                {
                    Skip(r.Type, "with no direction");
                    return;
                }

                GeoVector3 unit = Math.Abs(length - 1.0) <= 1E-12 ? direction : new GeoVector3(direction.X / length, direction.Y / length, direction.Z / length);
                bool ray = r.Type == "RAY";

                if (_place != null)
                {
                    origin = _place.Apply(origin);
                    GeoVector3 placed = _place.Linear(unit);
                    double placedLength = placed.Length;

                    if (!(placedLength > 0.0) || double.IsInfinity(placedLength))
                    {
                        Skip(r.Type, Flattened);
                        return;
                    }

                    unit = Math.Abs(placedLength - 1.0) <= 1E-12 ? placed : new GeoVector3(placed.X / placedLength, placed.Y / placedLength, placed.Z / placedLength);
                }

                if (_planAs2D && origin.Z == 0.0 && unit.Z == 0.0)
                {
                    var flat = new GeoVector2(unit.X, unit.Y);
                    double flatLength = flat.Length;
                    flat = Math.Abs(flatLength - 1.0) <= 1E-12 ? flat : new GeoVector2(flat.X / flatLength, flat.Y / flatLength);
                    Add(r, ray ? (object)new GeoRay2(Plan(origin), flat, true) : new GeoXLine2(Plan(origin), flat, true));
                    return;
                }

                Add(r, ray ? (object)new GeoRay3(origin, unit, true) : new GeoXLine3(origin, unit, true));
            }

            private void ReadCircle(Record r)
            {
                if (!TryPoint(r, 0, out GeoPoint3 centre) || !r.TryReal(40, out double radius))
                {
                    Skip(r.Type, Incomplete);
                    return;
                }

                radius = Scaled(radius);

                if (!(radius > _tolerance.EqualPoint))
                {
                    Skip(r.Type, "of no radius");
                    return;
                }

                var ocs = new Ocs(Extrusion(r));

                if (_place != null)
                {
                    PlacedCircle(r, new Frame(ocs, _place), centre, radius);
                    return;
                }

                if (FlatInPlane(ocs.Normal, new[] { centre }))
                {
                    Add(r, new GeoCircle2(Plan(ocs.World(centre)), radius));
                    return;
                }

                Add(r, new GeoCircle3(ocs.World(centre), ocs.Normal, radius));
            }

            /// <summary>
            /// Reads an ARC, which runs counter-clockwise about its extrusion from its start angle to its end angle, both
            /// degrees from the x axis of its plane's own system: equal angles are a whole turn, as a <see cref="GeoArc2"/>
            /// takes them. In space the arc is placed by its centre, its normal, its radius, the point it starts at and the
            /// angle it sweeps.
            /// </summary>
            private void ReadArc(Record r)
            {
                if (!TryPoint(r, 0, out GeoPoint3 centre) || !r.TryReal(40, out double radius) || !r.TryReal(50, out double startDegrees) || !r.TryReal(51, out double endDegrees))
                {
                    Skip(r.Type, Incomplete);
                    return;
                }

                radius = Scaled(radius);

                if (!(radius > _tolerance.EqualPoint))
                {
                    Skip(r.Type, "of no radius");
                    return;
                }

                double start = startDegrees * Math.PI / 180.0;
                double end = endDegrees * Math.PI / 180.0;
                var ocs = new Ocs(Extrusion(r));

                if (_place != null)
                {
                    PlacedArc(r, new Frame(ocs, _place), centre, radius, startDegrees, endDegrees);
                    return;
                }

                // On the floor seen from below an angle a of the system is the world's pi - a, and counter-clockwise about
                // down is clockwise about up: the arc runs clockwise from pi - start to pi - end, from the start the file
                // gives, as the arc read in space does.
                if (FlatInPlane(ocs.Normal, new[] { centre }))
                {
                    Add(r, IsUp(ocs.Normal)
                        ? new GeoArc2(Plan(centre), radius, start, end)
                        : new GeoArc2(Plan(ocs.World(centre)), radius, Math.PI - start, Math.PI - end, true));
                    return;
                }

                double sweepDegrees = (endDegrees - startDegrees) % 360.0;

                if (sweepDegrees <= 0.0)
                {
                    sweepDegrees += 360.0;
                }

                GeoPoint3 first = ocs.World(centre.X + radius * Math.Cos(start), centre.Y + radius * Math.Sin(start), centre.Z);
                Add(r, GeoArc3.FromCircle(ocs.World(centre), ocs.Normal, radius, first, sweepDegrees * Math.PI / 180.0));
            }

            /// <summary>
            /// Reads an ELLIPSE: its centre and the end of its major axis in world coordinates, the ratio of its radii, and
            /// the eccentric angles it runs between, counter-clockwise about its extrusion, its minor axis the extrusion
            /// crossed with its major. A whole one is an ellipse, and a part the spline that is exactly that part.
            /// </summary>
            private void ReadEllipse(Record r)
            {
                if (!TryPoint(r, 0, out GeoPoint3 centre) || !TryPoint(r, 1, out GeoPoint3 end) || !r.TryReal(40, out double ratio))
                {
                    Skip(r.Type, Incomplete);
                    return;
                }

                var axis = new GeoVector3(end.X, end.Y, end.Z);
                double major = axis.Length;

                if (!(major > _tolerance.EqualPoint) || double.IsInfinity(major))
                {
                    Skip(r.Type, "whose major axis has no length");
                    return;
                }

                if (!(ratio > 0.0) || ratio > 1.0 + 1E-9)
                {
                    Skip(r.Type, "whose ratio of radii is not above nought and up to 1");
                    return;
                }

                double minor = Math.Min(ratio, 1.0) * major;
                double from = r.Real(41, 0.0);
                double to = r.Real(42, Math.PI * 2.0);
                double span = (to - from) % (Math.PI * 2.0);

                if (span < 0.0)
                {
                    span += Math.PI * 2.0;
                }

                bool whole = span <= 1E-12 || span >= Math.PI * 2.0 - 1E-9;
                GeoVector3 normal = Extrusion(r);
                bool flat = FlatInPlane(normal, new[] { centre, end });

                try
                {
                    if (_place != null)
                    {
                        PlacedEllipse(r, centre, normal, axis, major, minor, from, span, whole);
                        return;
                    }

                    if (whole)
                    {
                        Add(r, flat
                            ? (object)new GeoEllipse2(Plan(centre), new GeoVector2(axis.X / major, axis.Y / major), major, minor, true)
                            : new GeoEllipse3(centre, normal, axis, major, minor));
                        return;
                    }

                    Add(r, EllipticalArc(centre, normal, axis, major, minor, from, span, flat));
                }
                catch (ArgumentException)
                {
                    Skip(r.Type, "whose major axis runs along its extrusion");
                }
            }

            /// <summary>
            /// The spline that is exactly a part of an ellipse: rational quadratic pieces of at most a quarter of a turn of the
            /// eccentric angle, each the image of a piece of a circle, which <see cref="GeoSpline2.FromArc"/> makes, under the
            /// stretch that makes the circle the ellipse; so each is exactly its piece of the ellipse, to rounding. Its ends
            /// are the ellipse's points at the two angles.
            /// </summary>
            private static object EllipticalArc(GeoPoint3 centre, GeoVector3 normal, GeoVector3 axis, double major, double minor, double from, double span, bool flat)
            {
                EllipseAxes(normal, axis, major, out GeoVector3 u, out GeoVector3 w);
                return ArcPieces(centre, u, w, major, minor, from, span, flat);
            }

            /// <summary>
            /// The axes of an ellipse of unit length: the major laid into the plane of the extrusion, and the extrusion crossed
            /// with it.
            /// </summary>
            /// <exception cref="ArgumentException">Thrown when the major axis runs along the extrusion.</exception>
            private static void EllipseAxes(GeoVector3 normal, GeoVector3 axis, double major, out GeoVector3 u, out GeoVector3 w)
            {
                GeoVector3 inPlane = axis.Subtract(normal.Multiply(axis.DotProduct(normal)));
                double left = inPlane.Length;

                if (!(left > 1E-12 * major))
                {
                    throw new ArgumentException("The major axis runs along the extrusion.");
                }

                u = new GeoVector3(inPlane.X / left, inPlane.Y / left, inPlane.Z / left);
                w = normal.CrossProduct(u);
            }

            /// <summary>
            /// The spline that is exactly the curve centre + a cos t u + b sin t w for t from an angle over a span, u and w
            /// any two vectors that span a plane: rational quadratic pieces of at most a quarter of a turn of t, each the image
            /// of a piece of a circle under the map that takes the circle's axes to a u and b w, so each is exactly its piece,
            /// to rounding.
            /// </summary>
            private static object ArcPieces(GeoPoint3 centre, GeoVector3 u, GeoVector3 w, double major, double minor, double from, double span, bool flat)
            {
                int pieces = Math.Max(1, (int)Math.Ceiling(span / (Math.PI * 0.5) - 1E-9));
                double step = span / pieces;
                int dim = flat ? 2 : 3;
                var ends = new double[(pieces + 1) * dim];
                var apexes = new double[pieces * dim];
                var weights = new double[pieces];
                double cosine = Math.Cos(step * 0.5);

                void Put(double[] into, int index, double angle, double stretch)
                {
                    double a = major * Math.Cos(angle) / stretch;
                    double b = minor * Math.Sin(angle) / stretch;
                    into[index * dim] = centre.X + a * u.X + b * w.X;
                    into[index * dim + 1] = centre.Y + a * u.Y + b * w.Y;

                    if (dim == 3)
                    {
                        into[index * dim + 2] = centre.Z + a * u.Z + b * w.Z;
                    }
                }

                for (int i = 0; i <= pieces; i++)
                {
                    Put(ends, i, i == pieces ? from + span : from + i * step, 1.0);
                }

                for (int i = 0; i < pieces; i++)
                {
                    Put(apexes, i, from + (i + 0.5) * step, cosine);
                    weights[i] = cosine;
                }

                NurbsCurve curve = NurbsCurve.FromArcPieces(dim, ends, apexes, weights);
                return flat ? (object)new GeoSpline2(curve) : new GeoSpline3(curve);
            }

            #endregion

            #region Splines

            /// <summary>
            /// Reads a SPLINE by its degree (71), its knots (each 40), its weights (each 41, none where it is not rational) and
            /// its control points (each 10, 20, 30), every number as the file prints it, in world coordinates. One whose knots
            /// are not clamped, as AutoCAD writes a closed one, is clamped by knot insertion, the same curve over the same
            /// span. One given by its fit points alone (each 11, 21, 31), with the tangents at its ends where it has them (12,
            /// 22, 32 and 13, 23, 33), is the spline the library passes through them, and said in a warning.
            /// </summary>
            private void ReadSpline(Record r)
            {
                var points = new List<GeoPoint3>();
                var fit = new List<GeoPoint3>();
                var knots = new List<double>();
                var weights = new List<double>();

                for (int i = 0; i < r.Count; i++)
                {
                    int code = r.CodeAt(i);
                    double value = code >= 10 && code <= 59 ? r.NumberAt(i) : 0.0;

                    switch (code)
                    {
                        case 10: points.Add(new GeoPoint3(Scaled(value), 0.0, 0.0)); break;
                        case 20: if (points.Count > 0) { points[points.Count - 1] = new GeoPoint3(points[points.Count - 1].X, Scaled(value), 0.0); } break;
                        case 30: if (points.Count > 0) { points[points.Count - 1] = new GeoPoint3(points[points.Count - 1].X, points[points.Count - 1].Y, Scaled(value)); } break;
                        case 11: fit.Add(new GeoPoint3(Scaled(value), 0.0, 0.0)); break;
                        case 21: if (fit.Count > 0) { fit[fit.Count - 1] = new GeoPoint3(fit[fit.Count - 1].X, Scaled(value), 0.0); } break;
                        case 31: if (fit.Count > 0) { fit[fit.Count - 1] = new GeoPoint3(fit[fit.Count - 1].X, fit[fit.Count - 1].Y, Scaled(value)); } break;
                        case 40: knots.Add(value); break;
                        case 41: weights.Add(value); break;
                    }
                }

                int flags = r.Int(70, 0);
                int degree = r.Int(71, points.Count == 0 ? 3 : 0);
                bool hasStart = TryDirection(r, 2, out GeoVector3 startTangent);
                bool hasEnd = TryDirection(r, 3, out GeoVector3 endTangent);

                if (points.Count == 0)
                {
                    if (fit.Count == 0)
                    {
                        Skip(r.Type, Incomplete);
                        return;
                    }

                    ReadFitSpline(r, fit, degree, hasStart && hasEnd && startTangent.Length > 0.0 && endTangent.Length > 0.0, startTangent, endTangent);
                    return;
                }

                if (weights.Count > 0 && weights.Count != points.Count)
                {
                    RefuseSpline(r, "whose weights are not one to each control point");
                    return;
                }

                // A spline is carried by any affine map by carrying its control points, its knots and weights kept.
                if (_place != null)
                {
                    points = points.ConvertAll(_place.Apply);
                }

                List<double> given = weights.Count > 0 ? weights : null;

                if (!TryClamped(degree, points, knots, given, out List<GeoPoint3> clampedPoints, out List<double> clampedKnots, out List<double> clampedWeights))
                {
                    if ((flags & 2) != 0)
                    {
                        _periodicSplines++;
                        Skip(r.Type, "periodic, its knots not clamped");
                    }
                    else
                    {
                        RefuseSpline(r, "that the library does not take: its degree, knots or weights do not make a spline");
                    }

                    return;
                }

                try
                {
                    Add(r, FlatInWorld(clampedPoints)
                        ? (object)new GeoSpline2(degree, clampedPoints.ConvertAll(Plan), clampedKnots, clampedWeights)
                        : new GeoSpline3(degree, clampedPoints, clampedKnots, clampedWeights));
                }
                catch (ArgumentException)
                {
                    RefuseSpline(r, "that the library does not take: its degree, knots or weights do not make a spline");
                }
            }

            private void RefuseSpline(Record r, string reason)
            {
                _refusedSplines++;
                Skip(r.Type, reason);
            }

            /// <summary>
            /// The spline through fit points: of the degree the file gives, or of one less than the points there are where
            /// they are too few for it and there are no tangents, as two points of a cubic are a straight line, said in a
            /// warning.
            /// </summary>
            private void ReadFitSpline(Record r, List<GeoPoint3> fit, int degree, bool tangents, GeoVector3 startTangent, GeoVector3 endTangent)
            {
                // Inside a block the spline is passed through the points where the block has them, and then placed.
                bool flat = _place == null && FlatInWorld(fit) && (!tangents || (startTangent.Z == 0.0 && endTangent.Z == 0.0));
                int distinct = 0;

                for (int i = 0; i < fit.Count; i++)
                {
                    if (i == 0 || !fit[i].IsEqualTo(fit[i - 1], _tolerance))
                    {
                        distinct++;
                    }
                }

                if (!tangents && distinct >= 2 && distinct <= degree)
                {
                    degree = distinct - 1;
                    _loweredFitSplines++;
                }

                try
                {
                    object spline;

                    if (flat)
                    {
                        var plan = fit.ConvertAll(Plan);
                        spline = tangents
                            ? GeoSpline2.Interpolate(plan, new GeoVector2(startTangent.X, startTangent.Y), new GeoVector2(endTangent.X, endTangent.Y), degree, _tolerance)
                            : GeoSpline2.Interpolate(plan, degree, _tolerance);
                    }
                    else
                    {
                        spline = tangents
                            ? GeoSpline3.Interpolate(fit, startTangent, endTangent, degree, _tolerance)
                            : GeoSpline3.Interpolate(fit, degree, _tolerance);
                    }

                    if (_place != null && !TryPlaceSpline((GeoSpline3)spline, out spline))
                    {
                        Skip(r.Type, Flattened);
                        return;
                    }

                    _fitSplines++;
                    Add(r, spline);
                }
                catch (ArgumentException)
                {
                    RefuseSpline(r, "whose fit points the library cannot pass a spline through");
                }
            }

            /// <summary>
            /// The control points, knots and weights of a spline with its knots clamped: as they are where they are, and
            /// where they are not, as a closed spline of AutoCAD's, the same curve over the span its knots of index degree
            /// and count give, by inserting each end of that span until it stands degree times, as Boehm's insertion does
            /// it in the homogeneous coordinates of a rational spline, and keeping the control points and knots of the span.
            /// False where the knots are not the count and order a spline's are.
            /// </summary>
            private static bool TryClamped(int degree, List<GeoPoint3> points, List<double> knots, List<double> weights, out List<GeoPoint3> clampedPoints, out List<double> clampedKnots, out List<double> clampedWeights)
            {
                clampedPoints = points;
                clampedKnots = knots;
                clampedWeights = weights;
                int n = points.Count;
                int p = degree;

                if (p < 1 || n <= p || knots.Count != n + p + 1)
                {
                    return true;
                }

                for (int i = 1; i < knots.Count; i++)
                {
                    if (!(knots[i] >= knots[i - 1]))
                    {
                        return true;
                    }
                }

                double start = knots[p];
                double end = knots[n];
                bool clampedAlready = true;

                for (int i = 0; i <= p; i++)
                {
                    clampedAlready &= knots[i].Equals(start) && knots[knots.Count - 1 - i].Equals(end);
                }

                if (clampedAlready || !(end > start))
                {
                    return true;
                }

                // In homogeneous coordinates: each point times its weight, and the weight.
                var h = new List<double[]>(n);

                for (int i = 0; i < n; i++)
                {
                    double w = weights == null ? 1.0 : weights[i];
                    h.Add(new[] { points[i].X * w, points[i].Y * w, points[i].Z * w, w });
                }

                var u = new List<double>(knots);

                if (!Raise(u, h, p, start, false) || !Raise(u, h, p, end, true))
                {
                    return false;
                }

                // A knot standing p times at j to j + p - 1 puts the curve on the control point before it, at j - 1; one
                // standing p + 1 times, as a clamped end does, on the one at j.
                int lastOfStart = u.LastIndexOf(start);
                int firstOfEnd = u.IndexOf(end);
                int from = lastOfStart - p;
                int to = firstOfEnd - 1;

                if (from < 0 || to >= h.Count || to < from + p)
                {
                    return false;
                }

                clampedPoints = new List<GeoPoint3>();
                clampedWeights = weights == null ? null : new List<double>();

                for (int i = from; i <= to; i++)
                {
                    double w = h[i][3];
                    clampedPoints.Add(new GeoPoint3(h[i][0] / w, h[i][1] / w, h[i][2] / w));
                    clampedWeights?.Add(w);
                }

                clampedKnots = new List<double>();

                for (int i = 0; i <= p; i++)
                {
                    clampedKnots.Add(start);
                }

                for (int i = lastOfStart + 1; i < firstOfEnd; i++)
                {
                    clampedKnots.Add(u[i]);
                }

                for (int i = 0; i <= p; i++)
                {
                    clampedKnots.Add(end);
                }

                return true;
            }

            /// <summary>
            /// Inserts a knot until it stands as many times as the degree, by Boehm's rule: at the start of the span in the
            /// span k where u[k] &lt;= knot &lt; u[k + 1], at its end in the one where u[k] &lt; knot &lt;= u[k + 1], each within the
            /// control points the curve has. False where there is no such span.
            /// </summary>
            private static bool Raise(List<double> u, List<double[]> h, int p, double knot, bool atEnd)
            {
                int times = p;

                foreach (double k in u)
                {
                    if (k.Equals(knot))
                    {
                        times--;
                    }
                }

                for (; times > 0; times--)
                {
                    int k = atEnd ? u.FindLastIndex(x => x < knot) : u.FindLastIndex(x => x <= knot);

                    if (k < p || k + 1 >= u.Count || k >= h.Count)
                    {
                        return false;
                    }

                    var inserted = new List<double[]>(h.Count + 1);

                    for (int i = 0; i <= h.Count; i++)
                    {
                        if (i <= k - p)
                        {
                            inserted.Add(h[i]);
                        }
                        else if (i > k)
                        {
                            inserted.Add(h[i - 1]);
                        }
                        else
                        {
                            double a = (knot - u[i]) / (u[i + p] - u[i]);
                            var q = new double[4];

                            for (int c = 0; c < 4; c++)
                            {
                                q[c] = (1.0 - a) * h[i - 1][c] + a * h[i][c];
                            }

                            inserted.Add(q);
                        }
                    }

                    h.Clear();
                    h.AddRange(inserted);
                    u.Insert(k + 1, knot);
                }

                return true;
            }

            #endregion

            #region Polylines

            /// <summary>
            /// Reads a LWPOLYLINE: its vertices (each 10, 20) and their bulges (42) in the plane of its extrusion, at its
            /// elevation (38) along it, closed where flag 1 is set.
            /// </summary>
            private void ReadLightweight(Record r)
            {
                double elevation = Scaled(r.Real(38, 0.0));
                var ocs = new Ocs(Extrusion(r));
                var points = new List<GeoPoint3>();
                var bulges = new List<double>();
                bool atVertex = false;

                for (int i = 0; i < r.Count; i++)
                {
                    int code = r.CodeAt(i);

                    if (code == 10)
                    {
                        points.Add(new GeoPoint3(Scaled(r.NumberAt(i)), 0.0, elevation));
                        bulges.Add(0.0);
                        atVertex = true;
                    }
                    else if (atVertex && code == 20)
                    {
                        points[points.Count - 1] = new GeoPoint3(points[points.Count - 1].X, Scaled(r.NumberAt(i)), elevation);
                    }
                    else if (atVertex && code == 42)
                    {
                        bulges[bulges.Count - 1] = r.NumberAt(i);
                    }
                }

                InPlane(r, ocs, points, bulges, (r.Int(70, 0) & 1) != 0);
            }

            /// <summary>
            /// Reads a POLYLINE of the plane or a 3D one with its vertices. A 2D one is in the plane of its extrusion at the
            /// polyline's elevation (its group 30, or the first vertex's height where it has none, as ezdxf reads it), with the
            /// bulge of the edge each vertex starts (42); a 3D one in world coordinates. Of a polyline fitted with a spline,
            /// the frame the spline was fitted to (vertices flagged 16) is left out and the spline drawn (flagged 8) kept, as
            /// AutoCAD draws it.
            /// </summary>
            private void ReadPolyline(Record r, List<Record> vertices, int flags)
            {
                bool threeD = (flags & 8) != 0;
                bool fitted = vertices.Exists(v => (v.Int(70, 0) & 8) != 0);
                var points = new List<GeoPoint3>();
                var bulges = new List<double>();

                foreach (Record v in vertices)
                {
                    if (fitted && (v.Int(70, 0) & 16) != 0)
                    {
                        continue;
                    }

                    if (!TryPoint(v, 0, out GeoPoint3 p))
                    {
                        continue;
                    }

                    points.Add(p);
                    bulges.Add(threeD ? 0.0 : v.Real(42, 0.0));
                }

                if (threeD)
                {
                    if (_place != null)
                    {
                        points = points.ConvertAll(_place.Apply);
                    }

                    Chain(r, points, bulges, (flags & 1) != 0, FlatInWorld(points), new GeoVector3(0.0, 0.0, 1.0));
                    return;
                }

                double elevation = r.Has(30) ? Scaled(r.Real(30, 0.0)) : points.Count > 0 ? points[0].Z : 0.0;

                for (int i = 0; i < points.Count; i++)
                {
                    points[i] = new GeoPoint3(points[i].X, points[i].Y, elevation);
                }

                InPlane(r, new Ocs(Extrusion(r)), points, bulges, (flags & 1) != 0);
            }

            /// <summary>
            /// Makes the chain or loop of a polyline in the plane of its extrusion: on the floor, asked for, a shape of the
            /// plane, mirrored where the extrusion is down, its bulges turned over with it; otherwise one of space.
            /// </summary>
            private void InPlane(Record r, Ocs ocs, List<GeoPoint3> points, List<double> bulges, bool closed)
            {
                if (_place != null)
                {
                    PlacedInPlane(r, new Frame(ocs, _place), points, bulges, closed);
                    return;
                }

                bool flat = FlatInPlane(ocs.Normal, points);

                if (!ocs.IsWorld)
                {
                    for (int i = 0; i < points.Count; i++)
                    {
                        points[i] = ocs.World(points[i]);
                    }

                    if (flat)
                    {
                        for (int i = 0; i < bulges.Count; i++)
                        {
                            bulges[i] = -bulges[i];
                        }
                    }
                }

                Chain(r, points, bulges, closed, flat, ocs.Normal);
            }

            /// <summary>
            /// Makes a chain or a loop of vertices, with arcs where an edge has a bulge: the bulge on a vertex belongs to the
            /// edge leaving it, and on the last vertex of an open chain to nothing. A vertex within the point tolerance of the
            /// one kept before it is dropped with the edge between them, the vertex kept taking the bulge of the edge running
            /// on, as the constructors of the chains drop one; a last vertex on the first of a loop goes with its edge back to
            /// the first. A loop of two vertices with an arc, a circle drawn as two halves, has its first arc halved at its
            /// middle, so that it is a loop of the three vertices a loop has; a loop that encloses no area, or in space does
            /// not lie in one plane, is an open chain that ends on its first vertex, and said in a warning.
            /// </summary>
            private void Chain(Record r, List<GeoPoint3> points, List<double> bulges, bool closed, bool flat, GeoVector3 normal)
            {
                var kept = new List<GeoPoint3>(points.Count);
                var keptBulges = new List<double>(points.Count);

                for (int i = 0; i < points.Count; i++)
                {
                    if (kept.Count > 0 && kept[kept.Count - 1].IsEqualTo(points[i], _tolerance))
                    {
                        keptBulges[keptBulges.Count - 1] = bulges[i];
                        continue;
                    }

                    kept.Add(points[i]);
                    keptBulges.Add(bulges[i]);
                }

                if (closed)
                {
                    while (kept.Count > 1 && kept[kept.Count - 1].IsEqualTo(kept[0], _tolerance))
                    {
                        kept.RemoveAt(kept.Count - 1);
                        keptBulges.RemoveAt(keptBulges.Count - 1);
                    }
                }

                if (kept.Count < 2)
                {
                    Skip(r.Type, "with fewer than two distinct vertices");
                    return;
                }

                if (!closed)
                {
                    keptBulges[keptBulges.Count - 1] = 0.0;
                }
                else if (kept.Count == 2)
                {
                    int arc = keptBulges[0] != 0.0 ? 0 : keptBulges[1] != 0.0 ? 1 : -1;

                    if (arc < 0)
                    {
                        OpenLoop(kept, keptBulges);
                        closed = false;
                    }
                    else
                    {
                        Halve(kept, keptBulges, arc, flat, normal);
                    }
                }

                bool arcs = keptBulges.Exists(b => b != 0.0);
                var normals = keptBulges.ConvertAll(b => b != 0.0 ? (flat ? new GeoVector3(0.0, 0.0, 1.0) : normal) : default(GeoVector3));

                if (closed && TryLoop(kept, keptBulges, normals, flat, arcs, out object loop))
                {
                    Add(r, loop);
                    return;
                }

                if (closed)
                {
                    OpenLoop(kept, keptBulges);
                    normals.Add(default(GeoVector3));
                }

                if (flat)
                {
                    GeoPoint2[] plan = kept.ConvertAll(Plan).ToArray();

                    if (!arcs)
                    {
                        Add(r, new GeoPolyline2(plan, plan.Length));
                        return;
                    }

                    var edges = new GeoEdge2[plan.Length - 1];

                    for (int i = 0; i < edges.Length; i++)
                    {
                        edges[i] = new GeoEdge2(plan[i], plan[i + 1], keptBulges[i]);
                    }

                    Add(r, GeoPolylineArc2.FromEdges(edges, _tolerance));
                    return;
                }

                Add(r, arcs ? (object)new GeoPolylineArc3(kept, keptBulges, normals, _tolerance) : new GeoPolyline3(kept.ToArray(), true));
            }

            /// <summary>
            /// The loop of the vertices, where it encloses an area: a polygon whose corners are not on one line and, in space,
            /// lie in one plane within the planar tolerance, as <see cref="GeoPolygon3"/> takes them; a loop with arcs whose
            /// area is above the vector tolerance read as an area, as a polygon's is.
            /// </summary>
            private bool TryLoop(List<GeoPoint3> kept, List<double> keptBulges, List<GeoVector3> normals, bool flat, bool arcs, out object loop)
            {
                loop = null;

                try
                {
                    if (flat)
                    {
                        GeoPoint2[] plan = kept.ConvertAll(Plan).ToArray();

                        if (!arcs)
                        {
                            _ = new GeoPolygon3(kept, _tolerance);
                            loop = new GeoPolygon2(plan, plan.Length);
                            return true;
                        }

                        GeoPolygonArc2 round = GeoPolygonArc2.FromValidated(plan, keptBulges.ToArray());

                        if (round.Area > _tolerance.EqualVector)
                        {
                            loop = round;
                            return true;
                        }

                        return false;
                    }

                    if (!arcs)
                    {
                        loop = new GeoPolygon3(kept, _tolerance);
                        return true;
                    }

                    var space = new GeoPolygonArc3(kept, keptBulges, normals, _tolerance);

                    if (space.Area > _tolerance.EqualVector)
                    {
                        loop = space;
                        return true;
                    }

                    return false;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            /// <summary>
            /// Cuts an arc edge of a loop of two vertices at its middle: the middle is a vertex, and each half has the bulge
            /// of half the angle, b / (1 + root(1 + b^2)) of the bulge b.
            /// </summary>
            private void Halve(List<GeoPoint3> kept, List<double> keptBulges, int edge, bool flat, GeoVector3 normal)
            {
                GeoPoint3 start = kept[edge];
                GeoPoint3 end = kept[(edge + 1) % 2];
                double bulge = keptBulges[edge];
                GeoPoint3 middle;

                if (flat)
                {
                    GeoPoint2 m = new GeoEdge2(Plan(start), Plan(end), bulge).MidPoint;
                    middle = new GeoPoint3(m.X, m.Y, 0.0);
                }
                else
                {
                    middle = new GeoEdge3(start, end, bulge, normal, _tolerance).MidPoint;
                }

                double half = bulge / (1.0 + Math.Sqrt(1.0 + bulge * bulge));
                keptBulges[edge] = half;
                kept.Insert(edge + 1, middle);
                keptBulges.Insert(edge + 1, half);
            }

            /// <summary>A loop made an open chain that ends on its first vertex, the closing edge keeping its bulge.</summary>
            private void OpenLoop(List<GeoPoint3> kept, List<double> keptBulges)
            {
                _openedLoops++;
                kept.Add(kept[0]);
                keptBulges.Add(0.0);
            }

            #endregion

            #region Leaders

            /// <summary>
            /// Reads a LEADER as its curve, as AutoCAD's <c>IntersectWith</c> meets it: the chain of its vertices (each 10, 20,
            /// 30) in world coordinates, a vertex within the point tolerance of the one kept before it left out; splined (72 of 1),
            /// the spline <see cref="DxfLeader.Explode"/> fits through them, of degree 3, leaving along its first segment and
            /// reaching along its last, its parameter from 0 to 1. Inside a block the spline is fitted through the vertices where
            /// the block has them, and then placed. The arrowhead and the hook line to the annotation, which AutoCAD works out when
            /// it draws them, are not read.
            /// </summary>
            private void ReadLeader(Record r)
            {
                var vertices = new List<GeoPoint3>();
                double[] point = null;

                for (int i = 0; i < r.Count; i++)
                {
                    int code = r.CodeAt(i);

                    if (code == 10)
                    {
                        point = new[] { Scaled(r.NumberAt(i)), 0.0, 0.0 };
                        vertices.Add(default(GeoPoint3));
                    }
                    else if ((code == 20 || code == 30) && point != null)
                    {
                        point[code / 10 - 1] = Scaled(r.NumberAt(i));
                    }
                    else
                    {
                        continue;
                    }

                    vertices[vertices.Count - 1] = new GeoPoint3(point[0], point[1], point[2]);
                }

                double tolerance = _tolerance.EqualPoint;
                string why = null;

                if (r.Int(72, 0) != 0 ? !AddFitted(r, DxfLeader.FittedCurve(vertices, tolerance), p => p, out why) : !AddChain(r, vertices, p => p))
                {
                    Skip(r.Type, why ?? "with fewer than two distinct vertices");
                }
            }

            /// <summary>
            /// Reads a MULTILEADER as the lines of its leaders, as AutoCAD's <c>IntersectWith</c> meets it: each leader line the
            /// chain from its first vertex, where its arrow points, through its vertices and the last point of its leader to the end
            /// of its dogleg, as <see cref="DxfMLeader.Explode"/> draws it, or, splined, the spline fitted through those points, as
            /// <see cref="DxfLeader"/>'s is (<see cref="DxfMLeader"/>). Its words or block and its arrowheads are not read.
            /// </summary>
            private void ReadMLeader(Record r)
            {
                var groups = new List<KeyValuePair<int, string>>(r.Count);

                for (int i = 0; i < r.Count; i++)
                {
                    groups.Add(new KeyValuePair<int, string>(r.CodeAt(i), r.ValueAt(i)));
                }

                GeoPoint3 Scale(GeoPoint3 p) => _scale == 1.0 ? p : new GeoPoint3(Scaled(p.X), Scaled(p.Y), Scaled(p.Z));
                bool any = false;
                string why = null;

                foreach (DxfMLeader.LinePath line in DxfMLeader.LinesOf(groups, _tolerance.EqualPoint / _scale))
                {
                    any |= line.Curve != null ? AddFitted(r, line.Curve, Scale, out why) : AddChain(r, line.Points, Scale);
                }

                if (!any)
                {
                    Skip(r.Type, why ?? "with no leader line of two distinct points");
                }
            }

            /// <summary>
            /// Adds a chain of points of the world, each taken to millimetres where asked by the map given and placed, a point within
            /// the point tolerance of the one kept before it left out: of the plane where it lies on the floor and that is asked for.
            /// False where fewer than two points are left.
            /// </summary>
            private bool AddChain(Record r, List<GeoPoint3> points, Func<GeoPoint3, GeoPoint3> scale)
            {
                var kept = new List<GeoPoint3>(points.Count);

                foreach (GeoPoint3 point in points)
                {
                    GeoPoint3 placed = Place(scale(point));

                    if (kept.Count == 0 || kept[kept.Count - 1].DistanceTo(placed) > _tolerance.EqualPoint)
                    {
                        kept.Add(placed);
                    }
                }

                if (kept.Count < 2)
                {
                    return false;
                }

                Add(r, FlatInWorld(kept) ? (object)new GeoPolyline2(kept.ConvertAll(Plan).ToArray(), kept.Count) : new GeoPolyline3(kept.ToArray(), true));
                return true;
            }

            /// <summary>
            /// Adds a spline fitted through points of the world, its control points taken to millimetres where asked by the map given
            /// and placed, as a placing carries a spline: of the plane where it lies on the floor and that is asked for. False, with
            /// the reason, where there is no curve (its points all one) or the library does not take the spline placed.
            /// </summary>
            private bool AddFitted(Record r, NurbsCurve curve, Func<GeoPoint3, GeoPoint3> scale, out string why)
            {
                why = null;

                if (curve == null)
                {
                    return false;
                }

                var control = new List<GeoPoint3>(curve.Count);

                for (int i = 0; i < curve.Count; i++)
                {
                    control.Add(Place(scale(new GeoPoint3(curve.Points[3 * i], curve.Points[3 * i + 1], curve.Points[3 * i + 2]))));
                }

                try
                {
                    Add(r, FlatInWorld(control)
                        ? (object)new GeoSpline2(curve.Degree, control.ConvertAll(Plan), curve.Knots, null)
                        : new GeoSpline3(curve.Degree, control, curve.Knots, null));
                    return true;
                }
                catch (ArgumentException)
                {
                    why = _place != null ? Flattened : "whose vertices the library cannot pass a spline through";
                    return false;
                }
            }

            #endregion

            #region Meshes

            /// <summary>
            /// Reads a polyface mesh: its vertices (flag 64) and its faces (flag 128 alone), each naming three or four of the
            /// vertices from one (71 to 74, the sign saying whether the edge shows). The faces make a body
            /// (<see cref="GeoSolid3"/>), closed or not as they are; a face out of flat is its triangles, as
            /// <see cref="GeoFace3.FromLoops(IEnumerable{GeoPoint3}, IEnumerable{IEnumerable{GeoPoint3}}, Tolerance)"/> makes them.
            /// </summary>
            private void ReadPolyface(Record r, List<Record> vertices)
            {
                var points = new List<GeoPoint3>();

                foreach (Record v in vertices)
                {
                    if ((v.Int(70, 0) & 64) != 0 && TryPoint(v, 0, out GeoPoint3 p))
                    {
                        points.Add(Place(p));
                    }
                }

                var loops = new List<GeoPoint3[]>();

                foreach (Record v in vertices)
                {
                    int vflags = v.Int(70, 0);

                    if ((vflags & 128) == 0 || (vflags & 64) != 0)
                    {
                        continue;
                    }

                    var corners = new List<GeoPoint3>(4);
                    bool named = true;
                    int last = -1;

                    foreach (int code in new[] { 71, 72, 73, 74 })
                    {
                        int n = v.Int(code, 0);

                        if (n == 0)
                        {
                            break;
                        }

                        int index = (n == int.MinValue ? int.MaxValue : Math.Abs(n)) - 1;

                        if (index >= points.Count)
                        {
                            named = false;
                            break;
                        }

                        if (index != last)
                        {
                            corners.Add(points[index]);
                            last = index;
                        }
                    }

                    if (!named || corners.Count < 3)
                    {
                        _droppedFaces++;
                        continue;
                    }

                    loops.Add(corners.ToArray());
                }

                Mesh(r, loops);
            }

            /// <summary>
            /// Reads a polygon mesh: a grid of M by N vertices (71, 72), row after row, closed in M where flag 1 is set and in
            /// N where flag 32 is, each cell a face of four corners.
            /// </summary>
            private void ReadPolygonMesh(Record r, List<Record> vertices, int flags)
            {
                int m = r.Int(71, 0);
                int n = r.Int(72, 0);
                var points = new List<GeoPoint3>();

                foreach (Record v in vertices)
                {
                    if (TryPoint(v, 0, out GeoPoint3 p))
                    {
                        points.Add(Place(p));
                    }
                }

                if (m < 2 || n < 2 || points.Count < (long)m * n)
                {
                    Skip(r.Type, "a polygon mesh with fewer vertices than its rows and columns ask for");
                    return;
                }

                bool closedM = (flags & 1) != 0;
                bool closedN = (flags & 32) != 0;
                var loops = new List<GeoPoint3[]>();

                for (int i = 0; i < (closedM ? m : m - 1); i++)
                {
                    for (int j = 0; j < (closedN ? n : n - 1); j++)
                    {
                        int i1 = (i + 1) % m;
                        int j1 = (j + 1) % n;
                        loops.Add(new[] { points[i * n + j], points[i * n + j1], points[i1 * n + j1], points[i1 * n + j] });
                    }
                }

                Mesh(r, loops);
            }

            /// <summary>
            /// The faces of a mesh as one body, or, fewer than the four a body needs, each as an entity of its own. A mesh a
            /// placement mirrors has the corners of each face taken in the other order, so that a body that faces outwards in
            /// its block faces outwards where it is placed.
            /// </summary>
            private void Mesh(Record r, List<GeoPoint3[]> loops)
            {
                var faces = new List<GeoFace3>();

                foreach (GeoPoint3[] loop in loops)
                {
                    if (_place != null && _place.Mirrors)
                    {
                        Array.Reverse(loop);
                    }

                    GeoFace3[] made = GeoFace3.FromLoops(loop, null, _tolerance);

                    if (made.Length == 0)
                    {
                        _droppedFaces++;
                    }

                    faces.AddRange(made);
                }

                if (faces.Count == 0)
                {
                    Skip(r.Type, "a mesh with no face that encloses an area");
                    return;
                }

                if (faces.Count >= 4)
                {
                    Add(r, new GeoSolid3(faces));
                    return;
                }

                _looseFaces++;

                foreach (GeoFace3 face in faces)
                {
                    Add(r, face);
                }
            }

            #endregion

            #region Faces and filled areas

            /// <summary>
            /// Reads a 3DFACE, its corners in world coordinates (10 to 13): the fourth the same as the third to the bit is a
            /// triangle, as are four corners of which two neighbours are the same to the bit; four in one plane within the
            /// planar tolerance are a face; four out of one plane are two triangles, cut along the shorter diagonal.
            /// </summary>
            private void ReadFace(Record r)
            {
                if (!TryPoint(r, 0, out GeoPoint3 a) || !TryPoint(r, 1, out GeoPoint3 b) || !TryPoint(r, 2, out GeoPoint3 c))
                {
                    Skip(r.Type, Incomplete);
                    return;
                }

                GeoPoint3 d = TryPoint(r, 3, out GeoPoint3 fourth) ? fourth : c;

                if (_place != null)
                {
                    bool third = SameBits(c, d);
                    a = _place.Apply(a);
                    b = _place.Apply(b);
                    c = _place.Apply(c);
                    d = third ? c : _place.Apply(d);
                }

                var corners = new List<GeoPoint3> { a, b, c, d };

                for (int i = corners.Count - 1; i >= 1; i--)
                {
                    if (SameBits(corners[i], corners[i - 1]))
                    {
                        corners.RemoveAt(i);
                    }
                }

                while (corners.Count > 1 && SameBits(corners[0], corners[corners.Count - 1]))
                {
                    corners.RemoveAt(corners.Count - 1);
                }

                if (corners.Count < 3)
                {
                    Skip(r.Type, "with fewer than three distinct corners");
                    return;
                }

                // The edges the file hides (70: 1, 2, 4 and 8 for the first to the fourth) are kept where the face is read
                // whole on its corners as they stand: a triangle written with its third corner twice, or a face of four.
                int hidden = r.Int(70, 0) & 15;

                if (corners.Count == 3)
                {
                    bool asWritten = SameBits(c, d) && !SameBits(a, b) && !SameBits(b, c) && !SameBits(a, c);
                    Add(r, new GeoTriangle3(corners[0], corners[1], corners[2]), asWritten ? hidden : 0);
                    return;
                }

                try
                {
                    var face = new GeoFace3(new GeoPolygon3(corners, _tolerance), null, _tolerance);
                    Add(r, face, face.Boundary.VertexCount == 4 ? hidden : 0);
                    return;
                }
                catch (ArgumentException)
                {
                    // Out of one plane, or enclosing no area: cut into two triangles below.
                }

                bool acrossAc = corners[0].DistanceTo(corners[2]) <= corners[1].DistanceTo(corners[3]);
                GeoTriangle3 first = acrossAc ? new GeoTriangle3(corners[0], corners[1], corners[2]) : new GeoTriangle3(corners[0], corners[1], corners[3]);
                GeoTriangle3 second = acrossAc ? new GeoTriangle3(corners[0], corners[2], corners[3]) : new GeoTriangle3(corners[1], corners[2], corners[3]);

                if (first.IsDegenerate(_tolerance) && second.IsDegenerate(_tolerance))
                {
                    Skip(r.Type, "enclosing no area");
                    return;
                }

                Add(r, first);
                Add(r, second);
            }

            private static bool SameBits(GeoPoint3 p, GeoPoint3 q)
                => BitConverter.DoubleToInt64Bits(p.X + 0.0) == BitConverter.DoubleToInt64Bits(q.X + 0.0)
                && BitConverter.DoubleToInt64Bits(p.Y + 0.0) == BitConverter.DoubleToInt64Bits(q.Y + 0.0)
                && BitConverter.DoubleToInt64Bits(p.Z + 0.0) == BitConverter.DoubleToInt64Bits(q.Z + 0.0);

            /// <summary>
            /// Reads a SOLID or a TRACE, a filled area of three or four corners in the plane of its extrusion (10 to 13, each
            /// at its own height or at that of the first), taken in DXF's order 1, 2, 4, 3; the fourth the same as the third
            /// to the bit is a triangle.
            /// </summary>
            private void ReadSolid(Record r)
            {
                if (!TryPoint(r, 0, out GeoPoint3 p1) || !TryPoint(r, 1, out GeoPoint3 p2) || !TryPoint(r, 2, out GeoPoint3 p3))
                {
                    Skip(r.Type, Incomplete);
                    return;
                }

                GeoPoint3 p4 = TryPoint(r, 3, out GeoPoint3 fourth) ? fourth : p3;
                GeoPoint3 Height(Record record, int n, GeoPoint3 p) => record.Has(30 + n) ? p : new GeoPoint3(p.X, p.Y, p1.Z);

                var corners = new List<GeoPoint3> { p1, Height(r, 1, p2) };

                if (!SameBits(p3, p4))
                {
                    corners.Add(Height(r, 3, p4));
                }

                corners.Add(Height(r, 2, p3));

                var ocs = new Ocs(Extrusion(r));
                bool flat;

                if (_place != null)
                {
                    var frame = new Frame(ocs, _place);
                    corners = corners.ConvertAll(frame.World);
                    flat = _planAs2D && frame.OnFloor && corners.TrueForAll(p => p.Z == 0.0);
                }
                else
                {
                    flat = FlatInPlane(ocs.Normal, corners);

                    if (!ocs.IsWorld)
                    {
                        for (int i = 0; i < corners.Count; i++)
                        {
                            corners[i] = ocs.World(corners[i]);
                        }
                    }
                }

                GeoPolygon3 polygon;

                try
                {
                    polygon = new GeoPolygon3(corners, _tolerance);
                }
                catch (ArgumentException)
                {
                    Skip(r.Type, "enclosing no area");
                    return;
                }

                if (!flat)
                {
                    Add(r, polygon);
                    return;
                }

                var plan = new GeoPoint2[polygon.VertexCount];

                for (int i = 0; i < plan.Length; i++)
                {
                    plan[i] = Plan(polygon.Vertices[i]);
                }

                Add(r, new GeoPolygon2(plan, plan.Length));
            }

            #endregion
        }
    }
}
