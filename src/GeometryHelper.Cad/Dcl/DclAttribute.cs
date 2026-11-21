namespace GeometryHelper.Cad.Dcl
{
    /// <summary>What a DCL attribute's value was written as: a string in quotes, an integer, a real or a bare name.</summary>
    internal enum DclValueKind
    {
        /// <summary>A string in double quotes.</summary>
        String,

        /// <summary>An integer, such as <c>60</c> or <c>-1</c>.</summary>
        Integer,

        /// <summary>A real, such as <c>1.5</c> or <c>.5</c>.</summary>
        Real,

        /// <summary>A bare name, such as <c>true</c>, <c>centered</c> or <c>dialog_background</c>.</summary>
        Name,
    }

    /// <summary>
    /// One attribute of a DCL tile, <c>name = value;</c>, kept as written: the string without its quotes and with its escapes
    /// resolved, a number or a name as it stands in the file.
    /// </summary>
    internal sealed class DclAttribute
    {
        internal DclAttribute(string name, DclValueKind kind, string text, int line)
        {
            Name = name;
            Kind = kind;
            Text = text;
            Line = line;
        }

        /// <summary>Gets the attribute's name, case-sensitive (<c>edit_width</c>).</summary>
        internal string Name { get; }

        /// <summary>Gets what the value was written as.</summary>
        internal DclValueKind Kind { get; }

        /// <summary>Gets the value as written: a string without its quotes, a number or a name as it stands.</summary>
        internal string Text { get; }

        /// <summary>Gets the line of the file the attribute is on, from 1.</summary>
        internal int Line { get; }
    }
}
