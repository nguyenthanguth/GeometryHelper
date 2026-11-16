using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of speed18 in the core: the DXF a DWG is made into written straight into one array, a group that holds a
    /// number kept with no object made of it, the objects of a DWG that carry what only some carry in an object of their own,
    /// the bits of a DWG read from the bytes they lie in, the list of a block's entities walked with no copy of it where
    /// nothing changed, and the count of a block's entities made with none. Each is held to what it was before, and the sizes
    /// to the bytes allocated, which do not vary with the machine as a time does.
    /// </summary>
    public class DxfSpeed27Tests
    {
        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>A DWG of lines and circles, as a drawing of that many entities written by the library.</summary>
        private static byte[] OneDwg(int count)
        {
            var writer = new DxfWriter();

            for (int i = 0; i < count; i++)
            {
                double x = i % 100 * 30.0;
                double y = i / 100 * 30.0;

                if (i % 3 == 0)
                {
                    writer.Add(new GeoCircle2(new GeoPoint2(x, y), 5.0));
                }
                else
                {
                    writer.Add(new GeoLine2(new GeoPoint2(x, y), new GeoPoint2(x + 20.0, y + 12.5)));
                }
            }

            return DwgWriter.ToDwg(DxfDatabase.Parse(writer.ToString()).ToText(), DwgWriter.DefaultVersion);
        }

        #region The DXF written as bytes

        /// <summary>
        /// A number as DXF text, as .NET Core made it before the writer wrote bytes (a string for each, its digits tried back),
        /// worked out in integers so that it is the same under .NET Framework, which formats and parses a little off.
        /// </summary>
        private static string FormerNumber(double value) => DxfRuntime1Tests.ReferenceNumber(value);

        private static string Written(Action<DwgDxfWriter> write)
        {
            var writer = new DwgDxfWriter();
            write(writer);
            return Encoding.ASCII.GetString(writer.ToArray());
        }

        [Fact]
        public void ANumberWrittenAsBytes_IsTheTextItWasWrittenAs_ForTheWholeAndTheFractionalAndTheOutOfTheWay()
        {
            double[] values =
            {
                0.0,
                1.0,
                -1.0,
                7.0,
                -7.0,
                10.0,
                100.0,
                123456789.0,
                -123456789012.0,
                999999999999999.0,
                -999999999999999.0,
                1E15,
                1E15 + 2.0,
                1E20,
                -1E20,
                0.5,
                -0.5,
                0.1,
                0.2 + 0.1,
                1.0 / 3.0,
                -2.0 / 3.0,
                123.456,
                1234567.891,
                1E-7,
                -1E-7,
                1E-300,
                1E300,
                double.Epsilon,
                -double.Epsilon,
                double.MaxValue,
                double.MinValue,
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
                1.797693134862314E+99,
                28.64788975654116,
                57.29577951308232,
                0.017453292519943295,
                Math.PI,
                Math.E * 1000.0,
                -0.0,
            };

            foreach (double value in values)
            {
                string expected = "40\r\n" + FormerNumber(value) + "\r\n";
                string got = Written(w => w.Write(40, value));

                Assert.True(
                    expected == got,
                    "the number " + BitConverter.DoubleToInt64Bits(value).ToString("X16") + " is " + got.Trim()
                );
            }
        }

        [Fact]
        public void ThirtyThousandNumbersOfEveryKind_AreWrittenAsTheyWereWritten()
        {
            var random = new Random(27);
            var writer = new DwgDxfWriter();
            var expected = new StringBuilder();

            for (int i = 0; i < 30_000; i++)
            {
                double value;

                switch (i % 6)
                {
                    case 0:
                        value = BitConverter.Int64BitsToDouble(((long)random.Next() << 32) | (uint)random.Next());
                        break;
                    case 1:
                        value = Math.Round((random.NextDouble() - 0.5) * 100000.0, random.Next(0, 6));
                        break;
                    case 2:
                        value = random.Next(-1_000_000, 1_000_000);
                        break;
                    case 3:
                        value = (random.NextDouble() - 0.5) * Math.Pow(10.0, random.Next(-12, 18));
                        break;
                    case 4:
                        value = random.Next(1, 360) * Math.PI / 180.0;
                        break;
                    default:
                        value = random.Next(0, 100000) / 1000.0;
                        break;
                }

                if (double.IsNaN(value))
                {
                    continue;
                }

                writer.Write(10 + (i % 5) * 10, value);
                expected.Append(10 + (i % 5) * 10).Append("\r\n").Append(FormerNumber(value)).Append("\r\n");
            }

            Assert.Equal(expected.ToString(), Encoding.ASCII.GetString(writer.ToArray()));
        }

        [Fact]
        public void TheCodesTheWholeNumbersAndTheHandles_AreWrittenAsTheirDigits()
        {
            long[] whole =
            {
                0,
                1,
                9,
                10,
                99,
                100,
                255,
                65535,
                1_000_000,
                int.MaxValue,
                int.MinValue,
                -1,
                -10,
                -123456789,
                long.MaxValue,
                long.MinValue,
                -long.MaxValue,
            };

            foreach (long value in whole)
            {
                string got = Written(w => w.Write(70, value));
                Assert.Equal("70\r\n" + value.ToString(CultureInfo.InvariantCulture) + "\r\n", got);
            }

            foreach (int code in new[] { 0, 5, 10, 100, 330, 1071, -1 })
            {
                Assert.Equal(
                    code.ToString(CultureInfo.InvariantCulture) + "\r\nA\r\n",
                    Written(w => w.Write(code, "A"))
                );
            }

            ulong[] handles =
            {
                0UL,
                1UL,
                9UL,
                0xAUL,
                0xFUL,
                0x10UL,
                0xFFUL,
                0x100UL,
                0x2F3A7UL,
                0x123456789ABCDEF0UL,
                ulong.MaxValue,
            };

            foreach (ulong handle in handles)
            {
                Assert.Equal(
                    "5\r\n" + handle.ToString("X", CultureInfo.InvariantCulture) + "\r\n",
                    Written(w => w.WriteHandle(5, handle))
                );
            }
        }

        [Fact]
        public void TextsAndEscapedBytes_AreWrittenAsTheyWere_AndNothingIsLostWhereTheArrayGrows()
        {
            var writer = new DwgDxfWriter(1);
            var expected = new StringBuilder();

            // Past the 4 096 bytes a writer is given at least, several times over, so that its array grows through each size.
            for (int i = 0; i < 20_000; i++)
            {
                string word = "T" + i.ToString(CultureInfo.InvariantCulture);
                writer.Write(1, word);
                expected.Append("1\r\n").Append(word).Append("\r\n");

                if (i % 7 == 0)
                {
                    writer.Write(1, new byte[] { (byte)'a', 0x01, (byte)'^', (byte)'b', 0x1F, 0x7F });
                    expected.Append("1\r\na^A^ ").Append("b^_").Append("\u007f").Append("\r\n");
                }

                if (i % 11 == 0)
                {
                    writer.Write(1, "é");
                    expected.Append("1\r\n?\r\n");
                }
            }

            writer.WriteChunks(3, 1, Encoding.ASCII.GetBytes(new string('x', 600)), 250, false);
            expected
                .Append("3\r\n")
                .Append('x', 250)
                .Append("\r\n3\r\n")
                .Append('x', 250)
                .Append("\r\n1\r\n")
                .Append('x', 100)
                .Append("\r\n");

            Assert.Equal(expected.ToString(), Encoding.GetEncoding("iso-8859-1").GetString(writer.ToArray()));
        }

        [Fact]
        public void TheArrayTheDxfIsWrittenIn_IsGivenWithNoCopyUnlessAQuarterOfItIsUnused()
        {
            // 4 096 bytes are made at least. A text that fills most of them leaves the array as it is, longer than the DXF.
            var nearlyFull = new DwgDxfWriter(100);
            nearlyFull.Write(1, new string('x', 3890));
            byte[] kept = nearlyFull.Take(out int length);

            Assert.Equal(3 + 3890 + 2, length);
            Assert.Equal(4096, kept.Length);
            Assert.Equal((byte)'x', kept[3]);

            // One that fills little of the room made for it is cut to its length.
            var roomy = new DwgDxfWriter(1 << 20);
            roomy.Write(1, "abc");
            byte[] cut = roomy.Take(out int cutLength);

            Assert.Equal(8, cutLength);
            Assert.Equal(8, cut.Length);
            Assert.Equal(Encoding.ASCII.GetBytes("1\r\nabc\r\n"), cut);
        }

        [Fact]
        public void ADwgsDxfAsItsArrayAndLength_IsTheDxfAsBytes_AndASavedDwgOpensAsTheDxfOfIt()
        {
            byte[] file = OneDwg(1_500);

            byte[] whole = DwgReader.ToDxf(file);
            byte[] buffer = DwgReader.ToDxfBuffer(file, out int length);

            Assert.Equal(whole.Length, length);
            Assert.True(buffer.Length >= length);
            Assert.Equal(whole, buffer.Take(length).ToArray());

            string path = Path.Combine(Path.GetTempPath(), "gh-dwg-speed27-" + Guid.NewGuid().ToString("N") + ".dwg");

            try
            {
                File.WriteAllBytes(path, file);
                DxfDatabase opened = DwgReader.Open(path, null, null, System.Threading.CancellationToken.None);
                DxfDatabase viaBytes = DxfDatabase.Open(new MemoryStream(whole, false));

                Assert.Equal(viaBytes.ToText(), opened.ToText());
                Assert.Equal(viaBytes.ModelSpace.Count, opened.ModelSpace.Count);
                Assert.Equal(1_500, opened.ModelSpace.Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ADwgOfTwentyThousandEntities_IsMadeIntoItsDxfInFewerThanTwoThousandBytesAnEntity()
        {
            // 2 742 bytes an entity under .NET Framework before: a string for each code, each whole number, each handle and
            // each number, the stream's own growth, a list of groups grown by adding and an object boxed for each number.
            const int count = 20_000;
            byte[] file = OneDwg(count);

            _ = DwgReader.ToDxf(file);
            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            byte[] dxf = DwgReader.ToDxf(file);
            long used = (GC.GetAllocatedBytesForCurrentThread() - before) / count;

            Assert.True(dxf.Length > count * 80);
            Assert.True(used < 2_000, "allocated " + used + " bytes an entity");
        }

        #endregion

        #region The bits of a DWG

        private static byte[] RandomBytes(int length, int seed)
        {
            var bytes = new byte[length];
            new Random(seed).NextBytes(bytes);
            return bytes;
        }

        [Fact]
        public void TheBitsReadFromTheBytesTheyLieIn_AreTheBitsReadOneByOne_ToTheLastBitAndAtTheEnd()
        {
            byte[] data = RandomBytes(40, 27);
            var random = new Random(270);

            for (int trial = 0; trial < 20_000; trial++)
            {
                long end = random.Next(0, data.Length * 8 + 1);
                long start = random.Next(0, (int)end + 1);
                int count = random.Next(0, 40);

                var fast = new DwgBitReader(data, start, end);
                var slow = new DwgBitReader(data, start, end);

                bool fastEnded = false;
                bool slowEnded = false;
                uint got = 0;
                uint expected = 0;

                try
                {
                    got = fast.Bits(count);
                }
                catch (FormatException)
                {
                    fastEnded = true;
                }

                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        expected = (expected << 1) | (slow.Bit() ? 1u : 0u);
                    }
                }
                catch (FormatException)
                {
                    slowEnded = true;
                }

                string step = "bits " + start + " to " + end + ", " + count + " asked";
                Assert.True(fastEnded == slowEnded, step);
                Assert.True(fast.Position == slow.Position, step + ", at " + fast.Position + " and " + slow.Position);

                if (!fastEnded)
                {
                    Assert.True(got == expected, step);
                }
            }
        }

        [Fact]
        public void ADoubleReadFromTheBytesItLiesIn_IsTheDoubleReadByteByByte_AtEveryBitOfTheFirstByte()
        {
            byte[] data = RandomBytes(40, 28);

            for (long start = 0; start <= data.Length * 8 - 64; start += 1)
            {
                var fast = new DwgBitReader(data, start, data.Length * 8);
                var slow = new DwgBitReader(data, start, data.Length * 8);
                long bits = 0;

                for (int i = 0; i < 8; i++)
                {
                    bits |= (long)slow.RawChar() << (8 * i);
                }

                Assert.True(
                    BitConverter.DoubleToInt64Bits(fast.RawDouble()) == bits,
                    "the double at bit " + start + " is not the one read byte by byte"
                );
                Assert.Equal(slow.Position, fast.Position);
            }

            // Fewer than 64 bits left: read the way it was, ending at the same bit.
            for (long end = 0; end < 64; end += 5)
            {
                var fast = new DwgBitReader(data, 3, 3 + end);
                var slow = new DwgBitReader(data, 3, 3 + end);

                Assert.Throws<FormatException>(() => fast.RawDouble());
                Assert.Throws<FormatException>(() =>
                {
                    for (int i = 0; i < 8; i++)
                    {
                        slow.RawChar();
                    }
                });
                Assert.Equal(slow.Position, fast.Position);
            }
        }

        #endregion

        #region A group that holds a number, and the object of a DWG

        [Fact]
        public void AGroupThatHoldsANumber_GivesItsValueAsTheBoxedNumberItWas_AndOnesOfOtherKindsAreAsTheyWere()
        {
            DwgGroup real = DwgGroup.Real(40, -0.0);
            DwgGroup whole = DwgGroup.Whole(70, -12);
            var text = new DwgGroup(1, "abc");
            var bytes = new byte[] { 1, 2 };
            var handle = new DwgGroup(330, 0xABCUL);
            var name = new DwgName(5UL);

            Assert.True(real.IsReal && !real.IsWhole);
            Assert.True(whole.IsWhole && !whole.IsReal);
            Assert.False(text.IsReal || text.IsWhole || handle.IsReal || handle.IsWhole);
            Assert.Equal(40, real.Code);
            Assert.IsType<double>(real.Value);
            Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0), BitConverter.DoubleToInt64Bits((double)real.Value));
            Assert.IsType<int>(whole.Value);
            Assert.Equal(-12, (int)whole.Value);
            Assert.Equal(-12, whole.WholeValue);
            Assert.Equal("abc", text.Value);
            Assert.IsType<ulong>(handle.Value);
            Assert.Same(bytes, new DwgGroup(3, bytes).Value);
            Assert.Same(name, new DwgGroup(2, name).Value);
            Assert.Null(new DwgGroup(2, null).Value);

            // Every double, to the bit.
            foreach (
                double value in new[] { 0.0, double.NaN, double.PositiveInfinity, double.Epsilon, 1.0 / 3.0, -1E300 }
            )
            {
                Assert.Equal(
                    BitConverter.DoubleToInt64Bits(value),
                    BitConverter.DoubleToInt64Bits(DwgGroup.Real(1, value).RealValue)
                );
            }
        }

        [Fact]
        public void AGroupAddedToAnObject_KeepsItsKind_AWholeNumberOtherThanAnIntNotMadeADouble()
        {
            var one = new DwgObject();
            one.Add(40, 1.5);
            one.Add(70, 3);
            one.Add(71, (short)4);
            one.Add(72, (byte)5);
            one.Add(330, 0xFFFFFFFFFFFFFFFFUL);
            one.Add(331, 5_000_000_000L);
            one.Add(332, 4_000_000_000U);
            one.Add(1, "text");
            one.Add(290, true);
            one.AddPoint(10, new[] { 1.0, 2.0, 3.0 });

            DwgGroup[] groups = one.Body.ToArray();

            Assert.Equal(typeof(double), groups[0].Value.GetType());
            Assert.Equal(typeof(int), groups[1].Value.GetType());
            Assert.Equal(typeof(int), groups[2].Value.GetType());
            Assert.Equal(typeof(int), groups[3].Value.GetType());
            Assert.Equal(typeof(ulong), groups[4].Value.GetType());
            Assert.Equal(0xFFFFFFFFFFFFFFFFUL, (ulong)groups[4].Value);
            Assert.Equal(typeof(long), groups[5].Value.GetType());
            Assert.Equal(5_000_000_000L, (long)groups[5].Value);
            Assert.Equal(typeof(uint), groups[6].Value.GetType());
            Assert.Equal(4_000_000_000U, (uint)groups[6].Value);
            Assert.Equal("text", groups[7].Value);
            Assert.IsType<bool>(groups[8].Value);
            Assert.True((bool)groups[8].Value);
            Assert.Equal(new[] { 10, 20, 30 }, groups.Skip(9).Select(g => g.Code).ToArray());
            Assert.Equal(new[] { 1.0, 2.0, 3.0 }, groups.Skip(9).Select(g => (double)g.Value).ToArray());
        }

        [Fact]
        public void WhatOnlySomeObjectsCarry_IsNotMadeForAnObjectThatHasNone_AndIsKeptOnceSet()
        {
            var plain = new DwgObject { Handle = 0x10 };

            // Reading what is not there makes nothing and answers as the field did.
            Assert.False(plain.HasReactors);
            Assert.Null(plain.CommonIfAny);
            Assert.Null(plain.BodyIfAny);
            Assert.Null(plain.XDataIfAny);
            Assert.Equal(0UL, plain.First);
            Assert.Equal(0UL, plain.Last);
            Assert.Equal(0UL, plain.SeqEnd);
            Assert.Equal(0UL, plain.BlockEntity);
            Assert.Equal(0UL, plain.EndBlock);
            Assert.Equal(0UL, plain.Annotation);
            Assert.Equal(0, plain.BlockFlags);
            Assert.Null(plain.Owned);
            Assert.Null(plain.RecordName);
            Assert.Null(plain.CellStyleMap);
            Assert.Null(plain.BlockBody);
            Assert.Null(plain.BookColour);
            Assert.Null(plain.ProxyGraphics);
            Assert.Null(plain.TextExtents);
            Assert.Null(plain.Viewports);
            Assert.Null(plain.BodyIfAny);

            // Setting the nought it is makes nothing either; setting what is not nought keeps it.
            plain.First = 0;
            plain.RecordName = null;
            plain.Owned = null;
            Assert.Equal(0UL, plain.First);

            var chain = new DwgObject { Handle = 0x11 };
            chain.First = 0x20;
            chain.Last = 0x30;
            chain.SeqEnd = 0x40;
            chain.Owned = new List<ulong> { 1, 2, 3 };
            chain.RecordName = new byte[] { 65 };
            chain.BlockFlags = 7;
            chain.Annotation = 0x99;
            chain.Entries.Add(5);
            chain.Viewports = new List<ulong> { 9 };

            Assert.Equal(0x20UL, chain.First);
            Assert.Equal(0x30UL, chain.Last);
            Assert.Equal(0x40UL, chain.SeqEnd);
            Assert.Equal(new ulong[] { 1, 2, 3 }, chain.Owned);
            Assert.Equal(new byte[] { 65 }, chain.RecordName);
            Assert.Equal(7, chain.BlockFlags);
            Assert.Equal(0x99UL, chain.Annotation);
            Assert.Equal(new ulong[] { 5 }, chain.Entries);
            Assert.Equal(new ulong[] { 9 }, chain.Viewports);
            Assert.Null(chain.BlockBody);

            // Set back to nothing once something is kept, it reads as nothing.
            chain.First = 0;
            chain.Owned = null;
            Assert.Equal(0UL, chain.First);
            Assert.Null(chain.Owned);
            Assert.Equal(0x30UL, chain.Last);

            // The lists are made when they are asked for to be added to, the same list each time.
            Assert.Same(plain.Reactors, plain.Reactors);
            plain.Reactors.Add(3);
            Assert.True(plain.HasReactors);
            Assert.Same(plain.Body, plain.Body);
            Assert.Same(plain.Common, plain.Common);
            Assert.Same(plain.XData, plain.XData);
            Assert.NotNull(plain.BodyIfAny);
            Assert.Same(plain.Body, plain.BodyIfAny);
        }

        [Fact]
        public void TheObjectsOfALineAndACircleOfADwg_AreFewerBytesAndHoldTheirGroupsInRoomMadeForThem()
        {
            byte[] file = OneDwg(3_000);
            DwgContainer container = DwgContainer.Read(file);
            var reader = new DwgObjectReader(
                container.Objects,
                DwgClass.ReadAll(container.Classes, container.Version),
                container.Version
            );
            int lines = 0;
            int circles = 0;

            foreach (KeyValuePair<ulong, long> entry in container.ObjectMap)
            {
                DwgObject one = reader.Read(entry.Key, entry.Value);

                if (one.Name == "LINE" || one.Name == "CIRCLE")
                {
                    // The room asked for is the room used: a line adds 7 groups, a circle 5.
                    int expected = one.Name == "LINE" ? 7 : 5;
                    Assert.Equal(expected, one.Body.Count);
                    Assert.Equal(expected, one.Body.Capacity);
                    Assert.False(one.HasReactors);
                    Assert.Null(one.XDataIfAny);
                    Assert.Equal(0UL, one.First);
                    Assert.Equal(0UL, one.SeqEnd);

                    if (one.Name == "LINE")
                    {
                        lines++;
                    }
                    else
                    {
                        circles++;
                    }
                }
            }

            Assert.Equal(2_000, lines);
            Assert.Equal(1_000, circles);
        }

        #endregion

        #region The entities a block holds

        private static DxfDatabase Drawing(int count)
        {
            var writer = new DxfWriter();

            for (int i = 0; i < count; i++)
            {
                writer.Add(new GeoLine2(new GeoPoint2(i, 0.0), new GeoPoint2(i, 5.0)));
            }

            return DxfDatabase.Parse(writer.ToString());
        }

        [Fact]
        public void TheWalksOfABlockWithNoEditBetween_ShareOneListOfItsEntities_AndAnEditMakesTheNextOne()
        {
            DxfDatabase database = Drawing(300);
            DxfSpace space = database.ModelSpace.EntitySpace;

            DxfItem[] first = space.Snapshot();
            DxfItem[] second = space.Snapshot();

            Assert.Equal(300, first.Length);
            Assert.Same(first, second);

            // A walk that reads the block does not change it.
            Assert.Equal(300, database.ModelSpace.Cast<DxfEntity>().Count());
            Assert.Equal(300, database.ModelSpace.AllItems().Count());
            Assert.Same(first, space.Snapshot());

            // An entity erased: the list of the walk that began before it is as it was, the next walk has one less.
            DxfEntity erased = database.ModelSpace.Cast<DxfEntity>().ElementAt(5);
            string handle = erased.Handle;
            DxfItem[] walking = space.Snapshot();
            erased.Erase();
            DxfItem[] after = space.Snapshot();

            Assert.NotSame(walking, after);
            Assert.Equal(300, walking.Length);
            Assert.Equal(299, after.Length);
            Assert.Contains(walking, i => i.Handle == handle);
            Assert.DoesNotContain(after, i => i.Handle == handle);
            Assert.Same(after, space.Snapshot());

            // One added: the next list has it last, and the one before is as it was.
            DxfLine added = new DxfLine(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(1.0, 1.0, 0.0));
            database.ModelSpace.AppendEntity(added);
            DxfItem[] last = space.Snapshot();

            Assert.NotSame(after, last);
            Assert.Equal(299, after.Length);
            Assert.Equal(300, last.Length);
            Assert.Equal(added.Handle, last[last.Length - 1].Handle);
        }

        [Fact]
        public void AnEntityErasedWhileTheBlockIsWalked_IsStillPassedOver_WhereTheWalkTookTheCachedList()
        {
            DxfDatabase database = Drawing(40);
            _ = database.ModelSpace.Cast<DxfEntity>().Count();
            var seen = new List<string>();

            foreach (DxfEntity entity in database.ModelSpace)
            {
                seen.Add(entity.Handle);

                // Each walked entity erased as it is met, as foreach (e in db.ModelSpace) e.Erase() does.
                entity.Erase();
            }

            Assert.Equal(40, seen.Count);
            Assert.Equal(40, seen.Distinct().Count());
            Assert.Equal(0, database.ModelSpace.Count);
            Assert.Empty(database.ModelSpace.Cast<DxfEntity>());
        }

        [Fact]
        public void TheCountOfABlock_IsTheNumberItsWalkGives_WithErasedEntitiesAndLinkedColumns_AndMakesNoCopyOfItsList()
        {
            DxfDatabase database = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            int before = database.ModelSpace.Count;
            var main = new DxfMText();
            var second = new DxfMText();
            var third = new DxfMText();
            database.ModelSpace.AppendEntity(main);
            database.ModelSpace.AppendEntity(second);
            database.ModelSpace.AppendEntity(third);
            Assert.Equal(before + 3, database.ModelSpace.Count);
            Assert.Equal(before + 3, database.ModelSpace.Cast<DxfEntity>().Count());

            main.XData = new[]
            {
                new DxfTypedValue(1001, "ACAD"),
                new DxfTypedValue(1000, "ACAD_MTEXT_COLUMNS_BEGIN"),
                new DxfTypedValue(1070, "47"),
                new DxfTypedValue(1070, "3"),
                new DxfTypedValue(1005, second.Handle),
                new DxfTypedValue(1005, third.Handle),
                new DxfTypedValue(1000, "ACAD_MTEXT_COLUMNS_END"),
            };

            Assert.Equal(before + 1, database.ModelSpace.Count);
            Assert.Equal(before + 1, database.ModelSpace.Cast<DxfEntity>().Count());

            // An entity erased on top of the columns.
            database.ModelSpace.Cast<DxfEntity>().First().Erase();
            Assert.Equal(before, database.ModelSpace.Count);
            Assert.Equal(before, database.ModelSpace.Cast<DxfEntity>().Count());

            // A block of a million entities: a count asks for no copy of the list, which was 7.8 MB.
            DxfDatabase many = Drawing(20_000);
            _ = many.ModelSpace.Count;
            GC.Collect();
            long used = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 20; i++)
            {
                Assert.Equal(20_000, many.ModelSpace.Count);
            }

            used = GC.GetAllocatedBytesForCurrentThread() - used;
            Assert.True(used < 20 * 20_000 * 8 / 2, "allocated " + used + " bytes for 20 counts of 20 000 entities");
        }

        #endregion
    }
}
