namespace GeometryHelper.Export
{
    /// <summary>
    /// What a table or a cell of it holds of its own over its table style, as AutoCAD's <c>TableStyleOverride</c> numbers it:
    /// those of the table from 1 to 117, those of a cell from 130 on, as <see cref="DxfTable.CellStyleOverrides(int, int)"/> gives them.
    /// </summary>
    public enum DxfTableStyleOverride
    {
        /// <summary>The title suppressed, 1.</summary>
        TitleSuppressed = 1,

        /// <summary>The header suppressed, 2.</summary>
        HeaderSuppressed = 2,

        /// <summary>The flow direction, 3.</summary>
        FlowDirection = 3,

        /// <summary>The horizontal cell margin, 4.</summary>
        HorizontalCellMargin = 4,

        /// <summary>The verticalical cell margin, 5.</summary>
        VerticalCellMargin = 5,

        /// <summary>The title row color, 6.</summary>
        TitleRowColor = 6,

        /// <summary>The header row color, 7.</summary>
        HeaderRowColor = 7,

        /// <summary>The data row color, 8.</summary>
        DataRowColor = 8,

        /// <summary>The title row fill none, 9.</summary>
        TitleRowFillNone = 9,

        /// <summary>The header row fill none, 10.</summary>
        HeaderRowFillNone = 10,

        /// <summary>The data row fill none, 11.</summary>
        DataRowFillNone = 11,

        /// <summary>The title row fill color, 12.</summary>
        TitleRowFillColor = 12,

        /// <summary>The header row fill color, 13.</summary>
        HeaderRowFillColor = 13,

        /// <summary>The data row fill color, 14.</summary>
        DataRowFillColor = 14,

        /// <summary>The title row alignment, 15.</summary>
        TitleRowAlignment = 15,

        /// <summary>The header row alignment, 16.</summary>
        HeaderRowAlignment = 16,

        /// <summary>The data row alignment, 17.</summary>
        DataRowAlignment = 17,

        /// <summary>The title row text style, 18.</summary>
        TitleRowTextStyle = 18,

        /// <summary>The header row text style, 19.</summary>
        HeaderRowTextStyle = 19,

        /// <summary>The data row text style, 20.</summary>
        DataRowTextStyle = 20,

        /// <summary>The title row text height, 21.</summary>
        TitleRowTextHeight = 21,

        /// <summary>The header row text height, 22.</summary>
        HeaderRowTextHeight = 22,

        /// <summary>The data row text height, 23.</summary>
        DataRowTextHeight = 23,

        /// <summary>The title row data type, 24.</summary>
        TitleRowDataType = 24,

        /// <summary>The header row data type, 25.</summary>
        HeaderRowDataType = 25,

        /// <summary>The data row data type, 26.</summary>
        DataRowDataType = 26,

        /// <summary>The title horizontal top color, 40.</summary>
        TitleHorizontalTopColor = 40,

        /// <summary>The title horizontal inside color, 41.</summary>
        TitleHorizontalInsideColor = 41,

        /// <summary>The title horizontal bottom color, 42.</summary>
        TitleHorizontalBottomColor = 42,

        /// <summary>The title verticalical left color, 43.</summary>
        TitleVerticalLeftColor = 43,

        /// <summary>The title verticalical inside color, 44.</summary>
        TitleVerticalInsideColor = 44,

        /// <summary>The title verticalical right color, 45.</summary>
        TitleVerticalRightColor = 45,

        /// <summary>The header horizontal top color, 46.</summary>
        HeaderHorizontalTopColor = 46,

        /// <summary>The header horizontal inside color, 47.</summary>
        HeaderHorizontalInsideColor = 47,

        /// <summary>The header horizontal bottom color, 48.</summary>
        HeaderHorizontalBottomColor = 48,

        /// <summary>The header verticalical left color, 49.</summary>
        HeaderVerticalLeftColor = 49,

        /// <summary>The header verticalical inside color, 50.</summary>
        HeaderVerticalInsideColor = 50,

        /// <summary>The header verticalical right color, 51.</summary>
        HeaderVerticalRightColor = 51,

        /// <summary>The data horizontal top color, 52.</summary>
        DataHorizontalTopColor = 52,

        /// <summary>The data horizontal inside color, 53.</summary>
        DataHorizontalInsideColor = 53,

        /// <summary>The data horizontal bottom color, 54.</summary>
        DataHorizontalBottomColor = 54,

        /// <summary>The data verticalical left color, 55.</summary>
        DataVerticalLeftColor = 55,

        /// <summary>The data verticalical inside color, 56.</summary>
        DataVerticalInsideColor = 56,

        /// <summary>The data verticalical right color, 57.</summary>
        DataVerticalRightColor = 57,

        /// <summary>The title horizontal top line weight, 70.</summary>
        TitleHorizontalTopLineWeight = 70,

        /// <summary>The title horizontal inside line weight, 71.</summary>
        TitleHorizontalInsideLineWeight = 71,

        /// <summary>The title horizontal bottom line weight, 72.</summary>
        TitleHorizontalBottomLineWeight = 72,

        /// <summary>The title verticalical left line weight, 73.</summary>
        TitleVerticalLeftLineWeight = 73,

        /// <summary>The title verticalical inside line weight, 74.</summary>
        TitleVerticalInsideLineWeight = 74,

        /// <summary>The title verticalical right line weight, 75.</summary>
        TitleVerticalRightLineWeight = 75,

