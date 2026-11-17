using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad
{
    /// <summary>How a tool palette lists its tools, as AutoCAD's View Options offers it.</summary>
    public enum CadToolViewStyle
    {
        /// <summary>Each tool as its icon alone, side by side.</summary>
        IconOnly,

        /// <summary>Each tool as its icon with its name under it, side by side.</summary>
        IconWithText,

        /// <summary>Each tool as a row, its small icon and its name beside it.</summary>
        List,
    }

    /// <summary>
    /// A tool palette: a name and the tools in the order they are listed, and how they are listed (<see cref="ViewStyle"/>
    /// and <see cref="IconSize"/>). A palette of a set is made through <see cref="CadToolPalettes"/>.
    /// </summary>
    public sealed class CadToolPalette
    {
        /// <summary>The smallest icon size, in pixels.</summary>
        public const int MinIconSize = 16;

        /// <summary>The largest icon size, in pixels.</summary>
        public const int MaxIconSize = 96;

        /// <summary>The icon size a palette starts with, in pixels.</summary>
        public const int DefaultIconSize = 32;

        private readonly List<CadTool> _tools = new List<CadTool>();
        private string _name;
        private int _iconSize = DefaultIconSize;

        /// <summary>Makes an empty palette.</summary>
        /// <param name="name">The name shown on its tab.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is blank.</exception>
        public CadToolPalette(string name)
        {
            Name = name;
        }

        /// <summary>Gets or sets the name shown on the palette's tab; the set it is in keeps them apart where it renames.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name set is blank.</exception>
        public string Name
        {
            get => _name;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value.Trim().Length == 0)
                {
                    throw new ArgumentException("A palette's name is not blank.", nameof(value));
                }

                _name = value.Trim();
            }
        }

        /// <summary>Gets the tools, in the order they are listed.</summary>
        public IReadOnlyList<CadTool> Tools => _tools;

        /// <summary>Gets or sets how the tools are listed; icons alone until set.</summary>
        public CadToolViewStyle ViewStyle { get; set; }

        /// <summary>Gets or sets the size of the icons in pixels, from <see cref="MinIconSize"/> to <see cref="MaxIconSize"/>; 32 until set.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size set is outside those.</exception>
        public int IconSize
        {
            get => _iconSize;
            set
            {
                if (value < MinIconSize || value > MaxIconSize)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "An icon is " + MinIconSize + " to " + MaxIconSize + " pixels."
                    );
                }

                _iconSize = value;
            }
        }

        /// <summary>Puts a tool last.</summary>
        /// <param name="tool">The tool.</param>
        /// <exception cref="ArgumentNullException">Thrown when the tool is null.</exception>
        public void Add(CadTool tool) => Insert(_tools.Count, tool);

        /// <summary>Puts a tool at a place of the list.</summary>
        /// <param name="index">The place, 0 first; one past the last, or beyond, puts it last, one below nought first.</param>
        /// <param name="tool">The tool.</param>
        /// <exception cref="ArgumentNullException">Thrown when the tool is null.</exception>
        public void Insert(int index, CadTool tool)
        {
            if (tool == null)
            {
                throw new ArgumentNullException(nameof(tool));
            }

            _tools.Insert(Math.Max(0, Math.Min(index, _tools.Count)), tool);
        }

        /// <summary>Takes a tool out.</summary>
        /// <param name="tool">The tool.</param>
        /// <returns>Whether the palette had it.</returns>
        public bool Remove(CadTool tool) => tool != null && _tools.Remove(tool);

        /// <summary>Moves a tool to another place of the list, as dragging it within the palette does.</summary>
        /// <param name="tool">The tool.</param>
        /// <param name="index">The place it has afterwards, as <see cref="Insert"/> counts it.</param>
        /// <returns>Whether the palette had the tool.</returns>
        public bool Move(CadTool tool, int index)
        {
            if (tool == null || !_tools.Remove(tool))
            {
                return false;
            }

            Insert(index, tool);
            return true;
        }

        /// <summary>Gets a copy of the palette, tools and view included, each tool a copy.</summary>
        /// <returns>The copy.</returns>
        public CadToolPalette Clone()
        {
            var copy = new CadToolPalette(_name) { ViewStyle = ViewStyle, IconSize = IconSize };

            foreach (CadTool tool in _tools)
            {
                copy._tools.Add(tool.Clone());
            }

            return copy;
        }
    }
}
