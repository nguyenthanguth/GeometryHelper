using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>What a routine waits for: the kind of a <see cref="LispAsk"/>.</summary>
    internal enum LispAskKind
    {
        /// <summary><c>getpoint</c>: a point, in the UCS, answered as a list of three reals.</summary>
        Point,

        /// <summary><c>getcorner</c>: the opposite corner of a box from a base point.</summary>
        Corner,

        /// <summary><c>getdist</c>: a distance, typed or as two points, answered as a real.</summary>
        Distance,

        /// <summary><c>getangle</c>: an angle counted from ANGBASE in ANGDIR's sense, in radians.</summary>
        Angle,

        /// <summary><c>getorient</c>: an angle from east, counter-clockwise, in radians.</summary>
        Orient,

        /// <summary><c>getint</c>: a whole number from -32768 to 32767.</summary>
        Integer,

        /// <summary><c>getreal</c>: a real.</summary>
        Real,

        /// <summary><c>getstring</c>: a text.</summary>
        String,

        /// <summary><c>getkword</c>: one of the keywords <c>initget</c> gave.</summary>
        Keyword,

        /// <summary>
        /// <c>read-line</c> and <c>read-char</c> with no file: a whole line from the keyboard, with no prompt text, taken as typed
        /// (spaces kept, a line that starts with a parenthesis is text), answered as the string typed.
        /// </summary>
        Line,

        /// <summary><c>entsel</c>: one object picked, answered as <c>(ename (x y z))</c>.</summary>
        Entity,

        /// <summary><c>nentsel</c>: one object, or one inside a block reference, picked.</summary>
        NestedEntity,

        /// <summary><c>ssget</c> with no mode: "Select objects:" until Enter, answered as the handles taken.</summary>
        Selection,

        /// <summary>The <c>PAUSE</c> of <c>command</c>: the user answers the prompt the command running asks; nothing is answered to the wait.</summary>
        Pause,
    }

    /// <summary>
    /// One wait of a routine for the user, as a built-in hands it over with <see cref="LispInterpreter.Ask"/>: what is asked,
    /// the message the routine gave, and what <c>initget</c> said about it. The session asks it as a prompt and answers with
    /// <see cref="LispInterpreter.Resume"/>; the answer is a LISP value of the kind <see cref="Kind"/> says (a list of three
    /// reals for a point, a real for a distance, an angle or a real, an int for an integer, a string for a text or a keyword,
    /// the list of <c>entsel</c> or nil for an object, a list of handles for a selection), or the string of a keyword
    /// whatever the kind.
    /// </summary>
    internal sealed class LispAsk
    {
        /// <summary>Gets or sets what is asked.</summary>
        internal LispAskKind Kind { get; set; }

        /// <summary>Gets or sets the message as the routine gave it, <c>\n</c> included; null for none.</summary>
        internal string Message { get; set; }

        /// <summary>Gets or sets the base point of <c>getpoint</c>, <c>getcorner</c>, <c>getdist</c>, <c>getangle</c> and <c>getorient</c>, in the UCS; null for none.</summary>
        internal GeoPoint3? BasePoint { get; set; }

        /// <summary>Gets or sets the <c>initget</c> bits in force for this ask.</summary>
        internal int Flags { get; set; }

        /// <summary>Gets or sets the keywords <c>initget</c> gave, as written (<c>Yes</c> is typed as Y, YE or YES).</summary>
        internal IReadOnlyList<string> Keywords { get; set; } = Array.Empty<string>();

        /// <summary>Gets or sets whether a space is part of the text, as in <c>(getstring T)</c>.</summary>
        internal bool Spaces { get; set; }

        /// <summary>Gets or sets whether one pick or one window ends the selection, as <c>(ssget ":S")</c> does.</summary>
        internal bool Single { get; set; }

        /// <summary>Gets or sets what a selection keeps: whether an object, by its handle, matches the filter of <c>ssget</c>; null for every object.</summary>
        internal Func<string, bool> Accepts { get; set; }

        /// <summary>Gets or sets whether an object, by its handle, is on a locked layer, for <c>ssget</c> with <c>:L</c>, which leaves such objects out and says how many it did; null for a selection that keeps them.</summary>
        internal Func<string, bool> Locked { get; set; }
    }
}
