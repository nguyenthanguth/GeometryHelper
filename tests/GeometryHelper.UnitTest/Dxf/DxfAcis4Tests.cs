using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.UnitTest.Dwg;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The ACIS data of R2000 (ACIS 4.00) turned into the ASM data of the other versions and back. The answer is AutoCAD
    /// 2021's own DXF of the same drawing (solids_AC1015 and solids_AC1032: a region of a circle of radius 10 and a box 30
    /// by 20 by 10 moved to x = 50, in R2000 and in R2018). R2000 has no asmheader, so every pointer of it stands one
    /// lower; its records have no history numbers (a transform and an attribute only the number); an edge has no
    /// parameters and no convexity; a text is written as its length and the word, without the at sign; and the splines of
    /// an exact curve or surface have no word of fullness. A cone of AutoCAD's own (26 lines of R2000, 27 of R2004, with
    /// the edge of its apex that has no curve) is kept here in both forms.
    /// </summary>
    public class DxfAcis4Tests
    {
        #region Helpers

        /// <summary>The solids of a drawing: a region is made again by the save from the shape it holds, a solid is turned.</summary>
        private static List<DwgAnswerKey.Record> Solids(byte[] dxf) =>
            DwgAnswerKey.Parse(dxf).Where(r => r.Section == "ENTITIES" && r.Type == "3DSOLID").ToList();

        /// <summary>The bodies of a drawing, the region first: what the ACIS data of R2018 holds.</summary>
        private static List<DwgAnswerKey.Record> Bodies(byte[] dxf) =>
            DwgAnswerKey
                .Parse(dxf)
                .Where(r => r.Section == "ENTITIES" && (r.Type == "3DSOLID" || r.Type == "REGION"))
                .ToList();

        /// <summary>The lines of the SAT of an entity, as the groups 3 and 1 after its AcDbModelerGeometry hold them, deciphered.</summary>
        private static List<string> Sat(DwgAnswerKey.Record entity)
        {
            var lines = new List<string>();
            var line = new StringBuilder();
            bool inside = false;

            foreach (KeyValuePair<int, string> group in entity.Groups)
            {
                if (group.Key == 100)
                {
                    inside = group.Value == "AcDbModelerGeometry";
                }
                else if (inside && (group.Key == 3 || group.Key == 1))
                {
                    line.Append(group.Value);

                    if (group.Key == 1)
                    {
                        lines.Add(DxfUnknownEntity.Plain(line.ToString()));
                        line.Clear();
                    }
                }
            }

            return lines;
        }

        /// <summary>The SAB of an entity, from the record of the data store of its file that names its handle.</summary>
        private static byte[] Sab(byte[] dxf, DwgAnswerKey.Record entity)
        {
            string handle = entity.Groups.First(g => g.Key == 5).Value.Trim();
            DwgAnswerKey.Record data = DwgAnswerKey
                .Parse(dxf)
                .Single(r =>
                    r.Section == "ACDSDATA"
                    && r.Type == "ACDSRECORD"
                    && r.Groups.Any(g => g.Key == 320 && g.Value.Trim() == handle)
                );
            string hex = string.Concat(data.Groups.Where(g => g.Key == 310).Select(g => g.Value.Trim()));
            int size = int.Parse(data.Groups.First(g => g.Key == 94).Value.Trim(), CultureInfo.InvariantCulture);
            var bytes = new byte[size];

            for (int i = 0; i < size; i++)
            {
                bytes[i] = byte.Parse(hex.Substring(2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return bytes;
        }

        private static byte[] Saved(byte[] file, string version, out IReadOnlyList<string> said)
        {
            DxfDatabase db = Opened(file);

            using (var stream = new MemoryStream())
            {
                said = db.Save(stream, version);
                return stream.ToArray();
            }
        }

        private static byte[] Saved(byte[] file, string version) => Saved(file, version, out _);

        /// <summary>
        /// The SAT of a body that AutoCAD made, as AutoCAD writes it of a body it read from R2000: the number of a vertex (R2007
        /// and later) is 2 for each, as ACIS 4.00 does not tell at which end of its edge a vertex stands.
        /// </summary>
        private static string AsRead(string sat) =>
            Regex.Replace(sat, @"^(vertex \$-1 -1 \$-1 \$\d+) [01] ", "$1 2 ", RegexOptions.Multiline);

        /// <summary>The lines of the SAT a call gave, each ended by a line feed.</summary>
        private static List<string> Lines(string sat) =>
            new List<string>(sat.Substring(0, sat.Length - 1).Split((char)10));

        private static string Joined(IEnumerable<string> lines) => string.Join("\n", lines) + "\n";

        #endregion

        #region To R2000

        [Fact]
        public void TheSolidOfR2018_SavedAsR2000_IsTheSatOfACIS4AutoCadWritesOfIt()
        {
            List<DwgAnswerKey.Record> key = Solids(DwgAnswerKey.Key("solids_AC1015"));
            byte[] saved = Saved(DwgAnswerKey.Key("solids_AC1032"), "AC1015", out IReadOnlyList<string> said);
            List<DwgAnswerKey.Record> solids = Solids(saved);

            Assert.Single(key);
            Assert.Single(solids);
            Assert.Empty(said);
            Assert.Equal(Sat(key[0]), Sat(solids[0]));
            Assert.Equal(2, Bodies(saved).Count);

            // The data store is for R2013 and later alone.
            Assert.DoesNotContain(DwgAnswerKey.Parse(saved), r => r.Section == "ACDSDATA");
        }

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        [InlineData("AC1027")]
        public void TheSolidOfEveryVersion_SavedAsR2000_IsTheSatOfACIS4AutoCadWritesOfIt(string version)
        {
            // The solid of the version is the one of R2018 turned into it, which equals AutoCAD's own.
            byte[] first = Saved(DwgAnswerKey.Key("solids_AC1032"), version, out IReadOnlyList<string> saidFirst);
            byte[] saved = Saved(first, "AC1015", out IReadOnlyList<string> said);
            List<DwgAnswerKey.Record> key = Solids(DwgAnswerKey.Key("solids_AC1015"));
            List<DwgAnswerKey.Record> solids = Solids(saved);

            Assert.Empty(saidFirst);
            Assert.Empty(said);
            Assert.Single(solids);
            Assert.Equal(Sat(key[0]), Sat(solids[0]));
        }

        [Fact]
        public void TheSatOfEachBodyOfR2018_TurnedIntoACIS4_IsTheSatAutoCadWritesOfIt()
        {
            // The region as well as the solid: the data of R2018 itself, in the form of R2000.
            byte[] r2018 = DwgAnswerKey.Key("solids_AC1032");
            List<DwgAnswerKey.Record> key = Bodies(DwgAnswerKey.Key("solids_AC1015"));
            List<DwgAnswerKey.Record> bodies = Bodies(r2018);
            AcisFormat form = AcisFormat.For("AC1015");

            Assert.Equal(2, key.Count);
            Assert.Equal(key.Count, bodies.Count);

            for (int i = 0; i < key.Count; i++)
            {
                Assert.Equal(Joined(Sat(key[i])), DwgSat.ToSat(Sab(r2018, bodies[i]), form));
            }
        }

        #endregion

        #region From R2000

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        public void TheSolidOfR2000_SavedAsR2004ToR2010_IsTheSatAutoCadWritesOfIt(string version)
        {
            byte[] r2018 = DwgAnswerKey.Key("solids_AC1032");
            AcisFormat form = AcisFormat.For(version);
            List<DwgAnswerKey.Record> bodies = Solids(r2018);
            byte[] saved = Saved(DwgAnswerKey.Key("solids_AC1015"), version, out IReadOnlyList<string> said);
            List<DwgAnswerKey.Record> solids = Solids(saved);

            Assert.Empty(said);
            Assert.Single(solids);

            // The SAB of R2018 turned into the text of the version is AutoCAD's text of it, which R2000 turned up is too.
            string answer = DwgSat.ToSat(Sab(r2018, bodies[0]), form);
            Assert.Equal(version == "AC1018" ? answer : AsRead(answer), Joined(Sat(solids[0])));
            Assert.DoesNotContain(solids[0].Groups, g => g.Key == 2 || g.Key == 290);
        }

        [Theory]
        [InlineData("AC1027")]
        [InlineData("AC1032")]
        public void TheSolidOfR2000_SavedAsR2013AndR2018_IsTheSabAutoCadWritesOfIt(string version)
        {
            byte[] r2018 = DwgAnswerKey.Key("solids_AC1032");
            AcisFormat form = AcisFormat.For(version);
            List<DwgAnswerKey.Record> bodies = Solids(r2018);
            byte[] saved = Saved(DwgAnswerKey.Key("solids_AC1015"), version, out IReadOnlyList<string> said);
            List<DwgAnswerKey.Record> solids = Solids(saved);

            Assert.Empty(said);
            Assert.Single(solids);
            Assert.Equal(AsRead(DwgSat.ToSat(Sab(r2018, bodies[0]), form)), DwgSat.ToSat(Sab(saved, solids[0]), form));
            Assert.Contains(solids[0].Groups, g => g.Key == 290);
            Assert.DoesNotContain(solids[0].Groups, g => g.Key == 1);
        }

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        [InlineData("AC1027")]
        [InlineData("AC1032")]
        public void TheSatOfEachBodyOfR2000_TurnedIntoAnotherVersion_IsTheDataAutoCadWritesOfIt(string version)
        {
            // The region as well as the solid, as text up to R2010 and as SAB (read into text to be compared) from R2013.
            byte[] r2018 = DwgAnswerKey.Key("solids_AC1032");
            List<DwgAnswerKey.Record> key = Bodies(DwgAnswerKey.Key("solids_AC1015"));
            List<DwgAnswerKey.Record> bodies = Bodies(r2018);
            AcisFormat form = AcisFormat.For(version);

            for (int i = 0; i < key.Count; i++)
            {
                string answer = DwgSat.ToSat(Sab(r2018, bodies[i]), form);
                answer = version == "AC1018" ? answer : AsRead(answer);
                string made = form.Binary
                    ? DwgSat.ToSat(DwgSat.ToSab(Sat(key[i]), form, string.Empty), form)
                    : DwgSat.ToSat(Sat(key[i]), form);

                Assert.NotNull(made);
                Assert.Equal(answer, made);
            }
        }

        [Fact]
        public void TheSolidsOfR2000_GoingUpAndDownAgain_AreTheSatTheyHad()
        {
            byte[] r2000 = DwgAnswerKey.Key("solids_AC1015");
            List<DwgAnswerKey.Record> key = Solids(r2000);

            foreach (string version in new[] { "AC1018", "AC1021", "AC1024", "AC1027", "AC1032" })
            {
                byte[] up = Saved(r2000, version, out IReadOnlyList<string> said);
                byte[] down = Saved(up, "AC1015", out IReadOnlyList<string> saidDown);
                List<DwgAnswerKey.Record> back = Solids(down);

                Assert.Empty(said);
                Assert.Empty(saidDown);
                Assert.Single(back);
                Assert.Equal(Sat(key[0]), Sat(back[0]));
            }
        }

        [Fact]
        public void ASolidOfR2000WithSplines_IsLeftOutOfR2013_AndKeptInTheOthers()
        {
            // The spline surfaces and their parameter curves of ACIS 4.00 are other curves than the ones ASM writes of the same
            // body (R2000 has 110 and 187 lines where R2004 has 112 and 216): no rule turns the one into the other.
            byte[] r2000 = DwgAnswerKey.Key("splines_AC1015");
            int solids = Solids(r2000).Count;

            Assert.True(solids > 0);

            byte[] as2013 = Saved(r2000, "AC1027", out IReadOnlyList<string> said);
            Assert.Empty(Solids(as2013));
            Assert.Contains(said, s => s.Contains("3DSOLID " + solids.ToString(CultureInfo.InvariantCulture)));

            List<DwgAnswerKey.Record> as2010 = Solids(Saved(r2000, "AC1024"));
            Assert.Equal(solids, as2010.Count);
            Assert.All(as2010, solid => Assert.StartsWith("400 ", Sat(solid)[0], StringComparison.Ordinal));
        }

        #endregion

        #region A cone

        private static readonly string[] Cone400 =
        {
            "400 23 1 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "body $-1 $1 $-1 $2 #",
            "lump $-1 $-1 $3 $0 #",
            "transform $-1 1 0 0 0 1 0 0 0 1 0 0 0 1 no_rotate no_reflect no_shear #",
            "shell $-1 $-1 $-1 $4 $-1 $1 #",
            "face $5 $6 $7 $3 $-1 $8 forward single #",
            "persubent-acadSolidHistory-attrib $-1 $-1 $-1 $4 1 1 1000000000 1001 #",
            "face $9 $-1 $10 $3 $-1 $11 forward single #",
            "loop $-1 $12 $13 $4 #",
            "cone-surface $-1 0 0 0 0 0 1 4 0 0 1 I I -0.3713906763541037215 0.9284766908852594147 4 forward I I I I #",
            "persubent-acadSolidHistory-attrib $-1 $-1 $-1 $6 1 1 1000000000 1002 #",
            "loop $-1 $-1 $14 $6 #",
            "plane-surface $-1 0 0 -10 0 0 -1 -1 0 0 forward_v I I I I #",
            "loop $-1 $-1 $15 $4 #",
            "coedge $-1 $13 $13 $14 $16 reversed $7 $-1 #",
            "coedge $-1 $14 $14 $13 $16 forward $10 $-1 #",
            "coedge $-1 $15 $15 $-1 $17 reversed $12 $-1 #",
            "edge $-1 $18 $18 $14 $19 forward #",
            "edge $-1 $20 $20 $15 $-1 forward #",
            "vertex $-1 $16 $21 #",
            "ellipse-curve $-1 0 0 -10 0 0 -1 8 0 0 1 I I #",
            "vertex $-1 $17 $22 #",
            "point $-1 8 0 -10 #",
            "point $-1 0 0 10 #",
        };

        private static readonly string[] Cone208 =
        {
            "20800 24 2 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "asmheader $-1 -1 @13 226.0.0.65535 #",
            "body $-1 -1 $-1 $2 $-1 $3 #",
            "lump $-1 -1 $-1 $-1 $4 $1 #",
            "transform $-1 -1 1 0 0 0 1 0 0 0 1 0 0 0 1 no_rotate no_reflect no_shear #",
            "shell $-1 -1 $-1 $-1 $-1 $5 $-1 $2 #",
            "face $6 -1 $-1 $7 $8 $4 $-1 $9 forward single #",
            "persubent-acadSolidHistory-attrib $-1 -1 $-1 $-1 $5 1 1 1000000000 1001 #",
            "face $10 -1 $-1 $-1 $11 $4 $-1 $12 forward single #",
            "loop $-1 -1 $-1 $13 $14 $5 #",
            "cone-surface $-1 -1 $-1 0 0 0 0 0 1 4 0 0 1 I I -0.3713906763541037215 0.9284766908852594147 4 forward I I I I #",
            "persubent-acadSolidHistory-attrib $-1 -1 $-1 $-1 $7 1 1 1000000000 1002 #",
            "loop $-1 -1 $-1 $-1 $15 $7 #",
            "plane-surface $-1 -1 $-1 0 0 -10 0 0 -1 -1 0 0 forward_v I I I I #",
            "loop $-1 -1 $-1 $-1 $16 $5 #",
            "coedge $-1 -1 $-1 $14 $14 $15 $17 reversed $8 $-1 #",
            "coedge $-1 -1 $-1 $15 $15 $14 $17 forward $11 $-1 #",
            "coedge $-1 -1 $-1 $16 $16 $-1 $18 reversed $13 $-1 #",
            "edge $-1 -1 $-1 $19 0 $19 6.283185307179586232 $15 $20 forward @7 unknown #",
            "edge $-1 -1 $-1 $21 1 $21 0 $16 $-1 forward @7 unknown #",
            "vertex $-1 -1 $-1 $17 $22 #",
            "ellipse-curve $-1 -1 $-1 0 0 -10 0 0 -1 8 0 0 1 I I #",
            "vertex $-1 -1 $-1 $18 $23 #",
            "point $-1 -1 $-1 8 0 -10 #",
            "point $-1 -1 $-1 0 0 10 #",
        };

        [Fact]
        public void ACone_OfR2000_GoingUp_GetsTheHeaderTheHistoryAndTheParametersOfItsEdges()
        {
            string up = DwgSat.ToSat(Cone400, AcisFormat.For("AC1018"));

            Assert.Equal(Joined(Cone208), up);

            // The circle runs a turn from its vertex at angle nought; the edge of the apex has no curve: 1 and 0.
            Assert.Contains("edge $-1 -1 $-1 $19 0 $19 6.283185307179586232 $15 $20 forward @7 unknown #", up);
            Assert.Contains("edge $-1 -1 $-1 $21 1 $21 0 $16 $-1 forward @7 unknown #", up);
        }

        [Fact]
        public void ACone_OfR2004_GoingToR2000_LosesTheHeaderTheHistoryAndTheParametersOfItsEdges()
        {
            string down = DwgSat.ToSat(Cone208, AcisFormat.For("AC1015"));

            Assert.Equal(Joined(Cone400), down);
            Assert.StartsWith("400 23 1 0 ", down, StringComparison.Ordinal);
        }

        [Fact]
        public void ACone_OfR2000_GoingUpToR2007_GivesItsVerticesTheNumberAutoCadGivesWhenItReadsR2000()
        {
            // AutoCAD knows no end of the vertices of ACIS 4.00: it writes 2 for each, not 0 and 1 as for a body it made.
            string up = DwgSat.ToSat(Cone400, AcisFormat.For("AC1021"));

            Assert.Contains("vertex $-1 -1 $-1 $17 2 $22 #", up);
            Assert.Contains("vertex $-1 -1 $-1 $18 2 $23 #", up);
        }

        [Fact]
        public void ACone_OfR2000_GoingUpToR2013_IsTheSabOfTheSameRecords()
        {
            byte[] sab = DwgSat.ToSab(Cone400, AcisFormat.For("AC1027"), string.Empty);

            Assert.NotNull(sab);
            Assert.Equal(
                AsRead(DwgSat.ToSat(Cone208, AcisFormat.For("AC1027"))),
                DwgSat.ToSat(sab, AcisFormat.For("AC1027"))
            );
        }

        [Fact]
        public void AnEdgeOfR2000_RunningAgainstItsCurve_IsRefused()
        {
            var reversed = Cone400
                .Select(l =>
                    l.StartsWith("edge $-1 $18", StringComparison.Ordinal) ? l.Replace("forward", "reversed") : l
                )
                .ToList();

            Assert.Contains(reversed, l => l.Contains("reversed #") && l.StartsWith("edge", StringComparison.Ordinal));
            Assert.Null(DwgSat.ToSat(reversed, AcisFormat.For("AC1018")));
        }

        private static List<string> Records400(int count, params string[] records)
        {
            var lines = new List<string>
            {
                "400 " + count.ToString(CultureInfo.InvariantCulture) + " 1 0           ",
                "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
                "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            };
            lines.AddRange(records);
            return lines;
        }

        [Fact]
        public void ATransformThatTurnsTheBody_IsTheWordRotate_InEveryVersion()
        {
            // The box of a solid turned 30 degrees about z and 20 about x (AutoCAD 2021's own transform of it).
            List<string> turned = Records400(
                3,
                "body $-1 $1 $-1 $2 #",
                "lump $-1 $-1 $-1 $0 #",
                "transform $-1 0.8660247915829388798 0.4698414665957796332 0.1710264793266062788 -0.5000010603626028338 0.8137869905526818881 0.2962257140146377643 0 -0.3420522332544198574 0.939680940386503738 10 20 30 1 rotate no_reflect no_shear #"
            );

            foreach (string version in new[] { "AC1018", "AC1021", "AC1024" })
            {
                string up = DwgSat.ToSat(turned, AcisFormat.For(version));

                Assert.Contains(" 10 20 30 1 rotate no_reflect no_shear #", up);
                Assert.Equal(Joined(turned), DwgSat.ToSat(Lines(up), AcisFormat.For("AC1015")));
            }

            Assert.NotNull(DwgSat.ToSab(turned, AcisFormat.For("AC1032"), string.Empty));
        }

        [Fact]
        public void ACurveOfIntersectionOfR2000_GoingToR2010_IsDatedByTheVersionItWasReadFrom()
        {
            // The spline of an exact curve has no word of fullness in R2000; R2010 dates it 20900 where AutoCAD read it from
            // ACIS 4.00 (22600 where it made the curve). The curve here runs from (8, 6, -10) to (8, -6, -10), a cone and a plane.
            List<string> curve = Records400(
                1,
                "intcurve-curve $-1 forward { exactcur nurbs 2 open 2 ",
                " 0 2 15.62049935181330085 2 ",
                " 8.000000000000001776 5.999999999999998224 -10 1 ",
                " 8.000000000000001776 0 -1.000000000000005329 1.249999999999999556 ",
                " 8.000000000000001776 -5.999999999999998224 -10 1 ",
                " 0 ",
                " ",
                " null_surface ",
                " null_surface ",
                " nullbs ",
                " nullbs ",
                " I I ",
                " 0 ",
                " 0 ",
                " 0 ",
                " F 1 F 0 } I I #"
            );

            Assert.Contains("{ exact_int_cur 20900 full nurbs 2 open 2", DwgSat.ToSat(curve, AcisFormat.For("AC1024")));
            Assert.Contains("{ exactcur full nurbs 2 open 2", DwgSat.ToSat(curve, AcisFormat.For("AC1018")));
            Assert.Contains("{ exact_int_cur full nurbs 2 open 2", DwgSat.ToSat(curve, AcisFormat.For("AC1021")));

            string up = DwgSat.ToSat(curve, AcisFormat.For("AC1024"));
            Assert.Equal(Joined(curve), DwgSat.ToSat(Lines(up), AcisFormat.For("AC1015")));
        }

        [Fact]
        public void TheTextOfAnAttributeOfR2000_IsItsLengthAndWords_AndASmallerNumberOfEnums()
        {
            var lines = new List<string>
            {
                "400 3 1 0           ",
                "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
                "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
                "body $-1 $1 $-1 $-1 #",
                "lump $-1 $-1 $-1 $0 #",
                "int64_attrib-name_attrib-gen-attrib $-1 $-1 $0 $-1 copy keep_all ignore 13 loft_end_face 0 #",
            };

            string up = DwgSat.ToSat(lines, AcisFormat.For("AC1018"));

            Assert.NotNull(up);
            Assert.Contains(
                "int64_attrib-name_attrib-gen-attrib $-1 -1 $-1 $1 $-1 copy keep_all ignore copy @13 loft_end_face 0 #",
                up
            );
            Assert.Equal(
                Joined(lines),
                DwgSat.ToSat(up.Substring(0, up.Length - 1).Split('\n'), AcisFormat.For("AC1015"))
            );
        }

        #endregion
    }
}
