using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// The checks the diagnostics can run; each is a toggle and they combine.
    /// </summary>
    [Flags]
    public enum DiagnosticChecks
    {
        /// <summary>
        /// No check.
        /// </summary>
        None = 0,

        /// <summary>
        /// The edges of bodies and meshes: used by one face (open), by three or more (non-manifold), or run the same way by the two faces
        /// meeting on them (inconsistent winding).
        /// </summary>
        Edges = 1,

        /// <summary>
        /// What the library says of a body: <c>Validate()</c>, <c>IsClosed()</c>, the sign of the volume.
        /// </summary>
        Report = 2,

        /// <summary>
        /// The normal of each face of a body, a mesh or a flat shape in space, drawn as an arrow.
        /// </summary>
        Normals = 4,

        /// <summary>
        /// Faces with a corner further off their plane than the planar tolerance of the scene.
        /// </summary>
        Flatness = 8,

        /// <summary>
        /// Loops that cross or touch themselves.
        /// </summary>
        Crossings = 16,

        /// <summary>
        /// Which way each loop runs, drawn as arrows.
        /// </summary>
        Winding = 32,

        /// <summary>
        /// Vertices closer to one another than the point tolerance of the scene.
        /// </summary>
        Duplicates = 64,

        /// <summary>
        /// Vertices where a loop turns back on itself, by more than 179.9 degrees.
        /// </summary>
        Needles = 128,

        /// <summary>
        /// Triangles, faces and loops with next to no area for their length.
        /// </summary>
        Slivers = 256,

        /// <summary>
        /// The gap between two items, as the library measures it, with a magnifier.
        /// </summary>
        Gap = 512,

        /// <summary>
        /// A sphere of the point tolerance round each vertex.
        /// </summary>
        Spheres = 1024,

        /// <summary>
        /// The items of a layer coloured one by one, with their number and area.
        /// </summary>
        Pieces = 2048,

        /// <summary>
        /// Every check.
        /// </summary>
        All = 4095,
    }

    /// <summary>
    /// The kinds of thing a diagnostic finds.
    /// </summary>
    public enum DiagnosticKind
    {
        /// <summary>
        /// An edge used by one face.
        /// </summary>
        OpenEdge,

        /// <summary>
        /// An edge used by three or more faces.
        /// </summary>
        NonManifoldEdge,

        /// <summary>
        /// An edge the two faces meeting on it both run the same way.
        /// </summary>
        InconsistentWinding,

        /// <summary>
        /// An edge used once that other faces cover along its whole length with edges of their own, a corner of each standing on it: the
        /// library takes the faces as meeting there, so it is a fact and not an open edge.
        /// </summary>
        TJunction,

        /// <summary>
        /// What the library says of a whole body.
        /// </summary>
        Report,

        /// <summary>
        /// A face not flat within the planar tolerance.
        /// </summary>
        NotFlat,

        /// <summary>
        /// The face normals drawn for an item.
        /// </summary>
        Normals,

        /// <summary>
        /// A loop crossing or touching itself.
        /// </summary>
        SelfCrossing,

        /// <summary>
        /// Which way a loop runs.
        /// </summary>
        LoopWinding,

        /// <summary>
        /// Two vertices closer than the point tolerance.
        /// </summary>
        DuplicateVertex,

        /// <summary>
        /// A vertex where the loop turns back on itself.
        /// </summary>
        Needle,

        /// <summary>
        /// A triangle, face or loop with next to no area for its length.
        /// </summary>
        Sliver,

        /// <summary>
        /// The gap between two items.
        /// </summary>
        Gap,

        /// <summary>
        /// The tolerance spheres drawn.
        /// </summary>
        ToleranceSpheres,

        /// <summary>
        /// One piece of a layer: its number and area.
        /// </summary>
        Piece,

        /// <summary>
        /// Something to know about the run: a check that could not be made, a cap that was reached.
        /// </summary>
        Note,
    }

    /// <summary>
    /// One thing a diagnostic found, with where it is and the number it is about.
    /// </summary>
    public sealed class DiagnosticFinding
    {
        internal DiagnosticFinding(DiagnosticChecks check, DiagnosticKind kind, bool isFault, int itemIndex, string itemName, string message, string source, Vec3 at, double size, double spotRadius)
        {
            Check = check;
            Kind = kind;
            IsFault = isFault;
            ItemIndex = itemIndex;
            ItemName = itemName;
            Message = message;
            Source = source;
            At = at;
            Size = size;
            SpotRadius = spotRadius;
        }

        /// <summary>
        /// What the finding draws, kept with it so that a finding left out for being one too many leaves nothing of itself on the view.
        /// </summary>
        internal readonly DiagnosticOverlay Drawing = new DiagnosticOverlay();

        /// <summary>
        /// Gets the number of the finding in its report, from 1, in the order the report lists them.
        /// </summary>
        public int Number { get; internal set; }

        /// <summary>
        /// Gets the check that found it.
        /// </summary>
        public DiagnosticChecks Check { get; }

        /// <summary>
        /// Gets what it is.
        /// </summary>
        public DiagnosticKind Kind { get; }

        /// <summary>
        /// Gets whether it is a fault, as against a fact to know: an open edge of a body is a fault, the winding of a loop is a fact.
        /// </summary>
        public bool IsFault { get; }

        /// <summary>
        /// Gets the index in <see cref="Scene.Items"/> of the item it is on; for a gap, the first of the two.
        /// </summary>
        public int ItemIndex { get; }

        /// <summary>
        /// Gets the name of that item.
        /// </summary>
        public string ItemName { get; }

        /// <summary>
        /// Gets what it says, with its numbers to ten significant digits.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets what gave the reading: the library member, when the library has one, or the viewer's own check.
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Gets where it is.
        /// </summary>
        public Vec3 At { get; }

        /// <summary>
        /// Gets the number it is about: a length, an area, a distance; nought where it has none.
        /// </summary>
        public double Size { get; }

        /// <summary>
        /// Gets how far round <see cref="At"/> to look when zooming to the spot.
        /// </summary>
        public double SpotRadius { get; }

        /// <summary>
        /// Gets the box to zoom to: the spot, and no less than a hundred times the tolerance of the scene wide.
        /// </summary>
        public Bounds3 Spot
        {
            get
            {
                var r = new Vec3(SpotRadius, SpotRadius, SpotRadius);
                return new Bounds3(At - r, At + r);
            }
        }

        /// <summary>
        /// Says the finding on one line.
        /// </summary>
        public override string ToString()
            => "#" + Number.ToString(CultureInfo.InvariantCulture) + " " + (ItemName ?? "scene") + ": " + Kind + " - " + Message + " [" + Source + "]";

        internal static int Compare(DiagnosticFinding a, DiagnosticFinding b)
        {
            int c = a.Check.CompareTo(b.Check);

            if (c != 0)
            {
                return c;
            }

            c = a.Kind.CompareTo(b.Kind);

            if (c != 0)
            {
                return c;
            }

            c = a.At.X.CompareTo(b.At.X);

            if (c != 0)
            {
                return c;
            }

            c = a.At.Y.CompareTo(b.At.Y);

            if (c != 0)
            {
                return c;
            }

            c = a.At.Z.CompareTo(b.At.Z);

            if (c != 0)
            {
                return c;
            }

            c = a.Size.CompareTo(b.Size);
            return c != 0 ? c : string.CompareOrdinal(a.Message, b.Message);
        }
    }

    /// <summary>
    /// A line or a chain to draw over the scene in a colour of its own.
    /// </summary>
    public sealed class DiagnosticStroke
    {
        internal DiagnosticStroke(DrawPolyline line, string color, double thickness)
        {
            Line = line;
            Color = color;
            Thickness = thickness;
        }

        /// <summary>
        /// Gets the chain.
        /// </summary>
        public DrawPolyline Line { get; }

        /// <summary>
        /// Gets the colour as #RRGGBB.
        /// </summary>
        public string Color { get; }

        /// <summary>
        /// Gets the thickness in pixels.
        /// </summary>
        public double Thickness { get; }
    }

    /// <summary>
    /// A dot to draw over the scene in a colour of its own, a fixed number of pixels across at any zoom.
    /// </summary>
    public sealed class DiagnosticDot
    {
        internal DiagnosticDot(Vec3 at, string color, double size)
        {
            At = at;
            Color = color;
            Size = size;
        }

        /// <summary>
        /// Gets where it is.
        /// </summary>
        public Vec3 At { get; }

        /// <summary>
        /// Gets the colour as #RRGGBB.
        /// </summary>
        public string Color { get; }

        /// <summary>
        /// Gets the size in pixels.
        /// </summary>
        public double Size { get; }
    }

    /// <summary>
    /// What the diagnostics draw over the scene, in double precision world coordinates: the strokes, the dots and the labels.
    /// </summary>
    public sealed class DiagnosticOverlay
    {
        /// <summary>
        /// Gets the chains, in the order the checks drew them.
        /// </summary>
        public List<DiagnosticStroke> Strokes { get; } = new List<DiagnosticStroke>();

        /// <summary>
        /// Gets the dots.
        /// </summary>
        public List<DiagnosticDot> Dots { get; } = new List<DiagnosticDot>();

        /// <summary>
        /// Gets the labels.
        /// </summary>
        public List<DrawLabel> Labels { get; } = new List<DrawLabel>();

        /// <summary>
        /// Gets whether nothing is drawn.
        /// </summary>
        public bool IsEmpty => Strokes.Count == 0 && Dots.Count == 0 && Labels.Count == 0;

        internal void Line(string color, double thickness, params Vec3[] points) => Strokes.Add(new DiagnosticStroke(new DrawPolyline(points, false), color, thickness));

        internal void Loop(string color, double thickness, Vec3[] points) => Strokes.Add(new DiagnosticStroke(new DrawPolyline(points, true), color, thickness));
    }

    /// <summary>
    /// The colours the diagnostics draw in.
    /// </summary>
    public static class DiagnosticColors
    {
        /// <summary>
        /// Open edges, used by one face.
        /// </summary>
        public const string Open = "#E02020";

        /// <summary>
        /// Non-manifold edges, used by three or more faces.
        /// </summary>
        public const string NonManifold = "#D000D0";

        /// <summary>
        /// Edges the two faces on them run the same way.
        /// </summary>
        public const string Winding = "#FF8C00";

        /// <summary>
        /// T-junctions, where the library takes faces as meeting along an edge.
        /// </summary>
        public const string TJunction = "#9A9A00";

        /// <summary>
        /// Crossings of a loop with itself.
        /// </summary>
        public const string Crossing = "#0070E0";

        /// <summary>
        /// Duplicate vertices.
        /// </summary>
        public const string Duplicate = "#E0A000";

        /// <summary>
        /// Needle spikes.
        /// </summary>
        public const string Needle = "#C00060";

        /// <summary>
        /// Slivers.
        /// </summary>
        public const string Sliver = "#8000C0";

        /// <summary>
        /// Faces not flat.
        /// </summary>
        public const string NotFlat = "#B04000";

        /// <summary>
        /// Face normals and the direction of loops.
        /// </summary>
        public const string Direction = "#008060";

        /// <summary>
        /// Tolerance spheres.
        /// </summary>
        public const string Sphere = "#6A5ACD";

        /// <summary>
        /// The gap itself and its magnified picture.
        /// </summary>
        public const string Gap = "#D0021B";

        /// <summary>
        /// The marker on the finding that was clicked.
        /// </summary>
        public const string Marker = "#00A86B";
    }

    /// <summary>
    /// The limits and switches of a diagnostic run.
    /// </summary>
    public sealed class DiagnosticOptions
    {
        /// <summary>
        /// Gets or sets the checks to run; by default every check but the normals, the winding, the gap, the spheres and the pieces, which
        /// draw a great deal or need a particular selection.
        /// </summary>
        public DiagnosticChecks Checks { get; set; } = DiagnosticChecks.Edges | DiagnosticChecks.Report | DiagnosticChecks.Flatness | DiagnosticChecks.Crossings
            | DiagnosticChecks.Duplicates | DiagnosticChecks.Needles | DiagnosticChecks.Slivers;

        /// <summary>
        /// Gets or sets whether the library is asked first, as it is by default. With it off the viewer's own checks alone are made, which
        /// is how the two are compared.
        /// </summary>
        public bool UseLibrary { get; set; } = true;

        /// <summary>
        /// Gets or sets the most findings of one kind kept for one item; 200 by default. The rest are counted in a note.
        /// </summary>
        public int MaxFindingsPerKind { get; set; } = 200;

        /// <summary>
        /// Gets or sets the most tolerance spheres drawn; 200 by default.
        /// </summary>
        public int MaxSpheres { get; set; } = 200;
    }

    /// <summary>
    /// What the library measured between two items, kept for the magnifier.
    /// </summary>
    public sealed class GapReading
    {
        internal GapReading(int first, int second, double distance, Vec3 from, Vec3 to, string source)
        {
            First = first;
            Second = second;
            Distance = distance;
            From = from;
            To = to;
            Source = source;
        }

        /// <summary>
        /// Gets the index of the first item.
        /// </summary>
        public int First { get; }

        /// <summary>
        /// Gets the index of the second item.
        /// </summary>
        public int Second { get; }

        /// <summary>
        /// Gets the distance the library gave.
        /// </summary>
        public double Distance { get; }

        /// <summary>
        /// Gets where the shortest segment starts, on the first item.
        /// </summary>
        public Vec3 From { get; }

        /// <summary>
        /// Gets where it ends, on the second.
        /// </summary>
        public Vec3 To { get; }

        /// <summary>
        /// Gets the library members that gave it.
        /// </summary>
        public string Source { get; }
    }

    /// <summary>
    /// What a diagnostic run found: the findings in a fixed order, and what is to be drawn.
    /// </summary>
    public sealed class DiagnosticReport
    {
        internal DiagnosticReport(IReadOnlyList<DiagnosticFinding> findings, DiagnosticOverlay overlay, IReadOnlyList<int> items, GapReading gap)
        {
            Findings = findings;
            Overlay = overlay;
            Items = items;
            Gap = gap;
        }

        /// <summary>
        /// Gets the findings, numbered from 1: item by item in the order of the scene, and within an item by check, kind, place, size and
        /// text.
        /// </summary>
        public IReadOnlyList<DiagnosticFinding> Findings { get; }

        /// <summary>
        /// Gets what is drawn over the scene.
        /// </summary>
        public DiagnosticOverlay Overlay { get; }

        /// <summary>
        /// Gets the items that were checked, in the order of the scene.
        /// </summary>
        public IReadOnlyList<int> Items { get; }

        /// <summary>
        /// Gets the gap the library measured, or null when the gap check did not run or the library gave none.
        /// </summary>
        public GapReading Gap { get; }

        /// <summary>
        /// Gets how many findings are faults.
        /// </summary>
        public int FaultCount
        {
            get
            {
                int n = 0;

                foreach (DiagnosticFinding finding in Findings)
                {
                    if (finding.IsFault)
                    {
                        n++;
                    }
                }

                return n;
            }
        }
    }

    /// <summary>
    /// The names the checks go by in a command and a pane.
    /// </summary>
    public static class DiagnosticNames
    {
        private static readonly KeyValuePair<string, DiagnosticChecks>[] _names =
        {
            new KeyValuePair<string, DiagnosticChecks>("edges", DiagnosticChecks.Edges),
            new KeyValuePair<string, DiagnosticChecks>("report", DiagnosticChecks.Report),
            new KeyValuePair<string, DiagnosticChecks>("normals", DiagnosticChecks.Normals),
            new KeyValuePair<string, DiagnosticChecks>("flat", DiagnosticChecks.Flatness),
            new KeyValuePair<string, DiagnosticChecks>("crossings", DiagnosticChecks.Crossings),
            new KeyValuePair<string, DiagnosticChecks>("winding", DiagnosticChecks.Winding),
            new KeyValuePair<string, DiagnosticChecks>("duplicates", DiagnosticChecks.Duplicates),
            new KeyValuePair<string, DiagnosticChecks>("needles", DiagnosticChecks.Needles),
            new KeyValuePair<string, DiagnosticChecks>("slivers", DiagnosticChecks.Slivers),
            new KeyValuePair<string, DiagnosticChecks>("gap", DiagnosticChecks.Gap),
            new KeyValuePair<string, DiagnosticChecks>("spheres", DiagnosticChecks.Spheres),
            new KeyValuePair<string, DiagnosticChecks>("pieces", DiagnosticChecks.Pieces),
        };

        /// <summary>
        /// Gets the names of the checks, in the order the pane lists them.
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, DiagnosticChecks>> All => _names;

        /// <summary>
        /// Reads the name of a check, or <c>all</c>, in either case.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="checks">The check.</param>
        /// <returns>true when the name is one; otherwise, false.</returns>
        public static bool TryParse(string name, out DiagnosticChecks checks)
        {
            checks = DiagnosticChecks.None;

            if (string.Equals(name, "all", StringComparison.OrdinalIgnoreCase))
            {
                checks = DiagnosticChecks.All;
                return true;
            }

            foreach (KeyValuePair<string, DiagnosticChecks> pair in _names)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    checks = pair.Value;
                    return true;
                }
            }

            return false;
        }
    }
}
