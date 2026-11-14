using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// An object selection filter, as AutoCAD's FILTER makes one: a list of lines, which an object has to pass all of, except
    /// where lines are grouped with AND, OR, XOR and NOT (a group may hold groups, ten deep at most); named lists kept for the
    /// session, the current one <c>*unnamed</c>, and written to and read from the file AutoCAD keeps them in, <c>filter.nfl</c>.
    /// Of a plan with 312 lines on WALLS and 40 circles there, Object = Line and Layer = WALLS pass the 312, and Object =
    /// Circle with a NOT group round Circle Radius &gt;= 1 passes the circles under 1 in radius.
    /// </summary>
    public sealed partial class CadSelectionFilter
    {
        /// <summary>The name of the list being edited where it has no name of its own, as AutoCAD's.</summary>
        public const string Unnamed = "*unnamed";

        private const int MaxNesting = 10;

        private static readonly char[] InvalidNameCharacters =
        {
            '*',
            '?',
            '<',
            '>',
            '/',
            '\\',
            '"',
            ':',
            ';',
            '|',
            ',',
            '=',
            '`',
            '#',
            '@',
            '.',
            '~',
            '[',
            ']',
            '\'',
            '!',
            '%',
            '^',
            '&',
            '(',
            ')',
            '+',
            '{',
            '}',
        };

        private readonly List<CadFilterLine> _lines = new List<CadFilterLine>();
        private readonly SortedDictionary<string, List<CadFilterLine>> _named = new SortedDictionary<
            string,
            List<CadFilterLine>
        >(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the items of the Select Filter list of AutoCAD 2021's dialog, in its order: the properties to test, the kinds of
        /// object (Arc, Line, Block) that make an Object line, and the begin and end of each group.
        /// </summary>
        public static IReadOnlyList<CadFilterChoice> Choices { get; } = MakeChoices();

        /// <summary>Gets the lines of the current list, in the order they were added.</summary>
        public IList<CadFilterLine> Lines => _lines;

        /// <summary>Gets or sets the name of the current list: <see cref="Unnamed"/>, or one saved.</summary>
        public string CurrentName { get; set; } = Unnamed;

        /// <summary>
        /// Gets or sets the file the named lists are kept in, AutoCAD's <c>filter.nfl</c> form: written whenever a list is saved
        /// or deleted, and read by <see cref="Reload"/>; null keeps them for the session only.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>Gets the names of the lists saved, <see cref="Unnamed"/> first, the others in ordinal order.</summary>
        public IReadOnlyList<string> Names
        {
            get
            {
                var names = new List<string> { Unnamed };
                names.AddRange(_named.Keys);
                return names;
            }
        }

        /// <summary>Saves the current list under a name, replacing a list of that name, and makes the name current.</summary>
        /// <param name="name">The name; AutoCAD refuses an empty one ("Null Filter name not allowed."), <c>*unnamed</c> ("Invalid
        /// Filter name.") and one with characters a name cannot hold ("Invalid characters in Filter name.").</param>
        /// <returns>Null where saved, else what AutoCAD says.</returns>
        public string SaveAs(string name)
        {
            string trimmed = (name ?? string.Empty).Trim();

            if (trimmed.Length == 0)
            {
                return "Null Filter name not allowed.";
            }

            if (string.Equals(trimmed, Unnamed, StringComparison.OrdinalIgnoreCase))
            {
                return "Invalid Filter name.";
            }

            if (trimmed.IndexOfAny(InvalidNameCharacters) >= 0)
            {
                return "Invalid characters in Filter name.";
            }

            _named[trimmed] = new List<CadFilterLine>(_lines);
            CurrentName = trimmed;
            Persist();
            return null;
        }

        /// <summary>Makes a list saved the current one, its lines copied into <see cref="Lines"/>; <see cref="Unnamed"/> keeps the lines there.</summary>
        /// <param name="name">The name.</param>
        /// <returns>Whether a list of the name was there.</returns>
        public bool Restore(string name)
        {
            if (string.Equals(name, Unnamed, StringComparison.OrdinalIgnoreCase))
            {
                CurrentName = Unnamed;
                return true;
            }

            if (name == null || !_named.TryGetValue(name, out List<CadFilterLine> lines))
            {
                return false;
            }

            _lines.Clear();
            _lines.AddRange(lines);
            CurrentName = name;
            return true;
        }

        /// <summary>Deletes the current list where it has a name, as Delete Current Filter List; <see cref="Unnamed"/> is refused.</summary>
        /// <returns>Null where deleted, else "Cannot delete *unnamed Filter list.".</returns>
        public string DeleteCurrent()
        {
            if (!_named.Remove(CurrentName))
            {
                return "Cannot delete *unnamed Filter list.";
            }

            CurrentName = Unnamed;
            Persist();
            return null;
        }

        /// <summary>
        /// Checks the groups of the lines as AutoCAD checks them when the filter is applied: each Begin has its End, nothing
        /// is empty, XOR has two operands and NOT one.
        /// </summary>
        /// <returns>Null where the lines are well formed, else AutoCAD's words: "Invalid Filter list - Begin/end clause mismatch.",
        /// "Premature end.", "Empty clause/bad nesting.", "Wrong number of XOR/NOT operands.", "Maximum nesting exceeded.".</returns>
        public string Validate() => Validate(_lines);

        /// <summary>Gets whether an entity passes the lines of the list: every one outside a group, and each group as it says.</summary>
        /// <param name="entity">The entity.</param>
        /// <returns>Whether it passes; an empty list passes all.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the groups of the lines are not well formed (<see cref="Validate()"/>).</exception>
        public bool Passes(DxfEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            return Compile()(entity);
        }

        /// <summary>Gets the objects of a list that pass, in the order given; handles the document has no entity of are passed over.</summary>
        /// <param name="document">The document.</param>
        /// <param name="handles">The handles.</param>
        /// <returns>The handles that pass.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the handles are null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the groups of the lines are not well formed (<see cref="Validate()"/>).</exception>
        public IReadOnlyList<string> Apply(CadDocument document, IEnumerable<string> handles)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (handles == null)
            {
                throw new ArgumentNullException(nameof(handles));
            }

            Func<DxfEntity, bool> passes = Compile();
            var passed = new List<string>();

            foreach (string handle in handles)
            {
                DxfEntity entity = document.FindEntity(handle);

                if (entity != null && passes(entity))
                {
                    passed.Add(handle);
                }
            }

            return passed;
        }

        /// <summary>Gets the kinds of the objects of a drawing, as an Object line names them (Line, Circle, MText), in ordinal order.</summary>
        /// <param name="document">The document.</param>
        /// <returns>The kinds.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static IReadOnlyList<string> KindsIn(CadDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var kinds = new SortedSet<string>(StringComparer.Ordinal);

            foreach (CadSpace space in document.Spaces)
            {
                foreach (DxfEntity entity in space.Block)
                {
                    kinds.Add(CadProperties.KindOf(entity));
                }
            }

            return new List<string>(kinds);
        }

        /// <summary>
        /// Gets the transparencies Select... lists for a Transparency line: ByLayer, ByBlock, then the per cents the objects of the
        /// drawing have, least first.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <returns>The values.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static IReadOnlyList<string> TransparenciesIn(CadDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var used = new SortedSet<int>();

            foreach (CadSpace space in document.Spaces)
            {
                foreach (DxfEntity entity in space.Block)
                {
                    int rank = CadFilterLine.TransparencyRankOf(entity.Transparency);

                    if (rank >= 0)
                    {
                        used.Add(rank);
                    }
                }
            }

            var values = new List<string> { "ByLayer", "ByBlock" };

            foreach (int percent in used)
            {
                values.Add(percent.ToString(CultureInfo.InvariantCulture));
            }

            return values;
        }

        /// <summary>
        /// Gets the lines Add Selected Object adds for an entity, as AutoCAD's: its kind, layer, colour and linetype, then what
        /// belongs to its kind, as an arc's centre and radius or a text's position, words, style, height and rotation.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>The lines.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        public static IReadOnlyList<CadFilterLine> LinesOf(DxfEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            DxfColor color = entity.Color;
            int index =
                color == null || color.IsByLayer ? 256
                : color.IsByBlock ? 0
                : color.ColorIndex;
            var lines = new List<CadFilterLine>
            {
                new CadFilterLine(CadFilterProperty.Object, CadFilterEntities.KindName(entity)),
                new CadFilterLine(CadFilterProperty.Layer, entity.Layer ?? "0"),
                new CadFilterLine(CadFilterProperty.Color, index.ToString(CultureInfo.InvariantCulture)),
                new CadFilterLine(CadFilterProperty.Linetype, entity.Linetype ?? "ByLayer"),
            };

            foreach (CadFilterProperty property in PropertiesOf(entity))
            {
                if (!CadFilterEntities.TryRead(entity, property, out string text, out double[] numbers))
                {
                    continue;
                }

                if (CadFilterLine.IsPoint(property))
                {
                    lines.Add(new CadFilterLine(property, string.Join(",", Invariant(numbers))));
                }
                else if (text != null)
                {
                    if (text.Length > 0)
                    {
                        lines.Add(new CadFilterLine(property, text));
                    }
                }
                else
                {
                    bool angle =
                        property == CadFilterProperty.BlockRotation || property == CadFilterProperty.TextRotation;
                    lines.Add(
                        new CadFilterLine(
                            property,
                            (angle ? numbers[0] * 180.0 / Math.PI : numbers[0]).ToString(
                                "R",
                                CultureInfo.InvariantCulture
                            )
                        )
                    );
                }
            }

            return lines;
        }

        private static IEnumerable<string> Invariant(double[] numbers)
        {
            foreach (double number in numbers)
            {
                yield return number.ToString("R", CultureInfo.InvariantCulture);
            }
        }

        private static CadFilterProperty[] PropertiesOf(DxfEntity entity)
        {
            switch (entity)
            {
                case DxfArc _:
                    return new[] { CadFilterProperty.ArcCenter, CadFilterProperty.ArcRadius };
                case DxfCircle _:
                    return new[] { CadFilterProperty.CircleCenter, CadFilterProperty.CircleRadius };
                case DxfLine _:
                    return new[] { CadFilterProperty.LineStart, CadFilterProperty.LineEnd };
                case DxfEllipse _:
                    return new[] { CadFilterProperty.EllipseCenter };
                case DxfAttributeDefinition _:
                    return new[] { CadFilterProperty.AttributeTag, CadFilterProperty.AttributePosition };
                case DxfDBText _:
                case DxfMText _:
                    return new[]
                    {
                        CadFilterProperty.TextPosition,
                        CadFilterProperty.TextValue,
                        CadFilterProperty.TextStyleName,
                        CadFilterProperty.TextHeight,
                        CadFilterProperty.TextRotation,
                    };
                case DxfBlockReference _:
                    return new[]
                    {
                        CadFilterProperty.BlockName,
                        CadFilterProperty.BlockPosition,
                        CadFilterProperty.BlockRotation,
                    };
                case DxfDBPoint _:
                    return new[] { CadFilterProperty.PointPosition };
                case DxfDimension _:
                    return new[] { CadFilterProperty.DimensionStyle };
                case DxfHatch _:
                    return new[] { CadFilterProperty.HatchPatternName };
                case DxfRasterImage _:
                    return new[] { CadFilterProperty.ImagePosition };
                case DxfViewport _:
                    return new[] { CadFilterProperty.ViewportCenter };
                case DxfUnknownEntity mline when mline.DxfName == "MLINE":
                    return new[] { CadFilterProperty.MultilineStyle };
                case DxfUnknownEntity shape when shape.DxfName == "SHAPE":
                    return new[] { CadFilterProperty.ShapeName, CadFilterProperty.ShapePosition };
                default:
                    return Array.Empty<CadFilterProperty>();
            }
        }

        /// <summary>Checks the groups of lines as <see cref="Validate()"/> does, for lines not yet given to the filter.</summary>
        /// <param name="lines">The lines.</param>
        /// <returns>Null where they are well formed, else AutoCAD's words.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the lines are null.</exception>
        public static string Validate(IReadOnlyList<CadFilterLine> lines)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            const string Head = "Invalid Filter list - ";
            var open = new Stack<CadFilterLine>();
            var operands = new Stack<int>();
            operands.Push(0);

            foreach (CadFilterLine line in lines)
            {
                if (line.IsGroupBegin)
                {
                    if (open.Count >= MaxNesting)
                    {
                        return Head + "Maximum nesting exceeded.";
                    }

                    open.Push(line);
                    operands.Push(0);
                }
                else if (line.IsGroupEnd)
                {
                    if (open.Count == 0 || !Closes(open.Peek().Property, line.Property))
                    {
                        return Head + "Begin/end clause mismatch.";
                    }

                    int count = operands.Pop();
                    CadFilterProperty begun = open.Pop().Property;

                    if (count == 0)
                    {
                        return Head + "Empty clause/bad nesting.";
                    }

                    if (
                        (begun == CadFilterProperty.BeginXor && count != 2)
                        || (begun == CadFilterProperty.BeginNot && count != 1)
                    )
                    {
                        return Head + "Wrong number of XOR/NOT operands.";
                    }

                    operands.Push(operands.Pop() + 1);
                }
                else
                {
                    operands.Push(operands.Pop() + 1);
                }
            }

            return open.Count > 0 ? Head + "Premature end." : null;
        }

        private static bool Closes(CadFilterProperty begin, CadFilterProperty end) =>
            (begin == CadFilterProperty.BeginAnd && end == CadFilterProperty.EndAnd)
            || (begin == CadFilterProperty.BeginOr && end == CadFilterProperty.EndOr)
            || (begin == CadFilterProperty.BeginXor && end == CadFilterProperty.EndXor)
            || (begin == CadFilterProperty.BeginNot && end == CadFilterProperty.EndNot);

        private Func<DxfEntity, bool> Compile()
        {
            string wrong = Validate(_lines);

            if (wrong != null)
            {
                throw new InvalidOperationException(wrong);
            }

            int at = 0;
            Func<DxfEntity, bool> all = Operands(_lines, ref at, CadFilterProperty.Object, out _);
            return all;
        }

        // The operands up to the End that closes a group, or to the end of the lines for the whole list, as one test.
        private static Func<DxfEntity, bool> Operands(
            IReadOnlyList<CadFilterLine> lines,
            ref int at,
            CadFilterProperty group,
            out int count
        )
        {
            var tests = new List<Func<DxfEntity, bool>>();

            while (at < lines.Count)
            {
                CadFilterLine line = lines[at++];

                if (line.IsGroupEnd)
                {
                    break;
                }

                if (line.IsGroupBegin)
                {
                    Func<DxfEntity, bool> inner = Operands(lines, ref at, line.Property, out _);
                    tests.Add(inner);
                }
                else
                {
                    CadFilterLine test = line;
                    tests.Add(entity => test.Passes(entity));
                }
            }

            count = tests.Count;
            Func<DxfEntity, bool>[] all = tests.ToArray();

            switch (group)
            {
                case CadFilterProperty.BeginOr:
                    return entity =>
                    {
                        foreach (Func<DxfEntity, bool> test in all)
                        {
                            if (test(entity))
                            {
                                return true;
                            }
                        }

                        return false;
                    };
                case CadFilterProperty.BeginXor:
                    return entity => all[0](entity) != all[1](entity);
                case CadFilterProperty.BeginNot:
                    return entity => !all[0](entity);
                default:
                    return entity =>
                    {
                        foreach (Func<DxfEntity, bool> test in all)
                        {
                            if (!test(entity))
                            {
                                return false;
                            }
                        }

                        return true;
                    };
            }
        }

        private static IReadOnlyList<CadFilterChoice> MakeChoices()
        {
            var list = new List<CadFilterChoice>();

            void Kind(string name) => list.Add(new CadFilterChoice(name, CadFilterProperty.Object, name));

            void Property(CadFilterProperty property) =>
                list.Add(new CadFilterChoice(CadFilterLine.NameOf(property), property, null));

            Kind("Arc");
            Property(CadFilterProperty.ArcCenter);
            Property(CadFilterProperty.ArcRadius);
            Kind("Attribute");
            Property(CadFilterProperty.AttributePosition);
            Property(CadFilterProperty.AttributeTag);
            Kind("Body");
            Kind("Block");
            Property(CadFilterProperty.BlockName);
            Property(CadFilterProperty.BlockPosition);
            Property(CadFilterProperty.BlockRotation);
            Kind("Circle");
            Property(CadFilterProperty.CircleCenter);
            Property(CadFilterProperty.CircleRadius);
            Property(CadFilterProperty.Color);
            Kind("Dimension");
            Property(CadFilterProperty.DimensionStyle);
            Property(CadFilterProperty.Elevation);
            Kind("Ellipse");
            Property(CadFilterProperty.EllipseCenter);
            Kind("Hatch");
            Property(CadFilterProperty.HatchPatternName);
            Kind("Image");
            Property(CadFilterProperty.ImagePosition);
            Property(CadFilterProperty.Layer);
            Kind("Leader");
            Kind("Line");
            Property(CadFilterProperty.LineStart);
            Property(CadFilterProperty.LineEnd);
            Property(CadFilterProperty.Linetype);
            Property(CadFilterProperty.LinetypeScale);
            Kind("MultiLine");
            Property(CadFilterProperty.MultilineStyle);
            Property(CadFilterProperty.NormalVector);
            Kind("Point");
            Property(CadFilterProperty.PointPosition);
            Kind("Polyline");
            Kind("Ray");
            Kind("Region");
            Kind("Shape");
            Property(CadFilterProperty.ShapePosition);
            Property(CadFilterProperty.ShapeName);
            Kind("Solid");
            Kind("Solid Body");
            Kind("Spline");
            Kind("Text");
            Property(CadFilterProperty.TextPosition);
            Property(CadFilterProperty.TextValue);
            Property(CadFilterProperty.TextStyleName);
            Property(CadFilterProperty.TextHeight);
            Property(CadFilterProperty.TextRotation);
            Kind("Trace");
            Kind("3dface");
            Kind("DGN Underlay");
            Kind("DWF Underlay");
            Kind("Camera");
            Kind("Light");
            Kind("Material");
            Kind("Surface(Planar)");
            Kind("Surface(Sweep)");
            Kind("Surface(Loft)");
            Kind("Surface(Revolve)");
            Kind("Surface(Extrusion)");
            Kind("Multileader");
            Kind("Mesh");
            Kind("Surface(Nurbs)");
            Kind("Surface(Blend)");
            Kind("Helix");
            Kind("Section Plane");
            Kind("Surface(Network)");
            Kind("Surface(Patch)");
            Kind("Surface(Fillet)");
            Kind("Surface(Offset)");
            Kind("Surface(Extend)");
            Property(CadFilterProperty.Transparency);
            Kind("Position Marker");
            Property(CadFilterProperty.Thickness);
            Kind("Tolerance");
            Kind("Viewport");
            Property(CadFilterProperty.ViewportCenter);
            Property(CadFilterProperty.XdataId);
            Kind("Xline");
            Property(CadFilterProperty.BeginAnd);
            Property(CadFilterProperty.EndAnd);
            Property(CadFilterProperty.BeginOr);
            Property(CadFilterProperty.EndOr);
            Property(CadFilterProperty.BeginXor);
            Property(CadFilterProperty.EndXor);
            Property(CadFilterProperty.BeginNot);
            Property(CadFilterProperty.EndNot);
            return list;
        }

        private void Persist()
        {
            if (FilePath == null)
            {
                return;
            }

            try
            {
                string folder = Path.GetDirectoryName(FilePath);

                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                File.WriteAllText(FilePath, ToNfl(), new System.Text.UTF8Encoding(false));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                GeometryHelperLog.Warn(
                    "CadSelectionFilter: the named lists could not be written to " + FilePath + ".",
                    exception
                );
            }
        }
    }
}
