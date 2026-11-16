using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The associative network of a dynamic block saved as a DXF of R2010 or R2007. AutoCAD 2021 writes a network, a variable
    /// and a constraint group of a drawing of R2018 there in the first layout: the action at version 1 without its four
    /// numbers more, and the group with its nodes by identifier, each with its class, its group, its identifier and byte and
    /// what its class holds, a circle and an arc with three reals where the second layout has four. The DWG writer taught
    /// the library that layout; the DXF saves hold it now, and the DWG path reads the same text.
    /// </summary>
    public class DxfSaveVersionConstraintsTests
    {
        #region Records

        private static string G(string groups) =>
            string.Concat(
                groups
                    .Split('|')
                    .Select(g =>
                        g.Substring(0, g.IndexOf('=')).PadLeft(3) + "\n" + g.Substring(g.IndexOf('=') + 1) + "\n"
                    )
            );

        private static string Record(string type, string handle, string owner, string groups) =>
            G("0=" + type + "|5=" + handle + "|330=" + owner + "|" + groups);

        /// <summary>A network of <paramref name="version"/> (the version 2 holds four numbers more) that owns the actions given.</summary>
        private static string Network(string version, params string[] actions) =>
            Record(
                "ACDBASSOCNETWORK",
                "F200",
                "F100",
                "100=AcDbAssocAction|90="
                    + version
                    + "|90=0|330=0|360=0|90=1|90=0|90=0"
                    + (version == "2" ? "|90=0|90=0|90=0|90=0" : string.Empty)
                    + "|100=AcDbAssocNetwork|90=0|90=12|90="
                    + actions.Length.ToString(CultureInfo.InvariantCulture)
                    + string.Concat(actions.Select(a => "|360=" + a))
                    + "|90=0"
            );

        private static string Variable(string version) =>
            Record(
                "ACDBASSOCVARIABLE",
                "F202",
                "F200",
                "100=AcDbAssocAction|90="
                    + version
                    + "|90=0|330=F200|360=0|90=9|90=0|90=0"
                    + (version == "2" ? "|90=0|90=0|90=0|90=0" : string.Empty)
                    + "|100=AcDbAssocVariable|90=2|1=Size|1=|1=AcDbCalc:1.0|1=|40=0.0625|290=0|290=0|90=0"
            );

        private const string BoundedLine1 =
            "1=AcConstrainedBoundedLine|330=F201|90=1|70=0|90=1|90=3|330=F203|90=0"
            + "|10=0.0|20=4.0|30=0.0|10=0.0|20=-1.0|30=0.0|290=0|10=0.0|20=4.0|30=0.0|11=36.0|21=4.0|31=0.0";

        private const string ImplicitPoint1 =
            "1=AcConstrainedImplicitPoint|330=F201|90=2|70=0|90=1|90=3|330=0|90=0|280=1|90=-1|90=1";

        private const string Distance1 =
            "1=AcDistanceConstraint|330=F201|90=4|70=4|90=2|90=2|90=2|90=0|290=0|290=1"
            + "|340=F203|340=F203|280=1|10=1.0|20=0.0|30=0.0";

        private const string BoundedLine2 =
            "90=1|90=1|90=3|70=0|330=F203|90=0"
            + "|10=0.0|20=4.0|30=0.0|10=0.0|20=-1.0|30=0.0|290=0|10=0.0|20=4.0|30=0.0|11=36.0|21=4.0|31=0.0";

        private const string ImplicitPoint2 = "90=2|90=1|90=3|70=0|330=0|90=0|280=1|90=-1|90=1";

        private const string Distance2 =
            "90=4|90=2|90=2|90=2|70=4|90=0|290=0|290=1|340=F203|340=F203|280=1|10=1.0|20=0.0|30=0.0";

        private const string Plane =
            "|10=0.0|20=0.0|30=0.0|10=1.0|20=0.0|30=0.0|10=0.0|20=1.0|30=0.0|360=0|90=1|360=F203";

        /// <summary>The group in the layout of AutoCAD 2010 and 2007: nodes with their class, group, identifier and byte.</summary>
        private static string Group1(string nodes = null) =>
            Record(
                "ACDBASSOC2DCONSTRAINTGROUP",
                "F201",
                "F200",
                "100=AcDbAssocAction|90=1|90=0|330=F200|360=0|90=3|90=7|90=0|90=2|70=0"
                    + Plane
                    + "|90=8|90=3|"
                    + (nodes ?? BoundedLine1 + "|" + ImplicitPoint1 + "|" + Distance1)
            );

        /// <summary>The group in the layout of AutoCAD 2013 and 2018: a directory of classes and identifiers, then the nodes.</summary>
        private static string Group2(string directory = null, string bodies = null) =>
            Record(
                "ACDBASSOC2DCONSTRAINTGROUP",
                "F201",
                "F200",
                "100=AcDbAssocAction|90=2|90=0|330=F200|360=0|90=3|90=7|90=0|90=0|90=0|90=0|90=0|90=2|70=0"
                    + Plane
                    + "|90=8|90=0|90=0|290=0|90=3|"
                    + (
                        directory
                        ?? "1=AcConstrainedBoundedLine|90=1|1=AcConstrainedImplicitPoint|90=2|1=AcDistanceConstraint|90=4"
                    )
                    + "|"
                    + (bodies ?? BoundedLine2 + "|" + ImplicitPoint2 + "|" + Distance2)
            );

        private static string File(string version, params string[] objects) =>
            G("0=SECTION|2=HEADER|9=$ACADVER|1=" + version)
            + G("0=ENDSEC|0=SECTION|2=ENTITIES|0=ENDSEC|0=SECTION|2=OBJECTS")
            + G("0=DICTIONARY|5=F100|330=0|100=AcDbDictionary|281=1|3=ACAD_ASSOCNETWORK|350=F200")
            + string.Concat(objects)
            + G("0=ENDSEC|0=EOF");

        private static string R2018(params string[] objects) => File("AC1032", objects);

        /// <summary>The drawing saved as the version, as the text of its file.</summary>
        private static string As(string text, string version)
        {
            DxfDatabase db = DxfDatabase.Parse(text);

            using (var stream = new MemoryStream())
            {
                db.Save(stream, version);
                return new UTF8Encoding(false).GetString(stream.ToArray());
            }
        }

        private static List<KeyValuePair<int, string>> Groups(string text, string handle)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            var all = new List<KeyValuePair<int, string>>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                all.Add(
                    new KeyValuePair<int, string>(
                        int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture),
                        lines[i + 1].Trim()
                    )
                );
            }

            int start = all.FindIndex(g => g.Key == 5 && g.Value == handle);
            Assert.True(start > 0, "No record " + handle);
            int end = all.FindIndex(start, g => g.Key == 0);
            return all.GetRange(start - 1, (end < 0 ? all.Count : end) - start + 1);
        }

        private static string Show(IEnumerable<KeyValuePair<int, string>> groups) =>
            string.Join("|", groups.Select(g => g.Key + "=" + g.Value));

        #endregion

        [Fact]
        public void ANetworkAndAVariableOfR2018_SavedAsR2010_HoldTheFirstVersionOfAnAction()
        {
            // The version 1, without the four numbers the version 2 adds after its three.
            string text = R2018(Network("2", "F201", "F202"), Variable("2"), Group2());
            string saved = As(text, "AC1024");

            Assert.Equal(Show(Groups(R2018(Network("1", "F201", "F202")), "F200")), Show(Groups(saved, "F200")));
            Assert.Equal(Show(Groups(R2018(Variable("1")), "F202")), Show(Groups(saved, "F202")));
        }

        [Fact]
        public void ANetworkAndAVariableOfR2018_SavedAsR2007_HoldTheFirstVersionOfAnAction()
        {
            string saved = As(R2018(Network("2", "F201", "F202"), Variable("2"), Group2()), "AC1021");

            Assert.Equal(Show(Groups(R2018(Variable("1")), "F202")), Show(Groups(saved, "F202")));
        }

        [Fact]
        public void ANetworkAndAVariableOfR2018_SavedAsR2013OrR2018_StayAtTheSecondVersion()
        {
            string text = R2018(Network("2", "F201", "F202"), Variable("2"), Group2());

            foreach (string version in new[] { "AC1027", "AC1032" })
            {
                string saved = As(text, version);

                Assert.Equal(Show(Groups(text, "F202")), Show(Groups(saved, "F202")));
                Assert.Equal(Show(Groups(text, "F201")), Show(Groups(saved, "F201")));
            }
        }

        [Fact]
        public void ANetworkAndAVariableOfR2010_SavedAsR2007_AreLeftAsTheyAre()
        {
            string text = File("AC1024", Network("1", "F201", "F202"), Variable("1"), Group1());
            string saved = As(text, "AC1021");

            Assert.Equal(Show(Groups(text, "F202")), Show(Groups(saved, "F202")));
            Assert.Equal(Show(Groups(text, "F201")), Show(Groups(saved, "F201")));
        }

        [Fact]
        public void AConstraintGroupOfR2018_SavedAsR2010_IsInTheFirstLayout()
        {
            // The group of the smallest of AutoCAD's samples, as AutoCAD 2021's DXF of the drawing saved as R2018 and as R2010 has
            // it: the nodes by identifier, each with its class and its group.
            string saved = As(R2018(Network("2", "F201", "F202"), Variable("2"), Group2()), "AC1024");

            Assert.Equal(Show(Groups(File("AC1024", Group1()), "F201")), Show(Groups(saved, "F201")));
        }

        [Fact]
        public void AConstraintGroupOfR2018_SavedAsR2007_IsInTheFirstLayout()
        {
            string saved = As(R2018(Network("2", "F201", "F202"), Variable("2"), Group2()), "AC1021");

            Assert.Equal(Show(Groups(File("AC1024", Group1()), "F201")), Show(Groups(saved, "F201")));
        }

        [Fact]
        public void TheNodesOfAConstraintGroupOfR2018_AreSavedAsR2010ByIdentifier()
        {
            // The directory and the bodies given the wrong way round come out by identifier.
            string text = R2018(
                Network("2", "F201", "F202"),
                Variable("2"),
                Group2(
                    "1=AcDistanceConstraint|90=4|1=AcConstrainedImplicitPoint|90=2|1=AcConstrainedBoundedLine|90=1",
                    Distance2 + "|" + ImplicitPoint2 + "|" + BoundedLine2
                )
            );

            Assert.Equal(Show(Groups(File("AC1024", Group1()), "F201")), Show(Groups(As(text, "AC1024"), "F201")));
        }

        [Fact]
        public void ACircleOfAConstraintGroupOfR2018_LosesItsFourthRealWhenSavedAsR2010()
        {
            string circle2 =
                "90=1|90=1|90=3|70=0|330=F203|90=0"
                + "|10=1.0|20=2.0|30=0.0|10=0.0|20=0.0|30=1.0|10=1.0|20=0.0|30=0.0|40=25.4|40=0.0|40=6.28|40=0.0";
            string text = R2018(
                Network("2", "F201", "F202"),
                Variable("2"),
                Group2(
                    "1=AcConstrainedCircle|90=1|1=AcConstrainedImplicitPoint|90=2|1=AcDistanceConstraint|90=4",
                    circle2 + "|" + ImplicitPoint2 + "|" + Distance2
                )
            );
            List<KeyValuePair<int, string>> group = Groups(As(text, "AC1024"), "F201");

            Assert.Equal(new[] { "25.4", "0.0", "6.28" }, group.Where(g => g.Key == 40).Select(g => g.Value));
        }

        [Fact]
        public void AConstraintGroupOfANodeNotKnown_IsLeftAsItIsWhenSavedAsR2010()
        {
            string text = R2018(
                Network("2", "F201", "F202"),
                Variable("2"),
                Group2(
                    "1=AcConstrainedBoundedLine|90=1|1=AcConstrainedImplicitPoint|90=2|1=AcMysteryConstraint|90=4",
                    BoundedLine2 + "|" + ImplicitPoint2 + "|90=4|90=0|70=0"
                )
            );
            string saved = As(text, "AC1024");

            Assert.Equal(Show(Groups(text, "F201")), Show(Groups(saved, "F201")));
        }

        [Fact]
        public void AConstraintGroupSavedAsR2010_IsWrittenToADwgOfR2010TheSameAsTheGroupOfR2018()
        {
            // The DWG path reads the text the converter gives, and writes the bytes it wrote from the text of R2018.
            string text = R2018(Network("2", "F201", "F202"), Variable("2"), Group2());
            byte[] fromR2018 = DwgWriter.ToDwg(text, "AC1024");
            byte[] fromR2010 = DwgWriter.ToDwg(As(text, "AC1024"), "AC1024");

            Assert.Equal(fromR2018, fromR2010);
        }

        #region Going up

        [Fact]
        public void ANetworkAndAVariableOfR2010_SavedAsR2018_HoldTheSecondVersionOfAnAction()
        {
            // The version 2: the four numbers after the three, all 0, as AutoCAD 2021 writes a drawing of R2010 as R2018.
            string text = File("AC1024", Network("1", "F201", "F202"), Variable("1"), Group1());

            foreach (string version in new[] { "AC1027", "AC1032" })
            {
                string saved = As(text, version);

                Assert.Equal(Show(Groups(R2018(Network("2", "F201", "F202")), "F200")), Show(Groups(saved, "F200")));
                Assert.Equal(Show(Groups(R2018(Variable("2")), "F202")), Show(Groups(saved, "F202")));
            }
        }

        [Fact]
        public void ANetworkAndAVariableOfR2007_SavedAsR2018_HoldTheSecondVersionOfAnAction()
        {
            string saved = As(File("AC1021", Network("1", "F201", "F202"), Variable("1"), Group1()), "AC1032");

            Assert.Equal(Show(Groups(R2018(Variable("2")), "F202")), Show(Groups(saved, "F202")));
        }

        [Fact]
        public void AConstraintGroupOfR2010_SavedAsR2018_IsInTheSecondLayout()
        {
            // The group of the smallest of AutoCAD's samples, as AutoCAD 2021's DXF of the drawing of R2010 as R2018 has it: a
            // directory of the classes and identifiers, two numbers and a flag of 0 before it, then the nodes without their class.
            string text = File("AC1024", Network("1", "F201", "F202"), Variable("1"), Group1());

            foreach (string version in new[] { "AC1027", "AC1032" })
            {
                Assert.Equal(Show(Groups(R2018(Group2()), "F201")), Show(Groups(As(text, version), "F201")));
            }
        }

        [Fact]
        public void AConstraintGroupOfR2007_SavedAsR2018_IsInTheSecondLayout()
        {
            string saved = As(File("AC1021", Network("1", "F201", "F202"), Variable("1"), Group1()), "AC1032");

            Assert.Equal(Show(Groups(R2018(Group2()), "F201")), Show(Groups(saved, "F201")));
        }

        [Fact]
        public void AConstraintGroupOfR2018_TakenDownToR2010AndUpAgain_IsTheGroupItWas()
        {
            string text = R2018(Network("2", "F201", "F202"), Variable("2"), Group2());
            string again = As(As(text, "AC1024"), "AC1032");

            Assert.Equal(Show(Groups(text, "F201")), Show(Groups(again, "F201")));
            Assert.Equal(Show(Groups(text, "F200")), Show(Groups(again, "F200")));
            Assert.Equal(Show(Groups(text, "F202")), Show(Groups(again, "F202")));
        }

        [Fact]
        public void ACircleOfAConstraintGroupOfR2010_GainsAFourthRealOfZeroWhenSavedAsR2018()
        {
            string circle1 =
                "1=AcConstrainedCircle|330=F201|90=1|70=0|90=1|90=3|330=F203|90=0"
                + "|10=1.0|20=2.0|30=0.0|10=0.0|20=0.0|30=1.0|10=1.0|20=0.0|30=0.0|40=25.4|40=0.0|40=6.28";
            string text = File(
                "AC1024",
                Network("1", "F201", "F202"),
                Variable("1"),
                Group1(circle1 + "|" + ImplicitPoint1 + "|" + Distance1)
            );
            List<KeyValuePair<int, string>> group = Groups(As(text, "AC1032"), "F201");

            Assert.Equal(new[] { "25.4", "0.0", "6.28", "0.0" }, group.Where(g => g.Key == 40).Select(g => g.Value));
        }

        [Fact]
        public void AnArcOfAConstraintGroupOfR2010_GainsAFourthRealBeforeItsTwoEndPoints()
        {
            string arc1 =
                "1=AcConstrainedArc|330=F201|90=1|70=0|90=1|90=3|330=F203|90=0"
                + "|10=1.0|20=2.0|30=0.0|10=0.0|20=0.0|30=1.0|10=1.0|20=0.0|30=0.0|40=25.4|40=0.5|40=2.5"
                + "|10=26.4|20=2.0|30=0.0|11=1.0|21=27.4|31=0.0";
            string text = File(
                "AC1024",
                Network("1", "F201", "F202"),
                Variable("1"),
                Group1(arc1 + "|" + ImplicitPoint1 + "|" + Distance1)
            );
            string body = Show(Groups(As(text, "AC1032"), "F201"));

            Assert.Contains("|40=25.4|40=0.5|40=2.5|40=0.0|10=26.4|20=2.0|30=0.0|11=1.0|21=27.4|31=0.0|", body);
        }

        [Fact]
        public void TheNodesOfAConstraintGroupOfR2010_AreSavedAsR2018ByIdentifier()
        {
            // The nodes given the wrong way round come out by identifier, in the directory and the bodies.
            string text = File(
                "AC1024",
                Network("1", "F201", "F202"),
                Variable("1"),
                Group1(Distance1 + "|" + ImplicitPoint1 + "|" + BoundedLine1)
            );

            Assert.Equal(Show(Groups(R2018(Group2()), "F201")), Show(Groups(As(text, "AC1032"), "F201")));
        }

        [Fact]
        public void AConstraintGroupOfANodeNotKnown_IsLeftAsItIsWhenSavedAsR2018()
        {
            string text = File(
                "AC1024",
                Network("1", "F201", "F202"),
                Variable("1"),
                Group1(BoundedLine1 + "|1=AcMysteryConstraint|330=F201|90=2|70=0|90=0")
            );
            string saved = As(text, "AC1032");

            Assert.Equal(Show(Groups(text, "F201")), Show(Groups(saved, "F201")));
        }

        // An ellipse, a bounded ellipse and a concentric constraint, which AutoCAD 2021 holds past the byte of a node in the groups
        // of both layouts alike (the numbers are made up here).
        private const string EllipseFields = "330=F203|90=0|10=700.0|20=0.0|30=0.0|11=20.0|21=0.0|31=0.0|40=0.5";

        private const string BoundedEllipseFields =
            "330=0|90=0|10=800.0|20=0.0|30=0.0|11=20.0|21=5.0|31=0.0|40=0.4"
            + "|10=814.5|20=8.8|30=0.0|11=795.3|21=-9.2|31=0.0";

        private const string ConcentricFields = "90=0|290=0|290=1";

        private static readonly string EllipsesNodes1 =
            "1=AcConstrainedEllipse|330=F201|90=1|70=0|90=1|90=4|"
            + EllipseFields
            + "|1=AcConstrainedBoundedEllipse|330=F201|90=2|70=0|90=1|90=4|"
            + BoundedEllipseFields
            + "|1=AcConcentricConstraint|330=F201|90=4|70=4|90=2|90=1|90=2|"
            + ConcentricFields;

        private static string EllipsesGroup2() =>
            Group2(
                "1=AcConstrainedEllipse|90=1|1=AcConstrainedBoundedEllipse|90=2|1=AcConcentricConstraint|90=4",
                "90=1|90=1|90=4|70=0|"
                    + EllipseFields
                    + "|90=2|90=1|90=4|70=0|"
                    + BoundedEllipseFields
                    + "|90=4|90=2|90=1|90=2|70=4|"
                    + ConcentricFields
            );

        [Fact]
        public void AnEllipseABoundedEllipseAndAConcentricConstraintOfR2018_AreInTheFirstLayoutWhenSavedAsR2010()
        {
            string saved = As(R2018(Network("2", "F201", "F202"), Variable("2"), EllipsesGroup2()), "AC1024");

            Assert.Equal(Show(Groups(File("AC1024", Group1(EllipsesNodes1)), "F201")), Show(Groups(saved, "F201")));
        }

        [Fact]
        public void AnEllipseABoundedEllipseAndAConcentricConstraintOfR2010_AreInTheSecondLayoutWhenSavedAsR2018()
        {
            string text = File("AC1024", Network("1", "F201", "F202"), Variable("1"), Group1(EllipsesNodes1));

            Assert.Equal(Show(Groups(R2018(EllipsesGroup2()), "F201")), Show(Groups(As(text, "AC1032"), "F201")));
        }

        [Fact]
        public void AConstraintGroupOfR2010_SavedAsR2018_IsWrittenToADwgOfR2018TheSameAsTheGroupOfR2018()
        {
            // The DWG path reads the text the converter gives, and writes the bytes it wrote from the text of R2018.
            string text = R2018(Network("2", "F201", "F202"), Variable("2"), Group2());
            byte[] fromR2018 = DwgWriter.ToDwg(text, "AC1032");
            byte[] fromR2010 = DwgWriter.ToDwg(As(As(text, "AC1024"), "AC1032"), "AC1032");

            Assert.Equal(fromR2018, fromR2010);
        }

        #endregion
    }
}
