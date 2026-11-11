using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What a prompt draws under the cursor while it waits, as AutoCAD 2021 drags the object a command is about to make: the
    /// entities the command would make were a point picked at the cursor the answer, made by the same code that makes them
    /// when it is, and the copies of objects it would move, each under a transformation. RECTANG asking its other corner, the
    /// cursor at (30, 20) from a first corner at the origin, previews the closed polyline 30 by 20 it makes when (30, 20) is
    /// picked.
    /// </summary>
    public sealed class CadPreview
    {
        private static readonly IReadOnlyList<DisplayItem2> NoItems = Array.Empty<DisplayItem2>();
        private static readonly IReadOnlyList<Affine2> NoMoves = Array.Empty<Affine2>();

        private readonly CadCommandSession _session;
        private readonly Func<CadInput, IEnumerable<DxfEntity>> _make;
        private readonly Func<CadInput, IReadOnlyList<Affine2>> _moves;
        private Func<CadInput, IReadOnlyList<DisplayItem2>> _drawn;
        private bool _fades;
        private bool _swaps;
        private DisplayBuilder _builder;
        private DxfDatabase _database;

        private CadPreview(
            CadCommandSession session,
            Func<CadInput, IEnumerable<DxfEntity>> make,
            IReadOnlyList<string> handles,
            Func<CadInput, IReadOnlyList<Affine2>> moves,
            bool band
        )
        {
            _session = session;
            _make = make;
            _moves = moves;
            Handles = handles ?? Array.Empty<string>();
            ShowsBand = band;
        }

        /// <summary>
        /// Gets whether the rubber band from the prompt's base point is drawn with the preview, as CIRCLE draws its radius to
        /// the cursor; false where AutoCAD draws the object alone, as RECTANG draws its rectangle.
        /// </summary>
        public bool ShowsBand { get; }

        /// <summary>
        /// Gets whether <see cref="Items"/> are pieces the answer takes out of the drawing rather than objects it makes, drawn
        /// faded as AutoCAD 2021's TRIM draws the piece under the cursor it would cut away: of a line 150 long crossed at 50,
        /// the 50 on the cursor's side. With <see cref="Shift"/> held at TRIM or EXTEND it is the other command's: TRIM then
        /// previews the object extended, not faded, and EXTEND fades the piece a trim takes out.
        /// </summary>
        public bool Fades => _fades != (_swaps && Shift);

        /// <summary>
        /// Gets or sets whether Shift is held, which a window sets before it asks for the preview: the answer previewed then
        /// carries it (<see cref="CadInput.Shift"/>), as a pick with Shift held does, so that TRIM previews what EXTEND would
        /// do to the object under the cursor and EXTEND what TRIM would.
        /// </summary>
        public bool Shift { get; set; }

        /// <summary>
        /// Gets or sets the text typed so far at a prompt for a line of text, which a window sets before it asks for the
        /// preview: the answer previewed is then that text, so that TEXT at "Enter text" previews the words typed where the
        /// line will stand. Null or empty previews nothing there.
        /// </summary>
        public string Typed { get; set; }

        /// <summary>Gets the objects whose copies <see cref="Moves"/> places, in ordinal order; empty where the preview moves none.</summary>
        public IReadOnlyList<string> Handles { get; }

        /// <summary>The prompt the preview is drawn for, which <see cref="CadPrompt.Previewing"/> gives it.</summary>
        internal CadPrompt Prompt { get; set; }

        /// <summary>
        /// Gets the display items of the entities the command would make were a point picked at the cursor, drawn as they will
        /// be once made: their layer, colour and linetype the current ones. Empty where the point makes nothing, as a second
        /// corner in line with the first makes no rectangle.
        /// </summary>
        /// <param name="cursor">The cursor, in drawing units.</param>
        /// <returns>The items.</returns>
        public IReadOnlyList<DisplayItem2> Items(GeoPoint2 cursor)
        {
            if (_drawn != null)
            {
                try
                {
                    return _drawn(Answer(cursor)) ?? NoItems;
                }
                catch (Exception)
                {
                    return NoItems;
                }
            }

            if (_make == null)
            {
                return NoItems;
            }

            IEnumerable<DxfEntity> made;
            bool quiet = _session.Quiet;

            try
            {
                // What the command's code would say of the point, as "Radius is too large.", is not written for a preview.
                _session.Quiet = true;
                made = _make(Answer(cursor));
            }
            catch (Exception)
            {
                // A point the command's code cannot make anything of previews nothing.
                return NoItems;
            }
            finally
            {
                _session.Quiet = quiet;
            }

            if (made == null)
            {
                return NoItems;
            }

            // Made once a prompt, as the layers and styles stand while it waits; anew after an undo gave another drawing.
            CadDocument document = _session.Document;

            if (_builder == null || !ReferenceEquals(_database, document.Database))
            {
                _database = document.Database;
                _builder = new DisplayBuilder(_database, document);
            }

            return _builder.BuildLoose(made);
        }

        /// <summary>
        /// Gets the transformations the copies of <see cref="Handles"/> are drawn under were a point picked at the cursor: one
        /// for ROTATE by a reference, one for each copy a rectangular array of 4 columns and 3 rows adds. Empty for none.
        /// </summary>
        /// <param name="cursor">The cursor, in drawing units.</param>
        /// <returns>The transformations.</returns>
        public IReadOnlyList<Affine2> Moves(GeoPoint2 cursor)
        {
            if (_moves == null)
            {
                return NoMoves;
            }

            try
            {
                return _moves(Answer(cursor)) ?? NoMoves;
            }
            catch (Exception)
            {
                return NoMoves;
            }
        }

        /// <summary>
        /// The answer a point picked at the cursor gives the prompt, as the session gives it: the point itself at a prompt for
        /// a point or an object; its distance from the base point at a prompt for a distance; at a prompt for an angle, the
        /// direction to it from the base point, measured in the UCS; at a prompt for one object, the object under the cursor
        /// with the point it is picked at, or the point alone where none is.
        /// </summary>
        private CadInput Answer(GeoPoint2 cursor)
        {
            CadInput answer = AnswerAt(cursor);
            answer.Shift = Shift;
            return answer;
        }

        private CadInput AnswerAt(GeoPoint2 cursor)
        {
            CadPrompt prompt = Prompt;
            GeoPoint2? from = prompt?.BasePoint;

            if (prompt != null && prompt.Kind == CadPromptKind.Entity)
            {
                CadDocument document = _session.Document;
                IReadOnlyList<string> picked = document
                    .GetPickIndex(document.WorkingSpace)
                    .Pick(cursor.X, cursor.Y, _session.PickAperture);

                if (picked.Count > 0 && document.FindEntity(picked[0]) != null)
                {
                    return new CadInput(CadInputKind.Selection, cursor, 0.0, null, new[] { picked[0] });
                }
            }

            if (prompt != null && prompt.Kind == CadPromptKind.String)
            {
                return new CadInput(CadInputKind.Text, cursor, 0.0, Typed ?? string.Empty, null);
            }

            if (
                prompt == null
                || (prompt.Kind != CadPromptKind.Distance && prompt.Kind != CadPromptKind.Angle)
                || !from.HasValue
            )
            {
                return new CadInput(CadInputKind.Point, cursor, 0.0, null, null);
            }

            GeoVector2 way = cursor - from.Value;
            double number = prompt.Kind == CadPromptKind.Distance ? way.Length : _session.PickedAngle(prompt, way);
            return new CadInput(CadInputKind.Number, cursor, number, null, null);
        }

        /// <summary>A preview of entities made from the answer a point picked at the cursor gives.</summary>
        internal static CadPreview Making(
            CadCommandContext c,
            Func<CadInput, IEnumerable<DxfEntity>> make,
            bool band = false
        ) => new CadPreview(c.Session, make, null, null, band);

        /// <summary>A preview of the pieces the answer at the cursor takes out of the drawing, drawn faded, as TRIM previews them.</summary>
        internal static CadPreview Fading(CadCommandContext c, Func<CadInput, IEnumerable<DxfEntity>> make) =>
            new CadPreview(c.Session, make, null, null, false) { _fades = true };

        /// <summary>
        /// A preview of TRIM or EXTEND, which Shift held turns into the other's: faded for the piece a trim takes out, and not
        /// for the object an extension runs on.
        /// </summary>
        internal static CadPreview Cutting(
            CadCommandContext c,
            Func<CadInput, IEnumerable<DxfEntity>> make,
            bool extend
        ) => new CadPreview(c.Session, make, null, null, false) { _fades = !extend, _swaps = true };

        /// <summary>
        /// A preview of the objects of another drawing, drawn by its own layers, styles and blocks under the move the answer at
        /// the cursor gives, as PASTECLIP shows the Clipboard's objects before the drawing has them.
        /// </summary>
        internal static CadPreview Bringing(CadCommandContext c, DxfDatabase from, Func<CadInput, Affine2> move)
        {
            var builder = new DisplayBuilder(from, c.Document);
            var entities = new List<DxfEntity>(from.ModelSpace);
            return new CadPreview(c.Session, null, null, null, false)
            {
                _drawn = input => builder.BuildLoose(entities, move(input)),
            };
        }

        /// <summary>A preview of one entity made from the answer a point picked at the cursor gives; null from the maker previews nothing.</summary>
        internal static CadPreview MakingOne(CadCommandContext c, Func<CadInput, DxfEntity> make, bool band = false) =>
            Making(c, input => One(make(input)), band);

        /// <summary>A preview of the copies of objects, each under the transformations the answer at the cursor gives.</summary>
        internal static CadPreview Moving(
            CadCommandContext c,
            IReadOnlyList<string> handles,
            Func<CadInput, IReadOnlyList<Affine2>> moves,
            bool band = false
        )
        {
            var sorted = new List<string>(handles);
            sorted.Sort(StringComparer.OrdinalIgnoreCase);
            return new CadPreview(c.Session, null, sorted, moves, band);
        }

        /// <summary>A preview of the copies of objects under one transformation the answer at the cursor gives; null gives none.</summary>
        internal static CadPreview MovingOne(
            CadCommandContext c,
            IReadOnlyList<string> handles,
            Func<CadInput, Affine2?> move,
            bool band = false
        ) => Moving(c, handles, input => move(input) is Affine2 m ? new[] { m } : NoMoves, band);

        /// <summary>The transformation of the plane a transformation of space makes, its Z left out.</summary>
        internal static Affine2 Flat(GeoTransform3 t) =>
            new Affine2(t[0, 0], t[1, 0], t[0, 1], t[1, 1], t[0, 3], t[1, 3]);

        private static IEnumerable<DxfEntity> One(DxfEntity entity) => entity == null ? null : new[] { entity };
    }
}
