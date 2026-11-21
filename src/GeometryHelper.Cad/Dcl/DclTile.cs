using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Cad.Dcl
{
    /// <summary>
    /// A DCL tile with its prototypes resolved: a primitive kind, the attributes merged down the chain of definitions, its
    /// children. The definitions of a loaded file are trees of these, and <c>new_dialog</c> works on a copy, whose tiles also
    /// carry what a dialog changes at run time (the value, the items of a list, the picture of an image).
    /// </summary>
    internal sealed class DclTile
    {
        private readonly Dictionary<string, DclAttribute> _attributes;
        private readonly List<DclTile> _children;

        internal DclTile(
            string kind,
            string name,
            IEnumerable<DclAttribute> attributes,
            IEnumerable<DclTile> children,
            string file,
            int line
        )
        {
            Kind = kind;
            Name = name;
            _attributes = new Dictionary<string, DclAttribute>(StringComparer.Ordinal);

            foreach (DclAttribute attribute in attributes ?? Enumerable.Empty<DclAttribute>())
            {
                _attributes[attribute.Name] = attribute;
            }

            _children = new List<DclTile>(children ?? Enumerable.Empty<DclTile>());

            foreach (DclTile child in _children)
            {
                child.Parent = this;
            }

            File = file;
            Line = line;
        }

        /// <summary>
        /// Gets the primitive: dialog, cluster, radio_cluster, button, edit_box, list_box, popup_list, toggle, radio_button,
        /// text, image, image_button, slider or spacer.
        /// </summary>
        internal string Kind { get; }

        /// <summary>Gets the name the tile was written as (<c>ok_button</c>, <c>edit_box</c>, <c>row</c>).</summary>
        internal string Name { get; }

        /// <summary>Gets the tile's key, the text of its <c>key</c> attribute; null for none.</summary>
        internal string Key => Get("key");

        /// <summary>Gets the attributes by name, case-sensitive.</summary>
        internal IReadOnlyDictionary<string, DclAttribute> Attributes => _attributes;

        /// <summary>Gets the children in order.</summary>
        internal IReadOnlyList<DclTile> Children => _children;

        /// <summary>Gets the tile this one is a child of; null for the top of a tree.</summary>
        internal DclTile Parent { get; private set; }

        /// <summary>Gets the file the tile was written in.</summary>
        internal string File { get; }

        /// <summary>Gets the line of the file the tile starts on, from 1.</summary>
        internal int Line { get; }

        /// <summary>Gets or sets the value a dialog holds for the tile, as <c>get_tile</c> gives it; null until <c>new_dialog</c> sets it.</summary>
        internal string Value { get; set; }

        /// <summary>Gets or sets whether the tile takes input; <c>mode_tile</c> 0 and 1 change it.</summary>
        internal bool IsEnabled { get; set; } = true;

        /// <summary>Gets the items of a list box or a popup list.</summary>
        internal List<string> Items { get; } = new List<string>();

        /// <summary>Gets what <c>fill_image</c> and <c>vector_image</c> drew on an image, in order.</summary>
        internal List<DclImageOp> Picture { get; } = new List<DclImageOp>();

        /// <summary>Gets or sets the action <c>action_tile</c> gave; null for none.</summary>
        internal string Action { get; set; }

        /// <summary>Gets or sets the text <c>client_data_tile</c> gave, the <c>$data</c> of the tile's actions; null for none.</summary>
        internal string ClientData { get; set; }

        /// <summary>Gets or sets whether <c>mode_tile</c> 4 flipped the highlight of an image button.</summary>
        internal bool IsFlipped { get; set; }

        /// <summary>Gets an attribute's text as written; null where the tile has no such attribute.</summary>
        internal string Get(string attribute) =>
            _attributes.TryGetValue(attribute, out DclAttribute found) ? found.Text : null;

        /// <summary>Gets whether an attribute is <c>true</c>, in any case.</summary>
        internal bool IsTrue(string attribute) =>
            string.Equals(Get(attribute), "true", StringComparison.OrdinalIgnoreCase);

        /// <summary>Gets an attribute as an integer: an integer as written, a real truncated; null for anything else.</summary>
        internal int? GetInteger(string attribute)
        {
            double? real = GetReal(attribute);
            return real.HasValue ? (int)Math.Truncate(real.Value) : (int?)null;
        }

        /// <summary>Gets an attribute written as an integer or a real; null for anything else.</summary>
        internal double? GetReal(string attribute)
        {
            if (
                _attributes.TryGetValue(attribute, out DclAttribute found)
                && (found.Kind == DclValueKind.Integer || found.Kind == DclValueKind.Real)
                && double.TryParse(found.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            )
            {
                return value;
            }

            return null;
        }

        /// <summary>Finds this tile or the first one under it with the key, depth-first; null for none.</summary>
        internal DclTile Find(string key)
        {
            if (key == null)
            {
                return null;
            }

            if (string.Equals(Key, key, StringComparison.Ordinal))
            {
                return this;
            }

            foreach (DclTile child in _children)
            {
                DclTile found = child.Find(key);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>Gives every tile under this one, depth-first, this one not included.</summary>
        internal IEnumerable<DclTile> Descendants()
        {
            foreach (DclTile child in _children)
            {
                yield return child;

                foreach (DclTile below in child.Descendants())
                {
                    yield return below;
                }
            }
        }

        /// <summary>Makes a deep copy with the same attributes and fresh run-time members.</summary>
        internal DclTile Copy() =>
            new DclTile(Kind, Name, _attributes.Values, _children.Select(child => child.Copy()), File, Line);
    }

    /// <summary>What an operation of an image's picture draws.</summary>
    internal enum DclImageOpKind
    {
        /// <summary>A filled rectangle: <c>fill_image</c>.</summary>
        Fill,

        /// <summary>A line: <c>vector_image</c>.</summary>
        Vector,
    }

    /// <summary>
    /// One operation of an image's picture, in pixels from the tile's top-left: a filled rectangle (x, y, width, height) or
    /// a line (x1, y1, x2, y2), in an ACI colour or a DCL colour (-2, -15, -16, -18).
    /// </summary>
    internal struct DclImageOp
    {
        internal DclImageOp(DclImageOpKind kind, int x1, int y1, int x2, int y2, int color)
        {
            Kind = kind;
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
            Color = color;
        }

        /// <summary>Gets what the operation draws.</summary>
        internal DclImageOpKind Kind { get; }

        /// <summary>Gets the left of a rectangle, or the first X of a line.</summary>
        internal int X1 { get; }

        /// <summary>Gets the top of a rectangle, or the first Y of a line.</summary>
        internal int Y1 { get; }

        /// <summary>Gets the width of a rectangle, or the second X of a line.</summary>
        internal int X2 { get; }

        /// <summary>Gets the height of a rectangle, or the second Y of a line.</summary>
        internal int Y2 { get; }

        /// <summary>Gets the colour: ACI 0-255, or -2, -15, -16, -18 for DCL's colours.</summary>
        internal int Color { get; }
    }
}
