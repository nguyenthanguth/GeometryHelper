using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// Entities copied from one drawing into another: written in the forms of R12 in a drawing of R12, their pointers taken to
    /// the objects of the same name, a copy of any class by <see cref="DxfEntity.Clone"/>; with the setters that are given
    /// their own values, <see cref="DxfEntity.Explode"/> on the base, and the extents left alone by the edits of a block.
    /// </summary>
    public class DxfCopyAcrossTests
    {
        // The members a copy into R12 has not, or has of its new drawing: its handle, owner and look of R2000 on.
        private static readonly HashSet<string> NotCompared = new HashSet<string>(StringComparer.Ordinal)
        {
            "Handle", "OwnerHandle", "Owner", "BlockName", "ExtensionDictionary", "IsErased", "XData", "LineWeight", "TrueColor",
            "Color", "LinetypeScale", "Visible", "Transparency", "BlockTableRecord", "GeometricExtents", "IsMTextAttribute",
            "MTextAttribute", "DynamicBlockTableRecord", "AnonymousBlockTableRecord",
        };

        private static string G(params object[] pairs)
        {
            var text = new StringBuilder();

            for (int i = 0; i < pairs.Length; i += 2)
            {
                text.Append(Convert.ToString(pairs[i], CultureInfo.InvariantCulture).PadLeft(3)).Append('\n');
                text.Append(Convert.ToString(pairs[i + 1], CultureInfo.InvariantCulture)).Append('\n');
            }

            return text.ToString();
        }

        #region Into a drawing of R12

        [Fact]
        public void EveryEntityOfALaterDrawing_IsWrittenInTheFormsOfR12_AndReadsBackTheSame()
        {
            string[] laterOnly = { "DxfEllipse", "DxfMText", "DxfPolyline", "DxfRay", "DxfSpline", "DxfUnknownEntity", "DxfXline" };

            foreach (string later in new[] { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase source = DxfDatabase.Parse(later);
                DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
                var pairs = new List<KeyValuePair<DxfEntity, DxfEntity>>();
                var classes = new SortedSet<string>(StringComparer.Ordinal);

                foreach (DxfEntity entity in source.ModelSpace.ToList())
                {
                    DxfEntity copy = entity.Clone();

                    if (laterOnly.Contains(entity.GetType().Name))
                    {
                        Assert.Throws<ArgumentException>(() => r12.ModelSpace.AppendEntity(copy));
                        continue;
                    }

                    r12.ModelSpace.AppendEntity(copy);
                    pairs.Add(new KeyValuePair<DxfEntity, DxfEntity>(entity, copy));
                    classes.Add(entity.GetType().Name);
                }

                Assert.Superset(new SortedSet<string> { "DxfArc", "DxfBlockReference", "DxfCircle", "DxfDBPoint", "DxfDBText", "DxfFace", "DxfLine", "DxfPolyline2d", "DxfSolid" }, classes);

                string saved = r12.ToText();
                Assert.DoesNotContain("\n100\n", saved.Substring(saved.IndexOf("ENTITIES", StringComparison.Ordinal)), StringComparison.Ordinal);
                DxfDatabase reopened = DxfDatabase.Parse(saved);

                foreach (KeyValuePair<DxfEntity, DxfEntity> pair in pairs)
                {
                    DxfEntity back = reopened.ModelSpace.Single(e => e.Handle == pair.Value.Handle);
                    Assert.Equal(pair.Key.GetType(), back.GetType());
                    Assert.Equal(new string[0], Differences(pair.Key, back));
                }
            }
        }

        [Fact]
        public void ACentredAttribOfR2018_StaysCentredInR12_AndAMultiLineOneLosesItsEmbeddedMText()
        {
            // The ATTRIB EXTRA centred (72 of 2 in AcDbText), whose part AcDbAttribute writes 71 and 72 again for its own
            // meanings; the ATTRIB MT a multi-line one, its MTEXT embedded after a 101.
            string text = G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1032", 9, "$HANDSEED", 5, "100", 0, "ENDSEC",
                0, "SECTION", 2, "ENTITIES",
                0, "INSERT", 5, "50", 100, "AcDbEntity", 8, "0", 100, "AcDbBlockReference", 66, 1, 2, "BOLT", 10, "100.0", 20, "0.0", 30, "0.0",
                0, "ATTRIB", 5, "51", 330, "50", 100, "AcDbEntity", 8, "0", 100, "AcDbText", 10, "101.0", 20, "1.0", 30, "0.0", 40, "2.0", 1, "xI1", 50, "5.0", 72, 2, 11, "104.0", 21, "1.0", 31, "0.0",
                100, "AcDbAttribute", 2, "EXTRA", 70, 0, 71, 1, 72, 0, 11, "104.0", 21, "1.0", 31, "0.0",
                0, "ATTRIB", 5, "52", 330, "50", 100, "AcDbEntity", 8, "0", 100, "AcDbText", 10, "101.0", 20, "6.0", 30, "0.0", 40, "1.0", 1, "first", 100, "AcDbAttribute", 280, 0, 2, "MT", 70, 0, 74, 3, 280, 0, 71, 2, 72, 0, 11, "101.0", 21, "6.0", 31, "0.0",
                101, "Embedded Object", 10, "101.0", 20, "6.0", 30, "0.0", 40, "1.0", 41, "0.0", 46, "0.0", 71, 1, 72, 5, 1, "first\\Psecond", 7, "Standard", 73, 1, 44, "1.0",
                0, "SEQEND", 5, "53", 330, "50", 100, "AcDbEntity", 8, "0",
                0, "ENDSEC", 0, "EOF");

            DxfDatabase source = DxfDatabase.Parse(text);
            DxfBlockReference insert = source.ModelSpace.OfType<DxfBlockReference>().Single();
            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            var copy = (DxfBlockReference)insert.Clone();
            r12.ModelSpace.AppendEntity(copy);

            string saved = r12.ToText();
            Assert.DoesNotContain("Embedded Object", saved, StringComparison.Ordinal);
            DxfBlockReference back = DxfDatabase.Parse(saved).ModelSpace.OfType<DxfBlockReference>().Single(b => b.Handle == copy.Handle);
            DxfAttributeReference extra = back.AttributeCollection.Single(a => a.Tag == "EXTRA");
            DxfAttributeReference mt = back.AttributeCollection.Single(a => a.Tag == "MT");

            Assert.Equal(insert.AttributeCollection[0].HorizontalMode, extra.HorizontalMode);
            Assert.False(extra.IsMirroredInX);
            Assert.False(mt.IsMirroredInX);
            Assert.Equal("first", mt.TextString);
            Assert.False(mt.IsMTextAttribute);
            Assert.Equal(Bits(insert.AttributeCollection[0].AlignmentPoint), Bits(extra.AlignmentPoint));
        }

        /// <summary>The public members of two entities, and of the records that follow them, that read otherwise.</summary>
        private static List<string> Differences(DxfEntity a, DxfEntity b)
        {
            var differences = new List<string>();

            foreach (PropertyInfo property in a.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (NotCompared.Contains(property.Name) || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                string x = Shown(property, a);
                string y = Shown(property, b);

                if (x != y)
                {
                    differences.Add(a.GetType().Name + "." + property.Name + " " + x + " -> " + y);
                }
            }

            if (!(a is DxfBlockReference))
            {
                Assert.Equal(string.Join(" ; ", a.GetGeometry()), string.Join(" ; ", b.GetGeometry()));
            }

            List<DxfEntity> fa = Following(a);
            List<DxfEntity> fb = Following(b);
            Assert.Equal(fa.Count, fb.Count);

            for (int i = 0; i < fa.Count; i++)
            {
                differences.AddRange(Differences(fa[i], fb[i]));
            }

            return differences;
        }

        private static List<DxfEntity> Following(DxfEntity e)
        {
            if (e is DxfBlockReference insert)
            {
                return insert.AttributeCollection.Cast<DxfEntity>().ToList();
            }

            return e is IEnumerable records ? records.OfType<DxfEntity>().ToList() : new List<DxfEntity>();
        }

        private static string Shown(PropertyInfo property, object o)
        {
            object value;

            try
            {
                value = property.GetValue(o);
            }
            catch (TargetInvocationException e)
            {
                return "throws " + e.InnerException.GetType().Name;
            }

            switch (value)
            {
                case null: return "null";
                case double d: return Bits(d);
                case GeoPoint3 p: return Bits(p);
                case GeoVector3 v: return Bits(v);
                case DxfEntity entity: return entity.GetType().Name;
                case string s: return s;
                case IEnumerable items: return "[" + string.Join(",", items.Cast<object>().Select(x => x is double dd ? Bits(dd) : x is DxfEntity de ? de.GetType().Name : Convert.ToString(x, CultureInfo.InvariantCulture))) + "]";
                default: return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        #endregion

        #region Pointers between drawings

        // The drawing a LINE is copied from: its material Steel (2F), plot style Normal (30), visual style Realistic (31), the
        // text style Mono (40), and the material again in its extended data (1005).
        private static string Source() => G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1032", 9, "$HANDSEED", 5, "100", 0, "ENDSEC",
            0, "SECTION", 2, "TABLES",
            0, "TABLE", 2, "STYLE", 5, "3", 100, "AcDbSymbolTable", 70, 1,
            0, "STYLE", 5, "40", 330, "3", 100, "AcDbSymbolTableRecord", 100, "AcDbTextStyleTableRecord", 2, "Mono", 70, 0, 40, "0.0", 41, "1.0", 50, "0.0", 71, 0, 42, "2.5", 3, "mono.shx", 4, string.Empty,
            0, "ENDTAB",
            0, "TABLE", 2, "APPID", 5, "9", 100, "AcDbSymbolTable", 70, 1,
            0, "APPID", 5, "12", 330, "9", 100, "AcDbSymbolTableRecord", 100, "AcDbRegAppTableRecord", 2, "GHAPP", 70, 0,
            0, "ENDTAB", 0, "ENDSEC",
            0, "SECTION", 2, "ENTITIES",
            0, "LINE", 5, "50", 100, "AcDbEntity", 8, "0", 347, "2F", 390, "30", 348, "31", 340, "40", 100, "AcDbLine", 10, "0.0", 20, "0.0", 30, "0.0", 11, "10.0", 21, "0.0", 31, "0.0",
            1001, "GHAPP", 1000, "kept", 1005, "2F",
            0, "ENDSEC",
            0, "SECTION", 2, "OBJECTS",
            0, "DICTIONARY", 5, "C", 330, "0", 100, "AcDbDictionary", 281, 1, 3, "ACAD_MATERIAL", 350, "D", 3, "ACAD_PLOTSTYLENAME", 350, "E", 3, "ACAD_VISUALSTYLE", 350, "F",
            0, "DICTIONARY", 5, "D", 330, "C", 100, "AcDbDictionary", 281, 1, 3, "Steel", 350, "2F",
            0, "DICTIONARY", 5, "E", 330, "C", 100, "AcDbDictionary", 281, 1, 3, "Normal", 350, "30",
            0, "DICTIONARY", 5, "F", 330, "C", 100, "AcDbDictionary", 281, 1, 3, "Realistic", 350, "31",
            0, "MATERIAL", 5, "2F", 330, "D", 100, "AcDbMaterial", 1, "Steel",
            0, "ACDBPLACEHOLDER", 5, "30", 330, "E",
            0, "VISUALSTYLE", 5, "31", 330, "F", 100, "AcDbVisualStyle", 2, "Realistic", 70, 0,
            0, "ENDSEC", 0, "EOF");

        // The drawing it is copied into: the material Steel at 7A, the text style MONO at 7B, a plot style of another name, no
        // visual style; and a POINT at 2F, where the source's material is.
        private static string Target() => G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1032", 9, "$HANDSEED", 5, "200", 0, "ENDSEC",
            0, "SECTION", 2, "TABLES",
            0, "TABLE", 2, "STYLE", 5, "3", 100, "AcDbSymbolTable", 70, 1,
            0, "STYLE", 5, "7B", 330, "3", 100, "AcDbSymbolTableRecord", 100, "AcDbTextStyleTableRecord", 2, "MONO", 70, 0, 40, "0.0", 41, "1.0", 50, "0.0", 71, 0, 42, "2.5", 3, "mono.shx", 4, string.Empty,
            0, "ENDTAB",
            0, "TABLE", 2, "APPID", 5, "9", 100, "AcDbSymbolTable", 70, 1,
            0, "APPID", 5, "12", 330, "9", 100, "AcDbSymbolTableRecord", 100, "AcDbRegAppTableRecord", 2, "GHAPP", 70, 0,
            0, "ENDTAB", 0, "ENDSEC",
            0, "SECTION", 2, "ENTITIES",
            0, "POINT", 5, "2F", 100, "AcDbEntity", 8, "0", 100, "AcDbPoint", 10, "0.0", 20, "0.0", 30, "0.0",
            0, "ENDSEC",
            0, "SECTION", 2, "OBJECTS",
            0, "DICTIONARY", 5, "A", 330, "0", 100, "AcDbDictionary", 281, 1, 3, "ACAD_MATERIAL", 350, "B", 3, "ACAD_PLOTSTYLENAME", 350, "E2",
            0, "DICTIONARY", 5, "B", 330, "A", 100, "AcDbDictionary", 281, 1, 3, "Steel", 350, "7A",
            0, "DICTIONARY", 5, "E2", 330, "A", 100, "AcDbDictionary", 281, 1, 3, "Other", 350, "7C",
            0, "MATERIAL", 5, "7A", 330, "B", 100, "AcDbMaterial", 1, "Steel",
            0, "ACDBPLACEHOLDER", 5, "7C", 330, "E2",
            0, "ENDSEC", 0, "EOF");

        private static string Group(DxfDatabase db, string handle, int code)
        {
            string record = Records(db.ToText()).Single(r => HandleOf(r) == handle);
            string[] lines = record.Split('\n');

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() == code.ToString(CultureInfo.InvariantCulture))
                {
                    return lines[i + 1].Trim();
                }
            }

            return null;
        }

        [Fact]
        public void AMaterialPointedAt_IsTheTargetsOfTheSameName()
        {
            DxfDatabase target = DxfDatabase.Parse(Target());
            DxfEntity copy = DxfDatabase.Parse(Source()).ModelSpace.Single().Clone();
            target.ModelSpace.AppendEntity(copy);

            Assert.Equal("7A", Group(target, copy.Handle, 347));
        }

        [Fact]
        public void ARecordOfATablePointedAt_IsTheTargetsOfTheSameName_LettersOfEitherCaseTheSame()
        {
            DxfDatabase target = DxfDatabase.Parse(Target());
            DxfEntity copy = DxfDatabase.Parse(Source()).ModelSpace.Single().Clone();
            target.ModelSpace.AppendEntity(copy);

            Assert.Equal("7B", Group(target, copy.Handle, 340));
        }

        [Fact]
        public void APlotStyleTheTargetHasNoNamesakeOf_IsLeftOut()
        {
            DxfDatabase target = DxfDatabase.Parse(Target());
            DxfEntity copy = DxfDatabase.Parse(Source()).ModelSpace.Single().Clone();
            target.ModelSpace.AppendEntity(copy);

            Assert.Null(Group(target, copy.Handle, 390));
        }

        [Fact]
        public void AVisualStyleInADictionaryTheTargetHasNot_IsLeftOut()
        {
            DxfDatabase target = DxfDatabase.Parse(Target());
            DxfEntity copy = DxfDatabase.Parse(Source()).ModelSpace.Single().Clone();
            target.ModelSpace.AppendEntity(copy);

            Assert.Null(Group(target, copy.Handle, 348));
        }

        [Fact]
        public void AHandleOfExtendedData_IsLeftOut_TheRestOfTheDataKept()
        {
            DxfDatabase target = DxfDatabase.Parse(Target());
            DxfEntity copy = DxfDatabase.Parse(Source()).ModelSpace.Single().Clone();
            target.ModelSpace.AppendEntity(copy);

            Assert.Equal(new[] { "1001 GHAPP", "1000 kept" }, copy.XData.Select(g => g.ToString()));
            Assert.DoesNotContain("1005", Records(target.ToText()).Single(r => HandleOf(r) == copy.Handle), StringComparison.Ordinal);
        }

        [Fact]
        public void ACopyWithinItsOwnDrawing_KeepsEveryPointer()
        {
            DxfDatabase source = DxfDatabase.Parse(Source());
            DxfEntity copy = source.ModelSpace.Single().Clone();
            source.ModelSpace.AppendEntity(copy);

            Assert.Equal("2F", Group(source, copy.Handle, 347));
            Assert.Equal("30", Group(source, copy.Handle, 390));
            Assert.Equal("31", Group(source, copy.Handle, 348));
            Assert.Equal("40", Group(source, copy.Handle, 340));
            Assert.Equal(new[] { "1001 GHAPP", "1000 kept", "1005 2F" }, copy.XData.Select(g => g.ToString()));
        }

        [Fact]
        public void AnAssociativeHatchCopied_CountsTheBoundaryObjectsLeft_AndIsNoLongerAssociative()
        {
            // A HATCH of one path tied to the LWPOLYLINE 2F (97 of 1, then 330 2F), associative (71 of 1), as ezdxf's
            // hatch.associate writes it.
            string text = G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1032", 9, "$HANDSEED", 5, "100", 0, "ENDSEC",
                0, "SECTION", 2, "ENTITIES",
                0, "LWPOLYLINE", 5, "2F", 102, "{ACAD_REACTORS", 330, "30", 102, "}", 100, "AcDbEntity", 8, "0", 100, "AcDbPolyline", 90, 4, 70, 1,
                10, "0.0", 20, "0.0", 10, "10.0", 20, "0.0", 10, "10.0", 20, "10.0", 10, "0.0", 20, "10.0",
                0, "HATCH", 5, "30", 100, "AcDbEntity", 8, "0", 62, 3, 100, "AcDbHatch", 10, "0.0", 20, "0.0", 30, "0.0", 210, "0.0", 220, "0.0", 230, "1.0",
                2, "SOLID", 70, 1, 71, 1, 91, 1, 92, 3, 72, 0, 73, 1, 93, 4, 10, "0.0", 20, "0.0", 10, "10.0", 20, "0.0", 10, "10.0", 20, "10.0", 10, "0.0", 20, "10.0",
                97, 1, 330, "2F", 75, 0, 76, 1, 98, 0,
                0, "ENDSEC", 0, "EOF");

            foreach (bool sameDrawing in new[] { false, true })
            {
                DxfDatabase source = DxfDatabase.Parse(text);
                DxfDatabase target = sameDrawing ? source : DxfDatabase.Parse(Target());
                DxfEntity hatch = source.ModelSpace.Single(e => e.Handle == "30");
                DxfEntity copy = hatch.Clone();
                target.ModelSpace.AppendEntity(copy);

                Assert.Equal("0", Group(target, copy.Handle, 97));
                Assert.Equal("0", Group(target, copy.Handle, 71));
                Assert.Null(Group(target, copy.Handle, 330));
                Assert.Contains("\n 97\n0\n 75\n0\n", Records(target.ToText()).Single(r => HandleOf(r) == copy.Handle), StringComparison.Ordinal);

                // The original keeps its boundary object and its associativity.
                Assert.Equal("1", Group(source, "30", 97));
                Assert.Equal("1", Group(source, "30", 71));
            }
        }

        #endregion

        #region Clone and Explode

        [Fact]
        public void AnUnknownEntity_IsClonedAsItIs_AndCopiedByTheIdentity()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfUnknownEntity hatch = source.ModelSpace.OfType<DxfUnknownEntity>().First(u => u.DxfName == "HATCH");
            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);

            var clone = Assert.IsType<DxfUnknownEntity>(hatch.Clone());
            var copy = Assert.IsType<DxfUnknownEntity>(hatch.GetTransformedCopy(GeoTransform3.Identity));
            Assert.Throws<NotSupportedException>(() => hatch.GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(1, 0, 0))));
            target.ModelSpace.AppendEntity(clone);
            target.ModelSpace.AppendEntity(copy);

            DxfDatabase reopened = DxfDatabase.Parse(target.ToText());
            Assert.Equal("HATCH", Assert.IsType<DxfUnknownEntity>(reopened.ModelSpace.Single(e => e.Handle == clone.Handle)).DxfName);
            Assert.Equal(hatch.GetGeometry().Count, reopened.ModelSpace.Single(e => e.Handle == copy.Handle).GetGeometry().Count);
        }

        [Fact]
        public void ACopyOfAVertex_IsAVertexOfItsClass_AndIsNotAppendedToABlock()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline2d polyline = Everything(db).OfType<DxfPolyline2d>().First();
            DxfEntity vertex = polyline.Cast<DxfEntity>().First();
            DxfEntity copy = vertex.Clone();

            Assert.Equal(vertex.GetType(), copy.GetType());
            Assert.Equal(vertex.GetType(), vertex.GetTransformedCopy(GeoTransform3.Identity).GetType());
            Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(copy));
        }

        [Fact]
        public void Explode_IsRefusedByAnEntityThatDoesNotBreak_AsAutoCADAnswersNotApplicable()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfEntity line = db.ModelSpace.OfType<DxfLine>().First();
            DxfEntity insert = db.ModelSpace.OfType<DxfBlockReference>().First();

            Assert.Throws<NotSupportedException>(() => line.Explode());
            Assert.NotEmpty(insert.Explode());
        }

        #endregion

        #region Setters given their own values

        [Fact]
        public void SettersGivenTheValuesTheyRead_NeverThrow_WhateverTheFileHolds()
        {
            // A LINE of linetype scale -2, an ELLIPSE of ratio 1.5, a layer of lineweight -1, a linetype of pattern -1, and a
            // header of $LTSCALE 0, $TEXTSIZE 0, $LUNITS 0, $CELWEIGHT 7 and $PROXYGRAPHICS 2: all outside what a setter takes.
            string text = G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1032", 9, "$HANDSEED", 5, "100", 9, "$LTSCALE", 40, "0.0",
                9, "$TEXTSIZE", 40, "0.0", 9, "$LUNITS", 70, 0, 9, "$CELWEIGHT", 370, 7, 9, "$PROXYGRAPHICS", 70, 2, 0, "ENDSEC",
                0, "SECTION", 2, "TABLES",
                0, "TABLE", 2, "LTYPE", 5, "5", 100, "AcDbSymbolTable", 70, 1,
                0, "LTYPE", 5, "14", 330, "5", 100, "AcDbSymbolTableRecord", 100, "AcDbLinetypeTableRecord", 2, "ODD", 70, 0, 3, string.Empty, 72, 65, 73, 0, 40, "-1.0",
                0, "ENDTAB",
                0, "TABLE", 2, "LAYER", 5, "2", 100, "AcDbSymbolTable", 70, 1,
                0, "LAYER", 5, "10", 330, "2", 100, "AcDbSymbolTableRecord", 100, "AcDbLayerTableRecord", 2, "0", 70, 0, 62, 7, 6, "Continuous", 370, -1,
                0, "ENDTAB", 0, "ENDSEC",
                0, "SECTION", 2, "ENTITIES",
                0, "LINE", 5, "50", 100, "AcDbEntity", 8, "0", 48, "-2.0", 100, "AcDbLine", 10, "0.0", 20, "0.0", 30, "0.0", 11, "1.0", 21, "0.0", 31, "0.0",
                0, "ELLIPSE", 5, "51", 100, "AcDbEntity", 8, "0", 100, "AcDbEllipse", 10, "0.0", 20, "0.0", 30, "0.0", 11, "10.0", 21, "0.0", 31, "0.0", 40, "1.5", 41, "0.0", 42, "6.283185307179586",
                0, "ENDSEC", 0, "EOF");

            DxfDatabase db = DxfDatabase.Parse(text);
            string before = db.ToText();
            DxfLine line = db.ModelSpace.OfType<DxfLine>().Single();
            DxfEllipse ellipse = db.ModelSpace.OfType<DxfEllipse>().Single();
            DxfLayerTableRecord layer = db.LayerTable["0"];
            DxfLinetypeTableRecord odd = db.LinetypeTable["ODD"];

            line.LinetypeScale = line.LinetypeScale;
            line.XData = line.XData;
            ellipse.RadiusRatio = ellipse.RadiusRatio;
            layer.LineWeight = layer.LineWeight;
            layer.ColorIndex = layer.ColorIndex;
            layer.TrueColor = layer.TrueColor;
            odd.PatternLength = odd.PatternLength;
            odd.NumDashes = odd.NumDashes;
            db.Ltscale = db.Ltscale;
            db.Textsize = db.Textsize;
            db.Lunits = db.Lunits;
            db.Celweight = db.Celweight;
            db.Saveproxygraphics = db.Saveproxygraphics;

            Assert.Equal(before, db.ToText());
            Assert.Throws<ArgumentOutOfRangeException>(() => line.LinetypeScale = -3.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => ellipse.RadiusRatio = 1.25);
        }

        #endregion

        #region The extents and the edits of a block

        [Fact]
        public void TheExtents_AreLeftAlone_ByWhatIsWrittenInABlock()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                GeoPoint3 least = db.Extmin;
                GeoPoint3 most = db.Extmax;
                DxfBlockTableRecord bolt = db.BlockTable["BOLT"];

                bolt.AppendEntity(new DxfLine(new GeoPoint3(-5000, -5000, -5000), new GeoPoint3(5000, 5000, 5000)));
                DxfLine inBlock = bolt.OfType<DxfLine>().First();
                inBlock.EndPoint = new GeoPoint3(9000, 9000, 9000);
                inBlock.TransformBy(GeoTransform3.Translation(new GeoVector3(10000, 0, 0)));

                Assert.Equal(Bits(least), Bits(DxfDatabase.Parse(db.ToText()).Extmin));
                Assert.Equal(Bits(most), Bits(DxfDatabase.Parse(db.ToText()).Extmax));
            }
        }

        #endregion
    }
}