        /// <summary>The header horizontal top line weight, 76.</summary>
        HeaderHorizontalTopLineWeight = 76,

        /// <summary>The header horizontal inside line weight, 77.</summary>
        HeaderHorizontalInsideLineWeight = 77,

        /// <summary>The header horizontal bottom line weight, 78.</summary>
        HeaderHorizontalBottomLineWeight = 78,

        /// <summary>The header verticalical left line weight, 79.</summary>
        HeaderVerticalLeftLineWeight = 79,

        /// <summary>The header verticalical inside line weight, 80.</summary>
        HeaderVerticalInsideLineWeight = 80,

        /// <summary>The header verticalical right line weight, 81.</summary>
        HeaderVerticalRightLineWeight = 81,

        /// <summary>The data horizontal top line weight, 82.</summary>
        DataHorizontalTopLineWeight = 82,

        /// <summary>The data horizontal inside line weight, 83.</summary>
        DataHorizontalInsideLineWeight = 83,

        /// <summary>The data horizontal bottom line weight, 84.</summary>
        DataHorizontalBottomLineWeight = 84,

        /// <summary>The data verticalical left line weight, 85.</summary>
        DataVerticalLeftLineWeight = 85,

        /// <summary>The data verticalical inside line weight, 86.</summary>
        DataVerticalInsideLineWeight = 86,

        /// <summary>The data verticalical right line weight, 87.</summary>
        DataVerticalRightLineWeight = 87,

        /// <summary>The title horizontal top visibility, 100.</summary>
        TitleHorizontalTopVisibility = 100,

        /// <summary>The title horizontal inside visibility, 101.</summary>
        TitleHorizontalInsideVisibility = 101,

        /// <summary>The title horizontal bottom visibility, 102.</summary>
        TitleHorizontalBottomVisibility = 102,

        /// <summary>The title verticalical left visibility, 103.</summary>
        TitleVerticalLeftVisibility = 103,

        /// <summary>The title verticalical inside visibility, 104.</summary>
        TitleVerticalInsideVisibility = 104,

        /// <summary>The title verticalical right visibility, 105.</summary>
        TitleVerticalRightVisibility = 105,

        /// <summary>The header horizontal top visibility, 106.</summary>
        HeaderHorizontalTopVisibility = 106,

        /// <summary>The header horizontal inside visibility, 107.</summary>
        HeaderHorizontalInsideVisibility = 107,

        /// <summary>The header horizontal bottom visibility, 108.</summary>
        HeaderHorizontalBottomVisibility = 108,

        /// <summary>The header verticalical left visibility, 109.</summary>
        HeaderVerticalLeftVisibility = 109,

        /// <summary>The header verticalical inside visibility, 110.</summary>
        HeaderVerticalInsideVisibility = 110,

        /// <summary>The header verticalical right visibility, 111.</summary>
        HeaderVerticalRightVisibility = 111,

        /// <summary>The data horizontal top visibility, 112.</summary>
        DataHorizontalTopVisibility = 112,

        /// <summary>The data horizontal inside visibility, 113.</summary>
        DataHorizontalInsideVisibility = 113,

        /// <summary>The data horizontal bottom visibility, 114.</summary>
        DataHorizontalBottomVisibility = 114,

        /// <summary>The data verticalical left visibility, 115.</summary>
        DataVerticalLeftVisibility = 115,

        /// <summary>The data verticalical inside visibility, 116.</summary>
        DataVerticalInsideVisibility = 116,

        /// <summary>The data verticalical right visibility, 117.</summary>
        DataVerticalRightVisibility = 117,

        /// <summary>The cell alignment, 130.</summary>
        CellAlignment = 130,

        /// <summary>The cell background fill none, 131.</summary>
        CellBackgroundFillNone = 131,

        /// <summary>The cell background color, 132.</summary>
        CellBackgroundColor = 132,

        /// <summary>The cell content color, 133.</summary>
        CellContentColor = 133,

        /// <summary>The cell text style, 134.</summary>
        CellTextStyle = 134,

        /// <summary>The cell text height, 135.</summary>
        CellTextHeight = 135,

        /// <summary>The cell top grid color, 136.</summary>
        CellTopGridColor = 136,

        /// <summary>The cell right grid color, 137.</summary>
        CellRightGridColor = 137,

        /// <summary>The cell bottom grid color, 138.</summary>
        CellBottomGridColor = 138,

        /// <summary>The cell left grid color, 139.</summary>
        CellLeftGridColor = 139,

        /// <summary>The cell top grid line weight, 140.</summary>
        CellTopGridLineWeight = 140,

        /// <summary>The cell right grid line weight, 141.</summary>
        CellRightGridLineWeight = 141,

        /// <summary>The cell bottom grid line weight, 142.</summary>
        CellBottomGridLineWeight = 142,

        /// <summary>The cell left grid line weight, 143.</summary>
        CellLeftGridLineWeight = 143,

        /// <summary>The cell top visibility, 144.</summary>
        CellTopVisibility = 144,

        /// <summary>The cell right visibility, 145.</summary>
        CellRightVisibility = 145,

        /// <summary>The cell bottom visibility, 146.</summary>
        CellBottomVisibility = 146,

        /// <summary>The cell left visibility, 147.</summary>
        CellLeftVisibility = 147,

        /// <summary>The cell data type, 148.</summary>
        CellDataType = 148,
    }
}
