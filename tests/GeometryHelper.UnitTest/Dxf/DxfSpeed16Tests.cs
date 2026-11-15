using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfLines;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of speed14 in the core: the one shape an entity of a drawing keeps bare, the width of a polyline and the groups
    /// of a text found in the walk of the record that finds its look, and the lists a polyline of four vertices is read
    /// through. Sizes are held by the bytes allocated or kept, which do not vary with the machine as a time does.
    /// </summary>
    public class DxfSpeed16Tests
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static DxfFile FileOf(string entities) => DxfFile.Parse(R2000(entities));

        private static string Line(string handle, double x) =>
            G(0, "LINE", 5, handle, 8, "0", 10, x, 20, 0.0, 30, 0.0, 11, x + 10.0, 21, 5.0, 31, 0.0);

        #region The one shape

        [Fact]
        public void TheOneShapeOfALine_IsKeptBare_AndTheListThatHoldsItIsMadeWhereAskedFor_AndKept()
        {
            DxfItem item = FileOf(Line("A1", 0.0)).ModelSpace.Single();

            IReadOnlyList<object> list = item.ShapesOrOne(out object one);

            Assert.Null(list);
            Assert.IsType<GeoLine3>(one);
            Assert.Same(one, item.Shape);

            // The list is made when it is asked for, holds the same shape, and is the same list every time, as before.
            IReadOnlyList<object> shapes = item.Shapes;

            Assert.Same(one, Assert.Single(shapes));
            Assert.Same(shapes, item.Shapes);
            Assert.Same(one, item.Shape);

            // And once it is made it is what the item gives.
            IReadOnlyList<object> again = item.ShapesOrOne(out object bare);

            Assert.Same(shapes, again);
            Assert.Null(bare);
        }

        [Fact]
        public void TheShapesOfAnEntityOfNoShapeAndOfSeveral_AreListsAsBefore()
        {
            // A LINE of no length has no shape; a 3DFACE of four corners that do not lie in one plane is read as two triangles.
            DxfFile file = FileOf(
                G(0, "LINE", 5, "A1", 8, "0", 10, 5.0, 20, 5.0, 30, 0.0, 11, 5.0, 21, 5.0, 31, 0.0)
                    + G(
                        0,
                        "3DFACE",
                        5,
                        "A2",
                        8,
                        "0",
                        10,
                        0.0,
                        20,
                        0.0,
                        30,
                        0.0,
                        11,
                        10.0,
                        21,
                        0.0,
                        31,
                        0.0,
                        12,
                        10.0,
                        22,
                        10.0,
                        32,
                        5.0,
                        13,
                        0.0,
                        23,
                        10.0,
                        33,
                        0.0
                    )
            );
            DxfItem none = file.ModelSpace[0];
            DxfItem two = file.ModelSpace[1];

            IReadOnlyList<object> noneList = none.ShapesOrOne(out object noneBare);
            IReadOnlyList<object> twoList = two.ShapesOrOne(out object twoBare);

            Assert.NotNull(noneList);
            Assert.Empty(noneList);
            Assert.Null(noneBare);
            Assert.Null(none.Shape);
            Assert.NotNull(twoList);
            Assert.Equal(2, twoList.Count);
            Assert.Null(twoBare);
            Assert.Null(two.Shape);
            Assert.Same(twoList, two.Shapes);
        }

        [Fact]
        public void TheShapeOfAnEntityChanged_IsReadAgain_BareAsBefore()
        {
            DxfItem item = FileOf(Line("A1", 0.0)).ModelSpace.Single();
            object first = item.Shape;

            item.Shape = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0));

            IReadOnlyList<object> list = item.ShapesOrOne(out object now);

            Assert.Null(list);
            Assert.NotSame(first, now);
            Assert.Equal(40.0, ((GeoLine3)now).Length, 6);
        }

        [Fact]
        public void TheOneShapeOfManyEntities_AskedBare_MakesNoListAndNoEnumerator()
        {
            // 20 000 lines, each walked bare: nothing is allocated for the walk, where a list of 24 bytes was kept for each and an
            // enumerator of 48 made for each walk of it.
            const int count = 20_000;
            var text = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                text.Append(Line(i.ToString("X", Inv), i));
            }

            DxfItem[] items = FileOf(text.ToString()).ModelSpace.ToArray();

            foreach (DxfItem item in items)
            {
                item.ShapesOrOne(out _);
            }

            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int found = 0;

            foreach (DxfItem item in items)
            {
                if (item.ShapesOrOne(out object one) == null && one != null)
                {
                    found++;
                }
            }

            long used = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(count, found);
            Assert.True(used / count <= 1, "allocated " + used / count + " bytes an entity");
        }

        [Fact]
        public void TheShapesOfEntitiesRead_KeepNoListAround_ButTheShapeAlone()
        {
            // The one shape of a line is 64 bytes (two points of 24 and the object); the list that held it in 24 more.
            const int count = 20_000;
            var text = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                text.Append(Line(i.ToString("X", Inv), i));
            }

            DxfItem[] items = FileOf(text.ToString()).ModelSpace.ToArray();

            long before = GC.GetTotalMemory(true);

            foreach (DxfItem item in items)
            {
                item.ShapesOrOne(out _);
            }

            long kept = GC.GetTotalMemory(true) - before;

            Assert.True(kept / count <= 72, "kept " + kept / count + " bytes an entity");
            GC.KeepAlive(items);
        }

        #endregion

        #region The width of a polyline, found with the look

        private static string Polyline(string handle, double? constant, double[] widths, string extra = null)
        {
            var text = new StringBuilder(
                G(0, "LWPOLYLINE", 5, handle, 8, "0", 100, "AcDbEntity", 100, "AcDbPolyline", 90, 4, 70, 1)
            );

            if (constant.HasValue)
            {
                text.Append(G(43, constant.Value));
            }

            for (int v = 0; v < 4; v++)
            {
                text.Append(G(10, v == 1 || v == 2 ? 10.0 : 0.0, 20, v >= 2 ? 10.0 : 0.0));

                if (v == 1 && widths != null)
                {
                    text.Append(G(40, widths[0], 41, widths[1]));
                }
            }

            return text + (extra ?? string.Empty);
        }

        [Fact]
        public void APolylineOfNoWidth_AskedFirstForItsHandle_IsKnownToHaveNone_WhereTheLookIsRead()
        {
            DxfItem plain = FileOf(Polyline("A1", null, null)).ModelSpace.Single();
            DxfItem wide = FileOf(Polyline("A1", null, new[] { 2.0, 1.0 })).ModelSpace.Single();
            DxfItem constant = FileOf(Polyline("A1", 0.5, null)).ModelSpace.Single();

            Assert.Equal("A1", plain.HandleForDisplay);
            Assert.Equal("A1", wide.HandleForDisplay);
            Assert.Equal("A1", constant.HandleForDisplay);

            Assert.True(plain.KnownWithoutWidth);
            Assert.False(wide.KnownWithoutWidth);
            Assert.False(constant.KnownWithoutWidth);
        }

        [Fact]
        public void APolylineOfNoWidth_AskedForItsLookTheOrdinaryWay_IsKnownToHaveNone_OnceItsWidthIsAsked()
        {
            DxfItem plain = FileOf(Polyline("A1", null, null)).ModelSpace.Single();

            Assert.Equal("A1", plain.Handle);
            Assert.False(plain.KnownWithoutWidth);

            Assert.False(plain.File.HasWidthOf(plain));
            Assert.True(plain.KnownWithoutWidth);
            Assert.False(plain.File.HasWidthOf(plain));
        }

        [Fact]
        public void APolylineKnownToHaveNoWidth_ThatIsGivenOne_HasWidthAtOnce()
        {
            DxfDatabase db = DxfDatabase.Parse(R2000(Polyline("A1", null, null)));
            DxfPolyline polyline = db.ModelSpace.OfType<DxfPolyline>().Single();

            Assert.Equal("A1", polyline.Item.HandleForDisplay);
            Assert.True(polyline.Item.KnownWithoutWidth);
            Assert.False(polyline.HasWidth);

            polyline.SetStartWidthAt(1, 0.75);

            Assert.False(polyline.Item.KnownWithoutWidth);
            Assert.True(polyline.HasWidth);

            polyline.SetStartWidthAt(1, 0.0);
            polyline.SetEndWidthAt(1, 0.0);

            Assert.False(polyline.HasWidth);

            polyline.ConstantWidth = 1.5;

            Assert.True(polyline.HasWidth);
            Assert.False(polyline.Item.KnownWithoutWidth);
        }

        [Fact]
        public void APolylineKnownToHaveNoWidth_MovedByTheRecordBeingWrittenAnew_IsAskedAnew()
        {
            DxfDatabase db = DxfDatabase.Parse(R2000(Polyline("A1", null, null)));
            DxfPolyline polyline = db.ModelSpace.OfType<DxfPolyline>().Single();

            Assert.Equal("A1", polyline.Item.HandleForDisplay);
            Assert.True(polyline.Item.KnownWithoutWidth);

            polyline.TransformBy(GeoTransform3.Translation(new GeoVector3(3, 4, 0)));

            // The record written anew forgets what was known of it, and the question is answered again from the new one.
            Assert.False(polyline.Item.KnownWithoutWidth);
            Assert.False(polyline.HasWidth);
            Assert.True(polyline.Item.KnownWithoutWidth);
        }

        [Fact]
        public void GeneratedPolylines_WhoseWidthIsFoundWithTheirLook_AnswerAsOnesAskedAlone_AndAsTheirVerticesDo()
        {
            const int count = 1_500;
            var rnd = new Random(16001);
            var records = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                records.Append(PolylineRecord(rnd, i + 1));
            }

            string file = R2000(records.ToString());
            DxfItem[] together = DxfFile.Parse(file).ModelSpace.ToArray();
            DxfItem[] alone = DxfFile.Parse(file).ModelSpace.ToArray();

            // An answer of another kind: the polylines read by their vertices, as an edit leaves them, each width asked of them.
            DxfPolyline[] byVertices = DxfDatabase.Parse(file).ModelSpace.OfType<DxfPolyline>().ToArray();
            var wrong = new List<string>();

            Assert.Equal(alone.Length, together.Length);
            Assert.Equal(alone.Length, byVertices.Length);

            for (int i = 0; i < alone.Length; i++)
            {
                bool wanted = alone[i].File.HasWidthOf(alone[i]);
                int vertices = byVertices[i].NumberOfVertices;
                bool byTheirVertices = vertices == 0 && byVertices[i].ConstantWidth != 0.0;

                for (int v = 0; v < vertices && !byTheirVertices; v++)
                {
                    byTheirVertices = byVertices[i].GetStartWidthAt(v) != 0.0 || byVertices[i].GetEndWidthAt(v) != 0.0;
                }

                if (byTheirVertices != wanted)
                {
                    wrong.Add(i + ": width " + wanted + " where the vertices say " + byTheirVertices);
                }

                string handle = together[i].HandleForDisplay;

                // The look found with the width is the look found alone, and the width kept is the one asked alone: none known
                // where the walk found none, and not known where it found one.
                if (!string.Equals(handle, alone[i].Handle, StringComparison.Ordinal))
                {
                    wrong.Add(i + ": handle " + handle + " against " + alone[i].Handle);
                }

                if (together[i].KnownWithoutWidth == wanted)
                {
                    wrong.Add(
                        i + ": known without width " + together[i].KnownWithoutWidth + " where it has width " + wanted
                    );
                }

                if (together[i].File.HasWidthOf(together[i]) != wanted)
                {
                    wrong.Add(i + ": width answered " + !wanted + " against " + wanted);
                }

                if (!string.Equals(together[i].Layer, alone[i].Layer, StringComparison.Ordinal))
                {
                    wrong.Add(i + ": layer " + together[i].Layer + " against " + alone[i].Layer);
                }
            }

            Assert.Empty(wrong);
        }

        private static string Group(Random rnd, int code, string value) =>
            rnd.Next(12) == 0 ? code.ToString(Inv).PadLeft(3 + rnd.Next(2)) + "\r\n" + value + "\r\n" : G(code, value);

        private static string Real(Random rnd, double v)
        {
            if (rnd.Next(40) == 0)
            {
                string[] bad = { "abc", "1e999", "NaN", "", " ", "1.2.3", "--1", "Infinity" };
                return bad[rnd.Next(bad.Length)];
            }

            return rnd.Next(3) == 0 ? v.ToString("E5", Inv) : v.ToString("R", Inv);
        }

        private static string PolylineRecord(Random rnd, int handle)
        {
            var text = new StringBuilder();
            text.Append(Group(rnd, 0, "LWPOLYLINE")).Append(Group(rnd, 5, handle.ToString("X", Inv)));

            if (rnd.Next(6) == 0)
            {
                text.Append(Group(rnd, 102, "{ACAD_REACTORS"))
                    .Append(Group(rnd, 43, Real(rnd, 3.0)))
                    .Append(Group(rnd, 102, "}"));
            }

            text.Append(Group(rnd, 8, rnd.Next(2) == 0 ? "0" : "Steel"));
            text.Append(Group(rnd, 100, "AcDbEntity"));

            if (rnd.Next(4) == 0)
            {
                text.Append(Group(rnd, 43, Real(rnd, 0.5)));
            }

            text.Append(Group(rnd, 100, "AcDbPolyline")).Append(Group(rnd, 90, "4")).Append(Group(rnd, 70, "1"));

            if (rnd.Next(3) == 0)
            {
                text.Append(Group(rnd, 43, rnd.Next(3) == 0 ? "0" : Real(rnd, rnd.Next(5) * 0.25)));
            }

            if (rnd.Next(8) == 0)
            {
                text.Append(Group(rnd, 999, "a comment"));
            }

            int vertices = rnd.Next(5);

            for (int v = 0; v < vertices; v++)
            {
                text.Append(Group(rnd, 10, Real(rnd, v * 10.0))).Append(Group(rnd, 20, Real(rnd, v * 5.0)));

                if (rnd.Next(4) == 0)
                {
                    text.Append(Group(rnd, 40, Real(rnd, rnd.Next(3) == 0 ? 0.0 : 1.5)));
                }

                if (rnd.Next(4) == 0)
                {
                    text.Append(Group(rnd, 41, Real(rnd, rnd.Next(3) == 0 ? 0.0 : 2.0)));
                }

                if (rnd.Next(6) == 0)
                {
                    text.Append(Group(rnd, 42, "0.5"));
                }
            }

            if (rnd.Next(6) == 0)
            {
                // A width after the extended data begins is not the polyline's.
                text.Append(Group(rnd, 1001, "APP")).Append(Group(rnd, 40, "9")).Append(Group(rnd, 41, "9"));
            }

            return text.ToString();
        }

        #endregion

        #region The groups of a text, found with the look

        private static string TextRecord(Random rnd, int handle)
        {
            var text = new StringBuilder();
            text.Append(Group(rnd, 0, "TEXT")).Append(Group(rnd, 5, handle.ToString("X", Inv)));

            if (rnd.Next(10) == 0)
            {
                text.Append(Group(rnd, 102, "{ACAD_XDICTIONARY"))
                    .Append(Group(rnd, 360, "5A"))
                    .Append(Group(rnd, 102, "}"));
            }

            text.Append(Group(rnd, 8, rnd.Next(2) == 0 ? "0" : "Notes")).Append(Group(rnd, 100, "AcDbEntity"));

            if (rnd.Next(6) == 0)
            {
                text.Append(Group(rnd, 10, "99")).Append(Group(rnd, 20, "99")).Append(Group(rnd, 40, "99"));
            }

            text.Append(Group(rnd, 100, "AcDbText"))
                .Append(Group(rnd, 10, Real(rnd, 12.5)))
                .Append(Group(rnd, 20, Real(rnd, -3.0)))
                .Append(Group(rnd, 30, "0"))
                .Append(Group(rnd, 40, Real(rnd, 2.5)))
                .Append(Group(rnd, 1, "T" + handle.ToString("X", Inv)));

            if (rnd.Next(3) == 0)
            {
                text.Append(Group(rnd, 50, Real(rnd, 30.0))).Append(Group(rnd, 41, Real(rnd, 0.8)));
            }

            if (rnd.Next(4) == 0)
            {
                text.Append(Group(rnd, 7, rnd.Next(2) == 0 ? "Standard" : " Arial "));
            }

            if (rnd.Next(5) == 0)
            {
                text.Append(Group(rnd, 72, "1"))
                    .Append(Group(rnd, 11, Real(rnd, 7.0)))
                    .Append(Group(rnd, 21, Real(rnd, 8.0)));
            }

            if (rnd.Next(8) == 0)
            {
                text.Append(Group(rnd, 100, "AcDbText")).Append(Group(rnd, 73, "2")).Append(Group(rnd, 71, "2"));
            }

            if (rnd.Next(8) == 0)
            {
                text.Append(Group(rnd, 999, "a comment")).Append(Group(rnd, 210, "0")).Append(Group(rnd, 220, "0"));
            }

            if (rnd.Next(6) == 0)
            {
                text.Append(Group(rnd, 1001, "APP")).Append(Group(rnd, 1, "not the words")).Append(Group(rnd, 40, "9"));
            }

            return text.ToString();
        }

        private static string Describe(DxfFile.TextGroups g) =>
            string.Join(
                "|",
                g.Have,
                Bits(g.X),
                Bits(g.Y),
                Bits(g.Z),
                Bits(g.AlignedX),
                Bits(g.AlignedY),
                Bits(g.AlignedZ),
                Bits(g.Height),
                Bits(g.Factor),
                Bits(g.Degrees),
                Bits(g.Oblique),
                Bits(g.NormalX),
                Bits(g.NormalY),
                Bits(g.NormalZ),
                g.Mirror,
                g.Horizontal,
                g.Vertical,
                g.Words ?? "~",
                g.Style ?? "~"
            );

        private static string Bits(double d) => BitConverter.DoubleToInt64Bits(d).ToString("X16", Inv);

        [Fact]
        public void GeneratedTexts_WhoseGroupsAreReadWithTheirLook_AreTheOnesReadAlone()
        {
            const int count = 1_500;
            var rnd = new Random(16002);
            var records = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                records.Append(TextRecord(rnd, i + 1));
            }

            string file = R2000(records.ToString());
            DxfItem[] together = DxfFile.Parse(file).ModelSpace.ToArray();
            DxfItem[] alone = DxfFile.Parse(file).ModelSpace.ToArray();
            var wrong = new List<string>();
            int given = 0;

            Assert.Equal(alone.Length, together.Length);

            for (int i = 0; i < alone.Length; i++)
            {
                bool wanted = alone[i].File.TryTextGroups(alone[i], out DxfFile.TextGroups groups);

                // The look is read first, as a drawing reads it, and the groups after: a thread's slots filled by that walk are read.
                string handle = together[i].HandleForDisplay;
                bool got = together[i].File.TryTextGroups(together[i], out DxfFile.TextGroups viaLook);

                if (got != wanted)
                {
                    wrong.Add(i + ": groups given " + got + " against " + wanted);
                    continue;
                }

                if (wanted)
                {
                    given++;

                    if (Describe(viaLook) != Describe(groups))
                    {
                        wrong.Add(i + ": " + Describe(viaLook) + " against " + Describe(groups));
                    }
                }

                if (!string.Equals(handle, alone[i].Handle, StringComparison.Ordinal))
                {
                    wrong.Add(i + ": handle " + handle + " against " + alone[i].Handle);
                }
            }

            Assert.Empty(wrong);

            // Some of the texts have an extension dictionary, which gives none: the run held both kinds.
            Assert.InRange(given, count / 2, count - 1);
        }

        [Fact]
        public void ATextWhoseGroupsWereReadWithItsLook_AndThenChanged_IsReadAnewFromItsNewRecord()
        {
            DxfDatabase db = DxfDatabase.Parse(
                R2000(
                    G(0, "TEXT", 5, "A1", 8, "0", 100, "AcDbEntity", 100, "AcDbText")
                        + G(10, 1.0, 20, 2.0, 30, 0.0, 40, 2.5, 1, "OLD", 100, "AcDbText")
                )
            );
            DxfDBText text = db.ModelSpace.OfType<DxfDBText>().Single();

            Assert.Equal("A1", text.Item.HandleForDisplay);
            text.TextString = "NEW";

            Assert.Equal("NEW", text.ReadForDisplay().TextString);
            Assert.Equal("NEW", text.TextString);
        }

        [Fact]
        public void ATextWhoseGroupsWereReadAheadOfItsDrawing_ButNeverDrawn_LeavesNothingHeldByTheThread()
        {
            DxfDatabase db = DxfDatabase.Parse(
                R2000(
                    G(0, "TEXT", 5, "A1", 8, "0", 100, "AcDbEntity", 100, "AcDbText")
                        + G(10, 1.0, 20, 2.0, 40, 2.5, 1, "A")
                )
            );
            DxfDBText text = db.ModelSpace.OfType<DxfDBText>().Single();

            Assert.Equal("A1", text.Item.HandleForDisplay);
            DxfFile.ReleaseTextStash();

            // Still read right, by a walk of its own.
            Assert.Equal("A", text.ReadForDisplay().TextString);
        }

        #endregion
    }
}
