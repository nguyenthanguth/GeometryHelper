using System;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// What the text of each kind of mark shows: a list of fields for part marks, one for assembly marks and one for bolt
    /// marks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By default a part mark shows the part's position mark, "P1"; an assembly mark the position mark of its main part,
    /// "C1"; and a bolt mark the count, the size and the length of its group, "4 x M20 x 80". Which of the maintainer's
    /// fields a drawing shows is open question Q5 of the drawing plan; these are the defaults until it is answered.
    /// </para>
    /// <code>
    /// var content = new MarkContent
    /// {
    ///     Part = new MarkFormat(new[] { MarkField.Mark, MarkField.Profile }, " "),        // "P2 PL25*400"
    ///     Bolt = new MarkFormat(new MarkField[0], " "),                                    // no bolt marks
    /// };
    /// </code>
    /// </remarks>
    public sealed class MarkContent
    {
        private MarkFormat _part = new MarkFormat(new[] { MarkField.Mark }, " ");
        private MarkFormat _assembly = new MarkFormat(new[] { MarkField.Mark }, " ");
        private MarkFormat _bolt = new MarkFormat(new[] { MarkField.Count, MarkField.Diameter, MarkField.Length }, " x ");

        /// <summary>Gets or sets the text of a part mark; the position mark by default.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the value set is null.</exception>
        public MarkFormat Part
        {
            get => _part;
            set => _part = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Gets or sets the text of an assembly mark, read from the main part; its position mark by default.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the value set is null.</exception>
        public MarkFormat Assembly
        {
            get => _assembly;
            set => _assembly = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Gets or sets the text of a bolt mark; the count, the size and the length, " x " between them, by default.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the value set is null.</exception>
        public MarkFormat Bolt
        {
            get => _bolt;
            set => _bolt = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
}
