namespace GeometryHelper.Dxf
{
    /// <summary>
    /// What was wrong with a drawing, as <see cref="DxfDatabase.Recover(string, out DxfRepairReport)"/> and
    /// <see cref="DxfDatabase.Audit"/> name it in a <see cref="DxfRepair"/>, in the order a file is read: first the bytes and
    /// lines, then the sections, the records, the handles and the tables.
    /// </summary>
    public enum DxfRepairKind
    {
        /// <summary>Lines ended by a carriage return alone, as an old Macintosh writes them: read as lines, written with line feeds.</summary>
        LineEnds,

        /// <summary>
        /// Text in the wrong code page: a file of R2007 or later whose bytes are not UTF-8, read in the page its header names
        /// and written as UTF-8; or a header naming a page a DXF cannot be in, named ANSI_1252 instead.
        /// </summary>
        Encoding,

        /// <summary>A line where a group code should stand that is not a whole number from 0 to 1071: left out, the next line read as a code.</summary>
        BadGroupCode,

        /// <summary>A group code on the last line of the file with no value after it: left out.</summary>
        MissingValue,

        /// <summary>A file cut short, with no EOF: the section it ends in closed and the EOF written.</summary>
        Truncated,

        /// <summary>
        /// A section not closed before the next opens, an ENDSEC that closes nothing, a section with no name, or records outside
        /// every section: closed, left out, named from what it holds, or moved into ENTITIES where they are entities.
        /// </summary>
        SectionStructure,

        /// <summary>Sections out of AutoCAD's order (HEADER, CLASSES, TABLES, BLOCKS, ENTITIES, OBJECTS): put in that order.</summary>
        SectionOrder,

        /// <summary>
        /// A section missing: ENTITIES added empty; a HEADER naming R2000 added to a file whose records are of R13 or later; TABLES,
        /// BLOCKS or OBJECTS of such a file reported, not made.
        /// </summary>
        MissingSection,

        /// <summary>A BLOCK with no ENDBLK before the next BLOCK or the end of the section: its ENDBLK written.</summary>
        MissingEndBlk,

        /// <summary>A POLYLINE, or an INSERT with attributes, with no SEQEND after its vertices or attributes: its SEQEND written.</summary>
        MissingSeqEnd,

        /// <summary>A group of a number whose value is no number: the number it begins with, or nought.</summary>
        BadNumber,

        /// <summary>A number that is not finite (NaN, an infinity): the entity left out, nought elsewhere.</summary>
        NonFiniteNumber,

        /// <summary>A LWPOLYLINE whose count of vertices (group 90) is not the number of its vertices: set to that number.</summary>
        WrongVertexCount,

        /// <summary>A HATCH whose count of boundary paths (group 91) is not the number of its paths: set to that number.</summary>
        WrongLoopCount,

        /// <summary>A record of a file of R13 or later with no handle, or one that is not hexadecimal: given a new one.</summary>
        MissingHandle,

        /// <summary>A handle two records have: the later given a new one, the records it owns pointed at it.</summary>
        DuplicateHandle,

        /// <summary>A <c>$HANDSEED</c> not above every handle, or missing: set above them.</summary>
        HandseedTooLow,

        /// <summary>An owner (group 330) that names no record: the entity's block, the vertex's polyline or the entry's table named instead.</summary>
        BadOwner,

        /// <summary>A layer an entity is on that the layer table does not list: added with the defaults.</summary>
        MissingLayer,

        /// <summary>A linetype an entity names that the linetype table does not list: added, drawn solid.</summary>
        MissingLinetype,

        /// <summary>A text style a text names that the style table does not list: added with the defaults.</summary>
        MissingTextStyle,

        /// <summary>A dimension style a dimension, a leader or a tolerance names that the table does not list: added with the defaults.</summary>
        MissingDimStyle,

        /// <summary>An INSERT of a block the drawing does not define: kept, drawing nothing.</summary>
        MissingBlock,

        /// <summary>An entity of a type neither AutoCAD's own nor named in the CLASSES section: kept as it is.</summary>
        UnknownEntity,

        /// <summary>
        /// An entry of a DICTIONARY that is broken, a name with no pointer after it, a pointer with no name before it, or a
        /// pointer that is not a handle: left out. A pointer to an object the file does not hold is not broken: AutoCAD writes
        /// those of objects it leaves out of a DXF.
        /// </summary>
        BrokenDictionaryEntry,

        /// <summary>
        /// What was left after the repairs still could not be read: only its entities kept (fixed), or an empty drawing (not
        /// fixed); or nothing of a DXF was found in the file, not one record (not fixed), the drawing empty.
        /// </summary>
        Unreadable,
    }
}
