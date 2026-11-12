using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What a line of an object selection filter tests, as the Select Filter list of AutoCAD's FILTER names it.</summary>
    public enum CadFilterProperty
    {
        /// <summary>The kind of the object, as the Properties panel names it: Line, Circle, MText.</summary>
        Object,

        /// <summary>The layer, a name or a wild-card pattern: <c>WALL*</c>.</summary>
        Layer,

        /// <summary>The colour by its index: 1 to 255, 256 ByLayer, 0 ByBlock.</summary>
        Color,

        /// <summary>The linetype by name, ByLayer and ByBlock among them, or a wild-card pattern.</summary>
        Linetype,
    }

    /// <summary>A line of an object selection filter: what it tests and the value it wants, as "Layer = WALLS".</summary>
    public sealed class CadFilterLine
    {
        private static readonly string[] ColorNames = { "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta", "White" };

        /// <summary>Makes a line.</summary>
        /// <param name="property">What it tests.</param>
        /// <param name="value">The value: a kind, a layer or linetype name (wild cards taken), or a colour index; for a
        /// colour, "1", "1 - Red", "Red", "ByLayer" and "ByBlock" are all taken.</param>
        /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a colour is not one AutoCAD's filter takes: "Invalid Color number.".</exception>
        public CadFilterLine(CadFilterProperty property, string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            Property = property;
            Value = value.Trim();

            if (property == CadFilterProperty.Color)
            {
                ColorIndex = ReadColor(Value) ?? throw new ArgumentException("Invalid Color number.", nameof(value));
                Value = ColorWords(ColorIndex);
            }
        }

        /// <summary>Gets what the line tests.</summary>
        public CadFilterProperty Property { get; }

        /// <summary>Gets the value it wants, a colour written as AutoCAD's list writes it: "1 - Red", "256 - By Layer".</summary>
        public string Value { get; }

        /// <summary>Gets the colour index a colour line wants; 0 for the other lines.</summary>
        public int ColorIndex { get; }

        /// <summary>Gets the line as the filter list shows it: the property padded to 21 characters, then "= " and the value, as "Layer                = WALLS".</summary>
        public string Text => Property.ToString().PadRight(21) + "= " + Value;

        /// <summary>Gets whether an entity passes the line.</summary>
        /// <param name="entity">The entity.</param>
        /// <returns>Whether it passes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        public bool Passes(DxfEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            switch (Property)
            {
                case CadFilterProperty.Object:
                    return string.Equals(CadProperties.KindOf(entity), Value, StringComparison.OrdinalIgnoreCase);
                case CadFilterProperty.Layer:
                    return CadWildcard.IsMatch(entity.Layer, Value, true);
                case CadFilterProperty.Linetype:
                    return CadWildcard.IsMatch(entity.Linetype, Value, true);
                default:
                    DxfColor color = entity.Color;
                    int index =
                        color == null || color.IsByLayer ? 256
                        : color.IsByBlock ? 0
                        : color.ColorIndex;
                    return index == ColorIndex;
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

    /// <summary>
    /// An object selection filter, as AutoCAD's FILTER makes one: a list of lines, each of which an object has to pass (the
    /// lines are taken together, as AutoCAD's are where no AND/OR group is given); named lists kept for the session, the
    /// current one <c>*unnamed</c>. Of a plan with 312 lines on WALLS and 40 circles there, Object = Line and Layer = WALLS
    /// pass the 312.
    /// </summary>
    public sealed class CadSelectionFilter
    {
        /// <summary>The name of the list being edited where it has no name of its own, as AutoCAD's.</summary>
        public const string Unnamed = "*unnamed";

        private readonly List<CadFilterLine> _lines = new List<CadFilterLine>();
        private readonly SortedDictionary<string, List<CadFilterLine>> _named = new SortedDictionary<
            string,
            List<CadFilterLine>
        >(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets the lines of the current list, in the order they were added.</summary>
        public IList<CadFilterLine> Lines => _lines;

        /// <summary>Gets or sets the name of the current list: <see cref="Unnamed"/>, or one saved.</summary>
        public string CurrentName { get; set; } = Unnamed;

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
        /// <param name="name">The name; AutoCAD refuses an empty one ("Null Filter name not allowed.") and one with characters
        /// a name cannot hold ("Invalid characters in Filter name.").</param>
        /// <returns>Null where saved, else what AutoCAD says.</returns>
        public string SaveAs(string name)
        {
            string trimmed = (name ?? string.Empty).Trim();

            if (trimmed.Length == 0)
            {
                return "Null Filter name not allowed.";
            }

            if (trimmed.IndexOfAny(new[] { '*', '?', '<', '>', '/', '\\', '"', ':', ';', '|', ',', '=', '`' }) >= 0)
            {
                return "Invalid characters in Filter name.";
            }

            _named[trimmed] = new List<CadFilterLine>(_lines);
            CurrentName = trimmed;
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
            return null;
        }

        /// <summary>Gets whether an entity passes every line; an empty list passes all.</summary>
        /// <param name="entity">The entity.</param>
        /// <returns>Whether it passes.</returns>
        public bool Passes(DxfEntity entity) => _lines.TrueForAll(l => l.Passes(entity));

        /// <summary>Gets the objects of a list that pass, in the order given; handles the document has no entity of are passed over.</summary>
        /// <param name="document">The document.</param>
        /// <param name="handles">The handles.</param>
        /// <returns>The handles that pass.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the handles are null.</exception>
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

            var passed = new List<string>();

            foreach (string handle in handles)
            {
                DxfEntity entity = document.FindEntity(handle);

                if (entity != null && Passes(entity))
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
        /// Gets the lines Add Selected Object adds for an entity, as AutoCAD's: its kind, layer, colour and linetype.
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
            return new[]
            {
                new CadFilterLine(CadFilterProperty.Object, CadProperties.KindOf(entity)),
                new CadFilterLine(CadFilterProperty.Layer, entity.Layer ?? "0"),
                new CadFilterLine(CadFilterProperty.Color, index.ToString(CultureInfo.InvariantCulture)),
                new CadFilterLine(CadFilterProperty.Linetype, entity.Linetype ?? "ByLayer"),
            };
        }
    }
}
