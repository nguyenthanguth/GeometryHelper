using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using GeometryHelper.Drawing;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;
using TSM = Tekla.Structures.Model;

namespace GeometryHelper.TeklaConvert.UnitTest
{
    /// <summary>
    /// <see cref="DrawingSourceConvert"/> on what it reads of Tekla, built in code: parts, bolt groups and welds as the
    /// reader would find them. Reading them out of Tekla itself needs a running Tekla Structures and is not covered here;
    /// those lines are listed in plan/17-tekla-bridge.md for a live check.
    /// </summary>
    public class DrawingSourceConvertTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        // A plate 300 by 200 by 20 lying on the world's XY, its four holes of 22 at 50 from the long edges and the short.
        private static readonly GeoPoint2[] HoleCentres = { new GeoPoint2(50, 50), new GeoPoint2(250, 50), new GeoPoint2(50, 150), new GeoPoint2(250, 150) };

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
        {
            var outline = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(x1 - x0, 0), new GeoPoint2(x1 - x0, y1 - y0), new GeoPoint2(0, y1 - y0));
            return GeoSolid3.Extrude(outline, new GeoCoordinateSystem3(new GeoPoint3(x0, y0, z0), GeoVector3.XAxis, GeoVector3.YAxis), z1 - z0, Tol);
        }

        /// <summary>The plate with its holes of 22, each faceted into 24 sides with corners on the circle, as Tekla facets them.</summary>
        private static GeoSolid3 PlateWithHoles()
        {
            var outline = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(300, 0), new GeoPoint2(300, 200), new GeoPoint2(0, 200));
            GeoPolygon2[] holes = HoleCentres
                .Select(c => new GeoPolygon2(Enumerable.Range(0, 24).Select(i => new GeoPoint2(c.X + 11 * Math.Cos(Math.PI * i / 12), c.Y + 11 * Math.Sin(Math.PI * i / 12)))))
                .ToArray();
            return GeoSolid3.Extrude(new GeoFace2(outline, holes), GeoCoordinateSystem3.Global, 20, Tol);
        }

        private static TeklaPartData Plate(string id) => new TeklaPartData
        {
            Id = id,
            Solid = PlateWithHoles(),
            Frame = GeoCoordinateSystem3.Global,
            Name = "PLATE",
            Mark = "P1",
            Profile = "PL20*300",
            Material = "S355",
            AssemblyId = "A",
            IsMain = true,
        };

        private static TeklaPartData Member(string id, GeoCoordinateSystem3 frame, bool isMain) => new TeklaPartData
        {
            Id = id,
            Solid = Box(0, 0, 0, 100, 100, 100),
            Frame = frame,
            AssemblyId = "A",
            IsMain = isMain,
        };

        /// <summary>
        /// An M20 group of four on the top of the plate, its Z up: Tekla's body for each bolt, a head up to 13 above the
        /// plate and a shank and nut down to 27 below it, its corners 15 from the axis.
        /// </summary>
        private static TeklaBoltGroupData FourBolts(string plateId)
        {
            var corners = new List<GeoPoint3>();

            foreach (GeoPoint2 c in HoleCentres)
            {
                for (int i = 0; i < 6; i++)
                {
                    double x = c.X + 15 * Math.Cos(Math.PI * i / 3), y = c.Y + 15 * Math.Sin(Math.PI * i / 3);
                    corners.Add(new GeoPoint3(x, y, 33));
                    corners.Add(new GeoPoint3(x, y, -27));
                }
            }

            return new TeklaBoltGroupData
            {
                Id = "G",
                HasBolt = true,
                Diameter = 20,
                HoleTolerance = 2,
                Length = 60,
                Standard = "8.8XOX",
                Frame = new GeoCoordinateSystem3(new GeoPoint3(50, 50, 20), GeoVector3.XAxis, GeoVector3.YAxis),
                Positions = HoleCentres.Select(c => new GeoPoint3(c.X, c.Y, 20)).ToArray(),
                BodyCorners = corners,
                PartToBoltToId = plateId,
                BoltedPartIds = new[] { plateId, "BEAM" },
            };
        }

        private static DrawingSource Build(TeklaDrawingData data, string frameOwnerId = null)
            => DrawingSourceBuild.Build(data, frameOwnerId, "Test", "test objects", Tol);

        private static double PrismVolume(double radius, double length) => 0.5 * 24 * radius * radius * Math.Sin(2 * Math.PI / 24) * length;

        [Fact]
        public void APartComesWithItsTextsItsAssemblyAndItsReportProperties()
        {
            var data = new TeklaDrawingData();
            TeklaPartData plate = Plate("P");
            plate.AssemblyPosition = "A12";
            plate.Length = 299.996;
            plate.Weight = 9.1234;
            data.Parts.Add(plate);

            DrawingObject read = Assert.Single(Build(data).Objects);

            Assert.Equal(DrawingObjectKind.Part, read.Kind);
            Assert.Equal(("PLATE", "P1", "PL20*300", "S355", "A"), (read.Name, read.Mark, read.Profile, read.Material, read.ParentId));
            Assert.True(read.IsMain);
            Assert.Same(plate.Solid, read.Solid);
            Assert.Equal(
                new[] { new KeyValuePair<string, string>("ASSEMBLY_POS", "A12"), new KeyValuePair<string, string>("LENGTH", "300"), new KeyValuePair<string, string>("WEIGHT", "9.12") },
                read.Attributes.ToArray());
        }

        [Fact]
        public void APartWithNoReportPropertiesHasNoAttributes_AndNoNamesComeAsEmptyTexts()
        {
            var data = new TeklaDrawingData();
            data.Parts.Add(Member("P", GeoCoordinateSystem3.Global, false));

            DrawingObject read = Assert.Single(Build(data).Objects);

            Assert.Empty(read.Attributes);
            Assert.Equal(string.Empty, read.Name);
            Assert.Equal(string.Empty, read.Mark);
        }

        [Fact]
        public void NumbersAreWrittenWithAPointInAnyCulture()
        {
            CultureInfo before = Thread.CurrentThread.CurrentCulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

                Assert.Equal("22.5", DrawingSourceBuild.Number(22.5));
                Assert.Equal("1234.57", DrawingSourceBuild.Number(1234.5678));
                Assert.Equal("3000", DrawingSourceBuild.Number(2999.996));
                Assert.Equal("0", DrawingSourceBuild.Number(-0.001));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = before;
            }
        }

        [Fact]
        public void TheSourceIsSetInTheFrameOfThePartGiven_NotThatOfTheMainPartWithTheLowestId()
        {
            var alongY = new GeoCoordinateSystem3(new GeoPoint3(0, 0, 0), GeoVector3.YAxis, GeoVector3.ZAxis);
            var alongX = new GeoCoordinateSystem3(new GeoPoint3(10, 0, 0), GeoVector3.XAxis, GeoVector3.ZAxis);
            var data = new TeklaDrawingData();
            data.Parts.Add(Member("B", alongY, true));
            data.Parts.Add(Member("A", alongX, true));

            Assert.Equal(alongY, Build(data, "B").Frame);
            Assert.Equal(alongX, Build(data).Frame);
        }

        [Fact]
        public void APartGivenAsTheFrameButLeftOut_LeavesTheFrameToTheRuleOfTheSource()
        {
            var alongX = new GeoCoordinateSystem3(new GeoPoint3(10, 0, 0), GeoVector3.XAxis, GeoVector3.ZAxis);
            var data = new TeklaDrawingData();
            data.Parts.Add(Member("B", alongX, false));
            TeklaPartData unread = Member("A", GeoCoordinateSystem3.Global, true);
            unread.Solid = null;
            data.Parts.Add(unread);

            Assert.Equal(alongX, Build(data, "A").Frame);
        }

        [Fact]
        public void AnObjectReadTwiceComesOnce_AndTheObjectsComeInTheOrderOfTheirIds()
        {
            var data = new TeklaDrawingData();
            data.Parts.Add(Member("c", GeoCoordinateSystem3.Global, false));
            data.Parts.Add(Member("B", GeoCoordinateSystem3.Global, true));
            data.Parts.Add(Member("a", GeoCoordinateSystem3.Global, false));
            data.Parts.Add(Member("B", GeoCoordinateSystem3.Global, false));
            data.Welds.Add(new TeklaWeldData { Id = "W", Solid = Box(0, 0, 0, 5, 5, 5) });
            data.Welds.Add(new TeklaWeldData { Id = "W", Solid = Box(0, 0, 0, 5, 5, 5) });

            DrawingSource source = Build(data);

            Assert.Equal(new[] { "B", "W", "a", "c" }, source.Objects.Select(o => o.Id));
            Assert.True(source.Objects[0].IsMain);
        }

        [Fact]
        public void TheReaderVisitsEachIdOnce()
        {
            var data = new TeklaDrawingData();

            Assert.True(data.Visit("P"));
            Assert.False(data.Visit("P"));
            Assert.True(data.Visit("p"));
        }

        [Fact]
        public void WhatIsLeftOutIsWrittenInOneDebugLine_AndEachCallInOneInfoLine()
        {
            var data = new TeklaDrawingData();
            TeklaPartData noSolid = Member("P2", GeoCoordinateSystem3.Global, false);
            noSolid.Solid = null;
            TeklaPartData noFrame = Member("P1", GeoCoordinateSystem3.Global, false);
            noFrame.Frame = null;
            data.Parts.Add(noSolid);
            data.Parts.Add(noFrame);
            data.Parts.Add(Member("P3", GeoCoordinateSystem3.Global, true));
            data.Welds.Add(new TeklaWeldData { Id = "W" });
            data.Skip("objects of a kind not drawn", "Grid");

            DrawingSource source;

            using (var log = new LogCapture())
            {
                source = Build(data);

                var debug = Assert.Single(log.Entries, e => e.Level == GeometryHelperLogLevel.Debug);
                Assert.Equal(
                    "Test: left out 4: 1 objects of a kind not drawn (Grid); 1 parts whose solid could not be read (P2); "
                    + "1 parts with no coordinate system (P1); 1 welds whose solid could not be read (W).",
                    debug.Message);
                var info = Assert.Single(log.Entries, e => e.Level == GeometryHelperLogLevel.Info);
                Assert.Equal("Test: 1 objects (1 parts, 0 bolts, 0 holes, 0 welds) from test objects, 4 left out.", info.Message);
                Assert.DoesNotContain(log.Entries, e => e.Level == GeometryHelperLogLevel.Warn);
            }

            Assert.Equal("P3", Assert.Single(source.Objects).Id);
        }

        [Fact]
        public void NothingReadGivesAnEmptySourceInTheWorldsFrame()
        {
            DrawingSource source = Build(new TeklaDrawingData());

            Assert.Empty(source.Objects);
            Assert.Equal(GeoCoordinateSystem3.Global, source.Frame);
        }

        [Fact]
        public void ABoltGroupOfFourGivesFourBolts_AsLongAsTheBodyTeklaDrawsForThem()
        {
            var data = new TeklaDrawingData();
            data.Parts.Add(Plate("PLATE"));
            data.BoltGroups.Add(FourBolts("PLATE"));

            DrawingSource source = Build(data);
            DrawingObject[] bolts = source.Objects.Where(o => o.Kind == DrawingObjectKind.Bolt).ToArray();

            Assert.Equal(new[] { "G-1", "G-2", "G-3", "G-4" }, bolts.Select(b => b.Id));

            for (int i = 0; i < 4; i++)
            {
                DrawingObject bolt = bolts[i];

                // From 27 under the plate to 13 over it: 60 long, along the group's Z.
                Assert.True(bolt.Frame.Origin.IsEqualTo(new GeoPoint3(HoleCentres[i].X, HoleCentres[i].Y, -27), Tol));
                Assert.True(bolt.Frame.XAxis.IsCodirectionalTo(GeoVector3.ZAxis, Tol));
                Assert.Equal(PrismVolume(10, 60), bolt.Solid.GetVolume(Tol), PrismVolume(10, 60) * 1E-9);
                Assert.True(bolt.Solid.Validate(Tol).IsValid);
                Assert.Equal("PLATE", bolt.ParentId);
                Assert.Equal("BOLT", bolt.Name);
                Assert.Equal(
                    new Dictionary<string, string>
                    {
                        ["BoltGroup"] = "G",
                        ["BoltedParts"] = "BEAM,PLATE",
                        ["Count"] = "4",
                        ["Diameter"] = "20",
                        ["HoleDiameter"] = "22",
                        ["Length"] = "60",
                        ["Standard"] = "8.8XOX",
                    },
                    bolt.Attributes.ToDictionary(a => a.Key, a => a.Value));
            }
        }

        [Fact]
        public void ABoltOfATiltedGroupRunsAlongTheGroupsZ()
        {
            // The group's plane turned 30 degrees about X, its Z leaning back; the body Tekla draws runs 47 one way and 13
            // the other along that Z, and its corners stand 15 off the axis.
            double c = Math.Cos(Math.PI / 6), s = Math.Sin(Math.PI / 6);
            var frame = new GeoCoordinateSystem3(new GeoPoint3(100, 100, 100), GeoVector3.XAxis, new GeoVector3(0, c, s));
            GeoVector3 z = frame.ZAxis;
            GeoPoint3 position = new GeoPoint3(100, 100, 100);
            GeoVector3 across = frame.YAxis.Multiply(15);
            GeoPoint3[] corners = { position.Add(z.Multiply(13)).Add(across), position.Add(z.Multiply(-47)).Add(across.Negate()) };
            var data = new TeklaDrawingData();
            data.BoltGroups.Add(new TeklaBoltGroupData { Id = "G", HasBolt = true, Diameter = 16, HoleTolerance = 2, Length = 60, Frame = frame, Positions = new[] { position }, BodyCorners = corners });

            DrawingObject bolt = Assert.Single(Build(data).Objects);

            Assert.True(bolt.Frame.Origin.IsEqualTo(position.Add(z.Multiply(-47)), Tol));
            Assert.True(bolt.Frame.XAxis.IsCodirectionalTo(z, Tol));
            Assert.Equal(PrismVolume(8, 60), bolt.Solid.GetVolume(Tol), PrismVolume(8, 60) * 1E-9);
            Assert.Null(bolt.ParentId);
            Assert.False(bolt.Attributes.ContainsKey("BoltedParts"));
        }

        [Fact]
        public void ABoltGroupOfHolesOnlyGivesHolesThroughThePartsBoltedThatAreRead()
        {
            var data = new TeklaDrawingData();
            data.Parts.Add(Plate("PLATE"));
            TeklaBoltGroupData group = FourBolts("PLATE");
            group.HasBolt = false;
            data.BoltGroups.Add(group);

            DrawingObject[] holes = Build(data).Objects.Where(o => o.Kind == DrawingObjectKind.Hole).ToArray();

            Assert.Equal(4, holes.Length);

            for (int i = 0; i < 4; i++)
            {
                // Through the plate, 0 to 20, at the hole's 22; BEAM is not read, so it adds nothing.
                Assert.True(holes[i].Frame.Origin.IsEqualTo(new GeoPoint3(HoleCentres[i].X, HoleCentres[i].Y, 0), Tol));
                Assert.Equal(PrismVolume(11, 20), holes[i].Solid.GetVolume(Tol), PrismVolume(11, 20) * 1E-9);
                Assert.Equal("HOLE", holes[i].Name);
                Assert.Equal("22", holes[i].Attributes["Diameter"]);
                Assert.Equal("22", holes[i].Attributes["HoleDiameter"]);
                Assert.False(holes[i].Attributes.ContainsKey("Length"));
                Assert.False(holes[i].Attributes.ContainsKey("Standard"));
            }
        }

        [Fact]
        public void ABoltWhoseBodyGivesNothing_RunsThroughItsHoleInThePartsBolted()
        {
            var data = new TeklaDrawingData();
            data.Parts.Add(Plate("PLATE"));
            TeklaBoltGroupData group = FourBolts("PLATE");
            group.BodyCorners = Array.Empty<GeoPoint3>();
            data.BoltGroups.Add(group);

            DrawingObject bolt = Build(data).Objects.First(o => o.Kind == DrawingObjectKind.Bolt);

            Assert.True(bolt.Frame.Origin.IsEqualTo(new GeoPoint3(50, 50, 0), Tol));
            Assert.Equal(PrismVolume(10, 20), bolt.Solid.GetVolume(Tol), PrismVolume(10, 20) * 1E-9);
        }

        [Fact]
        public void ABoltWithNothingAlongItsAxis_IsLeftOutAndCounted()
        {
            var data = new TeklaDrawingData();
            TeklaBoltGroupData group = FourBolts("PLATE");
            group.BodyCorners = Array.Empty<GeoPoint3>();
            data.BoltGroups.Add(group);
            data.BoltGroups.Add(new TeklaBoltGroupData { Id = "EMPTY", HasBolt = true, Diameter = 20, Frame = GeoCoordinateSystem3.Global });

            using (var log = new LogCapture())
            {
                Assert.Empty(Build(data).Objects);

                var debug = Assert.Single(log.Entries, e => e.Level == GeometryHelperLogLevel.Debug);
                Assert.Equal("Test: left out 5: 1 bolt groups with no positions (EMPTY); 4 bolts of no length along their axis (G-1, G-2, G-3, G-4).", debug.Message);
            }
        }

        [Fact]
        public void TheCornersOfOtherBoltsAreOutOfReach()
        {
            // Two bolts 50 apart: the first's corners, 15 off its axis, are 35 off the second's, beyond the reach of its 20.
            GeoPoint3[] corners = { new GeoPoint3(15, 0, 40), new GeoPoint3(-15, 0, -40), new GeoPoint3(50, 15, 10), new GeoPoint3(50, -15, -10) };

            Assert.True(DrawingSourceBuild.TrySpan(new GeoPoint3(50, 0, 0), GeoVector3.ZAxis, 20, corners, out double min, out double max));
            Assert.Equal((-10.0, 10.0), (min, max));
            Assert.False(DrawingSourceBuild.TrySpan(new GeoPoint3(200, 0, 0), GeoVector3.ZAxis, 20, corners, out _, out _));
        }

        [Fact]
        public void AFilletWeldAboveTheLineIsOnTheArrowSide()
        {
            var weld = new TeklaWeldData
            {
                Id = "W",
                Solid = Box(0, 0, 0, 200, 5, 5),
                TypeAbove = "WELD_TYPE_FILLET",
                TypeBelow = "WELD_TYPE_NONE",
                SizeAbove = 5,
                PrefixAbove = "a",
                LengthAbove = 200,
                MainPartId = "BEAM",
                SecondaryPartId = "PLATE",
            };

            DrawingObject read = DrawingSourceBuild.WeldObject(weld);

            Assert.Equal(DrawingObjectKind.Weld, read.Kind);
            Assert.Equal("BEAM", read.ParentId);
            Assert.Equal(GeoCoordinateSystem3.Global, read.Frame);
            Assert.Equal(
                new Dictionary<string, string>
                {
                    [WeldSymbol.LengthAttribute] = "200",
                    [WeldSymbol.SecondaryPartAttribute] = "PLATE",
                    [WeldSymbol.SideAttribute] = "Arrow",
                    [WeldSymbol.SizeAttribute] = "a5",
                    [WeldSymbol.TypeAttribute] = "Fillet",
                },
                read.Attributes.ToDictionary(a => a.Key, a => a.Value));
        }

        [Fact]
        public void AWeldBelowTheLineOnlyIsOnTheOtherSide_AndOneOnBothTakesWhatIsAbove()
        {
            var below = new TeklaWeldData { TypeAbove = "WELD_TYPE_NONE", TypeBelow = "WELD_TYPE_BEVEL_GROOVE_SINGLE_V_BUTT", SizeBelow = 8, PrefixBelow = "s", LengthBelow = 150 };
            var both = new TeklaWeldData { TypeAbove = "WELD_TYPE_FILLET", TypeBelow = "WELD_TYPE_SQUARE_GROOVE_SQUARE_BUTT", SizeAbove = 4, SizeBelow = 6, PrefixAbove = "a", PrefixBelow = "s", LengthAbove = 0, LengthBelow = 80, Length = 120 };

            Dictionary<string, string> belowRead = DrawingSourceBuild.WeldAttributes(below).ToDictionary(a => a.Key, a => a.Value);
            Dictionary<string, string> bothRead = DrawingSourceBuild.WeldAttributes(both).ToDictionary(a => a.Key, a => a.Value);

            Assert.Equal(("VButt", "Other", "s8", "150"), (belowRead["WeldType"], belowRead["WeldSide"], belowRead["WeldSize"], belowRead["WeldLength"]));
            Assert.Equal(("Fillet", "Both", "a4", "120"), (bothRead["WeldType"], bothRead["WeldSide"], bothRead["WeldSize"], bothRead["WeldLength"]));
        }

        [Fact]
        public void AWeldAlongTheWholeJointTakesTheLengthTeklaWorksOut_AndOneOfNoSizeHasNone()
        {
            var weld = new TeklaWeldData { TypeAbove = "WELD_TYPE_FILLET", TypeBelow = "WELD_TYPE_NONE", LengthAbove = 0, Length = 299.999 };

            Dictionary<string, string> read = DrawingSourceBuild.WeldAttributes(weld).ToDictionary(a => a.Key, a => a.Value);

            Assert.Equal("300", read["WeldLength"]);
            Assert.False(read.ContainsKey("WeldSize"));
            Assert.False(read.ContainsKey("WeldSecondaryPart"));
        }

        [Fact]
        public void AWeldOfAKindTheSymbolDoesNotDrawKeepsTeklasName()
        {
            Assert.Equal("WELD_TYPE_PLUG", DrawingSourceBuild.WeldTypeName("WELD_TYPE_PLUG"));
            Assert.Equal("BevelButt", DrawingSourceBuild.WeldTypeName("WELD_TYPE_BEVEL_GROOVE_SINGLE_BEVEL_BUTT"));
        }

        [Fact]
        public void AWeldOfNoKindCarriesOnlyThePartWeldedOn()
        {
            var weld = new TeklaWeldData { TypeAbove = "WELD_TYPE_NONE", TypeBelow = "WELD_TYPE_NONE", SizeAbove = 5, Length = 100, SecondaryPartId = "PLATE" };

            KeyValuePair<string, string> only = Assert.Single(DrawingSourceBuild.WeldAttributes(weld));

            Assert.Equal(new KeyValuePair<string, string>("WeldSecondaryPart", "PLATE"), only);
        }

        /// <summary>
        /// The Tekla names the weld kinds are mapped from are those of the Open API this build is made against, so a
        /// version that renamed one would fail here rather than give welds no symbol.
        /// </summary>
        [Fact]
        public void TheWeldKindsMappedAreNamedAsTheOpenApiNamesThem()
        {
            string[] names = Enum.GetNames(typeof(TSM.BaseWeld.WeldTypeEnum));

            foreach (string mapped in new[] { "WELD_TYPE_NONE", "WELD_TYPE_FILLET", "WELD_TYPE_SQUARE_GROOVE_SQUARE_BUTT", "WELD_TYPE_BEVEL_GROOVE_SINGLE_V_BUTT", "WELD_TYPE_BEVEL_GROOVE_SINGLE_BEVEL_BUTT" })
            {
                Assert.Contains(mapped, names);
            }
        }

        [Fact]
        public void APlateWithFourBoltsAndAWeldComesWithEveryBodyClosedAndValid()
        {
            var data = new TeklaDrawingData();
            data.Parts.Add(Plate("PLATE"));
            data.BoltGroups.Add(FourBolts("PLATE"));
            data.Welds.Add(new TeklaWeldData { Id = "W", Solid = Box(0, 200, 20, 300, 205, 25), TypeAbove = "WELD_TYPE_FILLET", MainPartId = "PLATE" });

            DrawingSource source = Build(data, "PLATE");

            Assert.Equal(6, source.Objects.Count);
            Assert.All(source.Objects, o => Assert.True(o.Solid.Validate(Tol).IsValid, o.Id));
            Assert.Equal(300 * 200 * 20 - 4 * PrismVolume(11, 20), source.Objects.Single(o => o.Id == "PLATE").Solid.GetVolume(Tol), 1E-3);
        }

        [Fact]
        public void TheCallsRefuseNull()
        {
            Assert.Throws<ArgumentNullException>(() => DrawingSourceConvert.FromAssembly(null));
            Assert.Throws<ArgumentNullException>(() => DrawingSourceConvert.FromPart(null));
            Assert.Throws<ArgumentNullException>(() => DrawingSourceConvert.FromObjects(null));
        }

        [Fact]
        public void NoObjectsGivenAskNothingOfTekla_AndGiveAnEmptySource()
        {
            using (var log = new LogCapture())
            {
                DrawingSource source = DrawingSourceConvert.FromObjects(new TSM.ModelObject[] { null });

                Assert.Empty(source.Objects);
                var info = Assert.Single(log.Entries, e => e.Level == GeometryHelperLogLevel.Info);
                Assert.Equal("DrawingSourceConvert.FromObjects: 0 objects (0 parts, 0 bolts, 0 holes, 0 welds) from 0 model objects, 0 left out.", info.Message);
            }
        }

        [Fact]
        public void AnObjectOfAKindNotDrawnIsLeftOutAndCounted_WithoutAskingTekla()
        {
            using (var log = new LogCapture())
            {
                DrawingSource source = DrawingSourceConvert.FromObjects(new TSM.ModelObject[] { new TSM.Grid(), new TSM.Grid() });

                Assert.Empty(source.Objects);
                var debug = Assert.Single(log.Entries, e => e.Level == GeometryHelperLogLevel.Debug);
                Assert.Equal("DrawingSourceConvert.FromObjects: left out 2: 2 objects of a kind not drawn (Grid, Grid).", debug.Message);
            }
        }

        [Fact]
        public void ABoltGroupAsARunningTeklaGaveItIsReadWithZAlongTheBoltAndXTowardsTheSecondPosition()
        {
            // Read from Tekla Structures 2025: four M20, origin (7200, 29850, 7121), X (0, 0, -371) along the bolt, Y (0, -1000, 0).
            GeoCoordinateSystem3? read = DrawingSourceBuild.BoltFrame(new GeoPoint3(7200, 29850, 7121), new GeoVector3(0, 0, -371), new GeoVector3(0, -1000, 0));

            Assert.True(read.HasValue);
            GeoCoordinateSystem3 frame = read.Value;
            Assert.True(frame.Origin.IsEqualTo(new GeoPoint3(7200, 29850, 7121), Tol));
            Assert.True(frame.ZAxis.IsCodirectionalTo(new GeoVector3(0, 0, -1), Tol));
            Assert.True(frame.XAxis.IsCodirectionalTo(new GeoVector3(0, -1, 0), Tol));
            Assert.True(frame.IsValid);
        }

        [Fact]
        public void BoltGroupAxesOfNoLengthOrAlongOneAnother_GiveNoFrame()
        {
            Assert.Null(DrawingSourceBuild.BoltFrame(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 0), new GeoVector3(0, 1, 0)));
            Assert.Null(DrawingSourceBuild.BoltFrame(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 5), new GeoVector3(0, 0, -7)));
        }

        [Fact]
        public void AStackOfTouchingPlatesIsOneRunOfTheBoltAndAPlateApartIsNotPartOfIt()
        {
            GeoSolid3 Plate(double z0, double z1) => Box(-60, -60, z0, 60, 60, z1);

            // Plates 0 to 10 and 10 to 30 touch; a third at 100 to 110 stands apart, up the axis. The bolt starts at z = 30 and goes down.
            Assert.True(DrawingSourceBuild.TryAxisSpan(new GeoPoint3(0, 0, 30), GeoVector3.ZAxis.Multiply(-1.0), new[] { Plate(0, 10), Plate(10, 30), Plate(100, 110) }, Tol, out double min, out double max));

            Assert.Equal(0.0, min, 6);
            Assert.Equal(30.0, max, 6);
        }

        [Fact]
        public void AnAxisThatMeetsNoMaterialGivesNoSpan()
        {
            GeoSolid3 block = Box(0, 0, 0, 10, 10, 10);

            Assert.False(DrawingSourceBuild.TryAxisSpan(new GeoPoint3(100, 100, 5), GeoVector3.ZAxis, new[] { block }, Tol, out _, out _));
        }

        [Fact]
        public void ABoltIsNoLongerThanTeklaSaysEvenWhereItsAxisRunsOnThroughAPart()
        {
            var data = new TeklaDrawingData();
            data.Parts.Add(Plate("PLATE"));
            TeklaBoltGroupData group = FourBolts("PLATE");
            group.BodyCorners = Array.Empty<GeoPoint3>();
            group.Length = 8;
            data.BoltGroups.Add(group);

            DrawingObject bolt = Build(data).Objects.First(o => o.Kind == DrawingObjectKind.Bolt);

            Assert.Equal(PrismVolume(10, 8), bolt.Solid.GetVolume(Tol), PrismVolume(10, 8) * 1E-9);
            Assert.True(bolt.Frame.Origin.IsEqualTo(new GeoPoint3(50, 50, 12), Tol));
        }
    }
}
