using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The dimension variables of the header AutoCAD's <c>Database</c> has as properties, read and set by name: each read from
    /// the drawings of dimensions of R12, R2000 and R2018 ezdxf 1.4.5 wrote, the value of AutoCAD's metric drawing where the
    /// header gives none, set, saved and read again, refused where the file's version has no such variable, and set to the
    /// value it has without a byte changing; the header as a dimension style by <c>GetDimstyleData</c> and back by
    /// <c>SetDimstyleData</c>, and the same of a leader; DIMTXTDIRECTION of a style and of a dimension. ezdxf 1.4.5 read every
    /// value set back, its recover and audit finding nothing.
    /// </summary>
    public class DxfDatabaseDimensionVariablesTests
    {
        private static readonly string[] Drawings = { DxfDimensionDrawings.R12, DxfDimensionDrawings.R2000, DxfDimensionDrawings.R2018 };

        private static readonly Row[] Rows =
        {
            Of("$DIMADEC", 70, 1015, db => db.Dimadec, (db, v) => db.Dimadec = v, 0, -1),
            Of("$DIMALT", 70, 1009, db => db.Dimalt, (db, v) => db.Dimalt = v, false, true),
            Of("$DIMALTD", 70, 1009, db => db.Dimaltd, (db, v) => db.Dimaltd = v, 3, 1),
            Of("$DIMALTF", 40, 1009, db => db.Dimaltf, (db, v) => db.Dimaltf = v, 1.0 / 25.4, 25.4, written: 0.03937007874),
            Of("$DIMALTRND", 40, 1015, db => db.Dimaltrnd, (db, v) => db.Dimaltrnd = v, 0.0, 0.5),
            Of("$DIMALTTD", 70, 1015, db => db.Dimalttd, (db, v) => db.Dimalttd = v, 3, 4),
            Of("$DIMALTTZ", 70, 1015, db => db.Dimalttz, (db, v) => db.Dimalttz = v, 0, 12),
            Of("$DIMALTU", 70, 1015, db => db.Dimaltu, (db, v) => db.Dimaltu = v, 2, 8),
            Of("$DIMALTZ", 70, 1015, db => db.Dimaltz, (db, v) => db.Dimaltz = v, 0, 8),
            Of("$DIMAPOST", 1, 1009, db => db.Dimapost, (db, v) => db.Dimapost = v, string.Empty, "[] in"),
            Of("$DIMARCSYM", 70, 1021, db => db.Dimarcsym, (db, v) => db.Dimarcsym = v, 0, 2),
            Of("$DIMASO", 70, 1009, db => db.Dimaso, (db, v) => db.Dimaso = v, true, false),
            Of("$DIMASSOC", 280, 1018, db => db.DimAssoc, (db, v) => db.DimAssoc = v, 2, 1),
            Of("$DIMASZ", 40, 1009, db => db.Dimasz, (db, v) => db.Dimasz = v, 2.5, 3.5),
            Of("$DIMATFIT", 70, 1015, db => db.Dimatfit, (db, v) => db.Dimatfit = v, 3, 1),
            Of("$DIMAUNIT", 70, 1015, db => db.Dimaunit, (db, v) => db.Dimaunit = v, 0, 1),
            Of("$DIMAZIN", 70, 1015, db => db.Dimazin, (db, v) => db.Dimazin = v, 0, 2),
            OfBlock("$DIMBLK", 1009, db => db.Dimblk, (db, v) => db.Dimblk = v, "_ARCHTICK"),
            OfBlock("$DIMBLK1", 1009, db => db.Dimblk1, (db, v) => db.Dimblk1 = v, "_DOT"),
            OfBlock("$DIMBLK2", 1009, db => db.Dimblk2, (db, v) => db.Dimblk2 = v, "_OPEN"),
            Of("$DIMCEN", 40, 1009, db => db.Dimcen, (db, v) => db.Dimcen = v, 2.5, -1.5),
            Of("$DIMCLRD", 70, 1009, db => db.Dimclrd, (db, v) => db.Dimclrd = v, DxfColor.ByBlock, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1)),
            Of("$DIMCLRE", 70, 1009, db => db.Dimclre, (db, v) => db.Dimclre = v, DxfColor.ByBlock, DxfColor.ByLayer),
            Of("$DIMCLRT", 70, 1009, db => db.Dimclrt, (db, v) => db.Dimclrt = v, DxfColor.ByBlock, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 5)),
            Of("$DIMDEC", 70, 1015, db => db.Dimdec, (db, v) => db.Dimdec = v, 2, 4),
            Of("$DIMDLE", 40, 1009, db => db.Dimdle, (db, v) => db.Dimdle = v, 0.0, 1.25),
            Of("$DIMDLI", 40, 1009, db => db.Dimdli, (db, v) => db.Dimdli = v, 3.75, 7.0),
            Of("$DIMDSEP", 70, 1015, db => db.Dimdsep, (db, v) => db.Dimdsep = v, ',', '.'),
            Of("$DIMEXE", 40, 1009, db => db.Dimexe, (db, v) => db.Dimexe = v, 1.25, 2.0),
            Of("$DIMEXO", 40, 1009, db => db.Dimexo, (db, v) => db.Dimexo = v, 0.625, 1.0),
            Of("$DIMFRAC", 70, 1015, db => db.Dimfrac, (db, v) => db.Dimfrac = v, 0, 2),
            Of("$DIMFXL", 40, 1021, db => db.Dimfxlen, (db, v) => db.Dimfxlen = v, 1.0, 5.0),
            Of("$DIMFXLON", 70, 1021, db => db.DimfxlenOn, (db, v) => db.DimfxlenOn = v, false, true),
            Of("$DIMGAP", 40, 1009, db => db.Dimgap, (db, v) => db.Dimgap = v, 0.625, -1.0),
            Of("$DIMJOGANG", 40, 1021, db => db.Dimjogang, (db, v) => db.Dimjogang = v, Math.PI / 4.0, Math.PI / 3.0, written: 0.785398163397),
            Of("$DIMJUST", 70, 1015, db => db.Dimjust, (db, v) => db.Dimjust = v, 0, 3),
            OfBlock("$DIMLDRBLK", 1015, db => db.Dimldrblk, (db, v) => db.Dimldrblk = v, "_DOT"),
            Of("$DIMLFAC", 40, 1009, db => db.Dimlfac, (db, v) => db.Dimlfac = v, 1.0, 100.0),
            Of("$DIMLIM", 70, 1009, db => db.Dimlim, (db, v) => db.Dimlim = v, false, true),
            OfLinetype("$DIMLTEX1", db => db.Dimltex1, (db, v) => db.Dimltex1 = v, "DASHED"),
            OfLinetype("$DIMLTEX2", db => db.Dimltex2, (db, v) => db.Dimltex2 = v, "CENTER"),
            OfLinetype("$DIMLTYPE", db => db.Dimltype, (db, v) => db.Dimltype = v, "CENTER"),
            Of("$DIMLUNIT", 70, 1015, db => db.Dimlunit, (db, v) => db.Dimlunit = v, 2, 4),
            Of("$DIMLWD", 70, 1015, db => db.Dimlwd, (db, v) => db.Dimlwd = v, DxfLineWeight.ByBlock, DxfLineWeight.LineWeight035),
            Of("$DIMLWE", 70, 1015, db => db.Dimlwe, (db, v) => db.Dimlwe = v, DxfLineWeight.ByBlock, DxfLineWeight.LineWeight018),
            Of("$DIMPOST", 1, 1009, db => db.Dimpost, (db, v) => db.Dimpost = v, string.Empty, "<> mm"),
            Of("$DIMRND", 40, 1009, db => db.Dimrnd, (db, v) => db.Dimrnd = v, 0.0, 0.5),
            Of("$DIMSAH", 70, 1009, db => db.Dimsah, (db, v) => db.Dimsah = v, false, true),
            Of("$DIMSCALE", 40, 1009, db => db.Dimscale, (db, v) => db.Dimscale = v, 1.0, 100.0),
            Of("$DIMSD1", 70, 1015, db => db.Dimsd1, (db, v) => db.Dimsd1 = v, false, true),
            Of("$DIMSD2", 70, 1015, db => db.Dimsd2, (db, v) => db.Dimsd2 = v, false, true),
            Of("$DIMSE1", 70, 1009, db => db.Dimse1, (db, v) => db.Dimse1 = v, false, true),
            Of("$DIMSE2", 70, 1009, db => db.Dimse2, (db, v) => db.Dimse2 = v, false, true),
            Of("$DIMSHO", 70, 1009, db => db.Dimsho, (db, v) => db.Dimsho = v, true, false),
            Of("$DIMSOXD", 70, 1009, db => db.Dimsoxd, (db, v) => db.Dimsoxd = v, false, true),
            Of("$DIMTAD", 70, 1009, db => db.Dimtad, (db, v) => db.Dimtad = v, 1, 0),
            Of("$DIMTDEC", 70, 1015, db => db.Dimtdec, (db, v) => db.Dimtdec = v, 2, 3),
            Of("$DIMTFAC", 40, 1009, db => db.Dimtfac, (db, v) => db.Dimtfac = v, 1.0, 0.7),
            Of("$DIMTFILL", 70, 1021, db => db.Dimtfill, (db, v) => db.Dimtfill = v, 0, 2),
            Of("$DIMTFILLCLR", 70, 1021, db => db.Dimtfillclr, (db, v) => db.Dimtfillclr = v, DxfColor.ByBlock, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3)),
            Of("$DIMTIH", 70, 1009, db => db.Dimtih, (db, v) => db.Dimtih = v, false, true),
            Of("$DIMTIX", 70, 1009, db => db.Dimtix, (db, v) => db.Dimtix = v, false, true),
            Of("$DIMTM", 40, 1009, db => db.Dimtm, (db, v) => db.Dimtm = v, 0.0, 0.05),
            Of("$DIMTMOVE", 70, 1015, db => db.Dimtmove, (db, v) => db.Dimtmove = v, 0, 2),
            Of("$DIMTOFL", 70, 1009, db => db.Dimtofl, (db, v) => db.Dimtofl = v, true, false),
            Of("$DIMTOH", 70, 1009, db => db.Dimtoh, (db, v) => db.Dimtoh = v, false, true),
            Of("$DIMTOL", 70, 1009, db => db.Dimtol, (db, v) => db.Dimtol = v, false, true),
            Of("$DIMTOLJ", 70, 1015, db => db.Dimtolj, (db, v) => db.Dimtolj = v, 0, 1),
            Of("$DIMTP", 40, 1009, db => db.Dimtp, (db, v) => db.Dimtp = v, 0.0, 0.1),
            Of("$DIMTSZ", 40, 1009, db => db.Dimtsz, (db, v) => db.Dimtsz = v, 0.0, 1.5),
            Of("$DIMTVP", 40, 1009, db => db.Dimtvp, (db, v) => db.Dimtvp = v, 0.0, 0.5),
            OfTextStyle("$DIMTXSTY", db => db.Dimtxsty, (db, v) => db.Dimtxsty = v, "GH_TXT"),
            Of("$DIMTXT", 40, 1009, db => db.Dimtxt, (db, v) => db.Dimtxt = v, 2.5, 3.5),
            Of("$DIMTXTDIRECTION", 70, 1024, db => db.Dimtxtdirection, (db, v) => db.Dimtxtdirection = v, false, true),
            Of("$DIMTZIN", 70, 1015, db => db.Dimtzin, (db, v) => db.Dimtzin = v, 8, 12),
            Of("$DIMUPT", 70, 1015, db => db.Dimupt, (db, v) => db.Dimupt = v, false, true),
            Of("$DIMZIN", 70, 1009, db => db.Dimzin, (db, v) => db.Dimzin = v, 8, 0),
        };

        #region The variables of the header

        [Fact]
        public void EveryVariableOfTheDrawingsOfEzdxf_ReadsAsEzdxfWroteIt()
        {
            Assert.Equal(77, Rows.Length);
            Assert.Equal(77, Rows.Select(r => r.Variable).Distinct().Count());

            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);

                foreach (Row row in Rows)
                {
                    Assert.True(db.GetHeader(row.Variable) != null || !db.File.Supports(row.Since), db.Version + " " + row.Variable + " is not in the drawing.");
                    object expected = db.GetHeader(row.Variable) != null && row.Written != null ? row.Written : Fallback(db, row);
                    Assert.Equal(db.Version + " " + row.Variable + " " + Show(expected), db.Version + " " + row.Variable + " " + Show(row.Get(db)));
                }
            }
        }

        [Fact]
        public void AVariableTheHeaderGivesNone_IsTheValueOfAutoCADsMetricDrawing()
        {
            foreach (string text in Drawings)
            {
                string bare = Rows.Aggregate(text, (t, row) => Without(t, row.Variable));
                DxfDatabase db = DxfDatabase.Parse(bare);

                foreach (Row row in Rows)
                {
                    Assert.Null(db.GetHeader(row.Variable));
                    Assert.Equal(row.Variable + " " + Show(Fallback(db, row)), row.Variable + " " + Show(row.Get(db)));
                }

                // The metric drawing's: arrowheads and texts 2.5, the text above the line; from R2000 two places, a comma.
                Assert.Equal(2.5, db.Dimasz);
                Assert.Equal(2.5, db.Dimtxt);
                Assert.Equal(1, db.Dimtad);

                if (db.File.Supports(1015))
                {
                    Assert.Equal(2, db.Dimdec);
                    Assert.Equal(',', db.Dimdsep);
                    Assert.Equal("Standard", db.Dimtxsty.Name);
                }
            }
        }

        [Fact]
        public void InAFileOfR12_WhatItsHeaderCannotHold_ReadsAsItsStylesReadIt_SoTheirDataAgree()
        {
            string text = DxfDimensionDrawings.R12;
            DxfDatabase db = DxfDatabase.Parse(text);

            // The variables of R2000 on read as a style of R12 reads them, acad.dwt's; those of R12 as the header gives them.
            Assert.Equal(4, db.Dimdec);
            Assert.Equal('.', db.Dimdsep);
            Assert.Equal(0, db.Dimtzin);
            Assert.Equal(4, db.Dimtdec);
            Assert.Equal(2, db.Dimalttd);
            Assert.Equal(1, db.Dimtolj);
            Assert.Null(db.Dimtxsty);
            Assert.Equal(2.5, db.Dimasz);
            Assert.Equal(8, db.Dimzin);

            // Set to what it reads, nothing is written and nothing refused.
            db.Dimdec = 4;
            db.Dimtxsty = null;
            Assert.Equal(text, db.ToText());

            // The header's data given to a dimension writes the overrides of R12 that differ, and refuses none.
            DxfRotatedDimension dim = db.ModelSpace.OfType<DxfRotatedDimension>().First();
            dim.SetDimstyleData(db.GetDimstyleData());
            Assert.Equal(db.Dimasz, ByHandle<DxfRotatedDimension>(DxfDatabase.Parse(db.ToText()), dim.Handle).Dimasz);

            // Added to the table as a style.
            DxfDimStyleTableRecord data = db.GetDimstyleData();
            data.Name = "GH_HEADER";
            db.DimStyleTable.Add(data);
            Assert.Equal(2.5, DxfDatabase.Parse(db.ToText()).DimStyleTable["GH_HEADER"].Dimasz);

            // A style given to the header, and the header's data read back: the style's every variable.
            DxfDatabase other = DxfDatabase.Parse(text);
            DxfDimStyleTableRecord standard = other.DimStyleTable["Standard"];
            other.SetDimstyleData(standard);
            DxfDimStyleTableRecord back = DxfDatabase.Parse(other.ToText()).GetDimstyleData();
            standard = DxfDatabase.Parse(other.ToText()).DimStyleTable["Standard"];

            foreach (var property in typeof(DxfDimStyleTableRecord).GetProperties().Where(p => p.CanWrite && p.Name.StartsWith("Dim", StringComparison.Ordinal)))
            {
                Assert.Equal(property.Name + " " + Show(property.GetValue(standard)), property.Name + " " + Show(property.GetValue(back)));
            }
        }

        [Fact]
        public void EveryVariableSet_IsWrittenInItsGroup_AndReadsBackAsSet()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                Row[] held = Rows.Where(r => db.File.Supports(r.Since)).ToArray();
                Assert.Equal(text == DxfDimensionDrawings.R12 ? 41 : text == DxfDimensionDrawings.R2000 ? 66 : 77, held.Length);

                foreach (Row row in held)
                {
                    row.Set(db);
                }

                string saved = db.ToText();
                OnlyTheseChanged(text, saved);
                DxfDatabase again = DxfDatabase.Parse(saved);

                foreach (Row row in held)
                {
                    Assert.Equal(row.Variable + " " + Show(row.Changed(again)), row.Variable + " " + Show(row.Get(again)));
                    Assert.Equal(row.Code, again.GetHeader(row.Variable)[0].TypeCode);
                }

                // The blocks, linetypes and the text style by their names, as AutoCAD's header names them.
                Assert.Equal("_ARCHTICK", again.GetHeader("$DIMBLK")[0].Value);
                Assert.Equal("1", again.GetHeader("$DIMCLRD")[0].Value);

                // A header that had none of them has each added at its end, and reads the same.
                string bare = held.Aggregate(text, (t, row) => Without(t, row.Variable));
                DxfDatabase added = DxfDatabase.Parse(bare);

                foreach (Row row in held)
                {
                    row.Set(added);
                }

                DxfDatabase addedAgain = DxfDatabase.Parse(added.ToText());

                foreach (Row row in held)
                {
                    Assert.Equal(row.Variable + " " + Show(row.Changed(addedAgain)), row.Variable + " " + Show(row.Get(addedAgain)));
                }
            }
        }

        [Fact]
        public void EveryVariableSetToTheValueItHas_ChangesNoByte()
        {
            foreach (string text in Drawings.Concat(Drawings.Select(t => Rows.Aggregate(t, (u, row) => Without(u, row.Variable)))))
            {
                DxfDatabase db = DxfDatabase.Parse(text);

                foreach (Row row in Rows)
                {
                    row.Again(db);
                }

                db.SetDimstyleData(db.GetDimstyleData());
                Assert.Equal(text, db.ToText());
            }
        }

        [Fact]
        public void AVariableTheFilesVersionHasNot_IsRefused_AndNothingIsWritten()
        {
            foreach (string text in new[] { DxfDimensionDrawings.R12, DxfDimensionDrawings.R2000 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                Row[] missing = Rows.Where(r => !db.File.Supports(r.Since)).ToArray();
                Assert.Equal(text == DxfDimensionDrawings.R12 ? 36 : 11, missing.Length);

                // Set to the value it reads, AutoCAD's own (DIMDEC 4 in R12), a variable the file has not writes nothing.
                Row[] changed = missing.Where(r => Show(r.Changed(db)) != Show(Fallback(db, r))).ToArray();
                Assert.Equal(text == DxfDimensionDrawings.R12 ? 33 : 11, changed.Length);
                missing = changed;

                foreach (Row row in missing)
                {
                    Assert.Throws<InvalidOperationException>(() => row.Set(db));
                }

                Assert.Equal(text, db.ToText());
            }

            DxfDatabase r2000 = DxfDatabase.Parse(DxfDimensionDrawings.R2000);
            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => r2000.Dimtxtdirection = true);
            Assert.Contains("R2010 (AC1024)", refused.Message, StringComparison.Ordinal);
            Assert.Contains("R2004 (AC1018)", Assert.Throws<InvalidOperationException>(() => r2000.DimAssoc = 1).Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AValueAutoCADDoesNotTake_IsRefused_AndNothingIsWritten()
        {
            string text = DxfDimensionDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDatabase other = DxfDatabase.Parse(text);
            var outOfRange = new Action[]
            {
                () => db.Dimadec = -2,
                () => db.Dimadec = 9,
                () => db.Dimaltd = 9,
                () => db.Dimalttz = 16,
                () => db.Dimaltu = 0,
                () => db.Dimaltu = 9,
                () => db.Dimarcsym = 3,
                () => db.DimAssoc = 3,
                () => db.Dimasz = -0.5,
                () => db.Dimatfit = 4,
                () => db.Dimaunit = 4,
                () => db.Dimazin = 4,
                () => db.Dimcen = double.NaN,
                () => db.Dimdec = 9,
                () => db.Dimfrac = 3,
                () => db.Dimgap = double.PositiveInfinity,
                () => db.Dimjust = 5,
                () => db.Dimlunit = 0,
                () => db.Dimlunit = 7,
                () => db.Dimlwd = (DxfLineWeight)7,
                () => db.Dimscale = -1.0,
                () => db.Dimtad = 5,
                () => db.Dimtdec = -1,
                () => db.Dimtfill = 3,
                () => db.Dimtmove = 3,
                () => db.Dimtolj = 3,
                () => db.Dimtxt = -2.5,
                () => db.Dimtzin = 16,
                () => db.Dimzin = -1,
            };

            foreach (Action refusal in outOfRange)
            {
                Assert.Throws<ArgumentOutOfRangeException>(refusal);
            }

            Assert.Throws<ArgumentNullException>(() => db.Dimpost = null);
            Assert.Throws<ArgumentException>(() => db.Dimapost = "two\nlines");
            Assert.Throws<ArgumentNullException>(() => db.Dimclrd = null);
            Assert.Throws<ArgumentException>(() => db.Dimclrt = DxfColor.FromRgb(1, 2, 3));
            Assert.Throws<ArgumentNullException>(() => db.Dimtxsty = null);
            Assert.Throws<ArgumentException>(() => db.Dimtxsty = other.TextStyleTable["GH_TXT"]);
            Assert.Throws<ArgumentException>(() => db.Dimblk = other.BlockTable["_DOT"]);
            Assert.Throws<ArgumentException>(() => db.Dimblk = db.BlockTable["*Model_Space"]);
            Assert.Throws<ArgumentException>(() => db.Dimltype = other.LinetypeTable["CENTER"]);
            Assert.Equal(text, db.ToText());
        }

        [Fact]
        public void AnArrowheadNamedWithoutItsUnderscore_IsItsBlock_AndOneTheDrawingHasNot_IsNull()
        {
            string text = DxfDimensionDrawings.R2018.Replace("$DIMBLK\n  1\n\n", "$DIMBLK\n  1\nARCHTICK\n").Replace("$DIMBLK1\n  1\n\n", "$DIMBLK1\n  1\n_NONE\n");
            Assert.NotEqual(DxfDimensionDrawings.R2018, text);
            DxfDatabase db = DxfDatabase.Parse(text);

            Assert.Equal("_ARCHTICK", db.Dimblk.Name);
            Assert.Null(db.Dimblk1);

            // Set to what it reads, the header is kept as it names them.
            db.Dimblk = db.Dimblk;
            db.Dimblk1 = null;
            Assert.Equal(text, db.ToText());

            db.Dimblk = null;
            Assert.Equal(string.Empty, db.GetHeader("$DIMBLK")[0].Value);
        }

        #endregion

        #region The header as a dimension style

        [Fact]
        public void GetDimstyleData_GivesTheHeaderAsAStyleInNoTable()
        {
            string text = DxfDimensionDrawings.R2018.Replace("$DIMASZ\n 40\n2.5\n", "$DIMASZ\n 40\n3.5\n").Replace("$DIMBLK\n  1\n\n", "$DIMBLK\n  1\n_DOT\n");
            DxfDatabase db = DxfDatabase.Parse(text);

            DxfDimStyleTableRecord data = db.GetDimstyleData();

            Assert.Null(data.Item);
            Assert.Equal("ISO-25", data.Name);
            Assert.Equal(3.5, data.Dimasz);
            Assert.Equal(2.5, data.Dimtxt);
            Assert.Equal(2, data.Dimdec);
            Assert.Equal(',', data.Dimdsep);
            Assert.Equal(0.785398163397, data.Dimjogang);
            Assert.Same(db.BlockTable["_DOT"], data.Dimblk);
            Assert.Same(db.TextStyleTable["Standard"], data.Dimtxsty);
            Assert.True(data.Dimclrd.IsByBlock);
            Assert.False(data.Dimtxtdirection);

            // Added to the table, the style holds the header's values.
            data.Name = "GH_HEADER";
            db.DimStyleTable.Add(data);
            DxfDimStyleTableRecord read = DxfDatabase.Parse(db.ToText()).DimStyleTable["GH_HEADER"];
            Assert.Equal(3.5, read.Dimasz);
            Assert.Equal("_DOT", read.Dimblk.Name);
            Assert.Equal(0.785398163397, read.Dimjogang, 12);
        }

        [Fact]
        public void SetDimstyleData_WritesTheVariablesOfAStyleThatDiffer_AndNoOther()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfDimStyleTableRecord style = db.DimStyleTable["GH_DIM"];

                db.SetDimstyleData(style);

                string saved = db.ToText();
                OnlyTheseChanged(text, saved);
                DxfDatabase again = DxfDatabase.Parse(saved);
                DxfDimStyleTableRecord data = again.GetDimstyleData();
                DxfDimStyleTableRecord written = again.DimStyleTable["GH_DIM"];
                Assert.Equal("ISO-25", again.Dimstyle);

                foreach (string name in new[] { "Dimasz", "Dimtxt", "Dimexo", "Dimexe", "Dimgap", "Dimscale", "Dimtad", "Dimzin", "Dimclrd", "Dimsah", "Dimtol", "Dimpost" })
                {
                    Assert.Equal(name + " " + Show(Value(written, name)), name + " " + Show(Value(data, name)));
                }

                Assert.Equal(written.Dimblk1?.Name, again.Dimblk1?.Name);
                Assert.Equal(written.Dimblk2?.Name, again.Dimblk2?.Name);

                if (again.File.Supports(1015))
                {
                    Assert.Equal(written.Dimdec, again.Dimdec);
                    Assert.Equal(written.Dimdsep, again.Dimdsep);
                    Assert.Equal(written.Dimlwd, again.Dimlwd);
                    Assert.Equal(written.Dimtxsty?.Name ?? "Standard", again.Dimtxsty.Name);
                }

                if (again.File.Supports(1021))
                {
                    Assert.Equal(written.Dimltype?.Name, again.Dimltype?.Name);
                    Assert.Equal(written.Dimjogang, again.Dimjogang, 12);
                }

                // Given the data it gives, nothing changes.
                again.SetDimstyleData(again.GetDimstyleData());
                Assert.Equal(saved, again.ToText());
            }
        }

        [Fact]
        public void SetDimstyleData_OfAStyleOfAnotherDrawing_OrAValueRefused_WritesNothing()
        {
            string text = DxfDimensionDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDatabase other = DxfDatabase.Parse(text);

            Assert.Throws<ArgumentNullException>(() => db.SetDimstyleData(null));
            Assert.Throws<ArgumentException>(() => db.SetDimstyleData(other.DimStyleTable["GH_DIM"]));

            var made = new DxfDimStyleTableRecord { Dimasz = 4.0, Dimdec = 12 };
            Assert.Throws<ArgumentOutOfRangeException>(() => db.SetDimstyleData(made));
            Assert.Equal(text, db.ToText());

            // A style made in code holds AutoCAD's own values where it is given none, the imperial ones, written as they are; one
            // that names no text style leaves the header's.
            var plain = new DxfDimStyleTableRecord { Dimasz = 4.0 };
            db.SetDimstyleData(plain);
            Assert.Equal(4.0, db.Dimasz);
            Assert.Equal(0.18, db.Dimtxt);
            Assert.Equal(4, db.Dimdec);
            Assert.Equal('.', db.Dimdsep);
            Assert.Equal("Standard", db.Dimtxsty.Name);
        }

        [Fact]
        public void SetDimstyleData_InAFileOfAnOlderVersion_PassesOverWhatItsHeaderHasNot()
        {
            // The data of the header of R2018, the same where R12 has the variables, changes nothing in R12; nor DIMDEC, which
            // came with R2000, as AutoCAD's SAVEAS to R12 leaves it out.
            string text = DxfDimensionDrawings.R12;
            DxfDatabase db = DxfDatabase.Parse(text);
            db.SetDimstyleData(DxfDatabase.Parse(DxfDimensionDrawings.R2018).GetDimstyleData());
            DxfDimStyleTableRecord data = db.GetDimstyleData();
            data.Dimdec = 1;
            db.SetDimstyleData(data);
            Assert.Equal(text, db.ToText());

            // DIMARCSYM, which a style of R2000 holds and the header only from R2007.
            DxfDatabase r2000 = DxfDatabase.Parse(DxfDimensionDrawings.R2000);
            DxfDimStyleTableRecord style = r2000.DimStyleTable["GH_DIM"];
            Assert.NotEqual(0, style.Dimarcsym);
            r2000.SetDimstyleData(style);
            Assert.Null(r2000.GetHeader("$DIMARCSYM"));
            Assert.Equal(style.Dimdec, r2000.Dimdec);
        }

        #endregion

        #region DIMTXTDIRECTION of a style and of a dimension

        [Fact]
        public void Dimtxtdirection_OfAStyle_IsItsGroup294_FromR2010()
        {
            string text = DxfDimensionDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDimStyleTableRecord style = db.DimStyleTable["GH_DIM"];
            Assert.False(style.Dimtxtdirection);

            style.Dimtxtdirection = true;

            string saved = db.ToText();
            Differ(text, saved, out List<string> gone, out List<string> come);
            Assert.Contains("\n  2\nGH_DIM\n", Assert.Single(gone), StringComparison.Ordinal);
            string record = Assert.Single(come);
            Assert.True(record.IndexOf("\n294\n1\n", StringComparison.Ordinal) > record.IndexOf("\n290\n", StringComparison.Ordinal));
            Assert.True(record.IndexOf("\n294\n1\n", StringComparison.Ordinal) < record.IndexOf("\n340\n", StringComparison.Ordinal));
            Assert.True(DxfDatabase.Parse(saved).DimStyleTable["GH_DIM"].Dimtxtdirection);

            // A style made in code is written with it in R2018, after DIMFXLON (290), off where it is given none; without it in
            // R2000, and refused there.
            var made = new DxfDimStyleTableRecord { Name = "GH_RTL", Dimtxtdirection = true };
            db.DimStyleTable.Add(made);
            Assert.Contains("\n294\n1\n", Records(db.ToText()).Single(r => r.Contains("\n  2\nGH_RTL\n")), StringComparison.Ordinal);
            db.DimStyleTable.Add(new DxfDimStyleTableRecord { Name = "GH_PLAIN" });
            Assert.Contains("\n290\n0\n294\n0\n371\n", Records(db.ToText()).Single(r => r.Contains("\n  2\nGH_PLAIN\n")), StringComparison.Ordinal);

            DxfDatabase r2000 = DxfDatabase.Parse(DxfDimensionDrawings.R2000);
            r2000.DimStyleTable.Add(new DxfDimStyleTableRecord { Name = "GH_LTR" });
            Assert.DoesNotContain("\n294\n", Records(r2000.ToText()).Single(r => r.Contains("\n  2\nGH_LTR\n")), StringComparison.Ordinal);
            Assert.Throws<InvalidOperationException>(() => r2000.DimStyleTable["GH_DIM"].Dimtxtdirection = true);
        }

        [Fact]
        public void Dimtxtdirection_OfADimension_IsAnOverride1070_294()
        {
            string text = DxfDimensionDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfRotatedDimension dim = ByHandle<DxfRotatedDimension>(db, "61");
            Assert.False(dim.Dimtxtdirection);

            dim.Dimtxtdirection = true;

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "61");
            Assert.Contains("1070\n294\n1070\n1\n", Records(saved).Single(r => HandleOf(r) == "61"), StringComparison.Ordinal);
            Assert.True(ByHandle<DxfRotatedDimension>(DxfDatabase.Parse(saved), "61").Dimtxtdirection);
            Assert.True(dim.GetDimstyleData().Dimtxtdirection);

            Assert.Throws<InvalidOperationException>(() => ByHandle<DxfRotatedDimension>(DxfDatabase.Parse(DxfDimensionDrawings.R2000), "61").Dimtxtdirection = true);
        }

        [Fact]
        public void Dimtxtdirection_PutIntoAStyleThatHasNone_StandsAfter290_AndTheHandlesAfterIt()
        {
            foreach (bool first in new[] { true, false })
            {
                DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                DxfDimStyleTableRecord style = db.DimStyleTable["GH_EMPTY"];
                string before = Records(db.ToText()).Single(r => r.Contains("\n  2\nGH_EMPTY\n"));
                Assert.DoesNotContain("\n294\n", before, StringComparison.Ordinal);
                Assert.DoesNotContain("\n340\n", before, StringComparison.Ordinal);

                if (first)
                {
                    style.Dimtxtdirection = true;
                }

                style.Dimtxsty = db.TextStyleTable["GH_TXT"];
                style.Dimldrblk = db.BlockTable["_DOT"];
                style.DimfxlenOn = true;
                style.Dimtxtdirection = true;

                string record = Records(db.ToText()).Single(r => r.Contains("\n  2\nGH_EMPTY\n"));
                int fxl = record.IndexOf("\n290\n1\n", StringComparison.Ordinal);
                int direction = record.IndexOf("\n294\n1\n", StringComparison.Ordinal);
                int textStyle = record.IndexOf("\n340\n", StringComparison.Ordinal);
                int leader = record.IndexOf("\n341\n", StringComparison.Ordinal);
                Assert.True(fxl >= 0 && fxl < direction && direction < textStyle && textStyle < leader, record);
            }
        }

        [Fact]
        public void ACopyIntoAnOlderDrawing_LeavesOutTheOverrideOfDimtxtdirection()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfRotatedDimension dim = ByHandle<DxfRotatedDimension>(source, "61");
            dim.Dimtxtdirection = true;
            dim.Dimasz = 4.25;

            DxfDatabase older = DxfDatabase.Parse(DxfDimensionDrawings.R2000);
            var copy = (DxfDimension)source.WblockCloneObjects(new DxfEntity[] { dim }, older.ModelSpace).Single();
            string record = Records(older.ToText()).Single(r => HandleOf(r) == copy.Handle);
            Assert.DoesNotContain("1070\n294\n", record, StringComparison.Ordinal);
            Assert.Contains("1070\n41\n1040\n4.25\n", record, StringComparison.Ordinal);

            DxfDatabase same = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            var kept = (DxfDimension)source.WblockCloneObjects(new DxfEntity[] { dim }, same.ModelSpace).Single();
            Assert.Contains("1070\n294\n1070\n1\n", Records(same.ToText()).Single(r => HandleOf(r) == kept.Handle), StringComparison.Ordinal);

            // A leader's alike.
            DxfDatabase leaders = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfLeader leader = ByHandle<DxfLeader>(leaders, "B3");
            DxfDimStyleTableRecord data = leader.GetDimstyleData();
            data.Dimtxtdirection = true;
            leader.SetDimstyleData(data);
            Assert.Contains("1070\n294\n1070\n1\n", Records(leaders.ToText()).Single(r => HandleOf(r) == "B3"), StringComparison.Ordinal);
            DxfDatabase olderLeaders = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2000);
            var leaderCopy = (DxfLeader)leaders.WblockCloneObjects(new DxfEntity[] { leader }, olderLeaders.ModelSpace).Single();
            Assert.DoesNotContain("1070\n294\n", Records(olderLeaders.ToText()).Single(r => HandleOf(r) == leaderCopy.Handle), StringComparison.Ordinal);
        }

        #endregion

        #region SetDimstyleData of a dimension and a leader

        [Fact]
        public void SetDimstyleData_OfARecordThatNamesNoTextStyle_LeavesTheTextStyleOfADimensionOrALeader()
        {
            string text = DxfDimensionDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfRotatedDimension dim = ByHandle<DxfRotatedDimension>(db, "4D");
            DxfTextStyleTableRecord own = dim.TextStyle;
            Assert.NotNull(own);

            dim.SetDimstyleData(new DxfDimStyleTableRecord { Dimasz = 4.25 });

            DxfRotatedDimension read = ByHandle<DxfRotatedDimension>(DxfDatabase.Parse(db.ToText()), "4D");
            Assert.Equal(4.25, read.Dimasz);
            Assert.Equal(own.Name, read.TextStyle?.Name);

            DxfDatabase leaders = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfLeader leader = ByHandle<DxfLeader>(leaders, "B3");
            leader.SetDimstyleData(new DxfDimStyleTableRecord { Dimasz = 4.25 });
            string record = Records(leaders.ToText()).Single(r => HandleOf(r) == "B3");
            Assert.Contains("1070\n41\n1040\n4.25\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("1070\n340\n", record, StringComparison.Ordinal);
        }

        [Fact]
        public void SetDimstyleData_OfADimensionOrALeader_RefusesWhatTheHeaderRefuses_BeforeWritingAny()
        {
            string text = DxfDimensionDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfRotatedDimension dim = ByHandle<DxfRotatedDimension>(db, "61");
            DxfDimStyleTableRecord data = dim.GetDimstyleData();
            data.Dimasz = 4.25;
            data.Dimdec = 12;
            Assert.Throws<ArgumentOutOfRangeException>(() => dim.SetDimstyleData(data));
            data.Dimdec = 2;
            data.Dimtxt = -1.0;
            Assert.Throws<ArgumentOutOfRangeException>(() => dim.SetDimstyleData(data));
            data.Dimtxt = 2.5;
            data.Dimblk = DxfDatabase.Parse(text).BlockTable["_DOT"];
            Assert.Throws<ArgumentException>(() => dim.SetDimstyleData(data));
            Assert.Equal(text, db.ToText());

            string leaderText = DxfHatchLeaderDrawings.R2018;
            DxfDatabase leaders = DxfDatabase.Parse(leaderText);
            DxfLeader leader = ByHandle<DxfLeader>(leaders, "B3");
            DxfDimStyleTableRecord leaderData = leader.GetDimstyleData();
            leaderData.Dimasz = 4.25;
            leaderData.Dimzin = 16;
            Assert.Throws<ArgumentOutOfRangeException>(() => leader.SetDimstyleData(leaderData));
            Assert.Equal(leaderText, leaders.ToText());
        }

        #endregion

        #region A leader's dimension style

        [Fact]
        public void GetDimstyleData_OfALeader_GivesItsOverridesOverItsStyle()
        {
            foreach (string text in new[] { DxfHatchLeaderDrawings.R2000, DxfHatchLeaderDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfLeader leader = ByHandle<DxfLeader>(db, "B2");

                DxfDimStyleTableRecord data = leader.GetDimstyleData();

                Assert.Null(data.Item);
                Assert.Equal("Standard", data.Name);
                Assert.Equal(leader.Dimasz, data.Dimasz);
                Assert.Equal(3.5, data.Dimasz);
                Assert.Equal(1.25, data.Dimgap);
                Assert.Equal(2.0, data.Dimscale);
                Assert.Equal(1, data.Dimclrd.ColorIndex);
                Assert.Equal(DxfLineWeight.LineWeight035, data.Dimlwd);
                Assert.True(data.Dimsah);
                Assert.Equal(db.DimStyleTable["Standard"].Dimexe, data.Dimexe);
                Assert.Equal(db.DimStyleTable["Standard"].Dimdec, data.Dimdec);
                Assert.Null(data.Dimldrblk);

                DxfDimStyleTableRecord plain = ByHandle<DxfLeader>(db, "B3").GetDimstyleData();
                Assert.Equal(2.5, plain.Dimasz);
                Assert.Equal(0.625, plain.Dimgap);
            }
        }

        [Fact]
        public void SetDimstyleData_OfALeader_WritesTheDifferencesAsOverrides()
        {
            foreach (string text in new[] { DxfHatchLeaderDrawings.R2000, DxfHatchLeaderDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfLeader plain = ByHandle<DxfLeader>(db, "B3");
                DxfLeader styled = ByHandle<DxfLeader>(db, "B2");

                // Its own data changes nothing; that of the other leader writes its overrides on this one.
                styled.SetDimstyleData(styled.GetDimstyleData());
                plain.SetDimstyleData(plain.GetDimstyleData());
                Assert.Equal(text, db.ToText());

                plain.SetDimstyleData(styled.GetDimstyleData());

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, "B3");
                string record = Records(saved).Single(r => HandleOf(r) == "B3");
                Assert.Contains("1000\nDSTYLE\n1002\n{\n", record, StringComparison.Ordinal);
                Assert.Contains("1070\n41\n1040\n3.5\n", record, StringComparison.Ordinal);
                Assert.Contains("1070\n176\n1070\n1\n", record, StringComparison.Ordinal);
                Assert.DoesNotContain("1070\n140\n", record, StringComparison.Ordinal);

                DxfLeader read = ByHandle<DxfLeader>(DxfDatabase.Parse(saved), "B3");
                Assert.Equal(3.5, read.Dimasz);
                Assert.Equal(1.25, read.Dimgap);
                Assert.Equal(2.0, read.Dimscale);
                Assert.True(read.Dimsah);
                Assert.Equal(DxfLineWeight.LineWeight035, read.Dimlwd);

                // An arrowhead of the drawing, by its handle; one of another drawing refused, nothing written.
                DxfDatabase again = DxfDatabase.Parse(saved);
                DxfLeader leader = ByHandle<DxfLeader>(again, "B3");
                DxfBlockTableRecord block = again.BlockTable.Cast<DxfBlockTableRecord>().First(b => !b.IsLayout && !b.IsAnonymous);
                DxfDimStyleTableRecord foreign = leader.GetDimstyleData();
                foreign.Dimldrblk = DxfDatabase.Parse(saved).BlockTable[block.Name];
                Assert.Throws<ArgumentException>(() => leader.SetDimstyleData(foreign));
                Assert.Equal(saved, again.ToText());

                DxfDimStyleTableRecord own = leader.GetDimstyleData();
                own.Dimldrblk = block;
                leader.SetDimstyleData(own);
                Assert.Same(block, leader.Dimldrblk);
                Assert.Contains("1070\n341\n1005\n" + block.Handle + "\n", Records(again.ToText()).Single(r => HandleOf(r) == "B3"), StringComparison.Ordinal);

                // None again: the handle 0, the closed filled arrow.
                own.Dimldrblk = null;
                leader.SetDimstyleData(own);
                Assert.Null(leader.Dimldrblk);
                Assert.Contains("1070\n341\n1005\n0\n", Records(again.ToText()).Single(r => HandleOf(r) == "B3"), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void SetDimstyleData_OfALeader_RefusesAValueOrAVersionBeforeWritingAny()
        {
            string text = DxfHatchLeaderDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLeader leader = ByHandle<DxfLeader>(db, "B3");

            Assert.Throws<ArgumentNullException>(() => leader.SetDimstyleData(null));

            DxfDimStyleTableRecord data = leader.GetDimstyleData();
            data.Dimasz = 5.0;
            data.Dimtxtdirection = true;
            Assert.Throws<InvalidOperationException>(() => leader.SetDimstyleData(data));
            Assert.Equal(text, db.ToText());

            leader.Erase();
            string erased = db.ToText();
            data.Dimtxtdirection = false;
            Assert.Throws<InvalidOperationException>(() => leader.SetDimstyleData(data));
            Assert.Equal(erased, db.ToText());
        }

        #endregion

        #region Helpers

        private static object Value(DxfDimStyleTableRecord style, string name) => typeof(DxfDimStyleTableRecord).GetProperty(name).GetValue(style);

        /// <summary>
        /// The value a variable reads where the header gives none: AutoCAD's metric drawing's where the file's version has it,
        /// else that of a style of the file, acad.dwt's, as a style made in code holds it (DIMASSOC, of no style, the metric).
        /// </summary>
        private static object Fallback(DxfDatabase db, Row row)
        {
            if (db.File.Supports(row.Since) || row.Variable == "$DIMASSOC")
            {
                return row.Fallback(db);
            }

            string name = row.Variable == "$DIMFXL" ? "Dimfxlen" : row.Variable == "$DIMFXLON" ? "DimfxlenOn"
                : "Dim" + row.Variable.Substring(4).ToLowerInvariant();
            return Value(new DxfDimStyleTableRecord(), name);
        }

        /// <summary>The text of a drawing without a variable of its header, where it has it.</summary>
        private static string Without(string text, string variable)
        {
            int at = text.IndexOf("  9\n" + variable + "\n", StringComparison.Ordinal);

            if (at < 0)
            {
                return text;
            }

            int nine = text.IndexOf("\n  9\n", at + 4, StringComparison.Ordinal);
            int zero = text.IndexOf("\n  0\n", at + 4, StringComparison.Ordinal);
            int end = (nine < 0 ? zero : Math.Min(nine, zero)) + 1;
            return text.Remove(at, end - at);
        }

        private static string Show(object value)
        {
            switch (value)
            {
                case null:
                    return "null";
                case double d:
                    return d.ToString("R", CultureInfo.InvariantCulture);
                case DxfColor c:
                    return c.ColorMethod + " " + c.ColorIndex.ToString(CultureInfo.InvariantCulture);
                case DxfSymbolTableRecord r:
                    return r.GetType().Name + " " + r.Name;
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        private static Row Of<T>(string variable, int code, int since, Func<DxfDatabase, T> get, Action<DxfDatabase, T> set, T fallback, T changed, object written = null)
            => new Row
            {
                Variable = variable,
                Code = code,
                Since = since,
                Get = db => get(db),
                Set = db => set(db, changed),
                Again = db => set(db, get(db)),
                Fallback = db => fallback,
                Changed = db => changed,
                Written = written,
            };

        private static Row OfBlock(string variable, int since, Func<DxfDatabase, DxfBlockTableRecord> get, Action<DxfDatabase, DxfBlockTableRecord> set, string changed)
            => new Row
            {
                Variable = variable,
                Code = 1,
                Since = since,
                Get = db => get(db),
                Set = db => set(db, db.BlockTable[changed]),
                Again = db => set(db, get(db)),
                Fallback = db => null,
                Changed = db => db.BlockTable[changed],
                Written = null,
            };

        private static Row OfLinetype(string variable, Func<DxfDatabase, DxfLinetypeTableRecord> get, Action<DxfDatabase, DxfLinetypeTableRecord> set, string changed)
            => new Row
            {
                Variable = variable,
                Code = 6,
                Since = 1021,
                Get = db => get(db),
                Set = db => set(db, db.LinetypeTable[changed]),
                Again = db => set(db, get(db)),
                Fallback = db => null,
                Changed = db => db.LinetypeTable[changed],
                Written = null,
            };

        private static Row OfTextStyle(string variable, Func<DxfDatabase, DxfTextStyleTableRecord> get, Action<DxfDatabase, DxfTextStyleTableRecord> set, string changed)
            => new Row
            {
                Variable = variable,
                Code = 7,
                Since = 1015,
                Get = db => get(db),
                Set = db => set(db, db.TextStyleTable[changed]),
                Again = db => set(db, get(db)),
                Fallback = db => db.TextStyleTable["Standard"],
                Changed = db => db.TextStyleTable[changed],
                Written = null,
            };

        /// <summary>A variable of the header: its name and group, the version it came with, and the values it is read and set to.</summary>
        private sealed class Row
        {
            public string Variable { get; set; }

            public int Code { get; set; }

            public int Since { get; set; }

            public Func<DxfDatabase, object> Get { get; set; }

            public Action<DxfDatabase> Set { get; set; }

            public Action<DxfDatabase> Again { get; set; }

            public Func<DxfDatabase, object> Fallback { get; set; }

            public Func<DxfDatabase, object> Changed { get; set; }

            // The value ezdxf's drawings hold, where it is not the one a new drawing of AutoCAD has: DIMALTF and DIMJOGANG
            // rounded to 11 and 12 places.
            public object Written { get; set; }
        }

        #endregion
    }
}
