using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Fields: a FIELD given to an object under ACAD_FIELD of its extension dictionary, read, replaced and taken out, the words of
    /// a text with the codes of its fields, a text's fields made words, and the field of a dimension's text copied to and from an
    /// MTEXT; ezdxf 1.4.5 read the probe's fields of R2004, R2007 and R2018 back, each owned by what names it, its recover and
    /// audit finding nothing.
    /// </summary>
    public class DxfFieldTests
    {
        private const string DateCode = "Drawn %<\\AcVar Date \\f \"dd/MM/yyyy\">%";

        [Fact]
        public void AFieldGivenToAText_IsWrittenUnderACAD_FIELD_AsAutoCADWritesOneNotEvaluatedYet()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDBText grid = ByHandle<DxfDBText>(db, "64");

            Assert.Null(grid.GetField());
            DxfField made = new DxfField(DateCode, true);
            DxfField field = grid.SetField(made);

            Assert.Same(made, field);
            Assert.Same(field, grid.GetField());
            Assert.Same(field, grid.GetField("text"));
            Assert.Equal("Grid %%c20 A", grid.TextString);
            DxfField child = Assert.Single(field.GetChildren());
            DxfDBDictionary fields = (DxfDBDictionary)grid.ExtensionDictionary.GetAt("ACAD_FIELD");

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "64", grid.ExtensionDictionary.Handle, fields.Handle, field.Handle, child.Handle, null);
            Assert.Equal("  0\nDICTIONARY\n  5\n" + fields.Handle + "\n102\n{ACAD_REACTORS\n330\n" + grid.ExtensionDictionary.Handle + "\n102\n}\n330\n" + grid.ExtensionDictionary.Handle
                + "\n100\nAcDbDictionary\n280\n1\n281\n1\n  3\nTEXT\n360\n" + field.Handle + "\n", Records(saved).Single(r => HandleOf(r) == fields.Handle));
            Assert.Equal("  0\nFIELD\n  5\n" + field.Handle + "\n102\n{ACAD_REACTORS\n330\n" + fields.Handle + "\n102\n}\n330\n" + fields.Handle
                + "\n100\nAcDbField\n  1\n_text\n  2\nDrawn %<\\_FldIdx 0>%\n 90\n1\n360\n" + child.Handle
                + "\n 97\n0\n 91\n63\n 92\n0\n 94\n0\n 95\n1\n 96\n0\n300\n\n 93\n0\n  7\nACFD_FIELD_VALUE\n 93\n1\n 90\n0\n 94\n0\n300\n\n302\n\n304\nACVALUE_END\n301\n\n 98\n0\n",
                Records(saved).Single(r => HandleOf(r) == field.Handle));
            Assert.Contains("100\nAcDbField\n  1\nAcVar\n  2\n\\AcVar Date \\f \"dd/MM/yyyy\"\n 90\n0\n 97\n0\n", Records(saved).Single(r => HandleOf(r) == child.Handle), StringComparison.Ordinal);
            Assert.Equal(field.Handle, child.OwnerHandle);
            Assert.Equal("  0\nCLASS\n  1\nFIELD\n  2\nAcDbField\n  3\nObjectDBX Classes\n 90\n1152\n 91\n0\n280\n0\n281\n0\n", Records(saved).Single(r => r.StartsWith("  0\nCLASS\n  1\nFIELD\n", StringComparison.Ordinal)));

            DxfDatabase again = DxfDatabase.Parse(saved);
            DxfField read = ByHandle<DxfDBText>(again, "64").GetField();
            Assert.Equal("_text", read.EvaluatorId);
            Assert.True(read.IsTextField);
            Assert.Equal("Drawn %<\\_FldIdx 0>%", read.GetFieldCode());
            Assert.Equal("AcVar", read.GetChildren()[0].EvaluatorId);
            Assert.False(read.GetChildren()[0].IsTextField);
            Assert.Equal(string.Empty, read.GetStringValue());
            Assert.Equal(DateCode, ByHandle<DxfDBText>(again, "64").GetTextWithFieldCodes());
            Assert.Equal(saved, again.ToText());

            // A second field keeps the class listed once.
            ByHandle<DxfMText>(again, "73").SetField(new DxfField("%<\\AcVar Login>%"));
            Assert.Single(Records(again.ToText()), r => r.StartsWith("  0\nCLASS\n  1\nFIELD\n", StringComparison.Ordinal));
        }

        [Fact]
        public void AFieldOfR2004_KeepsItsValueAsThatVersionWritesOne_AndOneOfR2000IsRefused()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018.Replace("AC1032", "AC1018"));
            DxfField field = ByHandle<DxfDBText>(db, "64").SetField(new DxfField("%<\\AcVar Login>%"));

            Assert.Contains("  7\nACFD_FIELD_VALUE\n 90\n0\n301\n\n 98\n0\n", Records(db.ToText()).Single(r => HandleOf(r) == field.Handle), StringComparison.Ordinal);
            Assert.Empty(field.GetChildren());
            Assert.Equal("%<\\AcVar Login>%", ByHandle<DxfDBText>(db, "64").GetTextWithFieldCodes());

            DxfDatabase r2000 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            string before = r2000.ToText();
            Assert.Throws<InvalidOperationException>(() => r2000.ModelSpace.OfType<DxfDBText>().First().SetField(new DxfField("%<\\AcVar Login>%")));
            Assert.Equal(before, r2000.ToText());
            Assert.Throws<InvalidOperationException>(() => DxfDatabase.Parse(DxfFileEzdxfDrawings.R12).ModelSpace.OfType<DxfDBText>().First().SetField(new DxfField("%<\\AcVar Login>%")));
            Assert.Throws<InvalidOperationException>(() => new DxfDBText().SetField(new DxfField("%<\\AcVar Login>%")));
            Assert.Null(new DxfDBText().GetField());
            Assert.Null(new DxfDBText().RemoveField());
        }

        [Fact]
        public void AFieldMadeInCode_ReadsItsCodeAsAutoCADDoes()
        {
            var text = new DxfField("Made %<\\AcVar Date>% by %<\\AcObjProp.16.2 Object(%<\\_ObjId 2130>%).Length \\f \"%lu2\">%!", true);
            Assert.Equal("_text", text.EvaluatorId);
            Assert.Equal("Made %<\\_FldIdx 0>% by %<\\_FldIdx 1>%!", text.GetFieldCode());
            Assert.Equal(new[] { "AcVar", "AcObjProp" }, text.GetChildren().Select(c => c.EvaluatorId));
            Assert.Equal("\\AcObjProp.16.2 Object(%<\\_ObjId 2130>%).Length \\f \"%lu2\"", text.GetChildren()[1].GetFieldCode());
            Assert.Null(text.Handle);
            Assert.Equal(string.Empty, text.GetStringValue());

            var plain = new DxfField("no field at all", true);
            Assert.Equal("no field at all", plain.GetFieldCode());
            Assert.Empty(plain.GetChildren());

            var one = new DxfField(" %<\\AcVar Filename \\f \"%fn2\">% ");
            Assert.Equal("AcVar", one.EvaluatorId);
            Assert.Equal("\\AcVar Filename \\f \"%fn2\"", one.GetFieldCode());

            Assert.Throws<ArgumentNullException>(() => new DxfField((string)null));
            Assert.Throws<ArgumentException>(() => new DxfField("\\AcVar Date"));
            Assert.Throws<ArgumentException>(() => new DxfField("Drawn %<\\AcVar Date>%"));
            Assert.Throws<ArgumentException>(() => new DxfField("%<\\AcVar Date>%%<\\AcVar Login>%"));
            Assert.Throws<ArgumentException>(() => new DxfField("%< AcVar Date>%"));
            Assert.Throws<ArgumentException>(() => new DxfField("%<\\AcVar Date\n>%"));
            Assert.Throws<ArgumentException>(() => new DxfField("Drawn %<\\AcVar Date", true));
        }

        [Fact]
        public void AFieldReplaced_ErasesTheOldWithItsChildren_AndAFieldInAFileIsRefused()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfDBText grid = ByHandle<DxfDBText>(db, "64");
            DxfField first = grid.SetField(new DxfField(DateCode, true));
            DxfField child = first.GetChildren()[0];
            DxfField second = grid.SetField(new DxfField("%<\\AcVar Login>% wrote", true));

            Assert.True(first.IsErased);
            Assert.True(child.IsErased);
            Assert.Same(second, grid.GetField());
            Assert.Equal(1, grid.ExtensionDictionary.GetAt("ACAD_FIELD") is DxfDBDictionary fields ? fields.Count : -1);
            string saved = db.ToText();
            Assert.DoesNotContain(Records(saved), r => HandleOf(r) == first.Handle || HandleOf(r) == child.Handle);
            Assert.Equal("%<\\AcVar Login>% wrote", grid.GetTextWithFieldCodes());

            Assert.Throws<ArgumentException>(() => ByHandle<DxfMText>(db, "73").SetField(second));
            Assert.Throws<ArgumentNullException>(() => grid.SetField(null));
            Assert.Throws<ArgumentNullException>(() => grid.SetField(null, new DxfField("%<\\AcVar Login>%")));
            Assert.Throws<ArgumentException>(() => grid.SetField(" ", new DxfField("%<\\AcVar Login>%")));
            Assert.Throws<ArgumentException>(() => grid.SetField("A\tB", new DxfField("%<\\AcVar Login>%")));
            Assert.Throws<ArgumentNullException>(() => grid.GetField(null));
            Assert.Equal(saved, db.ToText());
        }

        [Fact]
        public void AFieldTakenOut_GoesWithItsChildren_AndACAD_FIELDGoesWhenItIsEmpty()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDBText grid = ByHandle<DxfDBText>(db, "64");
            DxfField words = grid.SetField(new DxfField(DateCode, true));
            DxfField other = grid.SetField("GH_HEIGHT", new DxfField("%<\\AcVar Login>%"));
            DxfField child = words.GetChildren()[0];

            Assert.Same(other, grid.GetField("gh_height"));
            Assert.Null(grid.RemoveField("GH_NONE"));
            Assert.Same(words, grid.RemoveField());
            Assert.True(words.IsErased);
            Assert.True(child.IsErased);
            Assert.Null(grid.GetField());
            Assert.Null(grid.RemoveField());
            Assert.NotNull(grid.ExtensionDictionary.GetAt("ACAD_FIELD"));

            Assert.Throws<ArgumentException>(() => grid.RemoveField(words));
            Assert.Throws<ArgumentException>(() => ByHandle<DxfMText>(db, "73").RemoveField(other));
            Assert.Throws<ArgumentNullException>(() => grid.RemoveField((DxfField)null));
            grid.RemoveField(other);
            Assert.True(other.IsErased);
            Assert.False(grid.ExtensionDictionary.Contains("ACAD_FIELD"));

            // The extension dictionary stays, empty, as AutoCAD leaves it; released, the text is as it was read.
            Assert.Equal(0, grid.ExtensionDictionary.Count);
            grid.ReleaseExtensionDictionary();
            OnlyTheseChanged(text, db.ToText(), (string)null);
            Assert.DoesNotContain(Records(db.ToText()), r => r.StartsWith("  0\nFIELD\n", StringComparison.Ordinal));
        }

        [Fact]
        public void ATextsFieldsMadeWords_KeepTheWordsTheTextShows()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfDBText grid = ByHandle<DxfDBText>(db, "64");
            DxfMText mtext = ByHandle<DxfMText>(db, "73");
            Assert.Equal("Grid %%c20 A", grid.GetTextWithFieldCodes());
            Assert.Equal("{\\fArial|b1;Bold}\\Pline two é", mtext.GetMTextWithFieldCodes());

            grid.SetField(new DxfField("Grid %<\\AcVar Login>% A", true));
            mtext.SetField(new DxfField("{\\fArial|b1;Bold}\\P%<\\AcVar Filename>%", true));
            Assert.Equal("Grid %<\\AcVar Login>% A", grid.GetTextWithFieldCodes());
            Assert.Equal("{\\fArial|b1;Bold}\\P%<\\AcVar Filename>%", mtext.GetMTextWithFieldCodes());

            grid.ConvertFieldToText();
            mtext.ConvertFieldToText();
            mtext.ConvertFieldToText();
            Assert.Null(grid.GetField());
            Assert.Null(mtext.GetField());
            Assert.Equal("Grid %%c20 A", grid.TextString);
            Assert.Equal("{\\fArial|b1;Bold}\\Pline two é", mtext.Contents);
            Assert.Equal("Grid %%c20 A", grid.GetTextWithFieldCodes());
            Assert.Equal(0, mtext.ExtensionDictionary.Count);
        }

        [Fact]
        public void AFieldAsAutoCADKeepsIt_GivesItsValueAndItsCodesAtEveryDepth()
        {
            DxfDatabase made = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfField top = ByHandle<DxfMText>(made, "73").SetField(new DxfField("%<\\AcVar Date>% and %<\\AcExpr (%<\\AcVar Login>%)>%", true));
            DxfField date = top.GetChildren()[0];
            string saved = made.ToText();

            // As AutoCAD writes them: the code carried on in a 3, a placeholder of no child, a value kept, a child of its own.
            string topRecord = Records(saved).Single(r => HandleOf(r) == top.Handle);
            string dateRecord = Records(saved).Single(r => HandleOf(r) == date.Handle);
            string expression = Records(saved).Single(r => HandleOf(r) == top.GetChildren()[1].Handle);
            string edited = saved
                .Replace(topRecord, topRecord.Replace("  2\n%<\\_FldIdx 0>% and %<\\_FldIdx 1>%\n", "  2\n%<\\_FldIdx 0>% a\n  3\nnd %<\\_FldIdx 1>% %<\\_FldIdx 7>%\n"))
                .Replace(dateRecord, dateRecord.Replace(" 93\n1\n 90\n0\n 94\n0\n300\n\n302\n\n", " 93\n0\n 90\n4\n  1\n27/10/2026\n 94\n0\n300\n\n302\n27/10/2026\n"))
                .Replace(expression, expression.Replace("  2\n\\AcExpr (%<\\AcVar Login>%)\n 90\n0\n", "  2\n\\AcExpr (%<\\_FldIdx 0>%)\n 90\n1\n360\n" + date.Handle + "\n"));

            DxfDatabase db = DxfDatabase.Parse(edited);
            DxfMText mtext = ByHandle<DxfMText>(db, "73");
            DxfField read = mtext.GetField();
            Assert.Equal("%<\\_FldIdx 0>% and %<\\_FldIdx 1>% %<\\_FldIdx 7>%", read.GetFieldCode());
            Assert.Equal("27/10/2026", read.GetChildren()[0].GetStringValue());
            Assert.Equal("%<\\AcVar Date>% and %<\\AcExpr (%<\\AcVar Date>%)>% %<\\_FldIdx 7>%", mtext.GetMTextWithFieldCodes());
            Assert.Equal(edited, db.ToText());

            // The value AutoCAD wrote reads without its 302 from its text alone.
            string bare = edited.Replace("302\n27/10/2026\n", "302\n\n");
            Assert.Equal(string.Empty, ByHandle<DxfMText>(DxfDatabase.Parse(bare), "73").GetField().GetChildren()[0].GetStringValue().Trim());
            Assert.Equal("27/10/2026", ByHandle<DxfMText>(DxfDatabase.Parse(edited.Replace("302\n27/10/2026\n", string.Empty)), "73").GetField().GetChildren()[0].GetStringValue());
        }

        [Fact]
        public void TheFieldOfADimensionsText_IsCopiedFromAndToAnMTextOfItsDrawing()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfDimension dimension = ByHandle<DxfDimension>(db, "75");
            DxfMText mtext = ByHandle<DxfMText>(db, "73");
            DxfMText inBlock = ByHandle<DxfMText>(db, "85");
            DxfField source = mtext.SetField(new DxfField("%<\\AcObjProp Object(%<\\_ObjId 2130>%).Length>% long", true));

            dimension.FieldFromMText(mtext);
            DxfField copy = dimension.GetField();
            Assert.NotEqual(source.Handle, copy.Handle);
            Assert.NotEqual(source.GetChildren()[0].Handle, copy.GetChildren()[0].Handle);
            Assert.Equal(copy.Handle, copy.GetChildren()[0].OwnerHandle);
            Assert.Equal(mtext.GetMTextWithFieldCodes(), WithCodes(copy));
            Assert.Equal("<>", dimension.DimensionText);
            string copied = Records(db.ToText()).Single(r => HandleOf(r) == copy.GetChildren()[0].Handle);
            string original = Records(db.ToText()).Single(r => HandleOf(r) == source.GetChildren()[0].Handle);
            Assert.Equal(original.Substring(original.IndexOf("100\nAcDbField", StringComparison.Ordinal)), copied.Substring(copied.IndexOf("100\nAcDbField", StringComparison.Ordinal)));

            dimension.FieldToMText(inBlock);
            Assert.Equal("%<\\AcObjProp Object(%<\\_ObjId 2130>%).Length>% long", inBlock.GetMTextWithFieldCodes());
            Assert.Equal("<>", dimension.DimensionText);

            // An MTEXT with no field takes the dimension's out, as the two are to agree.
            mtext.RemoveField();
            dimension.FieldFromMText(mtext);
            Assert.Null(dimension.GetField());
            Assert.True(copy.IsErased);
            dimension.FieldToMText(inBlock);
            Assert.Null(inBlock.GetField());

            inBlock.SetField(new DxfField("%<\\AcVar Login>%"));
            dimension.FieldFromMText(inBlock);
            Assert.Equal("%<\\AcVar Login>%", WithCodes(dimension.GetField()));
            dimension.RemoveTextField();
            dimension.RemoveTextField();
            Assert.Null(dimension.GetField());

            Assert.Throws<ArgumentNullException>(() => dimension.FieldFromMText(null));
            Assert.Throws<ArgumentException>(() => dimension.FieldFromMText(new DxfMText()));
            Assert.Throws<ArgumentException>(() => dimension.FieldToMText(ByHandle<DxfMText>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018), "73")));
        }

        [Fact]
        public void AFieldSomeOfWhichBelongsToAnObjectAlready_IsRefusedBeforeAnythingIsWritten()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfDBText grid = ByHandle<DxfDBText>(db, "64");
            DxfMText mtext = ByHandle<DxfMText>(db, "73");
            var parent = new DxfField("a %<\\AcVar Date>%", true);
            DxfField child = parent.GetChildren()[0];

            grid.SetField(child);
            string before = db.ToText();
            Assert.Throws<ArgumentException>(() => mtext.SetField(parent));
            Assert.Equal(before, db.ToText());
            Assert.Null(mtext.GetField());
            Assert.Same(child, grid.GetField());

            // The other way about: the parent given first, its child is refused alone.
            var second = new DxfField("b %<\\AcVar Login>%", true);
            mtext.SetField(second);
            Assert.Throws<ArgumentException>(() => grid.SetField("GH_OTHER", second.GetChildren()[0]));
            grid.RemoveField();
            Assert.Equal("b %<\\AcVar Login>%", mtext.GetMTextWithFieldCodes());
        }

        [Fact]
        public void ACodeOfMoreThan250Characters_IsWrittenIn2Then3s_AndReadBackWhole()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            string words = string.Concat(Enumerable.Repeat("0123456789", 60)) + " %<\\AcVar Login>%";
            DxfField field = ByHandle<DxfMText>(db, "73").SetField(new DxfField(words, true));
            string record = Records(db.ToText()).Single(r => HandleOf(r) == field.Handle);
            string code = field.GetFieldCode();

            Assert.Equal(615, code.Length);
            Assert.Contains("  2\n" + code.Substring(0, 250) + "\n  3\n" + code.Substring(250, 250) + "\n  3\n" + code.Substring(500) + "\n 90\n1\n", record, StringComparison.Ordinal);
            DxfMText read = ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73");
            Assert.Equal(code, read.GetField().GetFieldCode());
            Assert.Equal(words, read.GetMTextWithFieldCodes());
        }

        [Fact]
        public void FieldsThatNameEachOtherAsChildren_EndAndKeepTheirPlaceholders()
        {
            DxfDatabase made = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfField top = ByHandle<DxfMText>(made, "73").SetField(new DxfField("x %<\\AcExpr (1)>% y", true));
            DxfField expression = top.GetChildren()[0];
            string saved = made.ToText();
            string record = Records(saved).Single(r => HandleOf(r) == expression.Handle);

            // The expression names the text field as its child, and the text field it: a ring.
            string ring = saved.Replace(record, record.Replace("  2\n\\AcExpr (1)\n 90\n0\n", "  2\n\\AcExpr (%<\\_FldIdx 0>%)\n 90\n1\n360\n" + top.Handle + "\n"));
            DxfMText mtext = ByHandle<DxfMText>(DxfDatabase.Parse(ring), "73");

            Assert.Equal("x %<\\AcExpr (%<\\_FldIdx 0>%)>% y", mtext.GetMTextWithFieldCodes());
        }

        [Fact]
        public void AChildTheFileHasNot_KeepsItsPlace_AndACopyKeepsThePlaceholdersTrue()
        {
            DxfDatabase made = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfField top = ByHandle<DxfMText>(made, "73").SetField(new DxfField("%<\\AcVar Date>% and %<\\AcVar Login>%", true));
            DxfField date = top.GetChildren()[0];
            string saved = made.ToText();
            string record = Records(saved).Single(r => HandleOf(r) == date.Handle);

            // The first child named by a handle the file has not; then the first owned by the dictionary, not by its parent.
            string dangling = saved.Replace(record, string.Empty);
            DxfDatabase db = DxfDatabase.Parse(dangling);
            DxfMText mtext = ByHandle<DxfMText>(db, "73");
            DxfField[] children = mtext.GetField().GetChildren();
            Assert.Equal(2, children.Length);
            Assert.Null(children[0]);
            Assert.Equal("AcVar", children[1].EvaluatorId);
            Assert.Equal("%<\\_FldIdx 0>% and %<\\AcVar Login>%", mtext.GetMTextWithFieldCodes());

            DxfDimension dimension = ByHandle<DxfDimension>(db, "75");
            dimension.FieldFromMText(mtext);
            DxfField copy = dimension.GetField();
            Assert.Equal(new[] { "AcVar" }, copy.GetChildren().Select(c => c.EvaluatorId));
            Assert.Equal("%<\\_FldIdx 1>% and %<\\_FldIdx 0>%", copy.GetFieldCode());
            Assert.Contains(" 90\n1\n360\n" + copy.GetChildren()[0].Handle + "\n 97\n", Records(db.ToText()).Single(r => HandleOf(r) == copy.Handle), StringComparison.Ordinal);

            string fields = Records(saved).Single(r => r.Contains("\n  3\nTEXT\n360\n" + top.Handle + "\n"));
            string owned = saved.Replace(record, record.Replace("330\n" + top.Handle + "\n100\nAcDbField", "330\n" + HandleOf(fields) + "\n100\nAcDbField"));
            DxfDatabase other = DxfDatabase.Parse(owned);
            DxfMText text = ByHandle<DxfMText>(other, "73");
            ByHandle<DxfDimension>(other, "75").FieldFromMText(text);
            DxfField copied = ByHandle<DxfDimension>(other, "75").GetField();
            Assert.Equal(2, copied.GetChildren().Length);
            Assert.Equal("%<\\_FldIdx 0>% and %<\\_FldIdx 1>%", copied.GetFieldCode());
            Assert.Equal(text.GetMTextWithFieldCodes(), WithCodes(copied));
        }

        [Fact]
        public void AFieldNamedTwice_IsCopiedOnce_SoThatAChainOfThemCopiesInTimeLinearInItsLength()
        {
            DxfDatabase made = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfField top = ByHandle<DxfMText>(made, "73").SetField(new DxfField("%<\\AcVar Login>%", true));
            DxfField first = top.GetChildren()[0];
            string saved = made.ToText();
            string record = Records(saved).Single(r => HandleOf(r) == first.Handle);

            // The child made the first of a chain of 12 fields, each naming the next twice.
            const int depth = 12;
            var chain = new System.Text.StringBuilder();

            for (int k = 0; k < depth; k++)
            {
                string handle = k == 0 ? first.Handle : "FF0" + k.ToString("X2", System.Globalization.CultureInfo.InvariantCulture);
                string owner = k == 0 ? top.Handle : "FF0" + (k - 1).ToString("X2", System.Globalization.CultureInfo.InvariantCulture);
                string next = "FF0" + (k + 1).ToString("X2", System.Globalization.CultureInfo.InvariantCulture);
                bool last = k == depth - 1;
                chain.Append("  0\nFIELD\n  5\n").Append(handle).Append("\n330\n").Append(owner).Append("\n100\nAcDbField\n  1\nAcExpr\n  2\n")
                    .Append(last ? "\\AcExpr (1)" : "\\AcExpr (%<\\_FldIdx 0>% + %<\\_FldIdx 1>%)").Append("\n 90\n").Append(last ? "0" : "2").Append('\n')
                    .Append(last ? string.Empty : "360\n" + next + "\n360\n" + next + "\n").Append(" 97\n0\n");
            }

            DxfDatabase db = DxfDatabase.Parse(saved.Replace(record, chain.ToString()));
            DxfMText mtext = ByHandle<DxfMText>(db, "73");
            int before = Records(db.ToText()).Count(r => r.StartsWith("  0\nFIELD\n", StringComparison.Ordinal));
            ByHandle<DxfDimension>(db, "75").FieldFromMText(mtext);
            int after = Records(db.ToText()).Count(r => r.StartsWith("  0\nFIELD\n", StringComparison.Ordinal));

            Assert.Equal(depth + 1, before);
            Assert.Equal(depth + 1, after - before);
            DxfField copy = ByHandle<DxfDimension>(db, "75").GetField().GetChildren()[0];
            Assert.Equal("\\AcExpr (%<\\_FldIdx 0>% + %<\\_FldIdx 2>%)", copy.GetFieldCode());
            Assert.Single(copy.GetChildren());
            Assert.Contains(" 90\n1\n360\n" + copy.GetChildren()[0].Handle + "\n 97\n", Records(db.ToText()).Single(r => HandleOf(r) == copy.Handle), StringComparison.Ordinal);
        }

        [Fact]
        public void TakingAFieldOutOfAnErasedObject_IsRefused()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfDBText grid = ByHandle<DxfDBText>(db, "64");
            DxfMText mtext = ByHandle<DxfMText>(db, "73");
            DxfDimension dimension = ByHandle<DxfDimension>(db, "75");
            grid.Erase();
            mtext.Erase();
            dimension.Erase();

            Assert.Throws<InvalidOperationException>(() => grid.RemoveField());
            Assert.Throws<InvalidOperationException>(() => grid.ConvertFieldToText());
            Assert.Throws<InvalidOperationException>(() => mtext.ConvertFieldToText());
            Assert.Throws<InvalidOperationException>(() => dimension.RemoveTextField());
        }

        /// <summary>The words with field codes a dimension's field stands for, as an MTEXT of it would give them.</summary>
        private static string WithCodes(DxfField field)
        {
            string code = field.GetFieldCode();
            DxfField[] children = field.GetChildren();

            for (int i = 0; i < children.Length; i++)
            {
                code = code.Replace("%<\\_FldIdx " + i + ">%", "%<" + children[i].GetFieldCode() + ">%");
            }

            return field.IsTextField ? code : "%<" + code + ">%";
        }
    }
}
