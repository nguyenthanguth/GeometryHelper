using System;
using System.Collections.Generic;

namespace GeometryHelper.Export
{
    /// <summary>
    /// An entity of a DXF file as <see cref="DxfReader"/> read it: the library's shape for it, what the file says of it, and
    /// every group of it as the file has it, so that nothing of the entity is lost.
    /// </summary>
    /// <remarks>
    /// <see cref="DxfWriter.Add(DxfEntity)"/> writes one back: its shape on its layer, in its colour, linetype, lineweight
    /// and thickness.
    /// </remarks>
    public sealed class DxfEntity
    {
        private static readonly IReadOnlyList<string> NoBlocks = Array.AsReadOnly(new string[0]);

        private static readonly IReadOnlyList<DxfGroup> NoGroups = Array.AsReadOnly(new DxfGroup[0]);

        // The lineweights AutoCAD has, in hundredths of a millimetre.
        private static readonly int[] Lineweights = { 0, 5, 9, 13, 15, 18, 20, 25, 30, 35, 40, 50, 53, 60, 70, 80, 90, 100, 106, 120, 140, 158, 200, 211 };

        /// <summary>Whether a lineweight is one of AutoCAD's.</summary>
        internal static bool IsLineweight(int weight) => Array.IndexOf(Lineweights, weight) >= 0;

        /// <summary>
        /// Initializes an entity of a shape of one's own, to be written with its look by <see cref="DxfWriter.Add(DxfEntity)"/>.
        /// </summary>
        /// <param name="shape">The shape: any the writer has an overload of Add for that needs no tolerance, such as a
        /// <see cref="Geometry.GeoPolygon2"/> or a <see cref="Geometry.GeoSolid3"/>.</param>
        /// <param name="layer">The layer; layer 0 when none is given.</param>
        /// <param name="colour">The AutoCAD colour index, 1 to 255; null to take the colour of the layer.</param>
        /// <param name="trueColour">The colour as 0xRRGGBB; null for none.</param>
        /// <param name="linetype">The name of the linetype, such as <c>DASHED</c>; null to take the linetype of the layer.</param>
        /// <param name="lineweight">The lineweight in hundredths of a millimetre, one of AutoCAD's: 0, 5, 9, 13, 15, 18, 20, 25,
        /// 30, 35, 40, 50, 53, 60, 70, 80, 90, 100, 106, 120, 140, 158, 200 or 211; null to take the layer's.</param>
        /// <param name="thickness">The thickness, written on the entities that have one; nought for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the shape is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the colour or true colour is outside its range, the lineweight not one of AutoCAD's, or the thickness not a finite number.</exception>
        /// <remarks>
        /// The entity has no <see cref="Type"/> and no <see cref="Handle"/>, the file not having named them, and no
        /// <see cref="Groups"/>. A plate on the layer steel in red is <c>new DxfEntity(plate, "steel", colour: 1)</c>.
        /// </remarks>
        public DxfEntity(object shape, string layer = null, int? colour = null, int? trueColour = null, string linetype = null, int? lineweight = null, double thickness = 0.0)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            if (colour.HasValue && (colour.Value < 1 || colour.Value > 255))
            {
                throw new ArgumentOutOfRangeException(nameof(colour), colour, "An AutoCAD colour index runs from 1 to 255.");
            }

            if (trueColour.HasValue && (trueColour.Value < 0 || trueColour.Value > 0xFFFFFF))
            {
                throw new ArgumentOutOfRangeException(nameof(trueColour), trueColour, "A true colour is red, green and blue, 0 to 0xFFFFFF.");
            }

            if (lineweight.HasValue && !IsLineweight(lineweight.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(lineweight), lineweight, "A lineweight is one of AutoCAD's, in hundredths of a millimetre: " + string.Join(", ", Lineweights) + ".");
            }

            Guard.Finite(thickness, nameof(thickness), "A thickness has to be a finite number.");

            Shape = shape;
            Layer = string.IsNullOrWhiteSpace(layer) ? "0" : layer;
            Colour = colour;
            TrueColour = trueColour;
            Linetype = string.IsNullOrWhiteSpace(linetype) ? null : linetype;
            Lineweight = lineweight;
            Thickness = thickness;
            Groups = NoGroups;
            BlockPath = NoBlocks;
            InsertHandles = NoBlocks;
        }

        internal DxfEntity(object shape, string type, string layer, int? colour, string handle, string linetype, int? lineweight, int? trueColour, double thickness, IReadOnlyList<DxfGroup> groups, int hiddenEdges = 0, IReadOnlyList<string> blockPath = null, IReadOnlyList<string> insertHandles = null)
        {
            Shape = shape;
            Type = type;
            Layer = layer;
            Colour = colour;
            Handle = handle;
            Linetype = linetype;
            Lineweight = lineweight;
            TrueColour = trueColour;
            Thickness = thickness;
            Groups = groups;
            BlockPath = blockPath ?? NoBlocks;
            InsertHandles = insertHandles ?? NoBlocks;
            HiddenEdges = hiddenEdges;
        }

        /// <summary>
        /// The edges a 3DFACE read whole hides (group 70: 1, 2, 4 and 8 for its first to its fourth), which the writer writes
        /// back on the one 3DFACE of the shape.
        /// </summary>
        internal int HiddenEdges { get; }

