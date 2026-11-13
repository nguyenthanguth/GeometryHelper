using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What a line of an object selection filter tests, as the Select Filter list of AutoCAD 2021's FILTER names it: a
    /// property of an object, or the begin or end of a group (AND, OR, XOR, NOT) of lines. Points are in world coordinates,
    /// angles in degrees, lengths in drawing units.
    /// </summary>
    public enum CadFilterProperty
    {
        /// <summary>The kind of the object, as the Properties panel names it (Line, Circle, MText) or AutoCAD's filter list does (Arc, Block, Polyline).</summary>
        Object,

        /// <summary>The layer, a name or a wild-card pattern: <c>WALL*</c>.</summary>
        Layer,

        /// <summary>The colour by its index: 1 to 255, 256 ByLayer, 0 ByBlock; tested with =, !=, &lt;, &lt;=, &gt; and &gt;=.</summary>
        Color,

        /// <summary>The linetype by name, ByLayer and ByBlock among them, or a wild-card pattern.</summary>
        Linetype,

        /// <summary>The centre of an arc (X, Y, Z).</summary>
        ArcCenter,

        /// <summary>The radius of an arc.</summary>
        ArcRadius,

        /// <summary>The insertion point of an attribute definition (X, Y, Z).</summary>
        AttributePosition,

        /// <summary>The tag of an attribute definition, a name or a pattern.</summary>
        AttributeTag,

        /// <summary>The name of the block a block reference inserts, a name or a pattern.</summary>
        BlockName,

        /// <summary>The insertion point of a block reference (X, Y, Z).</summary>
        BlockPosition,

        /// <summary>The rotation of a block reference, in degrees.</summary>
        BlockRotation,

        /// <summary>The centre of a circle (X, Y, Z).</summary>
        CircleCenter,

        /// <summary>The radius of a circle.</summary>
        CircleRadius,

        /// <summary>The dimension style of a dimension, a name or a pattern.</summary>
        DimensionStyle,

        /// <summary>The elevation of a polyline or a hatch.</summary>
        Elevation,

        /// <summary>The centre of an ellipse (X, Y, Z).</summary>
        EllipseCenter,

        /// <summary>The pattern name of a hatch, a name or a pattern.</summary>
        HatchPatternName,

        /// <summary>The insertion point of a raster image (X, Y, Z).</summary>
        ImagePosition,

        /// <summary>The linetype scale of an object.</summary>
        LinetypeScale,

        /// <summary>The start of a line (X, Y, Z).</summary>
        LineStart,

        /// <summary>The end of a line (X, Y, Z).</summary>
        LineEnd,

        /// <summary>The normal of a flat object (X, Y, Z).</summary>
        NormalVector,

        /// <summary>The position of a point object (X, Y, Z).</summary>
        PointPosition,

        /// <summary>The insertion point of a text or an MTEXT (X, Y, Z).</summary>
        TextPosition,

        /// <summary>The words of a text or an MTEXT, a text or a pattern.</summary>
        TextValue,

        /// <summary>The text style of a text or an MTEXT, a name or a pattern.</summary>
        TextStyleName,

        /// <summary>The height of a text or an MTEXT.</summary>
        TextHeight,

        /// <summary>The rotation of a text or an MTEXT, in degrees.</summary>
        TextRotation,

        /// <summary>The thickness of an object.</summary>
        Thickness,

        /// <summary>The centre of a viewport on its sheet (X, Y, Z).</summary>
        ViewportCenter,

        /// <summary>The name of an application that has extended data on the object, a name or a pattern.</summary>
        XdataId,

        /// <summary>The start of an AND group: every operand up to its end has to hold.</summary>
        BeginAnd,

        /// <summary>The end of an AND group.</summary>
        EndAnd,

        /// <summary>The start of an OR group: one operand at least has to hold.</summary>
        BeginOr,

        /// <summary>The end of an OR group.</summary>
        EndOr,

        /// <summary>The start of an XOR group of two operands: exactly one has to hold.</summary>
        BeginXor,

        /// <summary>The end of an XOR group.</summary>
        EndXor,

        /// <summary>The start of a NOT group of one operand: it must not hold.</summary>
        BeginNot,

        /// <summary>The end of a NOT group.</summary>
        EndNot,
    }

    /// <summary>
    /// An item of the Select Filter list of AutoCAD 2021's FILTER dialog: a property to test, or a kind of object (Arc, Line,
    /// Block) that sets an Object line, or the begin or end of a group.
    /// </summary>
    public sealed class CadFilterChoice
    {
        internal CadFilterChoice(string name, CadFilterProperty property, string value)
        {
            Name = name;
            Property = property;
            Value = value;
        }

        /// <summary>Gets the name the list shows: "Arc Radius", "Line", "** Begin   AND".</summary>
        public string Name { get; }

        /// <summary>Gets the property a line made from the choice tests; <see cref="CadFilterProperty.Object"/> for a kind of object.</summary>
        public CadFilterProperty Property { get; }

        /// <summary>Gets the kind an Object line takes ("Arc"); null for any other choice.</summary>
        public string Value { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// A line of an object selection filter: what it tests, how, and the value it wants, as "Layer = WALLS", "Arc Radius &gt;=
    /// 15.0000" or "Line End X &gt;= 100.0000 Y * 0.0000 Z * 0.0000"; or the begin or end of a group of lines.
    /// </summary>
    public sealed class CadFilterLine
    {
        private enum Kind
        {
            Object,
            Name,
            Color,
            Real,
            Angle,
            Vector,
            Group,
        }

        private sealed class Info
        {
            internal Info(string name, Kind kind, int code, string token = null)
            {
                Name = name;
                Kind = kind;
                Code = code;
                Token = token;
            }

            internal string Name { get; }

            internal Kind Kind { get; }

            internal int Code { get; }

            internal string Token { get; }
        }

        private const double Fuzz = 1E-8;

        private static readonly string[] ColorNames = { "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta", "White" };

        private static readonly string[] TestOperators = { "=", "!=", "<", "<=", ">", ">=" };

        private static readonly Dictionary<CadFilterProperty, Info> Infos = new Dictionary<CadFilterProperty, Info>
        {
            { CadFilterProperty.Object, new Info("Object", Kind.Object, 0) },
            { CadFilterProperty.Layer, new Info("Layer", Kind.Name, 8) },
            { CadFilterProperty.Color, new Info("Color", Kind.Color, 62) },
            { CadFilterProperty.Linetype, new Info("Linetype", Kind.Name, 6) },
            { CadFilterProperty.ArcCenter, new Info("Arc Center", Kind.Vector, 10) },
            { CadFilterProperty.ArcRadius, new Info("Arc Radius", Kind.Real, 40) },
            { CadFilterProperty.AttributePosition, new Info("Attribute Position", Kind.Vector, 10) },
            { CadFilterProperty.AttributeTag, new Info("Attribute Tag", Kind.Name, 2) },
            { CadFilterProperty.BlockName, new Info("Block Name", Kind.Name, 2) },
            { CadFilterProperty.BlockPosition, new Info("Block Position", Kind.Vector, 10) },
            { CadFilterProperty.BlockRotation, new Info("Block Rotation", Kind.Angle, 50) },
            { CadFilterProperty.CircleCenter, new Info("Circle Center", Kind.Vector, 10) },
            { CadFilterProperty.CircleRadius, new Info("Circle Radius", Kind.Real, 40) },
            { CadFilterProperty.DimensionStyle, new Info("Dimension Style", Kind.Name, 3) },
            { CadFilterProperty.Elevation, new Info("Elevation", Kind.Real, 38) },
            { CadFilterProperty.EllipseCenter, new Info("Ellipse Center", Kind.Vector, 10) },
            { CadFilterProperty.HatchPatternName, new Info("Hatch Pattern Name", Kind.Name, 2) },
            { CadFilterProperty.ImagePosition, new Info("Image Position", Kind.Vector, 10) },
            { CadFilterProperty.LinetypeScale, new Info("Linetype Scale", Kind.Real, 48) },
            { CadFilterProperty.LineStart, new Info("Line Start", Kind.Vector, 10) },
            { CadFilterProperty.LineEnd, new Info("Line End", Kind.Vector, 11) },
            { CadFilterProperty.NormalVector, new Info("Normal Vector", Kind.Vector, 210) },
            { CadFilterProperty.PointPosition, new Info("Point Position", Kind.Vector, 10) },
            { CadFilterProperty.TextPosition, new Info("Text Position", Kind.Vector, 10) },
            { CadFilterProperty.TextValue, new Info("Text Value", Kind.Name, 1) },
            { CadFilterProperty.TextStyleName, new Info("Text Style Name", Kind.Name, 7) },
            { CadFilterProperty.TextHeight, new Info("Text Height", Kind.Real, 40) },
            { CadFilterProperty.TextRotation, new Info("Text Rotation", Kind.Angle, 50) },
            { CadFilterProperty.Thickness, new Info("Thickness", Kind.Real, 39) },
            { CadFilterProperty.ViewportCenter, new Info("Viewport Center", Kind.Vector, 10) },
            { CadFilterProperty.XdataId, new Info("Xdata ID", Kind.Name, -3) },
            { CadFilterProperty.BeginAnd, new Info("** Begin   AND", Kind.Group, -4, "<AND") },
            { CadFilterProperty.EndAnd, new Info("** End      AND", Kind.Group, -4, "AND>") },
            { CadFilterProperty.BeginOr, new Info("** Begin   OR", Kind.Group, -4, "<OR") },
            { CadFilterProperty.EndOr, new Info("** End      OR", Kind.Group, -4, "OR>") },
            { CadFilterProperty.BeginXor, new Info("** Begin   XOR", Kind.Group, -4, "<XOR") },
            { CadFilterProperty.EndXor, new Info("** End      XOR", Kind.Group, -4, "XOR>") },
            { CadFilterProperty.BeginNot, new Info("** Begin   NOT", Kind.Group, -4, "<NOT") },
            { CadFilterProperty.EndNot, new Info("** End      NOT", Kind.Group, -4, "NOT>") },
        };

        private readonly string[] _operators;
        private readonly double[] _numbers;

        /// <summary>Makes a line.</summary>
        /// <param name="property">What it tests.</param>
        /// <param name="value">The value: a kind, a name or a wild-card pattern, a colour ("1", "1 - Red", "Red", "ByLayer",
        /// "ByBlock"), a number ("15"), an angle in degrees, or for a point "x,y,z"; ignored for the begin or end of a group.</param>
        /// <param name="operators">How the value is tested, "=", "!=", "&lt;", "&lt;=", "&gt;" or "&gt;=" for a colour or a
        /// number, and for a point also "*" for a coordinate that is not tested: none for "=", one for all three coordinates, or
        /// three. Names and kinds are tested with "=" alone.</param>
        /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the value is not one AutoCAD's filter takes ("Invalid Color number.",
        /// "Invalid Number.", "Invalid X coordinate.", "Invalid Name.") or an operator is not one the property takes ("Invalid
        /// Filter list - Invalid op_code string.").</exception>
        public CadFilterLine(CadFilterProperty property, string value, params string[] operators)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            Info info = Infos[property];
            Property = property;
            Value = value.Trim();
            _operators = ReadOperators(info.Kind, operators ?? Array.Empty<string>());
            _numbers = Array.Empty<double>();

            switch (info.Kind)
            {
                case Kind.Object:
                case Kind.Name:
                    if (Value.Length == 0)
                    {
                        throw new ArgumentException("Invalid Name.", nameof(value));
                    }

                    break;
                case Kind.Color:
                    ColorIndex = ReadColor(Value)
                        ?? throw new ArgumentException("Invalid Color number.", nameof(value));
                    Value = ColorWords(ColorIndex);
                    _numbers = new double[] { ColorIndex };
                    break;
                case Kind.Real:
                case Kind.Angle:
                    _numbers = new[] { ReadNumber(Value, "Invalid Number.", nameof(value)) };
                    Value = Number(_numbers[0]);
                    break;
                case Kind.Vector:
                    string[] parts = Value.Split(',');
                    string[] axes = { "X", "Y", "Z" };
                    _numbers = new double[3];

                    for (int i = 0; i < 3; i++)
                    {
                        string part = i < parts.Length ? parts[i] : string.Empty;
                        _numbers[i] = ReadNumber(part, "Invalid " + axes[i] + " coordinate.", nameof(value));
                    }

                    Value = Number(_numbers[0]) + "," + Number(_numbers[1]) + "," + Number(_numbers[2]);
                    break;
                default:
                    Value = string.Empty;
                    break;
            }
        }

        /// <summary>Gets what the line tests.</summary>
        public CadFilterProperty Property { get; }

        /// <summary>
        /// Gets the value it wants: a name as typed, a colour written as AutoCAD's list writes it ("1 - Red", "256 - By
        /// Layer"), a number to four places ("15.0000"), a point as "x,y,z"; empty for the begin or end of a group.
        /// </summary>
        public string Value { get; }

        /// <summary>Gets the colour index a colour line wants; 0 for the other lines.</summary>
        public int ColorIndex { get; }

        /// <summary>Gets the operators of the line: one for a name, a kind, a colour or a number, three (X, Y, Z) for a point, none for a group.</summary>
        public IReadOnlyList<string> Operators => _operators;

        /// <summary>Gets the name the Select Filter list gives the property: "Arc Radius", "Xdata ID", "** Begin   AND".</summary>
        public string Name => Infos[Property].Name;

        /// <summary>Gets whether the line begins a group.</summary>
        public bool IsGroupBegin => Infos[Property].Kind == Kind.Group && Infos[Property].Token[0] == '<';

        /// <summary>Gets whether the line ends a group.</summary>
        public bool IsGroupEnd => Infos[Property].Kind == Kind.Group && Infos[Property].Token[0] != '<';

        /// <summary>
        /// Gets the line as the filter list shows it: the property padded to 21 characters, then the operator and the value, as
        /// "Layer                = WALLS", "Arc Radius           &gt;= 15.0000" or "Line End             X &gt;= 100.0000  Y *
        /// 0.0000  Z * 0.0000"; a group's line is its name.
        /// </summary>
        public string Text
        {
            get
            {
                Info info = Infos[Property];

                switch (info.Kind)
                {
                    case Kind.Group:
                        return info.Name;
                    case Kind.Vector:
                        return info.Name.PadRight(21)
                            + "X "
                            + _operators[0]
                            + " "
                            + Number(_numbers[0])
                            + "  Y "
                            + _operators[1]
                            + " "
                            + Number(_numbers[1])
                            + "  Z "
                            + _operators[2]
                            + " "
                            + Number(_numbers[2]);
                    default:
                        return info.Name.PadRight(21) + _operators[0] + " " + Value;
                }
            }
        }

        /// <summary>Gets whether an entity passes the line; the begin or end of a group is no test.</summary>
        /// <param name="entity">The entity.</param>
        /// <returns>Whether it passes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the line begins or ends a group.</exception>
        public bool Passes(DxfEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            Info info = Infos[Property];

            switch (info.Kind)
            {
                case Kind.Group:
                    throw new InvalidOperationException("The begin or end of a group is no test.");
                case Kind.Object:
                    return CadFilterEntities.IsKind(entity, Value);
                case Kind.Color:
                    DxfColor color = entity.Color;
                    int index =
                        color == null || color.IsByLayer ? 256
                        : color.IsByBlock ? 0
                        : color.ColorIndex;
                    return Compare(index, _numbers[0], _operators[0]);
                default:
                    break;
            }

            if (Property == CadFilterProperty.Layer)
            {
                return CadWildcard.IsMatch(entity.Layer, Value, true);
            }

            if (Property == CadFilterProperty.Linetype)
            {
                return CadWildcard.IsMatch(entity.Linetype, Value, true);
            }

            if (!CadFilterEntities.TryRead(entity, Property, out string text, out double[] numbers))
            {
                return false;
            }

            switch (info.Kind)
            {
                case Kind.Name:
                    if (Property == CadFilterProperty.XdataId)
                    {
                        return CadFilterEntities.HasApplication(entity, Value);
                    }

                    return text != null && CadWildcard.IsMatch(text, Value, true);
                case Kind.Angle:
                    return Compare(numbers[0] * 180.0 / Math.PI, _numbers[0], _operators[0]);
                case Kind.Real:
                    return Compare(numbers[0], _numbers[0], _operators[0]);
                default:
                    for (int i = 0; i < 3; i++)
                    {
                        if (_operators[i] != "*" && !Compare(numbers[i], _numbers[i], _operators[i]))
                        {
                            return false;
                        }
                    }

                    return true;
            }
        }

        /// <summary>Writes a colour index as AutoCAD's filter list writes it: "1 - Red", "8", "256 - By Layer", "0 - By Block".</summary>
        /// <param name="index">The index, 0 to 256.</param>
        /// <returns>The words.</returns>
        public static string ColorWords(int index) =>
            index == 256 ? "256 - By Layer"
            : index == 0 ? "0 - By Block"
            : index <= 7 ? index.ToString(CultureInfo.InvariantCulture) + " - " + ColorNames[index - 1]
            : index.ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets the name the Select Filter list gives a property: "Arc Radius", "Xdata ID", "** Begin   AND".</summary>
        /// <param name="property">The property.</param>
        /// <returns>The name.</returns>
        public static string NameOf(CadFilterProperty property) => Infos[property].Name;

        /// <summary>Gets whether a property is tested with operators (a colour, a number, an angle or a point); a name and a kind are tested with "=" alone.</summary>
        /// <param name="property">The property.</param>
        /// <returns>Whether it takes operators.</returns>
        public static bool TakesOperators(CadFilterProperty property)
        {
            Kind kind = Infos[property].Kind;
            return kind == Kind.Color || kind == Kind.Real || kind == Kind.Angle || kind == Kind.Vector;
        }

        /// <summary>Gets whether a property is a point, tested on X, Y and Z each with its own operator.</summary>
        /// <param name="property">The property.</param>
        /// <returns>Whether it is a point.</returns>
        public static bool IsPoint(CadFilterProperty property) => Infos[property].Kind == Kind.Vector;

        /// <summary>Gets whether a property begins or ends a group.</summary>
        /// <param name="property">The property.</param>
        /// <returns>Whether it is the begin or end of a group.</returns>
        public static bool IsGroup(CadFilterProperty property) => Infos[property].Kind == Kind.Group;

        /// <summary>Gets the operators a number or a point may be tested with: = != &lt; &lt;= &gt; &gt;= and, for a coordinate, *.</summary>
        /// <param name="property">The property.</param>
        /// <returns>The operators, in the order the dialog lists them.</returns>
        public static IReadOnlyList<string> OperatorsOf(CadFilterProperty property)
        {
            if (!TakesOperators(property))
            {
                return new[] { "=" };
            }

            return Infos[property].Kind == Kind.Vector ? new[] { "=", "!=", "<", "<=", ">", ">=", "*" } : TestOperators;
        }

        internal static string GroupToken(CadFilterProperty property) => Infos[property].Token;

        internal static int CodeOf(CadFilterProperty property) => Infos[property].Code;

        internal static bool TryFind(string name, out CadFilterProperty property)
        {
            string collapsed = Collapse(name);

            foreach (KeyValuePair<CadFilterProperty, Info> pair in Infos)
            {
                if (string.Equals(Collapse(pair.Value.Name), collapsed, StringComparison.OrdinalIgnoreCase))
                {
                    property = pair.Key;
                    return true;
                }
            }

            if (string.Equals(collapsed, "Entity", StringComparison.OrdinalIgnoreCase))
            {
                property = CadFilterProperty.Object;
                return true;
            }

            property = default;
            return false;
        }

        /// <summary>Gets the numbers a colour, a number, an angle (degrees) or a point line wants; none for the other lines.</summary>
        public IReadOnlyList<double> Numbers => _numbers;

        internal static string Number(double value) =>
            (Math.Abs(value) < 5E-5 ? 0.0 : value).ToString("0.0000", CultureInfo.InvariantCulture);

        private static string Collapse(string text) =>
            string.Join(" ", (text ?? string.Empty).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));

        private static string[] ReadOperators(Kind kind, string[] given)
        {
            int count =
                kind == Kind.Vector ? 3
                : kind == Kind.Group ? 0
                : 1;
            var result = new string[count];

            for (int i = 0; i < count; i++)
            {
                string op =
                    given.Length == 0 ? "="
                    : given.Length == 1 ? given[0]
                    : i < given.Length ? given[i]
                    : "=";
                bool allowed =
                    op == "="
                    || (kind != Kind.Object && kind != Kind.Name && Array.IndexOf(TestOperators, op) >= 0)
                    || (kind == Kind.Vector && op == "*");

                if (!allowed || (given.Length != 0 && given.Length != 1 && given.Length != count))
                {
                    throw new ArgumentException("Invalid Filter list - Invalid op_code string.", nameof(given));
                }

                result[i] = op;
            }

            if (count == 0 && given.Length > 0)
            {
                throw new ArgumentException("Invalid Filter list - Invalid op_code string.", nameof(given));
            }

            return result;
        }

        private static bool Compare(double actual, double wanted, string op)
        {
            bool equal = Math.Abs(actual - wanted) <= Fuzz * Math.Max(1.0, Math.Abs(wanted));

            switch (op)
            {
                case "=":
                    return equal;
                case "!=":
                    return !equal;
                case "<":
                    return actual < wanted && !equal;
                case "<=":
                    return actual < wanted || equal;
                case ">":
                    return actual > wanted && !equal;
                default:
                    return actual > wanted || equal;
            }
        }

        private static double ReadNumber(string text, string message, string argument)
        {
            if (
                !double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                || double.IsNaN(number)
                || double.IsInfinity(number)
            )
            {
                throw new ArgumentException(message, argument);
            }

            return number;
        }

        private static int? ReadColor(string text)
        {
            string head = text.Split(new[] { " - " }, StringSplitOptions.None)[0].Trim();

            if (int.TryParse(head, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
            {
                return index >= 0 && index <= 256 ? index : (int?)null;
            }

            string word = head.Replace(" ", string.Empty);

            if (string.Equals(word, "ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                return 256;
            }

            if (string.Equals(word, "ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            int named = Array.FindIndex(ColorNames, n => string.Equals(n, word, StringComparison.OrdinalIgnoreCase));
            return named >= 0 ? named + 1 : (int?)null;
        }
    }
}
