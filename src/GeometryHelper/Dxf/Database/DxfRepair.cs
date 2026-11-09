namespace GeometryHelper.Dxf
{
    /// <summary>
    /// One thing found wrong with a drawing by <see cref="DxfDatabase.Recover(string, out DxfRepairReport)"/> or
    /// <see cref="DxfDatabase.Audit"/>: its kind, a sentence saying what and where, the line of the file or the handle of the
    /// record, and whether it was fixed. "Line 7 is not a group code ('1O') and was left out." is a repair of the kind
    /// <see cref="DxfRepairKind.BadGroupCode"/> on line 7, fixed.
    /// </summary>
    public sealed class DxfRepair
    {
        internal DxfRepair(DxfRepairKind kind, string message, int line, string handle, bool isFixed)
        {
            Kind = kind;
            Message = message;
            Line = line;
            Handle = handle;
            IsFixed = isFixed;
        }

        /// <summary>Gets what was wrong.</summary>
        public DxfRepairKind Kind { get; }

        /// <summary>Gets a sentence saying what was wrong, where, and what was done about it.</summary>
        public string Message { get; }

        /// <summary>Gets the line of the file it was found on, counted from 1; 0 where it is said by handle or of the whole file.</summary>
        public int Line { get; }

        /// <summary>Gets the handle of the record it was found in; null where it is said by line or the record has none.</summary>
        public string Handle { get; }

        /// <summary>Gets whether it was fixed: false for what is only reported, as an INSERT of a block the drawing does not define, and for everything <see cref="DxfDatabase.Audit"/> finds without fixing.</summary>
        public bool IsFixed { get; }

        /// <summary>Gives the message.</summary>
        /// <returns>The message.</returns>
        public override string ToString() => Message;
    }
}
