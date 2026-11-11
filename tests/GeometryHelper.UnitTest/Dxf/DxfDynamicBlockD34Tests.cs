using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfDynamicBlockD27Tests;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The dynamic-block leftovers of D27b, each as AutoCAD 2021 worked it (plugin D34): the functions, constants and the
    /// remainder of the expressions of a block's constraints, which AutoCAD evaluated through <c>AssocVariable</c>; attribute
    /// definitions in a polar stretch, and their INSERTs' ATTRIBs; and a justified TEXT of no second point.
    /// </summary>
    public class DxfDynamicBlockD34Tests
    {
        private static readonly Tolerance Exact = new Tolerance(1E-9, 1E-9);

        private static readonly Dictionary<string, double> Values = new Dictionary<string, double>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            ["Width"] = 10,
            ["Half"] = 2.5,
        };

        private static double Read(string name) =>
            Values.TryGetValue(name, out double value)
                ? value
                : throw new NotSupportedException("No variable " + name + ".");

        private static bool IsWhole(string name) => name.Equals("Width", StringComparison.OrdinalIgnoreCase);

        private static double Evaluate(string expression, out bool whole) =>
            DxfConstraintExpression.Evaluate(expression, Read, IsWhole, out whole);

        /// <summary>Asserts a value of AutoCAD 2021's within 1E-12 of its size, and its kind.</summary>
        private static void Same(string expression, double autocad, bool whole)
        {
            double value = Evaluate(expression, out bool isWhole);
            Assert.True(
                Math.Abs(value - autocad) <= 1E-12 * Math.Max(1.0, Math.Abs(autocad)),
                expression + " gave " + value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
            );
            Assert.Equal(whole, isWhole);
        }

        // ---- 1. expressions -------------------------------------------------------------------------------------------------

        [Fact]
        public void RoundOfAHalf_GoesUp_AsAutoCadGaveRoundOfMinusTwoAndAHalfMinusTwo()
        {
            // AutoCAD 2021 worked round out as the whole number at or below the value and a half (plugin D34 r1).
            Same("round(0.5)", 1, true);
            Same("round(2.5)", 3, true);
            Same("round(12.5)", 13, true);
            Same("round(-0.5)", 0, true);
            Same("round(-1.5)", -1, true);
            Same("round(-2.5)", -2, true);
            Same("round(-7/2)", -3, true);
            Same("round(0.49999999999999994)", 1, true);
            Same("round(2.4999999999999996)", 2, true);
            Same("round(-Half)", -2, true);
        }

        [Fact]
        public void TruncFloorAndCeil_AreWhole_AsAutoCadWorkedThemOut()
        {
            Same("trunc(2.7)", 2, true);
            Same("trunc(-2.7)", -2, true);
            Same("floor(2.7)", 2, true);
            Same("floor(-2.7)", -3, true);
            Same("ceil(2.2)", 3, true);
            Same("ceil(-2.2)", -2, true);
            Same("TRUNC(2.7)+1", 3, true);
            Same("floor(2.7)/1", 2, false);
        }

        [Fact]
        public void MinAndMax_TakeTwoValues_OfTheKindOfTheOneTheyGive_TheFirstOnATie()
        {
            // AutoCAD 2021 took a semicolon or a comma between the two, gave max(2.5;3) the whole 3 and max(2.0;2) the real 2,
            // and refused one value or three (plugin D34 r1, r2).
            Same("min(2;3)", 2, true);
            Same("max(2,3)", 3, true);
            Same("max(2.5;3)", 3, true);
            Same("max(2;3.5)", 3.5, false);
            Same("max(2;2.0)", 2, true);
            Same("max(2.0;2)", 2, false);
            Same("min(2.0;2)", 2, false);
            Same("max( 2 ; 3 )", 3, true);
            Same("max(min(1;2);0.5)", 1, true);
            Same("Max(Width;Half)", 10, true);
            Assert.Throws<NotSupportedException>(() => Evaluate("max(1;2;3)", out _));
            Assert.Throws<NotSupportedException>(() => Evaluate("min(5)", out _));
            Assert.Throws<NotSupportedException>(() => Evaluate("round(2;3)", out _));
        }

        [Fact]
        public void TheOtherFunctionsOfAutoCadsHelp_AreReal_AsAutoCadWorkedThemOut()
        {
            // AutoCAD 2021's values (plugin D34 r1, r2); its ln and log come up to 2E-13 of their size apart from the C
            // library's for arguments past 1000.
            Same("exp(1)", 2.7182818284590451, false);
            Same("exp(-1)", 0.36787944117144233, false);
            Same("ln(e)", 1, false);
            Same("ln(2)", 0.69314718055994573, false);
            Same("ln(0.5)", -0.69314718055994529, false);
            Same("log(100)", 2.0000000000000062, false);
            Same("log(2)", 0.30102999566398136, false);
            Same("log(0.001)", -3, false);
            Same("exp10(2)", 100.00000000000004, false);
            Same("exp10(-1)", 0.099999999999999978, false);
            Same("exp10(3)", 1000.0000000000007, false);
            Same("pow(2;3)", 8, false);
            Same("pow(2,0.5)", 1.4142135623730951, false);
            Same("pow(3;1.7)", 6.4730078399237794, false);
            Same("sinh(1)", 1.1752011936438014, false);
            Same("cosh(2)", 3.7621956910836314, false);
            Same("tanh(0.3)", 0.2913126124515909, false);
            Same("asinh(1)", 0.88137358701954294, false);
            Same("asinh(2)", 1.4436354751788103, false);
            Same("acosh(3)", 1.7627471740390861, false);
            Same("atanh(0.3)", 0.30951960420311181, false);
            Same("d2r(180)", 3.1415926535897931, false);
            Same("d2r(45)", 0.78539816339744828, false);
            Same("r2d(1)", 57.295779513082323, false);
            Same("r2d(pi)", 180, false);
            Same("sign(-3)", -1, true);
            Same("sign(0)", 0, true);
            Same("sign(2.5)", 1, true);
            Same("sign(-3)*2", -2, true);
            Same("abs(-2.5)", 2.5, false);
            Same("abs(-2)", 2, true);
            Same("EXP(1)", 2.7182818284590451, false);
        }

        [Fact]
        public void PiAndE_AreConstants_AheadOfAVariableOfTheirName_AsAutoCadReadPiTimesOne()
        {
            // AutoCAD 2021 read pi*1 as pi and e*1 as e beside variables pi of 5 and e of 7 (plugin D34 r2).
            var named = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["pi"] = 5, ["e"] = 7 };

            Assert.Equal(Math.PI, DxfConstraintExpression.Evaluate("pi*1", n => named[n]), 15);
            Assert.Equal(Math.E, DxfConstraintExpression.Evaluate("E", n => named[n]), 15);
            Same("2*pi", 6.2831853071795862, false);
            Same("-PI", -3.1415926535897931, false);
            Same("1e3", 1000, false);
            Assert.Throws<NotSupportedException>(() => Evaluate("2e", out _));
            Assert.Throws<NotSupportedException>(() => Evaluate("2pi", out _));
        }

        [Fact]
        public void TheRemainder_IsWholeOfTheSignOfTheValueDivided_AtTheLevelOfAProduct()
        {
            // AutoCAD 2021 gave 7%3 1, -7%3 -1, 7%-3 1, 2*5%3 1, 7%3*2 2, 2+7%3 3 and 7.0%2 1, and refused 7.5%2 and 7%0
            // (plugin D34 r1, r2).
            Same("7%3", 1, true);
            Same("-7%3", -1, true);
            Same("7%-3", 1, true);
            Same("-7%-3", -1, true);
            Same("2*5%3", 1, true);
            Same("7%3*2", 2, true);
            Same("2+7%3", 3, true);
            Same("7.0%2", 1, true);
            Same("round(7.5)%2", 0, true);
            Same("Width%3", 1, true);
            Assert.Throws<NotSupportedException>(() => Evaluate("7.5%2", out _));
            Assert.Throws<NotSupportedException>(() => Evaluate("7%0", out _));
        }

        [Fact]
        public void WhatAutoCadWouldNotWorkOut_IsRefused()
        {
            // AutoCAD 2021 refused each of these (plugin D34 r1, r2): out of range, no such function, random drawn anew.
            foreach (
                string expression in new[]
                {
                    "ln(-1)",
                    "log(0)",
                    "sqrt(-1)",
                    "acos(2)",
                    "asin(2)",
                    "atanh(1)",
                    "acosh(0.5)",
                    "1/0",
                    "max(2;1/0)",
                    "round(3000000000.5)",
                    "trunc(-3000000000.7)",
                    "floor(1e10)",
                    "log10(100)",
                    "random",
                    "random()",
                    "e2",
                }
            )
            {
                Assert.Throws<NotSupportedException>(() => Evaluate(expression, out _));
            }
        }

        [Fact]
        public void AWholeValuePast32Bits_IsReal_AsAutoCadKept2147483647PlusOne()
        {
            Same("2147483647+1", 2147483648, false);
            Same("round(2147483647.4)", 2147483647, true);
        }

        [Fact]
        public void AVariableOfAFunctionOfTwoValues_FollowsTheWidthSet()
        {
            // H2 = max(Width/2;20): 75 at Width 150.
            DxfDatabase db = Rectangle(heightExpression: "max(Width/2;20)");
            DxfBlockReference insert = Insert(db);

            Property(insert, "Width").Value = 150.0;

            Assert.Equal("(0,0)-(150,0) (150,0)-(150,75) (150,75)-(0,75) (0,75)-(0,0)", Drawn(insert));
        }

        // ---- 2. attribute definitions in a polar stretch -----------------------------------------------------------------------

        /// <summary>
        /// The pointer of D34's check e7: a line from (0, 0) to (100, 0) of a Distance and an Angle from (0, 0) to (100, 0), the
        /// frame from (90, 0) to (110, 20) holding the line's end and LEFT, an attribute definition at (100, 10), and MID, centred
        /// on (100, -10), turned only; both 5 high and locked in the block. One INSERT at (500, 0), turned a quarter at the scale 2,
        /// with an ATTRIB of each.
        /// </summary>
        private static DxfDatabase Pointer(bool locked = true)
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "BPOL", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            block.AppendEntity(line);
            var left = new DxfAttributeDefinition(new GeoPoint3(100, 10, 0), "L1", "LEFT", "Left", null)
            {
                Height = 5,
                LockPositionInBlock = locked,
            };
            block.AppendEntity(left);
            var mid = new DxfAttributeDefinition(new GeoPoint3(100, -10, 0), "M1", "MID", "Mid", null)
            {
                Height = 5,
                LockPositionInBlock = locked,
            };
            block.AppendEntity(mid);
            mid.Justify = DxfAttachmentPoint.MiddleCenter;
            mid.AlignmentPoint = new GeoPoint3(100, -10, 0);
            DxfBlockPolarParameter polar = block.AddPolarParameter(
                "Distance",
                null,
                "Angle",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                null,
                null
            );
            block.AddPolarStretchAction(
                polar,
                new GeoPoint2(90, 0),
                new GeoPoint2(110, 20),
                new DxfEntity[] { line, left },
                new DxfEntity[] { mid }
            );
            var insert = new DxfBlockReference(new GeoPoint3(500, 0, 0), block)
            {
                Rotation = Math.PI / 2,
                ScaleFactors = new GeoVector3(2, 2, 2),
            };
            db.ModelSpace.AppendEntity(insert);

            foreach (DxfAttributeDefinition definition in block.OfType<DxfAttributeDefinition>().ToList())
            {
                var attribute = new DxfAttributeReference();
                attribute.SetAttributeFromBlock(definition, insert.BlockTransform);
                insert.AttributeCollection.AppendAttribute(attribute);
            }

            return DxfDatabase.Parse(db.ToText());
        }

        private static DxfBlockReference Pointed(DxfDatabase db) => db.ModelSpace.OfType<DxfBlockReference>().Single();

        private static void SetPolar(DxfBlockReference insert, string name, double value) =>
            insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == name).Value = value;

        /// <summary>Asserts a point at x and y, within 1E-9.</summary>
        private static void At(GeoPoint3 point, double x, double y) =>
            Assert.True(point.IsEqualTo(new GeoPoint3(x, y, 0), Exact), point.ToString());

        [Fact]
        public void APolarStretch_TakesLockedAttributeDefinitions_ToStretchAndToTurnOnly()
        {
            DxfDatabase db = Pointer();

            Assert.Equal(2, db.BlockTable["BPOL"].OfType<DxfAttributeDefinition>().Count());
            Assert.Throws<ArgumentException>(() => Pointer(locked: false));
        }

        [Fact]
        public void LockedAttributeDefinitionsOfAPolarStretch_TurnAndMoveWithTheirAttributes_AsAutoCadWorkedThemOut()
        {
            // AutoCAD 2021, the INSERT turned a quarter at the scale 2 (plugin D34, e7): Distance 150 took LEFT's ATTRIB to
            // (480, 150); Angle 30 degrees then to (407.679, 119.904), turned to 120 degrees, LEFT to (59.952, 46.160) in the
            // block and MID, turned only, to (91.603, 41.340), its ATTRIB to (417.321, 183.205).
            DxfDatabase db = Pointer();
            DxfBlockReference insert = Pointed(db);

            SetPolar(insert, "Distance", 150.0);
            At(insert.AttributeCollection.Single(a => a.Tag == "LEFT").Position, 480, 150);

            SetPolar(insert, "Angle", Math.PI / 6);

            DxfBlockReference back = Pointed(DxfDatabase.Parse(db.ToText()));
            DxfAttributeReference left = back.AttributeCollection.Single(a => a.Tag == "LEFT");
            DxfAttributeReference mid = back.AttributeCollection.Single(a => a.Tag == "MID");
            At(left.Position, 407.67949192431126, 119.90381056766572);
            Assert.Equal(2 * Math.PI / 3, left.Rotation, 9);
            Assert.Equal(10.0, left.Height, 9);
            At(mid.AlignmentPoint, 417.32050807568862, 183.20508075688800);
            Assert.Equal(2 * Math.PI / 3, mid.Rotation, 9);
            List<DxfAttributeDefinition> definitions = back.BlockTableRecord.OfType<DxfAttributeDefinition>().ToList();
            At(definitions.Single(d => d.Tag == "LEFT").Position, 59.951905283832886, 46.160254037844375);
            At(definitions.Single(d => d.Tag == "MID").AlignmentPoint, 91.602540378443877, 41.339745962155597);
            Assert.Equal(Math.PI / 6, definitions.Single(d => d.Tag == "MID").Rotation, 9);
        }

        [Fact]
        public void AnUnlockedAttributeDefinitionOfAnotherWritersPolarStretch_StaysAndItsAttributeWithIt_AsAutoCadLeftThem()
        {
            // The polar stretch written of locked ATTDEFs whose lock another writer then took off: AutoCAD 2021 left both
            // ATTDEFs and both ATTRIBs where they were at Distance 150 and Angle 30 degrees, and stretched the line (plugin
            // D34, e8).
            DxfDatabase db = Pointer();

            foreach (DxfAttributeDefinition definition in db.BlockTable["BPOL"].OfType<DxfAttributeDefinition>())
            {
                definition.LockPositionInBlock = false;
            }

            DxfBlockReference insert = Pointed(db);
            SetPolar(insert, "Distance", 150.0);
            SetPolar(insert, "Angle", Math.PI / 6);

            DxfBlockReference back = Pointed(DxfDatabase.Parse(db.ToText()));
            At(back.AttributeCollection.Single(a => a.Tag == "LEFT").Position, 480, 200);
            At(back.BlockTableRecord.OfType<DxfAttributeDefinition>().Single(d => d.Tag == "LEFT").Position, 100, 10);
            At(
                back.BlockTableRecord.OfType<DxfAttributeDefinition>().Single(d => d.Tag == "MID").AlignmentPoint,
                100,
                -10
            );
            At(back.BlockTableRecord.OfType<DxfLine>().Single().EndPoint, 64.951905283832886, 37.499999999999986);
        }

        // ---- 3. a justified TEXT of no second point --------------------------------------------------------------------------

        [Fact]
        public void AJustifiedTextOfNoSecondPoint_ReadsItsAlignmentPointAtTheOrigin_AsAutoCadReadIt()
        {
            // AutoCAD 2021 read each justified TEXT of no group 11 with its alignment point at (0, 0, 0), and placed it about
            // that point (plugin D34, e6): a centred, a bottom-right and a top-left one here, written at (300, 0).
            var db = new DxfDatabase("AC1032");

            foreach (
                DxfAttachmentPoint justify in new[]
                {
                    DxfAttachmentPoint.MiddleCenter,
                    DxfAttachmentPoint.BottomRight,
                    DxfAttachmentPoint.TopLeft,
                }
            )
            {
                var text = new DxfDBText
                {
                    Position = new GeoPoint3(300, 0, 0),
                    TextString = "T",
                    Height = 10,
                };
                db.ModelSpace.AppendEntity(text);
                text.Justify = justify;
                text.AlignmentPoint = new GeoPoint3(300, 50, 0);
            }

            string written = db.ToText();
            var lines = written.Replace("\r\n", "\n").Split('\n').ToList();

            for (int i = 0; i + 1 < lines.Count; i += 2)
            {
                if ((lines[i].Trim() == "11" || lines[i].Trim() == "21" || lines[i].Trim() == "31") && TextAt(lines, i))
                {
                    lines.RemoveRange(i, 2);
                    i -= 2;
                }
            }

            List<DxfDBText> texts = DxfDatabase.Parse(string.Join("\n", lines)).ModelSpace.OfType<DxfDBText>().ToList();
            Assert.Equal(3, texts.Count);
            Assert.All(texts, t => At(t.AlignmentPoint, 0, 0));
        }

        /// <summary>Whether the group at the place given is of a TEXT: the entity it is in, by the last group 0 before it.</summary>
        private static bool TextAt(List<string> lines, int at)
        {
            for (int i = at - 2; i >= 0; i -= 2)
            {
                if (lines[i].Trim() == "0")
                {
                    return lines[i + 1].Trim() == "TEXT";
                }
            }

            return false;
        }
    }
}
