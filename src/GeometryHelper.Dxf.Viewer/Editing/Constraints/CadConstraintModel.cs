using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The geometric constraints of AutoCAD 2021's Parametric tab that the presenter solves.</summary>
    internal enum CadGeoKind
    {
        /// <summary>Two points on one place: GCCOINCIDENT.</summary>
        Coincident,

        /// <summary>A point on a curve (a line taken as endless, a circle or an arc taken whole): GCCOINCIDENT with Object.</summary>
        PointOnCurve,

        /// <summary>Two lines on one line: GCCOLLINEAR.</summary>
        Collinear,

        /// <summary>Two circles or arcs about one centre: GCCONCENTRIC.</summary>
        Concentric,

        /// <summary>A point held where it is: GCFIX.</summary>
        Fix,

        /// <summary>Two lines with one direction: GCPARALLEL.</summary>
        Parallel,

        /// <summary>Two lines at a right angle: GCPERPENDICULAR.</summary>
        Perpendicular,

        /// <summary>A line, or two points, level with the x axis: GCHORIZONTAL.</summary>
        Horizontal,

        /// <summary>A line, or two points, level with the y axis: GCVERTICAL.</summary>
        Vertical,

        /// <summary>A line and a circle or arc, or two of those, touching without crossing: GCTANGENT.</summary>
        Tangent,

        /// <summary>Two points, or two objects, mirror images about a line: GCSYMMETRIC.</summary>
        Symmetric,

        /// <summary>Two lines of one length: GCEQUAL.</summary>
        EqualLength,

        /// <summary>Two circles or arcs of one radius: GCEQUAL.</summary>
        EqualRadius,
    }

    /// <summary>The dimensional constraints of AutoCAD 2021's Parametric tab that the presenter solves.</summary>
    internal enum CadDimKind
    {
        /// <summary>The distance along x between two points: DCHORIZONTAL.</summary>
        Horizontal,

        /// <summary>The distance along y between two points: DCVERTICAL.</summary>
        Vertical,

        /// <summary>The distance between two points: DCALIGNED.</summary>
        Aligned,

        /// <summary>The angle from one line to another: DCANGULAR.</summary>
        Angular,

        /// <summary>The radius of a circle or arc: DCRADIUS.</summary>
        Radius,

        /// <summary>The diameter of a circle or arc: DCDIAMETER.</summary>
        Diameter,
    }

    /// <summary>Which part of an object a reference names.</summary>
    internal enum CadPart
    {
        /// <summary>The object whole: a line, a circle, an arc or a segment of a polyline.</summary>
        Whole,

        /// <summary>The start of a line, an arc or a segment.</summary>
        Start,

        /// <summary>The end of a line, an arc or a segment.</summary>
        End,

        /// <summary>The middle of a line or a segment.</summary>
        Mid,

        /// <summary>The centre of a circle or an arc.</summary>
        Center,
    }

    /// <summary>
    /// One thing a constraint is tied to: an object by its handle, the segment of a light polyline where it is one (the
    /// index of the segment, from 0; -1 for any other object), and the part of it: the object whole or one of its points.
    /// </summary>
    internal sealed class CadRef : IEquatable<CadRef>
    {
        internal CadRef(string handle, int segment, CadPart part)
        {
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            Segment = segment;
            Part = part;
        }

        /// <summary>The handle of the object, in capitals.</summary>
        internal string Handle { get; }

        /// <summary>The segment of a polyline, from 0; -1 where the object is none.</summary>
        internal int Segment { get; }

        /// <summary>The part of the object.</summary>
        internal CadPart Part { get; }

        /// <summary>Whether the reference is to a point of the object rather than the object.</summary>
        internal bool IsPoint => Part != CadPart.Whole;

        /// <summary>The same object and segment, as a reference to another part.</summary>
        internal CadRef Of(CadPart part) => new CadRef(Handle, Segment, part);

        /// <summary>Whether two references name the same object and segment, whatever the parts.</summary>
        internal bool SameObject(CadRef other) =>
            other != null
            && Segment == other.Segment
            && string.Equals(Handle, other.Handle, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public bool Equals(CadRef other) => SameObject(other) && Part == other.Part;

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as CadRef);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(Handle) * 31 + Segment * 7 + (int)Part;

        /// <inheritdoc/>
        public override string ToString() =>
            Handle + (Segment >= 0 ? "#" + Segment.ToString(CultureInfo.InvariantCulture) : string.Empty) + "." + Part;
    }

    /// <summary>One geometric constraint: its kind, what it ties, and the numbers it was made with that the solver keeps.</summary>
    internal sealed class CadGeoConstraint
    {
        internal CadGeoConstraint(CadGeoKind kind, IEnumerable<CadRef> refs)
        {
            Kind = kind;
            Refs = new List<CadRef>(refs);
        }

        /// <summary>The kind.</summary>
        internal CadGeoKind Kind { get; }

        /// <summary>
        /// What it ties, in the order picked: two points for a coincident, a point then a curve for a point on a curve, the
        /// lines, circles or arcs for the pairs, the point or points of a Fix, a line or two points for a horizontal or a
        /// vertical, two points or two objects then the mirror line for a symmetric.
        /// </summary>
        internal List<CadRef> Refs { get; }

        /// <summary>The place a Fix holds its point at, x then y; null for every other kind.</summary>
        internal double[] Place { get; set; }

        /// <summary>
        /// The side a tangent was made on, which the solver keeps: +1 or -1 for the side of the line the centre lies on, or for
        /// a pair of circles, +1 where they touch from outside and -1 where one lies inside the other; 0 where not yet set.
        /// </summary>
        internal int Side { get; set; }

        /// <summary>The handle of the constraint's own object in the file once it is written; null before.</summary>
        internal string Source { get; set; }

        /// <summary>The name AutoCAD gives the kind, as its Constraint Settings list them.</summary>
        internal string Label => LabelOf(Kind);

        internal static string LabelOf(CadGeoKind kind)
        {
            switch (kind)
            {
                case CadGeoKind.PointOnCurve:
                    return "Coincident";
                case CadGeoKind.EqualLength:
                case CadGeoKind.EqualRadius:
                    return "Equal";
                default:
                    return kind.ToString();
            }
        }

        /// <inheritdoc/>
        public override string ToString() => Kind + "(" + string.Join(", ", Refs) + ")";
    }

    /// <summary>One dimensional constraint: its kind, what it measures, its parameter and the dimension that shows it.</summary>
    internal sealed class CadDimConstraint
    {
        internal CadDimConstraint(CadDimKind kind, IEnumerable<CadRef> refs, string name)
        {
            Kind = kind;
            Refs = new List<CadRef>(refs);
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>The kind.</summary>
        internal CadDimKind Kind { get; }

        /// <summary>The two points, the two lines, or the one circle or arc it measures.</summary>
        internal List<CadRef> Refs { get; }

        /// <summary>The name of its parameter: d1, ang1, rad1, dia1.</summary>
        internal string Name { get; set; }

        /// <summary>
        /// For the distances, the sign AutoCAD writes for the direction it measures along: 1 where the first point is the
        /// lesser along the axis; -1 where it is the greater. Kept so that a distance is the same number when read again.
        /// </summary>
        internal int Direction { get; set; } = 1;

        /// <summary>Whether the dimension is drawn in the dynamic form (on AutoCAD's hidden layer, not plotted) rather than the annotational.</summary>
        internal bool Dynamic { get; set; } = true;

        /// <summary>
        /// The words its dimension shows, as CONSTRAINTNAMEFORMAT counts them: 0 the name (d1), 1 the value (10.0000), 2 the name
        /// and the value (d1=10.0000). A new constraint takes the session's setting; one read takes it from its dimension's text.
        /// </summary>
        internal int NameFormat { get; set; } = 2;

        /// <summary>The handle of the DIMENSION that shows it; null where it has none.</summary>
        internal string DimensionHandle { get; set; }

        /// <summary>Whether it is a reference, shown in brackets and measuring without driving; the presenter holds none yet.</summary>
        internal bool IsReference { get; set; }

        /// <summary>The handle of the constraint's own object in the file once it is written; null before.</summary>
        internal string Source { get; set; }

        /// <summary>The prefix of the names of this kind: d, ang, rad, dia.</summary>
        internal static string PrefixOf(CadDimKind kind)
        {
            switch (kind)
            {
                case CadDimKind.Angular:
                    return "ang";
                case CadDimKind.Radius:
                    return "rad";
                case CadDimKind.Diameter:
                    return "dia";
                default:
                    return "d";
            }
        }

        /// <inheritdoc/>
        public override string ToString() => Kind + " " + Name + "(" + string.Join(", ", Refs) + ")";
    }

    /// <summary>
    /// One parameter: the name of a dimensional constraint (d1) or a user parameter, its expression ("d1*2+3", or the number
    /// typed) and the value the expression came to. An angle's value is in degrees, as AutoCAD keeps it.
    /// </summary>
    internal sealed class CadParameter
    {
        internal CadParameter(string name, string expression, double value, bool isUser)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Expression = expression ?? string.Empty;
            Value = value;
            IsUser = isUser;
        }

        /// <summary>The name.</summary>
        internal string Name { get; set; }

        /// <summary>The expression; empty where the value was typed as a number that is not whole, as AutoCAD keeps it.</summary>
        internal string Expression { get; set; }

        /// <summary>The value the expression comes to.</summary>
        internal double Value { get; set; }

        /// <summary>Whether it is a user parameter, which no constraint measures.</summary>
        internal bool IsUser { get; }

        /// <summary>The handle of the parameter's own object in the file once it is written; null before.</summary>
        internal string Source { get; set; }

        /// <inheritdoc/>
        public override string ToString() =>
            Name + " = " + Expression + " (" + Value.ToString(CultureInfo.InvariantCulture) + ")";
    }

    /// <summary>
    /// The constraints of one space: the geometric and the dimensional constraints and the parameters, in the order made.
    /// </summary>
    internal sealed class CadConstraintSet
    {
        /// <summary>The geometric constraints.</summary>
        internal List<CadGeoConstraint> Geometric { get; } = new List<CadGeoConstraint>();

        /// <summary>The dimensional constraints.</summary>
        internal List<CadDimConstraint> Dimensional { get; } = new List<CadDimConstraint>();

        /// <summary>The parameters: those of the dimensional constraints and the user's own.</summary>
        internal List<CadParameter> Parameters { get; } = new List<CadParameter>();

        /// <summary>
        /// Why the constraints of the space cannot be edited: it holds a kind of constraint or of object the presenter does not
        /// work out, which writing the set again would lose; null where it can.
        /// </summary>
        internal string Refusal { get; set; }

        /// <summary>The handle of the network the set was read from; null for a set made here and not yet written.</summary>
        internal string Network { get; set; }

        /// <summary>Whether the set holds nothing.</summary>
        internal bool IsEmpty => Geometric.Count == 0 && Dimensional.Count == 0 && Parameters.Count == 0;

        /// <summary>The parameter of a name, letters of either case the same; null where there is none.</summary>
        internal CadParameter Parameter(string name)
        {
            foreach (CadParameter parameter in Parameters)
            {
                if (string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return parameter;
                }
            }

            return null;
        }

        /// <summary>The handles of every object some constraint is tied to, in ordinal order.</summary>
        internal SortedSet<string> Handles()
        {
            var handles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (CadGeoConstraint constraint in Geometric)
            {
                foreach (CadRef r in constraint.Refs)
                {
                    handles.Add(r.Handle);
                }
            }

            foreach (CadDimConstraint constraint in Dimensional)
            {
                foreach (CadRef r in constraint.Refs)
                {
                    handles.Add(r.Handle);
                }
            }

            return handles;
        }

        /// <summary>The next name of a prefix not used by a parameter: d1, d2 ... where d1 is taken.</summary>
        internal string NextName(string prefix)
        {
            for (int i = 1; ; i++)
            {
                string name = prefix + i.ToString(CultureInfo.InvariantCulture);

                if (Parameter(name) == null)
                {
                    return name;
                }
            }
        }
    }
}
