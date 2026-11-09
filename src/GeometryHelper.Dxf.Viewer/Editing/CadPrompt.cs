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

        /// <summary>A distance, typed or as the distance from the prompt's base point to a point picked.</summary>
        Distance,

        /// <summary>An angle in degrees, typed or as the direction from the prompt's base point to a point picked.</summary>
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
        private CadPrompt(CadPromptKind kind, string message, IReadOnlyList<string> keywords, string defaultValue, GeoPoint2? basePoint)
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

        /// <summary>Gets the whole text the command line shows: <c>Specify base point or [Displacement] &lt;Displacement&gt;:</c>.</summary>
        public string Text
        {
            get
            {
                var text = new StringBuilder(Message);

                if (Keywords.Count > 0)
                {
                    text.Append(Kind == CadPromptKind.Keyword ? " [" : " or [").Append(string.Join("/", Keywords)).Append(']');
                }

                if (!string.IsNullOrEmpty(Default))
                {
                    text.Append(" <").Append(Default).Append('>');
                }

                return text.Append(':').ToString();
            }
        }

        /// <summary>Asks for a point.</summary>
        /// <param name="message">The message: "Specify base point".</param>
        /// <param name="basePoint">The point a rubber band is drawn from; null for none.</param>
        /// <param name="keywords">The keywords offered; null for none.</param>
        /// <param name="defaultValue">The default Enter takes; null for none.</param>
        /// <param name="acceptsText">Whether text that is neither a point nor a keyword is taken as an answer of text, as ZOOM takes 2X.</param>
        /// <returns>The prompt.</returns>
        public static CadPrompt ForPoint(string message, GeoPoint2? basePoint = null, IReadOnlyList<string> keywords = null, string defaultValue = null, bool acceptsText = false)
            => new CadPrompt(CadPromptKind.Point, message, keywords, defaultValue, basePoint) { AcceptsText = acceptsText };

        /// <summary>Asks for a distance.</summary>
        /// <param name="message">The message: "Specify radius of circle".</param>
        /// <param name="basePoint">The point a picked point's distance is measured from; null for none.</param>
        /// <param name="keywords">The keywords offered; null for none.</param>
        /// <param name="defaultValue">The default Enter takes; null for none.</param>
        /// <returns>The prompt.</returns>
        public static CadPrompt ForDistance(string message, GeoPoint2? basePoint = null, IReadOnlyList<string> keywords = null, string defaultValue = null)
            => new CadPrompt(CadPromptKind.Distance, message, keywords, defaultValue, basePoint);

        /// <summary>Asks for an angle.</summary>
        /// <param name="message">The message: "Specify rotation angle".</param>
        /// <param name="basePoint">The point a picked point's direction is measured from; null for none.</param>
        /// <param name="keywords">The keywords offered; null for none.</param>
        /// <param name="defaultValue">The default Enter takes; null for none.</param>
        /// <returns>The prompt.</returns>
        public static CadPrompt ForAngle(string message, GeoPoint2? basePoint = null, IReadOnlyList<string> keywords = null, string defaultValue = null)
            => new CadPrompt(CadPromptKind.Angle, message, keywords, defaultValue, basePoint);

        /// <summary>Asks for objects until Enter: "Select objects:".</summary>
        /// <param name="message">The message; "Select objects" where null.</param>
        /// <returns>The prompt.</returns>
        public static CadPrompt ForSelection(string message = null) => new CadPrompt(CadPromptKind.Selection, message ?? "Select objects", null, null, null);

        /// <summary>Asks "Select objects or [Name/Description]:", as GROUP asks: objects until Enter, or a keyword.</summary>
        internal static CadPrompt ForSelection(string message, IReadOnlyList<string> keywords) => new CadPrompt(CadPromptKind.Selection, message, keywords, null, null);

        /// <summary>Asks for one object: "Select arc or circle:".</summary>
        /// <param name="message">The message.</param>
        /// <param name="keywords">The keywords offered; null for none.</param>
        /// <param name="defaultValue">The default Enter takes; null for none.</param>
        /// <returns>The prompt.</returns>
        public static CadPrompt ForEntity(string message, IReadOnlyList<string> keywords = null, string defaultValue = null)
            => new CadPrompt(CadPromptKind.Entity, message, keywords, defaultValue, null);

        /// <summary>Asks for one of a list of keywords: "Erase source objects? [Yes/No] &lt;No&gt;:".</summary>
        /// <param name="message">The message.</param>
        /// <param name="keywords">The keywords.</param>
        /// <param name="defaultValue">The default Enter takes; null for none.</param>
        /// <returns>The prompt.</returns>
        public static CadPrompt ForKeyword(string message, IReadOnlyList<string> keywords, string defaultValue = null)
            => new CadPrompt(CadPromptKind.Keyword, message, keywords ?? throw new ArgumentNullException(nameof(keywords)), defaultValue, null);

        /// <summary>Asks for one of a list of keywords or a text, as CECOLOR takes Truecolor or a colour: "Enter new object color [Truecolor/COlorbook] &lt;BYLAYER&gt;:".</summary>
        internal static CadPrompt ForKeywordOrText(string message, IReadOnlyList<string> keywords, string defaultValue)
            => new CadPrompt(CadPromptKind.Keyword, message, keywords, defaultValue, null) { AcceptsText = true };

        /// <summary>Asks for a text.</summary>
        /// <param name="message">The message: "Enter text".</param>
        /// <param name="defaultValue">The default Enter takes; null for none.</param>
        /// <returns>The prompt.</returns>
        public static CadPrompt ForString(string message, string defaultValue = null) => new CadPrompt(CadPromptKind.String, message, null, defaultValue, null);

        /// <summary>Asks for a whole number.</summary>
        /// <param name="message">The message.</param>
        /// <param name="keywords">The keywords offered; null for none.</param>
        /// <param name="defaultValue">The default Enter takes; null for none.</param>
        /// <returns>The prompt.</returns>
        public static CadPrompt ForInteger(string message, IReadOnlyList<string> keywords = null, string defaultValue = null)
            => new CadPrompt(CadPromptKind.Integer, message, keywords, defaultValue, null);

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

            // The capital letters first, as AutoCAD reads them, then the whole word, then a longer start of one.
            foreach (string keyword in Keywords)
            {
                if (string.Equals(Shortcut(keyword), answer, StringComparison.OrdinalIgnoreCase))
                {
                    return keyword;
                }
            }

            foreach (string keyword in Keywords)
            {
                if (string.Equals(keyword, answer, StringComparison.OrdinalIgnoreCase))
                {
                    return keyword;
                }
            }

            foreach (string keyword in Keywords)
            {
                string shortcut = Shortcut(keyword);

                if (answer.Length >= shortcut.Length && keyword.StartsWith(answer, StringComparison.OrdinalIgnoreCase)
                    && keyword.StartsWith(shortcut, StringComparison.OrdinalIgnoreCase))
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
        internal static readonly CadInput Nothing = new CadInput(CadInputKind.None, default, 0.0, null, Array.Empty<string>());

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

        /// <summary>Gets whether the answer is Enter with nothing typed.</summary>
        public bool IsNone => Kind == CadInputKind.None;

        /// <summary>Gets whether the answer is a point.</summary>
        public bool IsPoint => Kind == CadInputKind.Point;

        /// <summary>Gets whether the answer is a keyword, either case.</summary>
        /// <param name="keyword">The keyword as the prompt writes it.</param>
        /// <returns>True when it is that keyword.</returns>
        public bool Is(string keyword) => Kind == CadInputKind.Keyword && string.Equals(Text, keyword, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public override string ToString()
        {
            switch (Kind)
            {
                case CadInputKind.Point: return Point.X.ToString("R", CultureInfo.InvariantCulture) + "," + Point.Y.ToString("R", CultureInfo.InvariantCulture);
                case CadInputKind.Number: return Number.ToString("R", CultureInfo.InvariantCulture);
                case CadInputKind.Keyword:
                case CadInputKind.Text:
                    return Text;
                case CadInputKind.Selection: return Handles.Count.ToString(CultureInfo.InvariantCulture) + " object(s)";
                default: return "<Enter>";
            }
        }
    }
}
