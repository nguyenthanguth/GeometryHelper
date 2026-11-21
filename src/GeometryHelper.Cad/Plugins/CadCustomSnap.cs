using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Plugins
{
    /// <summary>
    /// An object snap a plugin adds, as AutoCAD's <c>CustomObjectSnapMode</c>: a name, a keyword typed as an override, a glyph,
    /// the entity types it applies to and the points the plugin works out for an entity under the cursor. Beyond AutoCAD, the
    /// window lists it in the Object Snap tab of Drafting Settings, the OSNAP status menu and the Shift+right-click menu, and
    /// turns it on and off as it does the built-in snaps, keeping the choice in the settings by keyword.
    /// </summary>
    /// <remarks>
    /// A snap on gives its points while object snap (F3) is on, and competes with the built-in snaps by distance from the
    /// cursor: at equal distance the built-in one wins, then the snap loaded first, then the first point the finder gave.
    /// A snap applies to model and paper space as they are shown, to the top-level entity under the aperture; it is not tried
    /// in a model space seen through a viewport, on entities nested in blocks, or for points typed after an override.
    /// </remarks>
    public sealed class CadCustomSnap
    {
        /// <summary>Makes a snap.</summary>
        /// <param name="name">The name the menus, Drafting Settings and the snap tip show: "Circle top".</param>
        /// <param name="keyword">The word typed to take only this snap for the next point, in either case: "CTOP". Checked when the snap is added to the host: letters and digits only, 2 to 16 long, not an AutoCAD snap keyword (END MID CEN GCE NOD QUA INT EXT INS PER TAN NEA APP PAR NON) or a word that starts with one, and not FROM, TT, M2P or MTP.</param>
        /// <param name="finder">Gives the points for an entity.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name, the keyword or the finder is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty.</exception>
        public CadCustomSnap(string name, string keyword, CadSnapFinder finder)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));

            if (name.Trim().Length == 0)
            {
                throw new ArgumentException("A snap's name is not empty.", nameof(name));
            }

            Keyword = (keyword ?? throw new ArgumentNullException(nameof(keyword))).ToUpperInvariant();
            Finder = finder ?? throw new ArgumentNullException(nameof(finder));
        }

        /// <summary>Gets the name the menus, Drafting Settings and the snap tip show: "Circle top".</summary>
        public string Name { get; }

        /// <summary>Gets the keyword typed as an override, in capitals: "CTOP".</summary>
        public string Keyword { get; }

        /// <summary>Gets what gives the points.</summary>
        public CadSnapFinder Finder { get; }

        /// <summary>
        /// Gets the types of entity the snap applies to, as AutoCAD's <c>ApplyToEntityType</c>: the entities whose type is one
        /// of them, or a subclass of one. Empty, until filled, for every entity. Each must derive from
        /// <see cref="DxfEntity"/>, which <see cref="CadPluginHost.AddObjectSnap"/> checks.
        /// </summary>
        public IList<Type> EntityTypes { get; } = new List<Type>();

        /// <summary>
        /// Gets or sets the marker: strokes in pixels about the snap point, Y down, as <see cref="CadSnapMarker.Strokes"/>
        /// gives them, drawn in the marker colour and width. Null, until set, for the default glyph: a square 9 across with a
        /// filled dot of radius 1.2 in its centre.
        /// </summary>
        public IReadOnlyList<CadMarkerStroke> Glyph { get; set; }

        /// <summary>Gets or sets whether the snap starts on for a user who has never chosen it: false until set.</summary>
        public bool IsOnByDefault { get; set; }

        /// <inheritdoc/>
        public override string ToString() => Name + " (" + Keyword + ")";
    }

    /// <summary>
    /// Works out the points of a custom snap for the entity under the cursor, in the world coordinates of the space shown;
    /// those farther from the cursor than the aperture are dropped. It is called once per entity per mouse move, so it
    /// should be quick; one that throws is switched off for the session, with a Warn and a line on the command line.
    /// </summary>
    /// <param name="request">The entity, the cursor and the rest.</param>
    /// <returns>The points, the first one preferred where two are as near; null for none.</returns>
    public delegate IEnumerable<GeoPoint2> CadSnapFinder(CadSnapRequest request);

    /// <summary>What a <see cref="CadSnapFinder"/> is given: the top-level entity under the aperture and where the cursor is.</summary>
    public sealed class CadSnapRequest
    {
        internal CadSnapRequest(
            CadDocument document,
            DxfEntity entity,
            string handle,
            GeoPoint2 cursor,
            double aperture,
            GeoPoint2? basePoint
        )
        {
            Document = document;
            Entity = entity;
            Handle = handle;
            Cursor = cursor;
            Aperture = aperture;
            BasePoint = basePoint;
        }

        /// <summary>Gets the drawing; null where the snap was asked without one.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the entity, which the finder reads and must not change.</summary>
        public DxfEntity Entity { get; }

        /// <summary>Gets the handle of the entity.</summary>
        public string Handle { get; }

        /// <summary>Gets the cursor, in the world coordinates of the space shown.</summary>
        public GeoPoint2 Cursor { get; }

        /// <summary>Gets how near a point must lie to the cursor to be taken, in drawing units: the aperture in pixels over the pixels a unit is drawn.</summary>
        public double Aperture { get; }

        /// <summary>Gets the point the rubber band is drawn from, as the built-in perpendicular works from it; null for none.</summary>
        public GeoPoint2? BasePoint { get; }
    }
}
