using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What dxfver11 changed in the engine, held to give what it gave before: a table's cells set one by one are written into the
    /// bytes of the cell alone (the saved bytes of every scenario are the SHA-256 the library gave before, in .NET Framework and
    /// .NET 10), the names under which the named object dictionary holds an object are found for every handle at once, the name
    /// of the block a table is drawn by is read from the head of its record, and a body or region saved to R2013 is neither
    /// dated by the clock nor given a GUID at random.
    /// </summary>
    public class DxfSpeed11Tests
    {
        #region The bytes the library saved before

        private static readonly string[] CellEditsLines =
        {
            "AC1032 round 0 native 3521A0133F47540E8B46C54B",
            "AC1032 round 0 R2013 A87BEADF4A3411385043231A",
            "AC1032 round 0 R2000 7A22D936934A99CD5486ADE4",
            "AC1032 round 1 native E4ADE7F450A836F425238DD6",
            "AC1032 round 1 R2013 7F1128C5D38F5484F4EDA1B4",
            "AC1032 round 1 R2000 7A22D936934A99CD5486ADE4",
            "AC1032 round 2 native 1F4B9D0A7D24FC9919A778CF",
            "AC1032 round 2 R2013 E76CDF7B0B7409A595F0DBEC",
            "AC1032 round 2 R2000 7A22D936934A99CD5486ADE4",
            "AC1015 round 0 native 5A863E4C564A98E1BED6416B",
            "AC1015 round 0 R2013 47D34E52C2F356EB9250712F",
            "AC1015 round 0 R2000 5A863E4C564A98E1BED6416B",
            "AC1015 round 1 native 0B1CF72C53DCBAE72D0E50B0",
            "AC1015 round 1 R2013 56415FC1748210F08C13A406",
            "AC1015 round 1 R2000 0B1CF72C53DCBAE72D0E50B0",
            "AC1015 round 2 native 3871B58F9E59E3FE9586CFBC",
            "AC1015 round 2 R2013 A7B02F88C1A859F7A0FAD65F",
            "AC1015 round 2 R2000 3871B58F9E59E3FE9586CFBC",
            "AC1018 round 0 native F596C027C9FD49969E119AB3",
            "AC1018 round 0 R2013 FCF3DD2D44423B7945EC8EEB",
            "AC1018 round 0 R2000 5A863E4C564A98E1BED6416B",
            "AC1018 round 1 native 23E57DBAD5CBA1078309FAF8",
            "AC1018 round 1 R2013 F3AC3975ED0C3540A57E5A0B",
            "AC1018 round 1 R2000 0B1CF72C53DCBAE72D0E50B0",
            "AC1018 round 2 native 88B3ACAFD1BAF0FD88E461DC",
            "AC1018 round 2 R2013 965B1801DD63DE3A1AA091B4",
            "AC1018 round 2 R2000 3871B58F9E59E3FE9586CFBC",
            "AC1024 round 0 native 81F2E82F697871DAA683A103",
            "AC1024 round 0 R2013 A87BEADF4A3411385043231A",
            "AC1024 round 0 R2000 7A22D936934A99CD5486ADE4",
            "AC1024 round 1 native C38F2BB394FE595BEB46E0AB",
            "AC1024 round 1 R2013 7F1128C5D38F5484F4EDA1B4",
            "AC1024 round 1 R2000 7A22D936934A99CD5486ADE4",
            "AC1024 round 2 native B6C652B83FD2D845DC389229",
            "AC1024 round 2 R2013 E76CDF7B0B7409A595F0DBEC",
            "AC1024 round 2 R2000 7A22D936934A99CD5486ADE4",
        };

        private static readonly string[] CellEditsAfterReadingLines =
        {
            "crlf False padded True round 0 43D4C843C9F4FF7CA9C4DEDD",
            "crlf False padded True round 1 DC6105AE14DEE5A06DA90221",
            "crlf False padded True R2013 A2ABE9585AF7C044E7D339AA",
            "crlf False padded False round 0 2AA448E61A076C185D850F0E",
            "crlf False padded False round 1 D7CBA1111002CFC468BE8DD2",
            "crlf False padded False R2013 DAAFE3C9BBAF491509C93E28",
            "crlf True padded True round 0 188F85026E672C40FF9C2463",
            "crlf True padded True round 1 D899A4C88FED97F74D76D2C3",
            "crlf True padded True R2013 D29677F0489065D20AED206B",
            "crlf True padded False round 0 76365C582DD127B1C3F346ED",
            "crlf True padded False round 1 42EAC5FDFE40DC7DDB178976",
            "crlf True padded False R2013 9004AB7821F0F35D0E892EDD",
        };

        private static readonly string[] ManyTablesLines =
        {
            "tables 10 B98F6E94A2FFF229C768DC6C",
            "tables 20 66DF34EB182A144DD9F3AE82",
            "tables 30 197B84D44E0130C7FA08221D",
            "tables R2013 630C59BECD1B9407E565993C",
        };

        private static readonly string[] ClonesLines =
        {
            "copies 25 D9F90AA6448A0BE57CC7CF57",
            "twice 9F394C281389D4817E37C0D2 A5C3D86DD4641CF69A8407A8",
        };

        [Fact]
        public void CellsEditedRoundAfterRound_SaveToTheBytesTheyDidBefore_InEveryVersionTheyAreMadeIn()
        {
            Assert.Equal(CellEditsLines, DxfSpeed11Scenarios.CellEdits());
        }

        [Fact]
        public void ATableReadBackInEveryLineEndAndPadding_AndEditedCellByCell_SavesToTheBytesItDidBefore()
        {
            Assert.Equal(CellEditsAfterReadingLines, DxfSpeed11Scenarios.CellEditsAfterReading());
        }

        [Fact]
        public void TablesAddedOneAfterAnother_SaveToTheBytesTheyDidBefore()
        {
            Assert.Equal(ManyTablesLines, DxfSpeed11Scenarios.ManyTables());
        }

        [Fact]
        public void TablesCopiedToAnotherDrawing_SaveToTheBytesTheyDidBefore()
        {
            Assert.Equal(ClonesLines, DxfSpeed11Scenarios.Clones());
        }

        #endregion

        #region Cells set one by one

        private static DxfDatabase Metric()
        {
            var db = new DxfDatabase("AC1032");
            db.SetHeader("$MEASUREMENT", new DxfTypedValue(70, "1"));
            db.NamedObjectsDictionary.SetAt("ACAD_TABLESTYLE", new DxfDBDictionary());
            db.TableStyleDictionary.SetAt("Standard", new DxfTableStyle());
            return db;
        }

        [Fact]
        public void TheTextOfEveryCellOfATableOf1500Cells_SetOneByOne_TakesSecondsNotMinutes()
        {
            // Each cell cost a reading and a writing of every group of the record: 8 ms at 1 000 cells, 12 s for these 1 500.
            DxfDatabase db = Metric();
            var table = new DxfTable(new GeoPoint3(0, 0, 0), 150, 10, 9.0, 30.0);
            db.ModelSpace.AppendEntity(table);
            var clock = Stopwatch.StartNew();

            for (int r = 0; r < 150; r++)
            {
                for (int c = 0; c < 10; c++)
                {
                    table.SetTextString(r, c, "cell " + (r * 10 + c));
                }
            }

            clock.Stop();

            Assert.Equal("cell 1234", table.TextString(123, 4));
            Assert.Equal("cell 1499", table.TextString(149, 9));
            Assert.Equal("cell 0", table.TextString(0, 0));
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), "took " + clock.Elapsed);
        }

        [Fact]
        public void ACellSetThroughTheCellAlone_IsReadBackAsSetEverywhereElseUntouched()
        {
            DxfDatabase db = Metric();
            var table = new DxfTable(new GeoPoint3(0, 0, 0), 8, 4, 9.0, 30.0);
            db.ModelSpace.AppendEntity(table);

            // The first edits write the record whole, the later ones into the bytes of one cell.
            for (int k = 0; k < 6; k++)
            {
                table.SetTextString(2, 1, "first " + k);
            }

            table.SetAlignment(3, 2, DxfCellAlignment.BottomRight);
            table.SetTextHeight(3, 2, 4.5);
            table.SetValue(4, 0, 42);
            table.SetTextString(5, 3, "long " + new string('x', 700));

            Assert.Equal("first 5", table.TextString(2, 1));
            Assert.Equal(DxfCellAlignment.BottomRight, table.Alignment(3, 2));
            Assert.Equal(4.5, table.TextHeight(3, 2));
            Assert.Equal(42, table.Value(4, 0));
            Assert.Equal("long " + new string('x', 700), table.TextString(5, 3));
            Assert.Equal(string.Empty, table.TextString(7, 3));
            Assert.Equal(string.Empty, table.TextString(1, 1));
        }

        [Fact]
        public void ACellOutsideTheTable_IsRefusedAsBefore_AfterTheCellsWereSetThroughTheirBytes()
        {
            DxfDatabase db = Metric();
            var table = new DxfTable(new GeoPoint3(0, 0, 0), 4, 3, 9.0, 30.0);
            db.ModelSpace.AppendEntity(table);

            for (int k = 0; k < 4; k++)
            {
                table.SetTextString(1, 1, "set " + k);
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => table.SetTextString(4, 0, "x"));
            Assert.Throws<ArgumentOutOfRangeException>(() => table.SetTextString(0, 3, "x"));
            Assert.Throws<ArgumentOutOfRangeException>(() => table.SetAlignment(-1, 0, DxfCellAlignment.TopLeft));
            Assert.Equal("set 3", table.TextString(1, 1));
        }

        [Fact]
        public void ACellSetAfterTheTableWasEditedByAnotherSetter_StillGivesTheBytesOfTheRecordWrittenWhole()
        {
            // Two tables edited in the same order but for the structure edit in the middle, which rewrites the record whole.
            DxfDatabase a = Metric();
            DxfDatabase b = Metric();
            var one = new DxfTable(new GeoPoint3(0, 0, 0), 6, 4, 9.0, 30.0);
            var two = new DxfTable(new GeoPoint3(0, 0, 0), 6, 4, 9.0, 30.0);
            a.ModelSpace.AppendEntity(one);
            b.ModelSpace.AppendEntity(two);

            foreach (DxfTable table in new[] { one, two })
            {
                for (int k = 0; k < 4; k++)
                {
                    table.SetTextString(2, 2, "a" + k);
                    table.SetTextString(3, 0, "b" + k);
                }
            }

            one.SetRowHeight(2, 12.0);
            two.SetRowHeight(2, 12.0);

            foreach (DxfTable table in new[] { one, two })
            {
                for (int k = 0; k < 4; k++)
                {
                    table.SetTextHeight(4, 1, 2.0 + k);
                    table.SetTextString(5, 3, "c" + k);
                }
            }

            Assert.Equal(
                DxfSpeed11Scenarios.Hash(DxfSpeed11Scenarios.Saved(a)),
                DxfSpeed11Scenarios.Hash(DxfSpeed11Scenarios.Saved(b))
            );
        }

        [Fact]
        public void TheBlockANamesBlockTableIsKept_ByTheNamesTheFileHoldsOfInserts_AfterCellsAreSetThroughTheirBytes()
        {
            DxfDatabase db = Metric();
            var table = new DxfTable(new GeoPoint3(0, 0, 0), 5, 3, 9.0, 30.0);
            db.ModelSpace.AppendEntity(table);
            string block = table.BlockTableRecord.Name;
            db.File.KeepInsertNames();

            for (int k = 0; k < 5; k++)
            {
                table.SetTextString(1, 1, "set " + k);
                table.SetTextHeight(2, 0, 3.0 + k);
            }

            Assert.False(db.File.InsertsNone(block));
            Assert.True(db.File.InsertsNone("*T999"));
        }

        #endregion

        #region What saving a drawing of many tables lists, and the styles it reads

        [Fact]
        public void EveryTableOfADrawingAndItsStyle_AreListedForRecomposing_InTheOrderOfTheirHandles()
        {
            DxfDatabase db = Metric();
            var handles = new List<string>();

            for (int t = 0; t < 40; t++)
            {
                var table = new DxfTable(new GeoPoint3(t * 100, 0, 0), 3, 2, 9.0, 30.0);
                db.ModelSpace.AppendEntity(table);
                handles.Add(table.Handle);
            }

            handles.Add(db.TableStyleDictionary.GetAt("Standard").Handle);
            var saved = DxfDatabase.Parse(Encoding.UTF8.GetString(DxfSpeed11Scenarios.Saved(db)));
            var recompose = (DxfXrecord)saved.NamedObjectsDictionary.GetAt("ACDB_RECOMPOSE_DATA");
            string[] listed = recompose.Data.Where(g => g.TypeCode == 330).Select(g => g.Value.Trim()).ToArray();

            Assert.Equal(
                handles.OrderBy(h => Convert.ToInt64(h, 16)).ToArray(),
                listed.OrderBy(h => Convert.ToInt64(h, 16)).ToArray()
            );
            Assert.Equal(listed.OrderBy(h => Convert.ToInt64(h, 16)).ToArray(), listed);
        }

        [Fact]
        public void ATableStyleRead_ThenChanged_IsReadAsChanged()
        {
            DxfDatabase db = Metric();
            var style = (DxfTableStyle)db.TableStyleDictionary.GetAt("Standard");

            double before = style.TextHeight(DxfRowType.DataRow);
            style.SetTextHeight(3.25, DxfRowType.DataRow);
            double middle = style.TextHeight(DxfRowType.DataRow);
            style.SetTextHeight(5.5, DxfRowType.DataRow);

            Assert.NotEqual(3.25, before);
            Assert.Equal(3.25, middle);
            Assert.Equal(5.5, style.TextHeight(DxfRowType.DataRow));
            Assert.Equal(5.5, style.TextHeight(DxfRowType.DataRow));
        }

        [Fact]
        public void TablesAddedOneAfterAnother_EachGetABlockOfItsOwn_AndACopyInTheDrawingSharesItsOriginals()
        {
            DxfDatabase db = Metric();
            var tables = new List<DxfTable>();

            for (int t = 0; t < 6; t++)
            {
                var table = new DxfTable(new GeoPoint3(t * 100, 0, 0), 2, 2, 9.0, 30.0);
                db.ModelSpace.AppendEntity(table);
                table.SetTextString(1, 1, "t" + t);
                tables.Add(table);
            }

            DxfTable copy = (DxfTable)db.WblockCloneObjects(new DxfEntity[] { tables[2] }, db.ModelSpace).Single();
            string[] names = tables.Select(t => t.BlockTableRecord.Name).ToArray();

            Assert.Equal(6, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(names, name => Assert.StartsWith("*T", name));

            // A picture another entity shares, as a copy in its own drawing does, is left as drawn (the table's documentation).
            Assert.Equal(names[2], copy.BlockTableRecord.Name);
        }

        #endregion

        #region The names a drawing holds its objects under

        public static IEnumerable<object[]> Drawings() =>
            new[]
            {
                "images_AC1032",
                "blocks_AC1032",
                "acis_AC1032",
                "curves_AC1032",
                "hatch_AC1032",
                "leaders_AC1032",
                "matvis_AC1032",
                "meshes_AC1032",
            }.Select(name => new object[] { name });

        private static string Text(string name) => Encoding.UTF8.GetString(DwgAnswerKey.Key(name));

        [Theory]
        [MemberData(nameof(Drawings))]
        public void TheNamesOfEveryObject_FoundForAllAtOnce_AreThoseFoundOneAtATime(string name)
        {
            DxfFile file = DxfFile.Parse(Text(name));
            Dictionary<ulong, KeyValuePair<DxfFile.Rec, bool>> objects = file.ReadObjects();
            Dictionary<ulong, KeyValuePair<string, string>> paths = file.NamedPaths(objects);
            int held = 0;

            foreach (ulong handle in objects.Keys.Concat(new ulong[] { 0, 0xFFFFFF, ulong.MaxValue }))
            {
                bool one = file.NamedPath(handle, out string dictionary, out string entry);

                Assert.Equal(one, paths.TryGetValue(handle, out KeyValuePair<string, string> all));

                if (one)
                {
                    held++;
                    Assert.Equal(dictionary, all.Key);
                    Assert.Equal(entry, all.Value);
                }
            }

            Assert.Equal(paths.Count > 0, held > 0);
        }

        [Fact]
        public void TheNamesOfTheObjectsOfADrawingWithManyDictionaries_AreThoseFoundOneAtATime()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

            for (int d = 0; d < 20; d++)
            {
                var dictionary = new DxfDBDictionary();
                db.NamedObjectsDictionary.SetAt("OWN" + d, dictionary);

                for (int e = 0; e < 5; e++)
                {
                    dictionary.SetAt("e" + e, new DxfXrecord());
                }
            }

            DxfFile file = DxfFile.Parse(db.ToText());
            Dictionary<ulong, KeyValuePair<DxfFile.Rec, bool>> objects = file.ReadObjects();
            Dictionary<ulong, KeyValuePair<string, string>> paths = file.NamedPaths(objects);

            Assert.True(paths.Count >= 100, "paths " + paths.Count + " of " + objects.Count);

            foreach (ulong handle in objects.Keys)
            {
                bool one = file.NamedPath(handle, out string dictionary, out string entry);

                Assert.Equal(one, paths.TryGetValue(handle, out KeyValuePair<string, string> all));
                Assert.True(!one || (dictionary == all.Key && entry == all.Value));
            }
        }

        [Fact]
        public void TablesCopiedToAnotherDrawing_ReadTheDictionariesOfTheFirstOnce_NotOnceForEachPointer()
        {
            DxfDatabase source = Metric();

            for (int i = 0; i < 80; i++)
            {
                source.NamedObjectsDictionary.SetAt("OBJ" + i, new DxfXrecord());
            }

            var tables = new List<DxfEntity>();

            for (int t = 0; t < 40; t++)
            {
                var table = new DxfTable(new GeoPoint3(t * 100, 0, 0), 3, 2, 9.0, 30.0);
                source.ModelSpace.AppendEntity(table);
                tables.Add(table);
            }

            DxfDatabase target = Metric();
            int before = source.File.WholeReadings;
            source.WblockCloneObjects(tables, target.ModelSpace);

            // The root dictionary read for the paths of every object at once, where each of 40 tables' pointers read it.
            Assert.InRange(source.File.WholeReadings - before, 0, 2);
        }

        #endregion

        #region The name of the block a table is drawn by, from the head of its record

        [Theory]
        [MemberData(nameof(Drawings))]
        public void TheFirstTextOfACodeInTheFirstSubclass_ReadFromTheBytes_IsThatOfTheListOfGroups(string name)
        {
            DxfDatabase db = DxfDatabase.Parse(Text(name));
            int read = 0;

            foreach (DxfBlockTableRecord owner in db.BlockTable)
            {
                foreach (DxfEntity entity in owner)
                {
                    foreach (int code in new[] { 2, 10, 70, 8 })
                    {
                        bool known = db.File.TryBaseTextOf(entity.Item, code, out string text);
                        var groups = new DxfDimGroups(entity.Item);

                        if (!known)
                        {
                            continue;
                        }

                        read++;
                        Assert.Equal(groups.Text(code, groups.Base), text);
                    }
                }
            }

            Assert.True(read > 0);
        }

        [Fact]
        public void TheBlockATableIsDrawnBy_ReadFromItsHead_IsTheOneItIsGiven_AndTheSameAfterItsCellsAreSet()
        {
            DxfDatabase db = Metric();
            var table = new DxfTable(new GeoPoint3(0, 0, 0), 20, 6, 9.0, 30.0);
            db.ModelSpace.AppendEntity(table);

            for (int k = 0; k < 5; k++)
            {
                table.SetTextString(3, 2, "x" + k);
            }

            Assert.True(db.File.TryBaseTextOf(table.Item, 2, out string name));
            Assert.StartsWith("*T", name);
            Assert.Equal(table.BlockTableRecord.Name, name);
        }

        [Fact]
        public void TheGroupsOfACellBetweenTwoPlacesOfTheRecord_AreThoseOfTheListOfGroupsBetweenThem()
        {
            DxfDatabase db = Metric();
            var table = new DxfTable(new GeoPoint3(0, 0, 0), 4, 3, 9.0, 30.0);
            db.ModelSpace.AppendEntity(table);
            table.SetTextString(1, 1, "cell é text");
            table.SetAlignment(1, 1, DxfCellAlignment.TopRight);
            table.SetValue(2, 0, 7.25);
            var places = new List<int>();
            IReadOnlyList<DxfTypedValue> own = db.File.OwnGroupsOf(table.Item, 0, places);

            for (int from = 0; from + 8 < own.Count; from += 37)
            {
                int to = from + 8;
                List<KeyValuePair<int, string>> between = db.File.GroupsBetween(
                    table.Item,
                    0,
                    places[2 * from],
                    places[2 * (to - 1) + 1]
                );

                Assert.Equal(to - from, between.Count);

                for (int i = 0; i < between.Count; i++)
                {
                    Assert.Equal(own[from + i].TypeCode, between[i].Key);
                    Assert.Equal(own[from + i].Value, between[i].Value);
                }
            }
        }

        #endregion

        #region Saved without the clock and the dice

        private static byte[] SavedAgain(byte[] file, string version, int pause)
        {
            using (var first = new MemoryStream())
            using (var second = new MemoryStream())
            {
                DxfDatabase.Open(new MemoryStream(file)).Save(first, version);

                if (pause > 0)
                {
                    Thread.Sleep(pause);
                }

                DxfDatabase.Open(new MemoryStream(file)).Save(second, version);
                Assert.Equal(first.ToArray(), second.ToArray());
                return second.ToArray();
            }
        }

        [Theory]
        [InlineData("AC1027")]
        [InlineData("AC1032")]
        public void ABodyAndARegionOfR2018_SavedToR2013Twice_ASecondAndAHalfApart_AreTheSameBytes(string version)
        {
            byte[] saved = SavedAgain(DwgAnswerKey.Key("acis_AC1032"), version, 1500);

            Assert.True(saved.Length > 1000);
        }

        [Fact]
        public void ABodyAndARegionOfR2010_SavedToR2013Twice_ASecondAndAHalfApart_AreTheSameBytes()
        {
            byte[] older = DxfSpeed11Scenarios.Saved(
                DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("acis_AC1032"))),
                "AC1024"
            );

            SavedAgain(older, "AC1027", 1500);
        }

        [Fact]
        public void ABodyAndARegionOfR2000_SavedToR2013Twice_ASecondAndAHalfApart_AreTheSameBytes()
        {
            byte[] older = DxfSpeed11Scenarios.Saved(
                DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("solids_AC1032"))),
                "AC1015"
            );

            SavedAgain(older, "AC1027", 1500);
        }

        [Fact]
        public void TwoBodiesOfOneDrawing_SavedToR2013FromSat_GetTwoIdentities()
        {
            byte[] older = DxfSpeed11Scenarios.Saved(
                DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("acis_AC1032"))),
                "AC1015"
            );
            string text = Encoding.UTF8.GetString(
                DxfSpeed11Scenarios.Saved(DxfDatabase.Open(new MemoryStream(older)), "AC1027")
            );
            var guids = new List<string>();
            string[] lines = text.Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() == "2" && lines[i + 1].StartsWith("{", StringComparison.Ordinal))
                {
                    guids.Add(lines[i + 1]);
                }
            }

            Assert.True(guids.Count >= 2);
            Assert.Equal(guids.Count, guids.Distinct().Count());
        }

        #endregion
    }
}