        /// <summary>
        /// Gets the shape: a <see cref="Geometry.GeoLine3"/> for a LINE, a <see cref="Geometry.GeoPolylineArc2"/> for a
        /// LWPOLYLINE with bulges read with <see cref="DxfReadOptions.PlanAs2D"/>, and so on; the guide lists them all.
        /// </summary>
        public object Shape { get; }

        /// <summary>
        /// Gets the type of the entity as the file names it, such as <c>LINE</c>, <c>LWPOLYLINE</c> or <c>3DFACE</c>; null for
        /// an entity made in code.
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// Gets the layer the entity is on (group 8), as <see cref="DxfDocument.Layers"/> names it; 0 where the file gives none.
        /// An entity of a block drawn on layer 0 is on the layer of the INSERT that placed it.
        /// </summary>
        public string Layer { get; }

        /// <summary>
        /// Gets the colour of the entity (group 62), an AutoCAD colour index from 1 to 255; null where it takes the colour of
        /// its layer (256), or the file gives none. An entity of a block whose colour is BYBLOCK (0) has the colour of the
        /// INSERT that placed it, and its true colour where it has none of its own; one of the drawing itself has none.
        /// </summary>
        public int? Colour { get; }

        /// <summary>
        /// Gets the colour of the entity as red, green and blue (group 420), 0xRRGGBB, which a CAD program draws in place
        /// of <see cref="Colour"/>: 0xFF8000 is orange. Null where the file gives none.
        /// </summary>
        public int? TrueColour { get; }

        /// <summary>
        /// Gets the name of the linetype of the entity (group 6), such as <c>DASHED</c>, <c>Continuous</c> or <c>ByBlock</c>;
        /// null where it takes the linetype of its layer, BYLAYER, written or not. An entity of a block whose linetype is
        /// BYBLOCK has the linetype of the INSERT that placed it.
        /// </summary>
        public string Linetype { get; }

        /// <summary>
        /// Gets the lineweight of the entity (group 370) in hundredths of a millimetre, one of AutoCAD's 0 to 211: 25 is a line
        /// 0.25 wide. Null where the file gives none, or one of the three the DXF reference gives as numbers below nought (-1
        /// by layer, -2 by block and -3 the default), or a number not among AutoCAD's, which a warning counts. An entity of a
        /// block whose lineweight is by block has the lineweight of the INSERT that placed it.
        /// </summary>
        public int? Lineweight { get; }

        /// <summary>
        /// Gets the thickness of the entity (group 39), how far it stands up along its extrusion, scaled to millimetres
        /// where <see cref="DxfReadOptions.ToMillimetres"/> asks; nought where the file gives none. The shape is of the
        /// entity itself and does not stand up by it. Of an entity placed by an INSERT it is stretched as the INSERT stretches
        /// the extrusion, and turned over where the placed extrusion points against the entity's own: 10 is 30 under a scale
        /// of 3 along z, and -30 under one of -3.
        /// </summary>
        public double Thickness { get; }

        /// <summary>
        /// Gets the handle of the entity (group 5), as the file writes it in hexadecimal; null where it has none, as in a
        /// file of R12, or for an entity made in code.
        /// </summary>
        public string Handle { get; }

        /// <summary>
        /// Gets every group of the entity's record, in the order of the file, after the group 0 that names its type:
        /// extended data (1000 to 1071) and groups no reader looks at among them, each value the text the file prints.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Of a POLYLINE they run on through its VERTEX records and its SEQEND, each opened by its own group 0, as the file
        /// has them. Two entities made from one record, the two triangles of a 3DFACE out of flat, share its groups.
        /// </para>
        /// <para>
        /// The reader takes from them the layer (8), the handle (5), the colour (62 and 420), the linetype (6), the
        /// lineweight (370), the thickness (39), paper space (67) and the groups of the shape: the points (10 to 13 with
        /// 20 to 23 and 30 to 33), the extrusion (210, 220, 230), the sizes, angles, bulges and elevation (38 to 51), the
        /// flags and counts (70 to 74), and the knots, weights and points of a spline. Any other group is here and nowhere
        /// else. <see cref="DxfWriter.Add(DxfEntity)"/> does not write them.
        /// </para>
        /// </remarks>
        public IReadOnlyList<DxfGroup> Groups { get; }

        /// <summary>
        /// Gets the names of the blocks the entity was placed through, outermost first, each as its definition writes it:
        /// <c>FRAME</c>, <c>BOLT</c> for a circle of the block BOLT inserted in the block FRAME inserted in the drawing. Empty
        /// for an entity of the drawing itself, and for one made in code.
        /// </summary>
        /// <remarks>
        /// The entity's <see cref="Handle"/> is its own, in the block; two INSERTs of one block give entities of one handle
        /// and one path, told apart by <see cref="InsertHandles"/>.
        /// </remarks>
        public IReadOnlyList<string> BlockPath { get; }

        /// <summary>
        /// Gets the handles of the INSERTs the entity was placed through, outermost first, one to each name of
        /// <see cref="BlockPath"/>: the first is the handle of the INSERT in the drawing. An INSERT without a handle, as in
        /// a file of R12, gives null; the cells of one MINSERT share its handle. Empty for an entity of the drawing itself.
        /// </summary>
        public IReadOnlyList<string> InsertHandles { get; }
    }
}
