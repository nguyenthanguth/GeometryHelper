using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// MIRROR of an MTEXT under MIRRTEXT 1, of a MINSERT, of a dynamic block's INSERT flipped, and of a multileader across a
    /// line at 45 degrees, as AutoCAD 2021 writes them (probed in its console, mirror-3 GHM3B): an MTEXT along x mirrored
    /// across a level line written with no direction, a MINSERT written as an INSERT is with its columns the other way, a
    /// flipped door still flipped, the sizes of a multileader kept to the bit.
    /// </summary>
    public class MirrorFollowUpsTests
    {
        private const double Degree = Math.PI / 180.0;

        /// <summary>Mirrors the one object of a kind across the line through two points, keeping it; gives the copy.</summary>
        private static T MirroredCopy<T>(CadDocument document, string source, GeoPoint2 first, GeoPoint2 second)
            where T : DxfEntity
        {
            var before = new HashSet<string>(All<T>(document.ModelSpace).Select(e => e.Handle));
            Run(Session(document), "MIRROR", new[] { source }, Enter, first, second, Enter);
            return All<T>(document.ModelSpace).Single(e => !before.Contains(e.Handle));
        }

        /// <summary>The value of the first group of a code in the record of a handle, trimmed; null where it has none.</summary>
        private static string Group(CadDocument document, string handle, int code)
        {
            string[] lines = document.Database.ToText().Replace("\r\n", "\n").Split('\n');
            bool found = false;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int at = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1].Trim();

                if (at == 0 && found)
                {
                    return null;
                }

                if (at == 5 && value == handle)
                {
                    found = true;
                }
                else if (found && at == code)
                {
                    return value;
                }
            }

            return null;
        }

        private static double Number(string value) =>
            value == null ? double.NaN : double.Parse(value, CultureInfo.InvariantCulture);

        [Fact]
        public void WithMirrtextOn_AnMTextAlongXMirroredAcrossALevelLine_IsWrittenWithNoDirection()
        {
            var db = new DxfDatabase("AC1027");
            db.Mirrtext = true;
            var text = new DxfMText();
            db.ModelSpace.AppendEntity(text);
            text.Contents = "ABC\\PDEF";
            text.TextHeight = 2.5;
            text.Width = 30;
            text.Location = new GeoPoint3(100, 10, 0);
            CadDocument document = CadDocument.From(db, "mtext.dxf");

            DxfMText copy = MirroredCopy<DxfMText>(document, text.Handle, P(0, 100), P(10, 100));

            // AutoCAD 2021 (GHM3B M1 252 L): at (100, 190), 210 of (0, 0, -1), no 11, its Direction (1, 0, 0) and its
            // Rotation 180 degrees.
            Assert.Null(Group(document, copy.Handle, 11));
            Assert.Equal(-1.0, Number(Group(document, copy.Handle, 230)));
            Near(new GeoPoint3(100, 190, 0), copy.Location, 1E-9);
            Assert.Equal(1.0, copy.Direction.X, 12);
            Assert.Equal(Math.PI, copy.Rotation, 12);
        }

        [Fact]
        public void AMInsertMirrored_IsWrittenAsAnInsertIs_ItsColumnsTheOtherWay()
        {
            // AutoCAD 2021 (GHM3B 24E and 24F), as turn, scales, mirror line and what it wrote: 41, 42, 50, 44 and 45 (null
            // where it left the group out).
            object[][] cases =
            {
                new object[] { 0.0, 1.0, 1.0, P(0, 100), P(10, 100), -1.0, null, 180.0, -15.0, 8.0 },
                new object[] { 0.0, 1.0, 1.0, P(500, 0), P(500, 10), -1.0, null, null, -15.0, 8.0 },
                new object[] { 0.0, 1.0, 1.0, P(0, 0), P(10, 10), -1.0, null, 270.0, -15.0, 8.0 },
                new object[] { 30.0, 2.0, 3.0, P(0, 100), P(10, 100), -2.0, 3.0, 150.0, -15.0, 8.0 },
                new object[]
                {
                    30.0,
                    2.0,
                    3.0,
                    P(0, 0),
                    P(10 * Math.Cos(30 * Degree), 5),
                    -2.0,
                    3.0,
                    210.0,
                    -15.0,
                    8.0,
                },
            };

            foreach (object[] c in cases)
            {
                var db = new DxfDatabase("AC1027");
                var block = new DxfBlockTableRecord { Name = "B1" };
                db.BlockTable.Add(block);
                block.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 1.0));
                var array = new DxfMInsertBlock(new GeoPoint3(20, 40, 0), block, 3, 2, 15, 8);
                db.ModelSpace.AppendEntity(array);
                array.Rotation = (double)c[0] * Degree;
                array.ScaleFactors = new GeoVector3((double)c[1], (double)c[2], 1);
                CadDocument document = CadDocument.From(db, "minsert.dxf");

                DxfMInsertBlock copy = MirroredCopy<DxfMInsertBlock>(
                    document,
                    array.Handle,
                    (GeoPoint2)c[3],
                    (GeoPoint2)c[4]
                );

                string at = string.Join(" ", c.Take(3)) + " " + c[3] + "-" + c[4];
                int[] codes = { 41, 42, 50, 44, 45 };

                for (int k = 0; k < codes.Length; k++)
                {
                    string written = Group(document, copy.Handle, codes[k]);
                    object expected = c[5 + k];
                    Assert.True(
                        expected == null
                            ? written == null || (codes[k] == 50 && Number(written) == 0.0)
                            : written != null && Math.Abs(Number(written) - (double)expected) < 1E-9,
                        at + ": " + codes[k] + " " + written
                    );
                }
            }
        }

        /// <summary>
        /// A drawing of a door, a dynamic block whose leaf, a line from (0, 0) to (90, 0), its flip parameter Hand turns over
        /// about the y axis, inserted at (400, 10) flipped (Hand 1, through an anonymous block of its own).
        /// </summary>
        private static CadDocument Door(out DxfBlockReference door)
        {
            var db = new DxfDatabase("AC1027");
            var block = new DxfBlockTableRecord { Name = "GH_DOOR", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var leaf = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(90, 0, 0));
            block.AppendEntity(leaf);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 20, 0)));
            DxfBlockFlipParameter hand = block.AddFlipParameter(
                "Hand",
                "Which way the door opens",
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(0, 50, 0)
            );
            block.AddFlipAction(hand, new[] { leaf });
            door = new DxfBlockReference(new GeoPoint3(400, 10, 0), block);
            db.ModelSpace.AppendEntity(door);
            Hand(door).Value = (short)1;
            return CadDocument.From(db, "door.dxf");
        }

        private static DxfDynamicBlockReferenceProperty Hand(DxfBlockReference door) =>
            door
                .DynamicBlockReferencePropertyCollection.Cast<DxfDynamicBlockReferenceProperty>()
                .Single(p => p.PropertyName == "Hand");

        [Fact]
        public void ADoorFlipped_MirroredOrCopied_IsStillTheDynamicBlockFlipped()
        {
            // AutoCAD 2021 (GHM3B 23C, MIRRTEXT 0 and 1, across each line): the copy places the same anonymous block, keeps the
            // INSERT's representation (AcDbBlockRepresentation) and reads Hand 1, written 41 of -1 and turned 180 across a
            // level line.
            CadDocument document = Door(out DxfBlockReference door);
            Assert.True(door.IsDynamicBlock);

            DxfBlockReference copy = MirroredCopy<DxfBlockReference>(document, door.Handle, P(0, 100), P(10, 100));

            Assert.True(copy.IsDynamicBlock);
            Assert.Equal(door.Name, copy.Name);
            Assert.Equal((short)1, Hand(copy).Value);
            Assert.Equal(-1.0, Number(Group(document, copy.Handle, 41)));
            Assert.Equal(180.0, Number(Group(document, copy.Handle, 50)));
            Near(new GeoPoint3(400, 190, 0), copy.Position, 1E-9);

            // Copied, as AutoCAD's COPY keeps it.
            var before = new HashSet<string>(All<DxfBlockReference>(document.ModelSpace).Select(e => e.Handle));
            Run(Session(document), "COPY", new[] { door.Handle }, Enter, P(0, 0), P(0, -100), Enter);
            DxfBlockReference copied = All<DxfBlockReference>(document.ModelSpace)
                .Single(e => !before.Contains(e.Handle));
            Assert.True(copied.IsDynamicBlock);
            Assert.Equal((short)1, Hand(copied).Value);

            // The copy set back on its own, the door it was copied from is as it was.
            Hand(copied).Value = (short)0;
            Assert.Equal((short)1, Hand(door).Value);
            Assert.Equal((short)0, Hand(copied).Value);
            AssertReopens(document);
        }

        [Fact]
        public void AMultileaderMirroredAcrossALineAt45Degrees_KeepsItsSizesToTheBit()
        {
            // AutoCAD 2021 (GHM3B 25D D45) wrote the words' height, the arrows' size and the landing gap of the context (41,
            // 140, 145) as they were: 0.18, not 0.18000000000000002.
            CadDocument document = BlankWithMLeaderStyle();
            Run(Session(document), "MLD", P(10, 0), P(30, 20), "II");
            string source = Assert.Single(All<DxfMLeader>(document.ModelSpace)).Handle;
            string[] before = new[] { 41, 140, 145 }.Select(c => Group(document, source, c)).ToArray();

            DxfMLeader copy = MirroredCopy<DxfMLeader>(document, source, P(0, 0), P(10, 10));

            Assert.Equal(before, new[] { 41, 140, 145 }.Select(c => Group(document, copy.Handle, c)).ToArray());
        }
    }
}
