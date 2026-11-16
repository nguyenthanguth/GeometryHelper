using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A HATCH mirrored keeps its normal and has what lies in its plane reflected, as AutoCAD 2021's <c>TransformBy</c> does
    /// (probed in its console, mirror-6 GHM6, hatches of ANSI31, HONEY, ANSI37, AR-B816, DASH, ANGLE, a user pattern, SOLID and a
    /// gradient, with loops of polylines, bulges, lines and arcs, circles, ellipses and splines, mirrored across five lines and
    /// planes, 1 200 cases in all): the loops and seed points reflected, a bulge changing its sign, an arc and a circle running the
    /// other way round, an ellipse's major axis reflected, and the angle of a gradient left as it is. The hatch here is a user
    /// pattern at 30 degrees with its lines 2 apart over (42, 50) to (58, 58), a seed point at (45, 52).
    /// </summary>
    public class DxfHatchReflectionTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-9, 1E-9);
        private static readonly double Root3 = Math.Sqrt(3.0);

        /// <summary>A transformation that mirrors across the upright plane through two points of the floor.</summary>
        private static GeoTransform3 Across(double x1, double y1, double x2, double y2) =>
            GeoTransform3.Mirror(
                new GeoPlane3(new GeoPoint3(x1, y1, 0.0), new GeoVector3(-(y2 - y1), x2 - x1, 0.0).Normalize())
            );

        private static DxfHatch UserHatch(DxfDatabase database)
        {
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternAngle = 30.0 * Math.PI / 180.0;
            hatch.PatternSpace = 2.0;
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(42, 50), new GeoPoint2(58, 50), new GeoPoint2(58, 58), new GeoPoint2(42, 58) },
                null
            );
            hatch.AppendSeedPoint(new GeoPoint2(45, 52));
            database.ModelSpace.AppendEntity(hatch);
            return hatch;
        }

        /// <summary>The groups of the record of a handle as "code=value", from its group 0 to the next.</summary>
        private static List<string> Groups(DxfDatabase database, string handle)
        {
            string[] lines = database.ToText().Replace("\r\n", "\n").Split('\n');
            var groups = new List<string>();
            bool found = false;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1].Trim();

                if (code == 0 && found)
                {
                    break;
                }

                if (code == 0)
                {
                    groups.Clear();
                }

                groups.Add(code + "=" + value);

                if (code == 5 && value == handle)
                {
                    found = true;
                }
            }

            return found ? groups : null;
        }

        private static double Value(IEnumerable<string> groups, int code, int skip = 0) =>
            double.Parse(
                groups
                    .Where(g => g.StartsWith(code + "=", StringComparison.Ordinal))
                    .Skip(skip)
                    .First()
                    .Substring((code + "=").Length),
                CultureInfo.InvariantCulture
            );

        private static void Near(double expected, double actual) =>
            Assert.True(Math.Abs(expected - actual) <= 1E-9, "Expected " + expected + " but was " + actual + ".");

        /// <summary>The first line of the pattern after the 78: its angle, base x and y, offset x and y.</summary>
        private static double[] FirstLine(List<string> groups)
        {
            List<string> from = groups
                .Skip(groups.FindIndex(g => g.StartsWith("78=", StringComparison.Ordinal)))
                .ToList();
            return new[] { Value(from, 53), Value(from, 43), Value(from, 44), Value(from, 45), Value(from, 46) };
        }

        /// <summary>The first group of a code after the one given: the points of a loop run from its 93 to its 97.</summary>
        private static List<GeoPoint2> LoopPoints(List<string> groups)
        {
            int from = groups.FindIndex(g => g.StartsWith("93=", StringComparison.Ordinal));
            int to = groups.FindIndex(from, g => g.StartsWith("97=", StringComparison.Ordinal));
            var points = new List<GeoPoint2>();

            for (int i = from; i < to; i++)
            {
                if (groups[i].StartsWith("10=", StringComparison.Ordinal))
                {
                    points.Add(
                        new GeoPoint2(
                            double.Parse(groups[i].Substring(3), CultureInfo.InvariantCulture),
                            double.Parse(groups[i + 1].Substring(3), CultureInfo.InvariantCulture)
                        )
                    );
                }
            }

            return points;
        }

        [Fact]
        public void AHatchMirroredAcrossALineAt45Degrees_KeepsItsNormal_AndReflectsItsLoopAndSeed()
        {
            var database = new DxfDatabase("AC1027");
            DxfHatch hatch = UserHatch(database);

            hatch.TransformBy(Across(0, 0, 10, 10));

            // AutoCAD 2021: the normal still (0, 0, 1), the rectangle over (50, 42) to (58, 58), the seed point at (52, 45).
            Assert.True(hatch.Normal.IsEqualTo(new GeoVector3(0.0, 0.0, 1.0), Tol));
            List<GeoPoint2> points = LoopPoints(Groups(database, hatch.Handle));
            Assert.Equal(4, points.Count);
            double[][] expected =
            {
                new[] { 50.0, 42.0 },
                new[] { 50.0, 58.0 },
                new[] { 58.0, 58.0 },
                new[] { 58.0, 42.0 },
            };

            for (int i = 0; i < 4; i++)
            {
                Near(expected[i][0], points[i].X);
                Near(expected[i][1], points[i].Y);
            }

            Assert.True(new GeoPoint2(52, 45).IsEqualTo(hatch.GetSeedPointAt(0), Tol));
            Assert.Equal(DxfDatabase.Parse(database.ToText()).ToText(), database.ToText());
        }

        [Fact]
        public void AHatchMirroredAcrossAGeneralLine_LeavesTheLinesOfItsPattern_AsAutoCadsMirrhatch0Does()
        {
            var database = new DxfDatabase("AC1027");
            DxfHatch hatch = UserHatch(database);

            hatch.TransformBy(Across(20, 5, 30, 20));

            // AutoCAD 2021's TransformBy follows MIRRHATCH, 0 by default: the line at 30 degrees through (0, 0), offset (-1, 1.732),
            // stays as it was, the pattern angle (52) too, the loop and the seed point reflected all the same.
            List<string> groups = Groups(database, hatch.Handle);
            double[] line = FirstLine(groups);
            Near(30.0, line[0]);
            Near(0.0, line[1]);
            Near(0.0, line[2]);
            Near(-1.0, line[3]);
            Near(Root3, line[4]);
            Near(30.0, Value(groups, 52));
            GeoPoint3 corner = Across(20, 5, 30, 20).Transform(new GeoPoint3(42, 50, 0));
            Near(corner.X, LoopPoints(groups)[0].X);
            Near(corner.Y, LoopPoints(groups)[0].Y);
            Assert.True(
                Across(20, 5, 30, 20)
                    .Transform(new GeoPoint3(45, 52, 0))
                    .IsEqualTo(new GeoPoint3(hatch.GetSeedPointAt(0).X, hatch.GetSeedPointAt(0).Y, 0.0), Tol)
            );
        }

        [Fact]
        public void AHatchMirroredAcrossItsOwnPlane_LeavesTheLinesOfItsPattern_UnlessAskedToReflectThem()
        {
            // AutoCAD 2021 (TransformBy across the plane z = 5): MIRRHATCH 0 leaves the line at 30 degrees, 1 writes 150.
            var kept = new DxfDatabase("AC1027");
            DxfHatch hatch = UserHatch(kept);
            var plane = GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(0, 0, 5), new GeoVector3(0, 0, 1)));
            hatch.TransformBy(plane);
            Near(30.0, FirstLine(Groups(kept, hatch.Handle))[0]);

            var reflected = new DxfDatabase("AC1027");
            DxfHatch other = UserHatch(reflected);
            other.TransformBy(plane, true);
            Near(150.0, FirstLine(Groups(reflected, other.Handle))[0]);
        }

        [Fact]
        public void AHatchTurnedOrMoved_IsTheSameWhateverItIsAskedAboutItsMirror()
        {
            var plain = new DxfDatabase("AC1027");
            DxfHatch hatch = UserHatch(plain);
            var asked = new DxfDatabase("AC1027");
            DxfHatch other = UserHatch(asked);
            GeoTransform3 turn = GeoTransform3
                .RotationZ(0.5)
                .Multiply(GeoTransform3.Translation(new GeoVector3(7, 3, 0)));

            hatch.TransformBy(turn);
            other.TransformBy(turn, true);

            Assert.Equal(plain.ToText(), asked.ToText());
        }

        [Fact]
        public void AHatchMirroredAcrossAGeneralLine_ReflectsTheLinesOfItsPattern_WhenAskedTo()
        {
            var database = new DxfDatabase("AC1027");
            DxfHatch hatch = UserHatch(database);

            hatch.TransformBy(Across(20, 5, 30, 20), true);

            // The line at 30 degrees, offset (-1, 1.732) from the base (0, 0), across a line at 56.31 degrees: AutoCAD 2021 with
            // MIRRHATCH 1 writes 82.6198649480404 (twice the angle of the mirror line less 30), the base (23.0769, -15.3846), the
            // offset reflected, the pattern angle (52) as it was.
            double phi = 2.0 * Math.Atan2(15.0, 10.0);
            double[] line = FirstLine(Groups(database, hatch.Handle));
            Near(phi * 180.0 / Math.PI - 30.0, line[0]);
            Near(82.6198649480404, line[0]);
            Near(23.07692307692307, line[1]);
            Near(-15.38461538461539, line[2]);
            Near(-1.0 * Math.Cos(phi) + Root3 * Math.Sin(phi), line[3]);
            Near(-1.0 * Math.Sin(phi) - Root3 * Math.Cos(phi), line[4]);
            Near(30.0, Value(Groups(database, hatch.Handle), 52));
            Assert.Equal(30.0, hatch.PatternAngle * 180.0 / Math.PI, 9);
        }

        [Fact]
        public void ABulgedLoopMirrored_ChangesTheSignOfItsBulges_AndWritesNoNegativeNought()
        {
            var database = new DxfDatabase("AC1027");
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternSpace = 2.0;
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(42, 50), new GeoPoint2(58, 50), new GeoPoint2(58, 58), new GeoPoint2(42, 58) },
                new[] { 0.0, 0.5, 0.0, -0.3 }
            );
            database.ModelSpace.AppendEntity(hatch);

            hatch.TransformBy(Across(0, 0, 0, 10));

            // AutoCAD 2021: x of each vertex negated, the bulges (0, -0.5, 0, 0.3), a bulge of nought still 0.
            List<string> groups = Groups(database, hatch.Handle);
            Assert.Equal(
                new[] { 0.0, -0.5, 0.0, 0.3 },
                groups.Where(g => g.StartsWith("42=")).Select(g => Value(new[] { g }, 42)).ToArray()
            );
            Assert.DoesNotContain(groups, g =>
                g.StartsWith("42=-0", StringComparison.Ordinal) && Value(new[] { g }, 42) == 0.0
            );
            Assert.Equal(-58.0, LoopPoints(groups)[2].X, 9);
        }

        [Fact]
        public void ALineAndAnArcMirroredAcrossAVerticalLine_RunTheOtherWayRound_AndChainStill()
        {
            var database = new DxfDatabase("AC1027");
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternSpace = 2.0;
            var up = new GeoVector3(0, 0, 1);
            hatch.AppendLoop(
                new DxfHatchLoop(
                    DxfHatchLoopTypes.External,
                    new object[]
                    {
                        new GeoLine3(44, 50, 0, 56, 50, 0),
                        new GeoArc3(new GeoPoint3(50, 50, 0), up, 6.0, -Math.PI / 2.0, Math.PI / 2.0),
                    }
                )
            );
            database.ModelSpace.AppendEntity(hatch);
            double area = hatch.Area;

            hatch.TransformBy(Across(0, 0, 0, 10));

            // AutoCAD 2021: the arc about (-50, 50), counter-clockwise from 0 to 180 now clockwise, held as -180 to 0 (flag 73 of 0),
            // and back again, 0 to 180 counter-clockwise, once mirrored a second time.
            List<string> groups = Groups(database, hatch.Handle);
            Near(-50.0, Value(groups, 10, 2));
            Near(-180.0, Value(groups, 50));
            Near(0.0, Value(groups, 51));
            Near(0.0, Value(groups, 73));
            Near(area, hatch.Area);
            object[] curves = hatch.GetLoopAt(0).Curves.ToArray();
            var line = Assert.IsType<GeoLine3>(curves[0]);
            var arc = Assert.IsType<GeoArc3>(curves[1]);
            Assert.True(
                line.EndPoint.IsEqualTo(arc.StartPoint, Tol),
                "The line ends at " + line.EndPoint + ", the arc starts at " + arc.StartPoint + "."
            );
            Assert.True(arc.EndPoint.IsEqualTo(line.StartPoint, Tol));

            hatch.TransformBy(Across(0, 0, 0, 10));
            groups = Groups(database, hatch.Handle);
            Near(0.0, Value(groups, 50));
            Near(180.0, Value(groups, 51));
            Near(1.0, Value(groups, 73));
        }

        [Fact]
        public void ACircleMirrored_RunsTheOtherWayRound_ItsAnglesMovedOnByTheAngleOfTheMirror()
        {
            var database = new DxfDatabase("AC1027");
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternSpace = 2.0;
            hatch.AppendLoop(
                new DxfHatchLoop(
                    DxfHatchLoopTypes.External,
                    new object[] { new GeoCircle3(new GeoPoint3(50, 54, 0), new GeoVector3(0, 0, 1), 6.0) }
                )
            );
            database.ModelSpace.AppendEntity(hatch);

            hatch.TransformBy(Across(20, 5, 30, 20));

            // AutoCAD 2021: the centre at (53.6923, 51.5385), 50 of -112.6198649480404 and 51 of 247.3801350519596 (the angle of
            // the mirror's image of the x axis is 112.62), clockwise.
            List<string> groups = Groups(database, hatch.Handle);
            Near(53.69230769230769, Value(groups, 10, 1));
            Near(51.53846153846153, Value(groups, 20, 1));
            Near(-112.6198649480404, Value(groups, 50));
            Near(247.3801350519596, Value(groups, 51));
            Near(0.0, Value(groups, 73));
        }

        [Fact]
        public void AnEllipseArcMirrored_HasItsMajorAxisReflected_ItsAnglesKept_AndItsFlagTurned()
        {
            var database = new DxfDatabase("AC1027");
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternSpace = 2.0;
            var ellipse = new GeoEllipse3(
                new GeoPoint3(50, 54, 0),
                new GeoVector3(0, 0, 1),
                new GeoVector3(1.0, 0.5, 0.0),
                9.0,
                4.0
            );
            hatch.AppendLoop(new DxfHatchLoop(DxfHatchLoopTypes.External, new object[] { ellipse }));
            database.ModelSpace.AppendEntity(hatch);

            hatch.TransformBy(Across(0, 0, 0, 10));

            // AutoCAD 2021: the centre (-50, 54), the major axis (-8.0498, 4.0249), the ratio 0.4444, the angles 0 and 360, clockwise.
            List<string> groups = Groups(database, hatch.Handle);
            Near(-50.0, Value(groups, 10, 1));
            Near(-8.049844718999244, Value(groups, 11));
            Near(4.024922359499622, Value(groups, 21));
            Near(4.0 / 9.0, Value(groups, 40));
            Near(0.0, Value(groups, 50));
            Near(360.0, Value(groups, 51));
            Near(0.0, Value(groups, 73));
        }

        [Fact]
        public void AHatchMirroredAcrossItsOwnPlane_TurnsItsNormalOver_AsTheTransformationTakesIt()
        {
            var database = new DxfDatabase("AC1027");
            DxfHatch hatch = UserHatch(database);

            // The plane z = 5: the hatch at z = 0 comes to z = 10, its normal to (0, 0, -1), AutoCAD 2021's x axis of that normal
            // the world's -x, so the loop at (42, 50) is written at (-42, 50), the elevation -10.
            hatch.TransformBy(GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(0, 0, 5), new GeoVector3(0, 0, 1))));

            Assert.True(hatch.Normal.IsEqualTo(new GeoVector3(0.0, 0.0, -1.0), Tol));
            Assert.Equal(-10.0, hatch.Elevation, 9);
            Near(-42.0, LoopPoints(Groups(database, hatch.Handle))[0].X);
            Near(50.0, LoopPoints(Groups(database, hatch.Handle))[0].Y);
        }

        [Fact]
        public void TheAngleOfAGradient_IsLeftAsItIs_ByATurnAndByAMirror()
        {
            var database = new DxfDatabase("AC1027");
            var hatch = new DxfHatch();
            hatch.SetGradient(DxfGradientPatternType.PreDefinedGradient, "LINEAR");
            hatch.GradientAngle = 30.0 * Math.PI / 180.0;
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(42, 50), new GeoPoint2(58, 50), new GeoPoint2(58, 58), new GeoPoint2(42, 58) },
                null
            );
            database.ModelSpace.AppendEntity(hatch);
            double angle = hatch.GradientAngle;

            // AutoCAD 2021 leaves 460 where it was under ROTATE, MIRROR and TransformBy alike (a gradient turned 30 degrees holds
            // 0.5236 still); a solid fill, which holds 0, holds it too.
            hatch.TransformBy(GeoTransform3.RotationAxis(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 0.7));
            Assert.Equal(angle, hatch.GradientAngle);
            hatch.TransformBy(Across(20, 5, 30, 20));
            Assert.Equal(angle, hatch.GradientAngle);

            var solid = new DxfHatch();
            solid.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(42, 50), new GeoPoint2(58, 50), new GeoPoint2(58, 58), new GeoPoint2(42, 58) },
                null
            );
            database.ModelSpace.AppendEntity(solid);
            solid.TransformBy(GeoTransform3.RotationAxis(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 0.7));
            Assert.Equal(0.0, solid.GradientAngle);
        }

        [Fact]
        public void ACircleMirroredAndScaledUnlikeAlongItsAxes_BecomesTheEllipseItIs_AndItsAreaDoubles()
        {
            var database = new DxfDatabase("AC1027");
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternSpace = 2.0;
            var up = new GeoVector3(0, 0, 1);
            hatch.AppendLoop(
                new DxfHatchLoop(
                    DxfHatchLoopTypes.External,
                    new object[]
                    {
                        new GeoLine3(44, 50, 0, 56, 50, 0),
                        new GeoArc3(new GeoPoint3(50, 50, 0), up, 6.0, -Math.PI / 2.0, Math.PI / 2.0),
                    }
                )
            );
            database.ModelSpace.AppendEntity(hatch);
            double area = hatch.Area;

            // Twice as wide after a mirror across the y axis: not a mirror a loop of arcs follows, so its arc becomes an ellipse
            // arc, the plane reflected across its own x axis first with the normal kept, and the area twice.
            hatch.TransformBy(GeoTransform3.Scaling(2.0, 1.0, 1.0).Multiply(Across(0, 0, 0, 10)));

            Assert.True(hatch.Normal.IsEqualTo(up, Tol));
            Assert.Equal(2.0 * area, hatch.Area, 6);
            object[] curves = DxfDatabase
                .Parse(database.ToText())
                .ModelSpace.OfType<DxfHatch>()
                .Single()
                .GetLoopAt(0)
                .Curves.ToArray();
            var line = Assert.IsType<GeoLine3>(curves[0]);
            Assert.True(line.StartPoint.IsEqualTo(new GeoPoint3(-88, 50, 0), Tol), line.StartPoint.ToString());
            Assert.True(line.EndPoint.IsEqualTo(new GeoPoint3(-112, 50, 0), Tol), line.EndPoint.ToString());
            Assert.IsType<GeoSpline3>(curves[1]);

            // The arc is the ellipse edge about (-100, 50), its half axes 12 along x and 6.
            List<string> groups = Groups(database, hatch.Handle);
            Assert.Contains("72=3", groups);
            Near(-100.0, Value(groups, 10, 2));
            Near(12.0, Math.Abs(Value(groups, 11, 1)));
            Near(0.5, Value(groups, 40));
        }

        /// <summary>The groups of the context of the default scale of an annotative hatch of one scale (1:1).</summary>
        private static List<string> DefaultContext(DxfDatabase database, DxfHatch hatch)
        {
            var manager = (DxfDBDictionary)hatch.ExtensionDictionary.GetAt("AcDbContextDataManager");
            var scales = (DxfDBDictionary)manager.GetAt("ACDB_ANNOTATIONSCALES");
            return Groups(database, scales.GetAt("*A1").Handle);
        }

        /// <summary>
        /// An annotative hatch of a user pattern at 30 degrees in the plane of a normal, the direction of its context as AutoCAD
        /// writes it, a vector of the world: the pattern angle taken along the x and y axes of the normal's own system.
        /// </summary>
        private static DxfDatabase InPlane(GeoVector3 normal, GeoVector3 direction, out DxfHatch hatch)
        {
            var made = new DxfDatabase("AC1027");
            made.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES")
                .AddContext(
                    new DxfAnnotationScale
                    {
                        Name = "1:1",
                        PaperUnits = 1,
                        DrawingUnits = 1,
                    }
                );
            DxfHatch first = UserHatch(made);
            first.Normal = normal;
            first.Annotative = DxfAnnotativeStates.True;

            // The direction the library writes is (0.866, 0.5, 0), of the plane; put the one AutoCAD writes in its place.
            string[] lines = made.ToText().Replace("\r\n", "\n").Split('\n');
            int context = Array.FindIndex(lines, (l) => l == "ACDB_HATCHSCALECONTEXTDATA_CLASS");
            context = Array.FindIndex(lines, context + 1, l => l == "ACDB_HATCHSCALECONTEXTDATA_CLASS");
            int scale = context + 1;

            // The groups are in pairs: the code line, then the value; the scale (40) is the last of its code before the direction.
            for (int i = context + 1; i + 1 < lines.Length && lines[i].Trim() != "0"; i += 2)
            {
                if (lines[i].Trim() == "40")
                {
                    scale = i + 1;
                }
            }

            for (int i = scale + 1, axis = 0; axis < 3 && i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() == (10 * (axis + 1)).ToString(CultureInfo.InvariantCulture))
                {
                    lines[i + 1] = new[] { direction.X, direction.Y, direction.Z }[axis]
                        .ToString("R", CultureInfo.InvariantCulture);
                    axis++;
                }
            }

            DxfDatabase database = DxfDatabase.Parse(string.Join("\n", lines));
            hatch = database.ModelSpace.OfType<DxfHatch>().Single();
            return database;
        }

        private static double[] Direction(DxfDatabase database, DxfHatch hatch)
        {
            List<string> groups = DefaultContext(database, hatch);
            return new[] { Value(groups, 10), Value(groups, 20), Value(groups, 30) };
        }

        [Fact]
        public void TheDirectionOfAContext_IsLeftAsItWas_WhereTheTransformationTakesTheHatchsSystemToTheNewOne()
        {
            // AutoCAD 2021, TransformBy and ROTATE alike (probed on 11 annotative hatches of 6 planes): a hatch of normal (0, 1, 0)
            // keeps the direction (-0.866, 0, 0.5) of its pattern at 30 degrees when turned 40 degrees about z, as one of normal
            // (1, 2, 3) does and as the plane of the drawing does turned about x; the direction is carried by a turn about y.
            var upright = new GeoVector3(0, 1, 0);
            var along = new GeoVector3(-Math.Sqrt(3.0) / 2.0, 0.0, 0.5);
            DxfDatabase database = InPlane(upright, along, out DxfHatch hatch);
            double[] before = Direction(database, hatch);
            Near(-Math.Sqrt(3.0) / 2.0, before[0]);

            hatch.TransformBy(GeoTransform3.RotationZ(40.0 * Math.PI / 180.0));
            double[] after = Direction(database, hatch);
            Near(before[0], after[0]);
            Near(before[1], after[1]);
            Near(before[2], after[2]);

            // About y, the axis of its normal, a fresh hatch turns in its own plane and the direction with it.
            DxfDatabase spun = InPlane(upright, along, out DxfHatch spinning);
            spinning.TransformBy(GeoTransform3.RotationY(40.0 * Math.PI / 180.0));
            GeoVector3 turned = GeoTransform3.RotationY(40.0 * Math.PI / 180.0).Transform(along);
            double[] about = Direction(spun, spinning);
            Near(turned.X, about[0]);
            Near(turned.Y, about[1]);
            Near(turned.Z, about[2]);

            // The plane of the drawing turned about x keeps (0.866, 0.5, 0), about z it turns (0.342, 0.940, 0).
            DxfDatabase flat = InPlane(
                new GeoVector3(0, 0, 1),
                new GeoVector3(Math.Sqrt(3.0) / 2.0, 0.5, 0.0),
                out DxfHatch plain
            );
            plain.TransformBy(GeoTransform3.RotationX(40.0 * Math.PI / 180.0));
            double[] kept = Direction(flat, plain);
            Near(Math.Sqrt(3.0) / 2.0, kept[0]);
            Near(0.5, kept[1]);
            Near(0.0, kept[2]);

            DxfDatabase level = InPlane(
                new GeoVector3(0, 0, 1),
                new GeoVector3(Math.Sqrt(3.0) / 2.0, 0.5, 0.0),
                out DxfHatch another
            );
            another.TransformBy(GeoTransform3.RotationZ(40.0 * Math.PI / 180.0));
            double[] z = Direction(level, another);
            Near(Math.Cos(70.0 * Math.PI / 180.0), z[0]);
            Near(Math.Sin(70.0 * Math.PI / 180.0), z[1]);
        }

        [Fact]
        public void TheContextOfAnAnnotativeHatchMirrored_KeepsItsLines_ButReflectsItsDirection_UnlessAskedToReflectThem()
        {
            var made = new List<DxfDatabase>();
            var hatches = new List<DxfHatch>();

            for (int i = 0; i < 2; i++)
            {
                var database = new DxfDatabase("AC1027");
                database
                    .ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES")
                    .AddContext(
                        new DxfAnnotationScale
                        {
                            Name = "1:1",
                            PaperUnits = 1,
                            DrawingUnits = 1,
                        }
                    );
                DxfHatch hatch = UserHatch(database);
                hatch.Annotative = DxfAnnotativeStates.True;
                made.Add(database);
                hatches.Add(hatch);
            }

            GeoTransform3 across = Across(20, 5, 30, 20);
            hatches[0].TransformBy(across);
            hatches[1].TransformBy(across, true);

            // AutoCAD 2021, the line at 30 degrees through (0, 0), offset (-1, 1.732), direction (0.866, 0.5): MIRRHATCH 0 leaves the
            // line in the context as it was, and reflects the direction ((0.1285, 0.9917) across a line at 56.31 degrees); 1 turns
            // the line to 82.6198649480404 as it does the record's.
            List<string> kept = DefaultContext(made[0], hatches[0]);
            Near(30.0, Value(kept, 53));
            Near(-1.0, Value(kept, 45));
            Near(Root3, Value(kept, 46));
            Near(0.1285, Math.Round(Value(kept, 10), 4));
            Near(0.9917, Math.Round(Value(kept, 20), 4));

            List<string> reflected = DefaultContext(made[1], hatches[1]);
            Near(82.6198649480404, Value(reflected, 53));
            Near(0.1285, Math.Round(Value(reflected, 10), 4));
        }
    }
}
