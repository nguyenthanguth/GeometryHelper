using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The solids, regions and words a drawing saved as R12 (AC1009) holds as AutoCAD 2021 writes them: a 3DSOLID or a REGION
    /// as the lines, arcs, circles, points and 3D polylines of the edges of its faces (face by face, loop by loop, a line
    /// carrying the normal of its flat face); an INSERT of a broken entity on the drawing's current layer and in its current
    /// colour; an MTEXT wrapped at its width by the fonts of the drawing. The bodies are built here in code, ACIS records
    /// written by hand for the cylinder and the cone, the faces of a box and a tilted region from curves.
    /// </summary>
    public class DxfSaveR12BodiesTests
    {
        private const double Near = 1E-9;

        // ---- the saved text ----------------------------------------------------------------------------------------------------

        private static string R12(DxfDatabase database, out IReadOnlyList<string> said)
        {
            using (var stream = new MemoryStream())
            {
                said = database.Save(stream, "AC1009");
                return Encoding.GetEncoding(1252).GetString(stream.ToArray());
            }
        }

        private static string R12(DxfDatabase database) => R12(database, out _);

        private sealed class Record
        {
            internal Record(string type)
            {
                Type = type;
                Groups = new List<KeyValuePair<int, string>>();
            }

            internal string Type { get; }

            internal List<KeyValuePair<int, string>> Groups { get; }

            internal string First(int code) => Groups.Where(g => g.Key == code).Select(g => g.Value).FirstOrDefault();

            internal double Number(int code) => double.Parse(First(code), CultureInfo.InvariantCulture);

            internal double NumberOr(int code, double otherwise) => First(code) == null ? otherwise : Number(code);

            internal bool Has(int code) => Groups.Any(g => g.Key == code);
        }

        /// <summary>The records of the sections, by section: a record runs from its group 0 to the next.</summary>
        private static Dictionary<string, List<Record>> Sections(string text)
        {
            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            var sections = new Dictionary<string, List<Record>>();
            List<Record> current = null;
            Record record = null;
            bool header = false;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1];

                if (code == 0 && value == "SECTION")
                {
                    string name = lines[i + 3];
                    sections[name] = current = new List<Record>();
                    header = name == "HEADER";
                    record = null;
                    i += 2;
                    continue;
                }

                if (code == 0 && (value == "ENDSEC" || value == "EOF"))
                {
                    current = null;
                    record = null;
                    continue;
                }

                if (current == null)
                {
                    continue;
                }

                if (header ? code == 9 : code == 0)
                {
                    record = new Record(value);
                    current.Add(record);
                    continue;
                }

                record?.Groups.Add(new KeyValuePair<int, string>(code, value));
            }

            return sections;
        }

        /// <summary>The records of the block an INSERT of a handle in model space names, without its BLOCK and its ENDBLK.</summary>
        private static List<Record> BlockOfInsert(Dictionary<string, List<Record>> sections, string handle)
        {
            Record insert = sections["ENTITIES"].Single(r => r.Type == "INSERT" && r.First(5) == handle);
            return BlockNamed(sections, insert.First(2));
        }

        private static List<Record> BlockNamed(Dictionary<string, List<Record>> sections, string name)
        {
            List<Record> blocks = sections["BLOCKS"];
            int start = blocks.FindIndex(r => r.Type == "BLOCK" && r.First(2) == name);
            Assert.True(start >= 0, "no block " + name);
            return blocks.Skip(start + 1).TakeWhile(r => r.Type != "ENDBLK").ToList();
        }

        // ---- the drawing -----------------------------------------------------------------------------------------------------------

        private static DxfDatabase Drawing() => new DxfDatabase("AC1032");

        /// <summary>The text of a drawing with its REGION taken for a 3DSOLID, the ACIS data the same.</summary>
        private static DxfDatabase RegionAsSolid(DxfDatabase database) =>
            DxfDatabase.Parse(Regex.Replace(database.ToText(), @"(\r?\n\s*0\r?\n)REGION(\r?\n)", "${1}3DSOLID${2}"));

        private static AcisVec V(double x, double y, double z) => new AcisVec(x, y, z);

        /// <summary>A box 40 by 30 by 20 about the origin as the faces of a body: each face of four lines, counter-clockwise seen from outside.</summary>
        private static AcisBody BoxBody()
        {
            double[] half = { 20, 15, 10 };
            var faces = new List<AcisFace>();

            for (int axis = 0; axis < 3; axis++)
            {
                foreach (int side in new[] { 1, -1 })
                {
                    int u = (axis + 1) % 3;
                    int v = (axis + 2) % 3;
                    var corners = new List<AcisVec>();

                    foreach (int[] uv in new[] { new[] { -1, -1 }, new[] { 1, -1 }, new[] { 1, 1 }, new[] { -1, 1 } })
                    {
                        double[] p = new double[3];
                        p[axis] = side * half[axis];
                        p[u] = uv[0] * half[u];
                        p[v] = uv[1] * half[v];
                        corners.Add(V(p[0], p[1], p[2]));
                    }

                    if (side < 0)
                    {
                        corners.Reverse();
                    }

                    var lines = new List<AcisCurve>();

                    for (int i = 0; i < 4; i++)
                    {
                        lines.Add(AcisCurve.Line(corners[i], corners[(i + 1) % 4]));
                    }

                    var normal = new double[3];
                    normal[axis] = side;
                    faces.Add(
                        new AcisFace(
                            corners[0],
                            V(normal[0], normal[1], normal[2]),
                            new[] { new AcisLoop(lines, false) }
                        )
                    );
                }
            }

            return new AcisBody(faces);
        }

        /// <summary>A 3DSOLID in model space of the body given, of a drawing of R2018.</summary>
        private static DxfDatabase SolidOf(AcisBody body, out string handle)
        {
            DxfDatabase database = Drawing();
            DxfUnknownEntity region = DxfUnknownEntity.CreateRegion(new DxfRegionShape(body), database);
            database.ModelSpace.AppendEntity(region);
            handle = region.Handle;
            return RegionAsSolid(database);
        }

        /// <summary>
        /// A drawing with a 3DSOLID in model space whose ACIS data is the SAT text given, its records one to a line in the order of
        /// their numbers (a pointer is <c>$</c> and the number), after the three lines of its head; the solid's handle is given back.
        /// </summary>
        private static DxfDatabase SolidOfSat(string[] records, out string handle)
        {
            handle = "ABCD";
            var lines = new List<string>
            {
                "400 0 1 0",
                "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0",
                "1 9.9999999999999995e-007 1e-010",
            };
            lines.AddRange(records.Select(r => r + " #"));
            var entity = new StringBuilder();
            entity.Append("  0\n3DSOLID\n  5\nABCD\n100\nAcDbEntity\n  8\n0\n100\nAcDbModelerGeometry\n 70\n     1\n");

            foreach (KeyValuePair<int, string> group in DxfUnknownEntity.SatGroups(lines))
            {
                entity.Append(group.Key.ToString(CultureInfo.InvariantCulture).PadLeft(3)).Append('\n');
                entity.Append(group.Value).Append('\n');
            }

            entity.Append("100\nAcDb3dSolid\n");
            string text = Drawing().ToText().Replace("\r", string.Empty);
            int entities = text.IndexOf("ENTITIES\n", StringComparison.Ordinal);
            Match end = Regex.Match(text.Substring(entities), @"\n\s*0\nENDSEC");
            Assert.True(end.Success);
            int at = entities + end.Index + 1;
            return DxfDatabase.Parse(text.Substring(0, at) + entity + text.Substring(at));
        }

        // The SAT of a cylinder 20 high, 10 in radius, on the Z axis, its flat ends 15 above and below the XY plane: the side face
        // has a loop for the circle at each end, the ends a loop each.
        private static readonly string[] Cylinder =
        {
            "body $-1 $1 $-1 $2",
            "lump $-1 $-1 $3 $0",
            "transform $-1 1 0 0 0 1 0 0 0 1 0 0 0 1 no_rotate no_reflect no_shear",
            "shell $-1 $-1 $-1 $4 $-1 $1",
            "face $-1 $5 $7 $3 $-1 $9 forward single",
            "face $-1 $6 $8 $3 $-1 $10 forward single",
            "face $-1 $-1 $11 $3 $-1 $12 forward single",
            "loop $-1 $13 $14 $4",
            "loop $-1 $-1 $15 $5",
            "cone-surface $-1 0 0 0 0 0 1 10 0 0 1 I I 0 1 10 forward I I I I",
            "plane-surface $-1 0 0 -15 0 0 -1 -1 0 0 forward_v I I I I",
            "loop $-1 $-1 $16 $6",
            "plane-surface $-1 0 0 15 0 0 1 1 0 0 forward_v I I I I",
            "loop $-1 $-1 $17 $4",
            "coedge $-1 $14 $14 $15 $18 reversed $7 $-1",
            "coedge $-1 $15 $15 $14 $18 forward $8 $-1",
            "coedge $-1 $16 $16 $17 $19 forward $11 $-1",
            "coedge $-1 $17 $17 $16 $19 reversed $13 $-1",
            "edge $-1 $20 $20 $15 $22 forward",
            "edge $-1 $21 $21 $16 $23 forward",
            "vertex $-1 $18 $24",
            "vertex $-1 $19 $25",
            "ellipse-curve $-1 0 0 -15 0 0 -1 10 0 0 1 I I",
            "ellipse-curve $-1 0 0 15 0 0 1 10 0 0 1 I I",
            "point $-1 10 0 -15",
            "point $-1 10 0 15",
        };

        // The same with a point for its top: the side face's second loop is an edge of no curve at the apex (0, 0, 15).
        private static readonly string[] Cone =
        {
            "body $-1 $1 $-1 $2",
            "lump $-1 $-1 $3 $0",
            "transform $-1 1 0 0 0 1 0 0 0 1 0 0 0 1 no_rotate no_reflect no_shear",
            "shell $-1 $-1 $-1 $4 $-1 $1",
            "face $-1 $5 $6 $3 $-1 $8 forward single",
            "face $-1 $-1 $7 $3 $-1 $9 forward single",
            "loop $-1 $10 $11 $4",
            "loop $-1 $-1 $12 $5",
            "cone-surface $-1 0 0 0 0 0 1 5 0 0 1 I I -0.3162277660168379412 0.948683298050513768 5 forward I I I I",
            "plane-surface $-1 0 0 -15 0 0 -1 -1 0 0 forward_v I I I I",
            "loop $-1 $-1 $13 $4",
            "coedge $-1 $11 $11 $12 $14 reversed $6 $-1",
            "coedge $-1 $12 $12 $11 $14 forward $7 $-1",
            "coedge $-1 $13 $13 $-1 $15 reversed $10 $-1",
            "edge $-1 $16 $16 $12 $18 forward",
            "edge $-1 $17 $17 $13 $-1 forward",
            "vertex $-1 $14 $19",
            "vertex $-1 $15 $20",
            "ellipse-curve $-1 0 0 -15 0 0 -1 10 0 0 1 I I",
            "point $-1 10 0 -15",
            "point $-1 0 0 15",
        };

        // ---- a 3DSOLID ---------------------------------------------------------------------------------------------------------

        [Fact]
        public void ASolidBox_IsTheFourLinesOfEachOfItsSixFaces_EachWithItsFacesNormal()
        {
            DxfDatabase database = SolidOf(BoxBody(), out string handle);

            Dictionary<string, List<Record>> sections = Sections(R12(database, out IReadOnlyList<string> said));
            List<Record> block = BlockOfInsert(sections, handle);

            Assert.DoesNotContain(sections["ENTITIES"], r => r.Type == "3DSOLID");
            Assert.Equal(24, block.Count);
            Assert.All(block, r => Assert.Equal("LINE", r.Type));
            Assert.Contains(said, s => s.Contains("3DSOLID"));
            Assert.DoesNotContain(said, s => s.StartsWith("Left out", StringComparison.Ordinal));

            for (int face = 0; face < 6; face++)
            {
                List<Record> lines = block.Skip(face * 4).Take(4).ToList();
                double[] normal =
                {
                    lines[0].NumberOr(210, 0.0),
                    lines[0].NumberOr(220, 0.0),
                    lines[0].NumberOr(230, 1.0),
                };

                // The normal is that of the face the four lines bound, pointing out of the box, and is left out where it is (0, 0, 1).
                Assert.Equal(1.0, Math.Sqrt(normal.Sum(n => n * n)), 9);
                Assert.Equal(!lines[0].Has(210), normal[2] == 1.0);

                foreach (Record line in lines)
                {
                    double[] start = { line.Number(10), line.Number(20), line.Number(30) };
                    double[] end = { line.Number(11), line.Number(21), line.Number(31) };
                    double[] half = { 20, 15, 10 };
                    int axis = Array.FindIndex(normal, n => Math.Abs(n) > 0.5);

                    Assert.Equal(normal[axis] * half[axis], start[axis], 9);
                    Assert.Equal(normal[axis] * half[axis], end[axis], 9);
                    Assert.Equal(normal[0], line.NumberOr(210, 0.0), 12);
                    Assert.Equal(normal[1], line.NumberOr(220, 0.0), 12);
                    Assert.Equal(normal[2], line.NumberOr(230, 1.0), 12);
                }

                // The four lines join end to end round the face.
                for (int i = 0; i < 4; i++)
                {
                    Record next = lines[(i + 1) % 4];
                    Assert.Equal(lines[i].Number(11), next.Number(10), 9);
                    Assert.Equal(lines[i].Number(21), next.Number(20), 9);
                    Assert.Equal(lines[i].Number(31), next.Number(30), 9);
                }
            }
        }

        [Fact]
        public void ACylinder_IsTheFourCirclesOfItsFacesLoops_AboutTheNormalOfAFlatFaceElseTheirOwn()
        {
            DxfDatabase database = SolidOfSat(Cylinder, out string handle);

            Dictionary<string, List<Record>> sections = Sections(R12(database, out IReadOnlyList<string> said));
            List<Record> block = BlockOfInsert(sections, handle);

            // The side face's two loops first (the circle at each end, about the normal each curve has), then the bottom's and
            // the top's: a circle about (0, 0, -1) at 15 below stands at 15 in its own system, as AutoCAD writes it.
            Assert.Equal(new[] { "CIRCLE", "CIRCLE", "CIRCLE", "CIRCLE" }, block.Select(r => r.Type).ToArray());
            double[] z = { 15, 15, 15, 15 };
            bool[] down = { true, false, true, false };

            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(0.0, block[i].Number(10), 9);
                Assert.Equal(0.0, block[i].Number(20), 9);
                Assert.Equal(z[i], block[i].Number(30), 9);
                Assert.Equal(10.0, block[i].Number(40), 9);
                Assert.Equal(down[i], block[i].Has(210));

                if (down[i])
                {
                    Assert.Equal(-1.0, block[i].Number(230), 9);
                }
            }

            Assert.Contains(said, s => s.Contains("3DSOLID"));
        }

        [Fact]
        public void ACone_HasAPointAtItsApex_AsItsEdgeOfNoCurveIsAVertexAlone()
        {
            DxfDatabase database = SolidOfSat(Cone, out string handle);

            List<Record> block = BlockOfInsert(Sections(R12(database)), handle);

            Assert.Equal(new[] { "CIRCLE", "POINT", "CIRCLE" }, block.Select(r => r.Type).ToArray());
            Assert.Equal(0.0, block[1].Number(10), 9);
            Assert.Equal(0.0, block[1].Number(20), 9);
            Assert.Equal(15.0, block[1].Number(30), 9);
        }

        [Fact]
        public void ASolidWithAnEdgeThatIsASpline_KeepsTheOtherEdges_AndSaysWhatItLeftOut()
        {
            // The top circle of the cylinder made a spline: its two uses, by the side face and by the top, are left out.
            string[] records = (string[])Cylinder.Clone();
            records[23] = "intcurve-curve $-1 forward { exactcur nullbs }";
            DxfDatabase database = SolidOfSat(records, out string handle);

            Dictionary<string, List<Record>> sections = Sections(R12(database, out IReadOnlyList<string> said));
            List<Record> block = BlockOfInsert(sections, handle);

            Assert.Equal(2, block.Count);
            Assert.All(block, r => Assert.Equal("CIRCLE", r.Type));
            Assert.Contains(said, s => s.Contains("splines") && s.Contains("3DSOLID 2"));
        }

        [Fact]
        public void ASolidWithNoEdges_IsAnEmptyBlock_AsAutoCadWritesASphere()
        {
            DxfDatabase database = SolidOfSat(
                new[]
                {
                    "body $-1 $1 $-1 $2",
                    "lump $-1 $-1 $3 $0",
                    "transform $-1 1 0 0 0 1 0 0 0 1 0 0 0 1 no_rotate no_reflect no_shear",
                    "shell $-1 $-1 $-1 $4 $-1 $1",
                    "face $-1 $-1 $-1 $3 $-1 $5 forward single",
                    "sphere-surface $-1 0 0 0 15 1 0 0 0 0 1 forward_v I I I I",
                },
                out string handle
            );

            Dictionary<string, List<Record>> sections = Sections(R12(database, out IReadOnlyList<string> said));

            Assert.Empty(BlockOfInsert(sections, handle));
            Assert.DoesNotContain(said, s => s.StartsWith("Left out", StringComparison.Ordinal));
            Assert.Contains(said, s => s.Contains("3DSOLID 1"));
        }

        [Fact]
        public void ASolidInABlock_IsItsLinesWhereItStood_WithNoBlockOfItsOwn()
        {
            DxfDatabase database = Drawing();
            var block = new DxfBlockTableRecord { Name = "Plate" };
            database.BlockTable.Add(block);
            DxfUnknownEntity region = DxfUnknownEntity.CreateRegion(new DxfRegionShape(BoxBody()), database);
            block.AppendEntity(region);
            database.ModelSpace.AppendEntity(new DxfBlockReference(GeoPoint3.Origin, block));

            Dictionary<string, List<Record>> sections = Sections(R12(RegionAsSolid(database)));

            List<Record> inside = BlockNamed(sections, "PLATE");
            Assert.Equal(24, inside.Count(r => r.Type == "LINE"));
            Assert.DoesNotContain(sections["BLOCKS"], r =>
                r.Type == "BLOCK" && r.First(2).StartsWith("*U", StringComparison.Ordinal)
            );
        }

        [Fact]
        public void ARegionOffTheXyPlane_HasLinesThatCarryItsNormal()
        {
            double c = Math.Cos(0.7);
            double s = Math.Sin(0.7);
            var lines = new List<AcisCurve>
            {
                AcisCurve.Line(V(0, 0, 0), V(40, 0, 0)),
                AcisCurve.Line(V(40, 0, 0), V(40, 30 * c, 30 * s)),
                AcisCurve.Line(V(40, 30 * c, 30 * s), V(0, 30 * c, 30 * s)),
                AcisCurve.Line(V(0, 30 * c, 30 * s), V(0, 0, 0)),
            };
            DxfDatabase database = Drawing();
            DxfUnknownEntity region = DxfUnknownEntity.CreateRegion(
                new DxfRegionShape(AcisBody.Create(lines, new List<IReadOnlyList<AcisCurve>>(), 1E-6)),
                database
            );
            database.ModelSpace.AppendEntity(region);

            List<Record> block = BlockOfInsert(Sections(R12(database)), region.Handle);

            // The normal AutoCAD 2021 writes for the region: (0, -sin 0.7, cos 0.7).
            Assert.Equal(4, block.Count);
            Assert.All(block, r => Assert.Equal(0.0, r.Number(210), 9));
            Assert.All(block, r => Assert.Equal(-s, r.Number(220), 9));
            Assert.All(block, r => Assert.Equal(c, r.Number(230), 9));
        }

        // ---- the INSERT of a broken entity --------------------------------------------------------------------------------------

        [Fact]
        public void TheInsertOfABrokenEntity_IsOnTheCurrentLayerAndInTheCurrentColour_NotTheEntitys()
        {
            DxfDatabase database = Drawing();
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "Plates" });
            DxfUnknownEntity region = DxfUnknownEntity.CreateRegion(new DxfRegionShape(BoxBody()), database);
            region.Layer = "Plates";
            region.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 5);
            database.ModelSpace.AppendEntity(region);
            database.Cecolor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3);
            database.Clayer = "Plates";

            Dictionary<string, List<Record>> sections = Sections(R12(database));
            Record insert = sections["ENTITIES"].Single(r => r.Type == "INSERT" && r.First(5) == region.Handle);

            // The pieces carry the entity's look; the INSERT the drawing's current one.
            Assert.Equal("PLATES", insert.First(8));
            Assert.Equal("3", insert.First(62).Trim());
            List<Record> block = BlockNamed(sections, insert.First(2));
            Assert.All(block, r => Assert.Equal("PLATES", r.First(8)));
            Assert.All(block, r => Assert.Equal("5", r.First(62).Trim()));

            database.Clayer = "0";
            database.Cecolor = DxfColor.FromColorIndex(DxfColorMethod.ByLayer, 256);
            Record plain = Sections(R12(database))["ENTITIES"]
                .Single(r => r.Type == "INSERT" && r.First(5) == region.Handle);
            Assert.Equal("0", plain.First(8));
            Assert.False(plain.Has(62));
        }

        // ---- an MTEXT wrapped by the fonts ---------------------------------------------------------------------------------------

        /// <summary>A capital as txt.shx draws one: the pen down where it starts, up 6 and along 4, lifted, down 6 and along 2.</summary>
        private static readonly byte[] Capital = { 1, 0x64, 0x40, 2, 0x6C, 0x20, 0 };

        /// <summary>The bytes of a unifont of AutoCAD's form holding the capitals and the space: each capital 6 wide, 4 high at 4 high letters.</summary>
        private static byte[] Unifont()
        {
            var letters = new SortedDictionary<int, byte[]> { { ' ', new byte[] { 2, 0x60, 0 } }, { '?', Capital } };

            for (char c = 'A'; c <= 'Z'; c++)
            {
                letters.Add(c, Capital);
            }

            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 unifont 1.0\r\n\u001A"));
            byte[] header = Encoding.ASCII.GetBytes("GH TEST").Concat(new byte[] { 0, 6, 2, 2, 0, 0, 0 }).ToArray();
            bytes.AddRange(BitConverter.GetBytes((uint)(letters.Count + 1)));
            bytes.AddRange(BitConverter.GetBytes((ushort)header.Length));
            bytes.AddRange(header);

            foreach (KeyValuePair<int, byte[]> letter in letters)
            {
                byte[] record = new byte[] { 0 }
                    .Concat(letter.Value)
                    .ToArray();
                bytes.AddRange(BitConverter.GetBytes((ushort)letter.Key));
                bytes.AddRange(BitConverter.GetBytes((ushort)record.Length));
                bytes.AddRange(record);
            }

            return bytes.ToArray();
        }

        private static void InFolder(Action<string> test)
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-r12-fonts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                File.WriteAllBytes(Path.Combine(folder, "txt.shx"), Unifont());
                test(folder);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static DxfMText Words(DxfDatabase database, string contents, double width)
        {
            var mtext = new DxfMText
            {
                Contents = contents,
                TextHeight = 4,
                Width = width,
                Location = new GeoPoint3(10, 20, 0),
            };
            database.ModelSpace.AppendEntity(mtext);
            return mtext;
        }

        [Fact]
        public void AnMtextWrappedAtItsWidth_IsTheTextsOfItsLines_WhereTheDrawingHasFonts()
        {
            InFolder(folder =>
            {
                // Each letter 6 wide, a space 6: "ABC DEF GHI" is 66 and has to wrap at 40.
                DxfDatabase measured = Drawing();
                measured.FontSearchPath = folder;
                DxfMText mtext = Words(measured, "ABC DEF GHI JKL", 40);
                string handle = mtext.Handle;

                Dictionary<string, List<Record>> sections = Sections(R12(measured));
                List<Record> texts = BlockOfInsert(sections, handle);

                Assert.Equal(new[] { "ABC DEF", "GHI JKL" }, texts.Select(t => t.First(1)).ToArray());
                Assert.All(texts, t => Assert.Equal("TEXT", t.Type));

                // With no fonts it is laid out as before, a line to a paragraph.
                DxfDatabase blind = Drawing();
                string blindHandle = Words(blind, "ABC DEF GHI JKL", 40).Handle;
                List<Record> one = BlockOfInsert(Sections(R12(blind)), blindHandle);
                Assert.Single(one);
            });
        }

        [Fact]
        public void AnMtextOfOneDynamicColumn_AndOneWithALineFeedInItsWords_AreWrappedAndBrokenToo()
        {
            InFolder(folder =>
            {
                DxfDatabase database = Drawing();
                database.FontSearchPath = folder;
                DxfMText mtext = Words(database, "ABC DEF GHI JKL", 40);
                mtext.ColumnType = DxfColumnType.DynamicColumns;
                mtext.ColumnCount = 1;
                string handle = mtext.Handle;

                List<Record> texts = BlockOfInsert(Sections(R12(database)), handle);
                Assert.Equal(new[] { "ABC DEF", "GHI JKL" }, texts.Select(t => t.First(1)).ToArray());

                // A line feed among the words (written ^J, as AutoCAD 2021 writes between paragraphs) ends a paragraph.
                string text = database.ToText().Replace("ABC DEF GHI JKL", "ABC^JDEF GHI JKL");
                DxfDatabase fed = DxfDatabase.Parse(text);
                fed.FontSearchPath = folder;
                List<Record> lines = BlockOfInsert(Sections(R12(fed)), handle);
                Assert.Equal(new[] { "ABC", "DEF GHI", "JKL" }, lines.Select(t => t.First(1)).ToArray());
            });
        }

        // ---- dynamic blocks and hatches ------------------------------------------------------------------------------------------

        /// <summary>The dynamic block GH_PANEL, a rectangle 100 by 50 of four lines given Width from (0, 0) to (100, 0) and the stretch of its right edge.</summary>
        private static DxfBlockTableRecord Panel(DxfDatabase database)
        {
            var block = new DxfBlockTableRecord { Name = "GH_PANEL", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(block);
            var lines = new[]
            {
                new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0)),
            };

            foreach (DxfLine line in lines)
            {
                block.AppendEntity(line);
            }

            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                "Overall width of the panel",
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                DxfBlockParameterValueSet.Increments(50, 300, 10)
            );
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            return block;
        }

        [Fact]
        public void ADynamicBlock_IsAnAnonymousBlockOfR12_AndTheInsertsOfItNameIt()
        {
            DxfDatabase database = Drawing();
            DxfBlockTableRecord block = Panel(database);
            database.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), block));
            var stretched = new DxfBlockReference(new GeoPoint3(0, 200, 0), block);
            database.ModelSpace.AppendEntity(stretched);
            stretched.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == "Width").Value = 200.0;
            string stretchedName = stretched.AnonymousBlockTableRecord.Name;

            Dictionary<string, List<Record>> sections = Sections(R12(database, out IReadOnlyList<string> said));

            // The block of the table keeps its place, named *U and the first number above that of the anonymous block before
            // it, the first free where none is; the INSERT of it names that, the INSERT of its variant its own.
            string[] names = sections["BLOCKS"].Where(r => r.Type == "BLOCK").Select(r => r.First(2)).ToArray();
            Assert.DoesNotContain("GH_PANEL", names);
            Assert.Equal(names.Length, names.Distinct().Count());
            string[] inserted = sections["ENTITIES"].Where(r => r.Type == "INSERT").Select(r => r.First(2)).ToArray();
            Assert.Equal(2, inserted.Length);
            Assert.All(inserted, n => Assert.Matches("^[*]U[0-9]+$", n));
            Assert.All(inserted, n => Assert.Contains(n, names));
            Assert.Equal(2, inserted.Distinct().Count());
            Assert.Contains(stretchedName.ToUpperInvariant(), inserted);
            Assert.Equal(4, BlockNamed(sections, inserted[0]).Count(r => r.Type == "LINE"));
            Assert.Contains(said, s => s.Contains("GH_PANEL as " + inserted[0]));

            // A block named anonymous has the flag of one, or AutoCAD discards the drawing: "Invalid anonymous block".
            Assert.All(
                sections["BLOCKS"]
                    .Where(r => r.Type == "BLOCK" && r.First(2).StartsWith("*U", StringComparison.Ordinal)),
                r => Assert.Equal(1, int.Parse(r.First(70).Trim(), CultureInfo.InvariantCulture) & 1)
            );
        }

        [Fact]
        public void AHatchsInsert_KeepsTheColoursOfItsGradient_AndIsOfTheLinetypeContinuous()
        {
            DxfDatabase database = Drawing();
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) },
                new double[4]
            );
            database.ModelSpace.AppendEntity(hatch);
            hatch.XData = new[]
            {
                new DxfTypedValue(1001, "GradientColor1ACI"),
                new DxfTypedValue(1070, "5"),
                new DxfTypedValue(1001, "GradientColor2ACI"),
                new DxfTypedValue(1070, "2"),
            };

            Record insert = Sections(R12(database))["ENTITIES"]
                .Single(r => r.Type == "INSERT" && r.First(5) == hatch.Handle);

            Assert.Equal("CONTINUOUS", insert.First(6));
            Assert.False(insert.Has(62));
            string[] xdata = insert
                .Groups.Where(g => g.Key >= 1000)
                .Select(g => g.Key + "=" + g.Value.Trim())
                .ToArray();
            Assert.Equal(new[] { "1001=GRADIENTCOLOR1ACI", "1070=5", "1001=GRADIENTCOLOR2ACI", "1070=2" }, xdata);
        }

        // ---- what AutoCAD 2021 opens ---------------------------------------------------------------------------------------------

        [Fact]
        public void ASymbolsName_IsAsR12AllowsIt_InTheTableAndEveryEntityNamingIt()
        {
            DxfDatabase database = Drawing();
            string layer = "Plates {1} of the second floor north wing";
            string style = "{18b73a54-fa43-428c-adcb-c1c7bbdf2c84}";
            database.LayerTable.Add(new DxfLayerTableRecord { Name = layer });
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = style, FileName = "txt.shx" });
            var text = new DxfDBText
            {
                TextString = "x",
                Height = 1,
                Position = new GeoPoint3(0, 0, 0),
                Layer = layer,
                TextStyleName = style,
            };
            database.ModelSpace.AppendEntity(text);

            Dictionary<string, List<Record>> sections = Sections(R12(database));

            // A character R12 does not allow is an underscore, the name is cut to 26 characters: AutoCAD 2021 writes these, and
            // discards a drawing that holds another ("Improper table entry name").
            Assert.Contains(sections["TABLES"], r => r.Type == "LAYER" && r.First(2) == "PLATES__1__OF_THE_SECOND_F");
            Assert.Contains(sections["TABLES"], r => r.Type == "STYLE" && r.First(2) == "_18B73A54-FA43-428C-ADCB-C");
            Record drawn = sections["ENTITIES"].Single(r => r.Type == "TEXT" && r.First(5) == text.Handle);
            Assert.Equal("PLATES__1__OF_THE_SECOND_F", drawn.First(8));
            Assert.Equal("_18B73A54-FA43-428C-ADCB-C", drawn.First(7));
            Assert.All(
                sections["TABLES"]
                    .Where(r => r.Type != "APPID" && r.Has(2) && !r.First(2).StartsWith("*", StringComparison.Ordinal)),
                r => Assert.Matches("^[A-Z0-9$_-]{0,26}$", r.First(2))
            );
        }

        [Fact]
        public void EveryApplicationAnEntityNamesInItsExtendedData_IsInTheTableOfApplications()
        {
            DxfDatabase database = Drawing();
            var tagged = new DxfBlockTableRecord { Name = "Tagged" };
            database.BlockTable.Add(tagged);
            var definition = new DxfAttributeDefinition(new GeoPoint3(0, 0, 0), "N1", "NOTE", "Note", null);
            tagged.AppendEntity(definition);
            definition.LockPositionInBlock = true;
            // R2010 writes the version of an attribute (280) before its tag, and then whether its place is locked.
            database = DxfDatabase.Parse(
                Regex.Replace(database.ToText(), @"(AcDbAttributeDefinition\r?\n)", "${1}280\n0\n")
            );
            Dictionary<string, List<Record>> sections = Sections(R12(database));

            var applications = new HashSet<string>(
                sections["TABLES"].Where(r => r.Type == "APPID").Select(r => r.First(2)),
                StringComparer.OrdinalIgnoreCase
            );
            string[] named = sections["BLOCKS"]
                .Concat(sections["ENTITIES"])
                .SelectMany(r => r.Groups.Where(g => g.Key == 1001).Select(g => g.Value))
                .Distinct()
                .ToArray();

            // An attribute's flags are the extended data of AcDbAttr, which AutoCAD discards a drawing for if its table lacks.
            Assert.Contains("AcDbAttr", named);
            Assert.All(named, n => Assert.Contains(n, applications));
        }

        [Fact]
        public void TheVerticesAndTheEndOfAPolylineMadeOfALightweightOne_CarryItsLinetypeAndColour()
        {
            DxfDatabase database = Drawing();
            var lw = new DxfPolyline { Linetype = "ByBlock", Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 5) };
            lw.AddVertexAt(0, new GeoPoint2(0, 0), 0.0, 0.0, 0.0);
            lw.AddVertexAt(1, new GeoPoint2(10, 0), 0.0, 0.0, 0.0);
            database.ModelSpace.AppendEntity(lw);

            List<Record> entities = Sections(R12(database))["ENTITIES"];
            int at = entities.FindIndex(r => r.Type == "POLYLINE" && r.First(5) == lw.Handle);

            Assert.Equal("BYBLOCK", entities[at].First(6));
            foreach (Record part in entities.Skip(at + 1).Take(3))
            {
                Assert.Equal("BYBLOCK", part.First(6));
                Assert.Equal("5", part.First(62).Trim());
            }
        }
    }
}
