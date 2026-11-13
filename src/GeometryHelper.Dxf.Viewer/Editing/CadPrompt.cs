using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What a prompt asks for.</summary>
    public enum CadPromptKind
    {
        /// <summary>A point, picked or typed as x,y, @dx,dy or @distance&lt;angle.</summary>
        Point,

        /// <summary>
        /// A distance, typed or as the distance from the prompt's base point to a point picked; with no base point, as the
        /// distance between two points, the second asked "Specify second point:".
        /// </summary>
        Distance,

        /// <summary>
        /// An angle in degrees, typed or as the direction from the prompt's base point to a point picked; with no base point,
        /// as the direction from one point to a second, asked "Specify second point:".
        /// </summary>
        Angle,

        /// <summary>Objects, picked one by one or by a window, until Enter ends the selection.</summary>
        Selection,

        /// <summary>One object, picked.</summary>
        Entity,

        /// <summary>One of the prompt's keywords.</summary>
        Keyword,

        /// <summary>A text.</summary>
        String,

        /// <summary>A whole number.</summary>
        Integer,
    }

    /// <summary>
    /// One question a command asks on the command line, as AutoCAD asks it: a message, the keywords offered in square brackets,
    /// each typed by its capital letters, and the default Enter takes in angle brackets, so that
    /// <c>Specify base point or [Displacement] &lt;Displacement&gt;:</c> is a point prompt with the keyword Displacement,
    /// which is also its default.
    /// </summary>
    public sealed class CadPrompt
    {
        private CadPrompt(
            CadPromptKind kind,
            string message,
            IReadOnlyList<string> keywords,
            string defaultValue,
            GeoPoint2? basePoint
        )
        {
            Kind = kind;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Keywords = keywords ?? Array.Empty<string>();
            Default = defaultValue;
            BasePoint = basePoint;
        }

        /// <summary>Gets what the prompt asks for.</summary>
        public CadPromptKind Kind { get; }

        /// <summary>Gets the message before the keywords: "Specify base point".</summary>
        public string Message { get; }

        /// <summary>Gets the keywords offered, written as AutoCAD writes them, the capital letters being what is typed: "Displacement", "mOde".</summary>
        public IReadOnlyList<string> Keywords { get; }

        /// <summary>Gets the default Enter takes, as shown in angle brackets; null for none. A default that is one of the keywords is that keyword.</summary>
        public string Default { get; }

        /// <summary>Gets whether text that is neither a point nor a keyword is taken as an answer of text, as ZOOM takes 2X; false for most prompts.</summary>
        public bool AcceptsText { get; private set; }

        /// <summary>Gets the point a rubber band is drawn from and a typed relative point, distance or angle is measured from; null for none.</summary>
        public GeoPoint2? BasePoint { get; }

        /// <summary>
        /// Gets the point perpendicular and tangent snaps are drawn from where the prompt has no <see cref="BasePoint"/>, as
        /// AutoCAD 2021's dimension commands draw them from the last point given (LASTPOINT) at their first extension line
        /// origin, the vertex of an angle and the feature of an ordinate; null for every other prompt. No rubber band is drawn
        /// from it, and ortho and polar do not work from it.
        /// </summary>
        public GeoPoint2? SnapFrom { get; private set; }

        /// <summary>
        /// Whether a number typed at a prompt for a point with no base point is a distance from the last point given
        /// (LASTPOINT) towards the cursor, as SETVAR's points take it in AutoCAD 2021: GRIDUNIT 5 with the last point at
        /// (10, 20) is the point 5 from it. False for every other prompt, which refuses a number alone.
        /// </summary>
        internal bool FromLastPoint { get; private set; }

        /// <summary>Makes a number typed a distance from the last point (<see cref="FromLastPoint"/>).</summary>
        internal CadPrompt MeasuringFromLastPoint()
        {
            FromLastPoint = true;
            return this;
        }

        /// <summary>Gives the prompt the point its perpendicular and tangent snaps are drawn from (<see cref="SnapFrom"/>).</summary>
        internal CadPrompt SnappingFrom(GeoPoint2 point)
        {
            SnapFrom = point;
            return this;
        }

        /// <summary>
        /// Gets the text edited in place on the canvas where the prompt asks for one, as TEXTEDIT and MTEXT ask: the window shows
        /// its edit box or the MTEXT editor over the text and answers the prompt with the words when the edit ends, Enter with
        /// nothing where it is cancelled; a test answers it as any prompt for text. Null for every other prompt.
        /// </summary>
        public CadInPlaceEdit InPlace { get; private set; }

        /// <summary>
        /// Gets the block dragged with the cursor while the prompt waits, as INSERT drags it while it asks the insertion point,
        /// the scale and the rotation: the canvas draws the block's outlines where the cursor would place it. Null for every
        /// other prompt.
        /// </summary>
        public CadBlockDrag Drag { get; private set; }

        /// <summary>
        /// Gets the point the canvas marks with a cross while the prompt waits, as -ATTEDIT marks the attribute it is editing;
        /// null for none.
        /// </summary>
        public GeoPoint2? Mark { get; private set; }

        /// <summary>
        /// Gets the band an option of "Select objects:" draws while the prompt asks its next point: the box of Window, Crossing
        /// and BOX from its first corner, the line of Fence and the polygon of WPolygon and CPolygon through the points given,
        /// each to the cursor. Null for every other prompt.
        /// </summary>
        public CadSelectionBand Band { get; internal set; }

        /// <summary>
        /// Gets whether the prompt follows the cursor, as REVCLOUD's Freehand follows the crosshairs: the canvas answers it with
        /// each point the cursor moves to, and the history keeps none of those answers. False for every other prompt.
        /// </summary>
        public bool Tracks { get; private set; }

        /// <summary>
        /// Gets what the canvas draws under the cursor while the prompt waits, as AutoCAD 2021 drags the object about to be
        /// made: the rectangle RECTANG would make with the cursor as its other corner, the circle CIRCLE would make with the
        /// cursor on it, the copies ROTATE would turn by a reference. Null where the prompt draws no more than its rubber band.
        /// </summary>
        public CadPreview Preview { get; private set; }

        /// <summary>Gives the prompt what it draws under the cursor (<see cref="Preview"/>).</summary>
        internal CadPrompt Previewing(CadPreview preview)
        {
            Preview = preview;
            preview.Prompt = this;
            return this;
        }

        /// <summary>Makes the prompt one the cursor's moves answer (<see cref="Tracks"/>).</summary>
        internal CadPrompt Tracking()
        {
            Tracks = true;
            return this;
        }

        /// <summary>Gives the prompt a block dragged with the cursor (<see cref="Drag"/>).</summary>
        internal CadPrompt Dragging(CadBlockDrag drag)
        {
            Drag = drag;
            return this;
        }

        /// <summary>Gives the prompt a point marked on the canvas (<see cref="Mark"/>).</summary>
        internal CadPrompt Marking(GeoPoint2 point)
        {
            Mark = point;
            return this;
        }

        /// <summary>
        /// Gets whether the angle asked is an amount turned, as ROTATE's rotation angle, the included angle of an arc and the
        /// rotation of INSERT are, and not a direction: an angle typed is then turned the way ANGDIR counts and ANGBASE is left
        /// out of it, and a point picked gives its direction counted from ANGBASE, as AutoCAD's own prompts do. With ANGBASE at
        /// north and ANGDIR clockwise, 30 typed at ROTATE turns the objects 30 degrees clockwise, where 30 typed for the
        /// rotation of a text sets it along the direction 60 degrees counter-clockwise from east. False for a prompt that asks
        /// a direction, and for every prompt that asks no angle.
        /// </summary>
        public bool AsksTurn { get; private set; }

        /// <summary>
        /// Gets whether the angle asked is taken as typed, counter-clockwise, whatever ANGBASE and ANGDIR are, as the obliquing
        /// angle of a text style and the direction -UNITS asks for angle nought are. False for every other prompt.
        /// </summary>
        public bool AsksPlainAngle { get; private set; }

        /// <summary>Makes the angle asked an amount turned (<see cref="AsksTurn"/>).</summary>
        internal CadPrompt Turning()
        {
            AsksTurn = true;
            return this;
        }

        /// <summary>Makes the angle asked one taken as typed (<see cref="AsksPlainAngle"/>).</summary>
        internal CadPrompt Plain()
        {
            AsksPlainAngle = true;
            return this;
        }

        /// <summary>
        /// The words written for a typed answer the prompt cannot take, where they are not those of its kind: SETVAR's
        /// "Requires an integer between 0 and 7." for GRIDSTYLE, "Invalid 2D point." for SNAPUNIT; null for its kind's.
        /// </summary>
        internal string Refusal { get; private set; }

        /// <summary>Whether a pick on nothing asks again without "Nothing selected.", as AutoCAD 2021's MLEDIT asks.</summary>
        internal bool QuietMiss { get; private set; }

        /// <summary>Makes a pick on nothing ask again without a word (<see cref="QuietMiss"/>).</summary>
        internal CadPrompt MissingQuietly()
        {
            QuietMiss = true;
            return this;
        }

        /// <summary>The words written for a pick on nothing where they are not "Nothing selected.": CAL's "No object picked."; null for the usual.</summary>
        internal string MissText { get; private set; }

        /// <summary>Gives a pick on nothing its own words (<see cref="MissText"/>).</summary>
        internal CadPrompt MissingWith(string words)
        {
            MissText = words;
            return this;
        }

        /// <summary>Gives the prompt its own words for a typed answer it cannot take (<see cref="Refusal"/>).</summary>
        internal CadPrompt Refusing(string words)
        {
            Refusal = words;
            return this;
        }

        // Keywords taken but not shown, as a grip edit takes MOVE or MO at its STRETCH prompt.
        private IReadOnlyList<string> _hidden = Array.Empty<string>();

        /// <summary>Gives the prompt keywords it takes without showing them in its brackets, as AutoCAD's grip modes take MOVE or MO.</summary>
        internal CadPrompt Hiding(params string[] keywords)
        {
            _hidden = keywords;
            return this;
        }

        /// <summary>
        /// Gets the whole text the command line shows: <c>Specify base point or [Displacement] &lt;Displacement&gt;:</c>; after
        /// "&gt;&gt;" for a command run inside another, as 'ZOOM's <c>&gt;&gt;Specify first corner:</c>.
        /// </summary>
        public string Text
        {
            get
            {
                var text = new StringBuilder(_transparent ? ">>" : string.Empty).Append(Message);

                if (Keywords.Count > 0)
                {
                    text.Append(Kind == CadPromptKind.Keyword || _noOr ? " [" : " or [")
                        .Append(string.Join("/", Keywords))
                        .Append(']');
                }

                if (!string.IsNullOrEmpty(Default))
                {
                    text.Append(" <").Append(Default).Append('>');
                }

                return _noColon ? text.ToString() : text.Append(':').ToString();
            }
        }

        // Whether the keywords follow the message with no "or", as AutoCAD writes TEXTALIGN's "Select text objects to align [alIgnment/Options]:".
        private bool _noOr;

        /// <summary>Makes the prompt's keywords follow its message with no "or", as AutoCAD writes TEXTALIGN's.</summary>
        internal CadPrompt WithoutOr()
        {
            _noOr = true;
            return this;
        }

        // Whether the text ends without a colon, as AutoCAD's "of" of a point filter.
        private bool _noColon;

        /// <summary>Makes the prompt's text end without a colon, as AutoCAD writes "of" after ".X".</summary>
        internal CadPrompt WithoutColon()
        {
            _noColon = true;
            return this;
        }

        // Whether the prompt is a transparent command's, written after ">>".
        private bool _transparent;

        /// <summary>Makes the prompt's text start with "&gt;&gt;", as AutoCAD writes the prompts of 'PAN and 'ZOOM.</summary>
        internal CadPrompt Transparently()
        {
            _transparent = true;
            return this;
        }

        /// <summary>Asks for a point.</summary>
        /// <param name="message">The message: "Specify base point".</param>
        /// <param name="basePoint">The point a rubber band is drawn from; null for none.</param>
        /// <param name="keywords">The keywords offered; null for none.</param>
        /// <param name="defaultValue">The default Enter takes; null for none.</param>
        /// <param name="acceptsText">Whether text that is neither a point nor a keyword is taken as an answer of text, as ZOOM takes 2X.</param>
        /// <returns>The prompt.</returns>
        public static CadPrompt ForPoint(
            string message,
            GeoPoint2? basePoint = null,
            IReadOnlyList<string> keywords = null,
            string defaultValue = null,
            bool acceptsText = false
        ) =>
            new CadPrompt(CadPromptKind.Point, message, keywords, defaultValue, basePoint)
            {
                AcceptsText = acceptsText,
            };

        /// <summary>Asks for a distance.</summary>
        /// <param name="message">The message: "Specify radius of circle".</param>
        /// <param name="basePoint">The point a picked point's distance is measured from; null for none.</param>
        /// <param name="keywords">The keywords offered; null for none.</param>
        /// <param name="defaultValue">The default Enter takes; null for none.</param>
        /// <returns>The prompt.</returns>
        public static CadPrompt ForDistance(
            string message,
            GeoPoint2? basePoint = null,
            IReadOnlyList<string> keywords = null,
            string defaultValue = null
        ) => new CadPrompt(CadPromptKind.Distance, message, keywords, defaultValue, basePoint);

        /// <summary>Asks for an angle.</summary>
        /// <param name="message">The message: "Specify rotation angle".</param>
        /// <param name="basePoint">The point a picked point's direction is measured from; null for none.</param>
        /// <param name="keywords">The keywords offered; null for none.</param>
        /// <param name="defaultValue">The default Enter takes; null for none.</param>
        /// <returns>The prompt.</returns>
        public static CadPrompt ForAngle(
            string message,
            GeoPoint2? basePoint = null,
            IReadOnlyList<string> keywords = null,
            string defaultValue = null
        ) => new CadPrompt(CadPromptKind.Angle, message, keywords, defaultValue, basePoint);

        /// <summary>Asks for objects until Enter: "Select objects:".</summary>
        /// <param name="message">The message; "Select objects" where null.</param>
        /// <returns>The prompt.</returns>
        public static CadPrompt ForSelection(string message = null) =>
            new CadPrompt(CadPromptKind.Selection, message ?? "Select objects", null, null, null);

        /// <summary>Asks "Select objects or [Name/Description]:", as GROUP asks: objects until Enter, or a keyword.</summary>
        internal static CadPrompt ForSelection(string message, IReadOnlyList<string> keywords) =>
            new CadPrompt(CadPromptKind.Selection, message, keywords, null, null);

        /// <summary>Asks for one object: "Select arc or circle:".</summary>
        /// <param name="message">The message.</param>
        /// <param name="keywords">The keywords offered; null for none.</param>
        /// <param name="defaultValue">The default Enter takes; null for none.</param>
        /// <returns>The prompt.</returns>
        public static CadPrompt ForEntity(
            string message,
            IReadOnlyList<string> keywords = null,
            string defaultValue = null
        ) => new CadPrompt(CadPromptKind.Entity, message, keywords, defaultValue, null);

        /// <summary>Asks for one of a list of keywords: "Erase source objects? [Yes/No] &lt;No&gt;:".</summary>
        /// <param name="message">The message.</param>
        /// <param name="keywords">The keywords.</param>
        /// <param name="defaultValue">The default Enter takes; null for none.</param>
        /// <returns>The prompt.</returns>
        public static CadPrompt ForKeyword(
            string message,
            IReadOnlyList<string> keywords,
            string defaultValue = null
        ) =>
            new CadPrompt(
                CadPromptKind.Keyword,
                message,
                keywords ?? throw new ArgumentNullException(nameof(keywords)),
                defaultValue,
                null
            );

        /// <summary>Asks for one of a list of keywords or a text, as CECOLOR takes Truecolor or a colour: "Enter new object color [Truecolor/COlorbook] &lt;BYLAYER&gt;:".</summary>
        internal static CadPrompt ForKeywordOrText(
            string message,
            IReadOnlyList<string> keywords,
            string defaultValue
        ) => new CadPrompt(CadPromptKind.Keyword, message, keywords, defaultValue, null) { AcceptsText = true };

        /// <summary>Asks for a text.</summary>
        /// <param name="message">The message: "Enter text".</param>
        /// <param name="defaultValue">The default Enter takes; null for none.</param>
        /// <returns>The prompt.</returns>
        public static CadPrompt ForString(string message, string defaultValue = null) =>
            new CadPrompt(CadPromptKind.String, message, null, defaultValue, null);

        /// <summary>Asks for the words of a text edited in place (<see cref="InPlace"/>): a prompt for text the window answers from its editor.</summary>
        /// <param name="message">The message: "Edit text".</param>
        /// <param name="edit">The text edited.</param>
        /// <returns>The prompt.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the edit is null.</exception>
        public static CadPrompt ForInPlaceEdit(string message, CadInPlaceEdit edit) =>
            new CadPrompt(CadPromptKind.String, message, null, null, null)
            {
                InPlace = edit ?? throw new ArgumentNullException(nameof(edit)),
            };

        /// <summary>Asks for a whole number.</summary>
        /// <param name="message">The message.</param>
        /// <param name="keywords">The keywords offered; null for none.</param>
        /// <param name="defaultValue">The default Enter takes; null for none.</param>
        /// <returns>The prompt.</returns>
        public static CadPrompt ForInteger(
            string message,
            IReadOnlyList<string> keywords = null,
            string defaultValue = null
        ) => new CadPrompt(CadPromptKind.Integer, message, keywords, defaultValue, null);

        /// <summary>
        /// Finds the keyword a typed answer names: the whole keyword or its capital letters, either case, or a longer start of
        /// it; "D", "disp" and "Displacement" all name Displacement, and "CL" names CLose where C names Close.
        /// </summary>
        /// <param name="typed">The answer typed.</param>
        /// <returns>The keyword as the prompt writes it, or null.</returns>
        public string MatchKeyword(string typed)
        {
            if (string.IsNullOrWhiteSpace(typed))
            {
                return null;
            }

            string answer = typed.Trim();
            string found = Match(Keywords, answer);

            if (found != null || _hidden.Count == 0)
            {
                return found;
            }

            return Match(_hidden, answer);
        }

        /// <summary>The keyword of a list an answer names: its capital letters first, as AutoCAD reads them, then the whole word, then a longer start of one.</summary>
        private static string Match(IReadOnlyList<string> keywords, string answer)
        {
            foreach (string keyword in keywords)
            {
                if (string.Equals(Shortcut(keyword), answer, StringComparison.OrdinalIgnoreCase))
                {
                    return keyword;
                }
            }

            foreach (string keyword in keywords)
            {
                if (string.Equals(keyword, answer, StringComparison.OrdinalIgnoreCase))
                {
                    return keyword;
                }
            }

            foreach (string keyword in keywords)
            {
                string shortcut = Shortcut(keyword);

                if (
                    answer.Length >= shortcut.Length
                    && keyword.StartsWith(answer, StringComparison.OrdinalIgnoreCase)
                    && keyword.StartsWith(shortcut, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return keyword;
                }
            }

            return null;
        }

        /// <summary>The capital letters of a keyword, what AutoCAD types it by: D for Displacement, O for mOde, CL for CLose.</summary>
        /// <param name="keyword">The keyword.</param>
        /// <returns>Its capital letters, or the whole keyword where it has none.</returns>
        public static string Shortcut(string keyword)
        {
            if (keyword == null)
            {
                throw new ArgumentNullException(nameof(keyword));
            }

            var letters = new StringBuilder();

            foreach (char c in keyword)
            {
                if (char.IsUpper(c) || char.IsDigit(c))
                {
                    letters.Append(c);
                }
            }

            return letters.Length == 0 ? keyword : letters.ToString();
        }

        /// <inheritdoc/>
        public override string ToString() => Text;
    }

    /// <summary>What kind of answer was given to a prompt.</summary>
    public enum CadInputKind
    {
        /// <summary>Enter or Space with nothing typed: the prompt's default, or the end of a list.</summary>
        None,

        /// <summary>A point.</summary>
        Point,

        /// <summary>A number: a distance, an angle in radians, a whole number.</summary>
        Number,

        /// <summary>One of the prompt's keywords.</summary>
        Keyword,

        /// <summary>A text.</summary>
        Text,

        /// <summary>Objects: the selection gathered, or the one object picked.</summary>
        Selection,
    }

    /// <summary>The answer a command got to its last prompt.</summary>
    public sealed class CadInput
    {
        internal static readonly CadInput Nothing = new CadInput(
            CadInputKind.None,
            default,
            0.0,
            null,
            Array.Empty<string>()
        );

        internal CadInput(CadInputKind kind, GeoPoint2 point, double number, string text, IReadOnlyList<string> handles)
        {
            Kind = kind;
            Point = point;
            Number = number;
            Text = text;
            Handles = handles ?? Array.Empty<string>();
        }

        /// <summary>Gets what kind of answer it is.</summary>
        public CadInputKind Kind { get; }

        /// <summary>Gets the point of a point answer, the point picked for a distance or an angle, and the point one object was picked at on the canvas.</summary>
        public GeoPoint2 Point { get; }

        /// <summary>Gets the number of a distance, an angle (in radians) or a whole number.</summary>
        public double Number { get; }

        /// <summary>Gets the keyword of a keyword answer, as the prompt writes it, or the text of a text answer.</summary>
        public string Text { get; }

        /// <summary>Gets the handles of a selection, in ordinal order, or of the one object picked.</summary>
        public IReadOnlyList<string> Handles { get; }

        /// <summary>
        /// Gets whether Shift was held as the one object was picked on the canvas, as TRIM takes a Shift-pick to extend; false
        /// for every other answer.
        /// </summary>
        public bool Shift { get; internal set; }

        /// <summary>
        /// Gets whether Ctrl was held as the point, distance or angle was picked on the canvas, as ARC and PLINE take a point picked
        /// with Ctrl held to draw the arc the other way round, and a grip edit takes its first point so picked as Copy; false for
        /// an answer typed and for every other answer.
        /// </summary>
        public bool Control { get; internal set; }

        /// <summary>
        /// The objects of a selection that clicks alone picked, each with the point it was clicked at, in the order clicked; an
        /// object a window, a crossing box or ALL took as well is not among them. Empty for every other answer.
        /// </summary>
        internal IReadOnlyList<KeyValuePair<string, GeoPoint2>> Clicks { get; set; } =
            Array.Empty<KeyValuePair<string, GeoPoint2>>();

        /// <summary>
        /// The objects of a selection that windows or crossing boxes alone took, each with the box and whether it crossed, in
        /// the order taken; an object another option took whole as well is not among them. Empty for every other answer.
        /// </summary>
        internal IReadOnlyList<Tuple<string, DisplayBox, bool>> Windows { get; set; } =
            Array.Empty<Tuple<string, DisplayBox, bool>>();

        /// <summary>Whether the one object of a selection was picked on the canvas, its <see cref="Point"/> the point it was picked at; false where it was given by its handle.</summary>
        internal bool IsPickedAt { get; set; }

        /// <summary>The object snap a point answer was picked with (<see cref="CadCommandSession.Feed(CadSnapResult, bool)"/>), which an associative dimension ties its point to; null for a point typed or picked with none.</summary>
        internal CadSnapResult Snap { get; set; }

        /// <summary>Gets whether the answer is Enter with nothing typed.</summary>
        public bool IsNone => Kind == CadInputKind.None;

        /// <summary>Gets whether the answer is a point.</summary>
        public bool IsPoint => Kind == CadInputKind.Point;

        /// <summary>Gets whether the answer is a keyword, either case.</summary>
        /// <param name="keyword">The keyword as the prompt writes it.</param>
        /// <returns>True when it is that keyword.</returns>
        public bool Is(string keyword) =>
            Kind == CadInputKind.Keyword && string.Equals(Text, keyword, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public override string ToString()
        {
            switch (Kind)
            {
                case CadInputKind.Point:
                    return Point.X.ToString("R", CultureInfo.InvariantCulture)
                        + ","
                        + Point.Y.ToString("R", CultureInfo.InvariantCulture);
                case CadInputKind.Number:
                    return Number.ToString("R", CultureInfo.InvariantCulture);
                case CadInputKind.Keyword:
                case CadInputKind.Text:
                    return Text;
                case CadInputKind.Selection:
                    return Handles.Count.ToString(CultureInfo.InvariantCulture) + " object(s)";
                default:
                    return "<Enter>";
            }
        }
    }
}
