using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The GEODATA of a drawing, AutoCAD's GeoLocationData: made in code, posted to model space's extension dictionary, set and
    /// read back, its coordinate system in pieces and the points of its mesh, one of version 1 read by the groups ezdxf's
    /// comment gives it, and one in ezdxf's own shape. ezdxf 1.4.5 read the probe's drawings back with the values set, the
    /// GEODATA model space's, its recover and audit finding nothing and no handle left dangling.
    /// </summary>
    public class DxfGeoDataTests
    {
        private const string GeoDataClass = "  0\nCLASS\n  1\nGEODATA\n  2\nAcDbGeoData\n  3\nObjectDBX Classes\n 90\n4095\n 91\n0\n280\n0\n281\n0\n";

        [Fact]
        public void AGeoDataMadeInCode_IsEzdxfsNewOne()
        {
            var geo = new DxfGeoData();

            Assert.Equal(DxfTypeOfCoordinates.Geographic, geo.TypeOfCoordinates);
            Assert.Equal(Bits(new GeoPoint3(0, 0, 0)), Bits(geo.DesignPoint));
            Assert.Equal(Bits(new GeoPoint3(0, 0, 0)), Bits(geo.ReferencePoint));
            Assert.Equal(Bits(1.0), Bits(geo.HorizontalUnitsScale));
            Assert.Equal(DxfUnits.Inches, geo.HorizontalUnits);
            Assert.Equal(Bits(1.0), Bits(geo.VerticalUnitsScale));
            Assert.Equal(DxfUnits.Inches, geo.VerticalUnits);
            Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(geo.UpDirection));
            Assert.Equal(Bits(1.0), Bits(geo.NorthDirectionVector.Y));
            Assert.Equal(Bits(0.0), Bits(geo.NorthDirectionVector.X));
            Assert.Equal(DxfScaleEstimationMethod.Unity, geo.ScaleEstimationMethod);
            Assert.Equal(Bits(1.0), Bits(geo.ScaleFactor));
            Assert.False(geo.DoSeaLevelCorrection);
            Assert.Equal(Bits(0.0), Bits(geo.SeaLevelElevation));
            Assert.Equal(Bits(0.0), Bits(geo.CoordinateProjectionRadius));
            Assert.Equal(string.Empty, geo.CoordinateSystem);
            Assert.Equal(string.Empty, geo.GeoRSSTag);
            Assert.Equal(0, geo.NumMeshPoints);
            Assert.Empty(geo.GetMeshPointMaps());
            Assert.Null(geo.BlockTableRecord);
            Assert.Null(geo.Handle);
            Assert.Equal("GEODATA , Geographic", geo.ToString());
        }

        [Fact]
        public void AGeoDataPostedToModelSpace_IsWrittenInItsExtensionDictionary_AndReadsBackAsSet()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            Assert.Null(db.GeoDataObject);

            // A drawing in millimetres on a local grid, its point (100, 200) at 500 000 east and 4 100 000 north.
            var geo = new DxfGeoData
            {
                BlockTableRecord = db.ModelSpace,
                TypeOfCoordinates = DxfTypeOfCoordinates.Local,
                DesignPoint = new GeoPoint3(100, 200, 0),
                ReferencePoint = new GeoPoint3(500000, 4100000, 12.5),
                HorizontalUnits = DxfUnits.Millimeters,
                HorizontalUnitsScale = 0.001,
            };
            geo.PostToDb();

            Assert.Same(geo, db.GeoDataObject);
            Assert.Same(db.ModelSpace, geo.BlockTableRecord);
            string dictionary = db.ModelSpace.ExtensionDictionary.Handle;
            Assert.Equal(dictionary, geo.OwnerHandle);
            Assert.Same(geo, db.ModelSpace.ExtensionDictionary.GetAt("ACAD_GEOGRAPHICDATA"));

            // The GEODATA as ezdxf writes a new one, the block's handle after the version; model space's BLOCK_RECORD given its
            // extension dictionary, which names the GEODATA by a 360; the class listed.
            string saved = db.ToText();
            Assert.Equal(
                "  0\nGEODATA\n  5\n" + geo.Handle + "\n102\n{ACAD_REACTORS\n330\n" + dictionary + "\n102\n}\n330\n" + dictionary + "\n100\nAcDbGeoData\n 90\n2\n330\n17\n 70\n1\n"
                + " 10\n100\n 20\n200\n 30\n0\n 11\n500000\n 21\n4100000\n 31\n12.5\n 40\n0.001\n 91\n4\n 41\n1.0\n 92\n1\n210\n0.0\n220\n0.0\n230\n1.0\n 12\n0.0\n 22\n1.0\n"
                + " 95\n1\n141\n1.0\n294\n0\n142\n0.0\n143\n0.0\n301\n\n 93\n0\n 96\n0\n",
                Records(saved).Single(r => HandleOf(r) == geo.Handle));
            Assert.Contains("  3\nACAD_GEOGRAPHICDATA\n360\n" + geo.Handle + "\n", Records(saved).Single(r => HandleOf(r) == dictionary), StringComparison.Ordinal);
            Assert.Contains("102\n{ACAD_XDICTIONARY\n360\n" + dictionary + "\n102\n}\n", Records(saved).Single(r => HandleOf(r) == "17"), StringComparison.Ordinal);

            Differ(text, saved, out List<string> gone, out List<string> come);
            Assert.Equal(new[] { "17" }, gone.Where(r => !r.Contains("\nHEADER\n")).Select(HandleOf).ToArray());
            Assert.Equal(new[] { "17", dictionary, geo.Handle }.OrderBy(h => h, StringComparer.Ordinal), come.Where(r => !r.Contains("\nHEADER\n") && r != GeoDataClass).Select(HandleOf).OrderBy(h => h, StringComparer.Ordinal));
            Assert.Single(come, r => r == GeoDataClass);

            DxfGeoData read = DxfDatabase.Parse(saved).GeoDataObject;
            Assert.Equal(geo.Handle, read.Handle);
            Assert.Equal(DxfTypeOfCoordinates.Local, read.TypeOfCoordinates);
            Assert.Equal(Bits(new GeoPoint3(100, 200, 0)), Bits(read.DesignPoint));
            Assert.Equal(Bits(new GeoPoint3(500000, 4100000, 12.5)), Bits(read.ReferencePoint));
            Assert.Equal(DxfUnits.Millimeters, read.HorizontalUnits);
            Assert.Equal(Bits(0.001), Bits(read.HorizontalUnitsScale));
            Assert.Equal("*Model_Space", read.BlockTableRecord.Name);
            Assert.Equal("GEODATA " + geo.Handle + ", Local", read.ToString());
        }

        [Fact]
        public void AGeoDataOfTheFile_IsSetInPlace_AndSettingWhatItHasChangesNothing()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            new DxfGeoData { BlockTableRecord = db.ModelSpace }.PostToDb();
            db = DxfDatabase.Parse(db.ToText());
            DxfGeoData geo = db.GeoDataObject;
            string before = db.ToText();

            geo.TypeOfCoordinates = geo.TypeOfCoordinates;
            geo.DesignPoint = geo.DesignPoint;
            geo.ReferencePoint = geo.ReferencePoint;
            geo.HorizontalUnitsScale = geo.HorizontalUnitsScale;
            geo.HorizontalUnits = geo.HorizontalUnits;
            geo.VerticalUnitsScale = geo.VerticalUnitsScale;
            geo.VerticalUnits = geo.VerticalUnits;
            geo.UpDirection = geo.UpDirection;
            geo.NorthDirectionVector = geo.NorthDirectionVector;
            geo.ScaleEstimationMethod = geo.ScaleEstimationMethod;
            geo.ScaleFactor = geo.ScaleFactor;
            geo.DoSeaLevelCorrection = geo.DoSeaLevelCorrection;
            geo.SeaLevelElevation = geo.SeaLevelElevation;
            geo.CoordinateProjectionRadius = geo.CoordinateProjectionRadius;
            geo.CoordinateSystem = geo.CoordinateSystem;
            geo.GeoRSSTag = geo.GeoRSSTag;
            geo.ObservationFromTag = geo.ObservationFromTag;
            geo.ObservationToTag = geo.ObservationToTag;
            geo.ObservationCoverageTag = geo.ObservationCoverageTag;
            geo.SetMeshPointMaps(new GeoPoint2[0], new GeoPoint2[0]);
            geo.ResetMeshPointMaps();
            Assert.Equal(before, db.ToText());

            geo.VerticalUnits = DxfUnits.Meters;
            geo.UpDirection = new GeoVector3(0, 0.6, 0.8);
            geo.NorthDirectionVector = new GeoVector2(-1, 0);
            geo.ScaleEstimationMethod = DxfScaleEstimationMethod.Prismoidal;
            geo.DoSeaLevelCorrection = true;
            geo.SeaLevelElevation = -2.5;
            geo.GeoRSSTag = "<georss:point>36.9 127.0</georss:point>";
            geo.ObservationCoverageTag = "coverage";
            OnlyTheseChanged(before, db.ToText(), geo.Handle);

            // Each value in its place, the tags absent from ezdxf's new GEODATA put where AutoCAD's order puts them.
            string record = Records(db.ToText()).Single(r => HandleOf(r) == geo.Handle);
            Assert.Contains(" 92\n6\n210\n0\n220\n0.6\n230\n0.8\n 12\n-1\n 22\n0\n 95\n4\n141\n1.0\n294\n1\n142\n-2.5\n143\n0.0\n301\n\n302\n<georss:point>36.9 127.0</georss:point>\n307\ncoverage\n 93\n0\n", record, StringComparison.Ordinal);
            DxfGeoData read = DxfDatabase.Parse(db.ToText()).GeoDataObject;
            Assert.Equal(DxfUnits.Meters, read.VerticalUnits);
            Assert.Equal(Bits(new GeoVector3(0, 0.6, 0.8)), Bits(read.UpDirection));
            Assert.Equal(Bits(-1.0), Bits(read.NorthDirectionVector.X));
            Assert.Equal(DxfScaleEstimationMethod.Prismoidal, read.ScaleEstimationMethod);
            Assert.True(read.DoSeaLevelCorrection);
            Assert.Equal(Bits(-2.5), Bits(read.SeaLevelElevation));
            Assert.Equal("coverage", read.ObservationCoverageTag);
            Assert.Equal(string.Empty, read.ObservationFromTag);
        }

        [Fact]
        public void TheCoordinateSystem_IsWrittenInPiecesOf255_NoPieceEndingOnACaret_AsEzdxfWritesIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            var geo = new DxfGeoData { BlockTableRecord = db.ModelSpace };

            // 254 letters, a line feed written ^J from the 255th character, then 300 more: the first piece stops short of the caret.
            string definition = new string('a', 254) + "\n" + new string('b', 300);
            geo.CoordinateSystem = definition;
            geo.PostToDb();

            string record = Records(db.ToText()).Single(r => HandleOf(r) == geo.Handle);
            Assert.Contains("143\n0.0\n303\n" + new string('a', 254) + "\n303\n^J" + new string('b', 253) + "\n301\n" + new string('b', 47) + "\n 93\n0\n", record, StringComparison.Ordinal);
            Assert.Equal(definition, DxfDatabase.Parse(db.ToText()).GeoDataObject.CoordinateSystem);

            // Shortened, the pieces go; a carriage return is written as a line feed.
            geo.CoordinateSystem = "<A>\r\n<B>\r<C>";
            record = Records(db.ToText()).Single(r => HandleOf(r) == geo.Handle);
            Assert.DoesNotContain("303\n", record, StringComparison.Ordinal);
            Assert.Contains("143\n0.0\n301\n<A>^J<B>^J<C>\n 93\n0\n", record, StringComparison.Ordinal);
            Assert.Equal("<A>\n<B>\n<C>", DxfDatabase.Parse(db.ToText()).GeoDataObject.CoordinateSystem);
        }

        [Fact]
        public void TheCoordinateSystem_IsNotCutBetweenTheTwoHalvesOfALetterOfTwoUnits()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018.Replace("$ACADVER\n  1\nAC1032\n", "$ACADVER\n  1\nAC1024\n"));
            var geo = new DxfGeoData { BlockTableRecord = db.ModelSpace };

            // 254 letters, then U+1F600, two units of UTF-16 from the 255th character: the first piece stops short of it.
            string definition = new string('a', 254) + "\U0001F600" + "tail";
            geo.CoordinateSystem = definition;
            geo.PostToDb();

            // No value ends on the first half, as a letter or as its escape, which a reader takes for a letter of its own.
            string record = Records(db.ToText()).Single(r => HandleOf(r) == geo.Handle);
            Assert.DoesNotContain(record.Split('\n'), line => (line.Length > 0 && char.IsHighSurrogate(line[line.Length - 1])) || Regex.IsMatch(line, @"\\U\+D[89AB][0-9A-F]{2}$"));
            Assert.Contains("143\n0.0\n303\n" + new string('a', 254) + "\n301\n\U0001F600tail\n 93\n0\n", record, StringComparison.Ordinal);

            using (var stream = new MemoryStream())
            {
                db.Save(stream);
                stream.Position = 0;
                Assert.Equal(definition, DxfDatabase.Open(stream).GeoDataObject.CoordinateSystem);
            }
        }

        [Fact]
        public void AGeoDataInEzdxfsOwnShape_IsRead_AndSavedAsItWas()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            var made = new DxfGeoData
            {
                BlockTableRecord = db.ModelSpace,
                TypeOfCoordinates = DxfTypeOfCoordinates.Local,
                DesignPoint = new GeoPoint3(10, 20, 0),
                ReferencePoint = new GeoPoint3(500000, 4100000, 0),
            };
            made.PostToDb();
            string handle = made.Handle;

            // As ezdxf writes one: named by a 350, its host 0, the coordinate system in a piece of 303 and the last of 301, three
            // points of the mesh and a face on them.
            string text = db.ToText()
                .Replace("  3\nACAD_GEOGRAPHICDATA\n360\n" + handle + "\n", "  3\nACAD_GEOGRAPHICDATA\n350\n" + handle + "\n")
                .Replace("AcDbGeoData\n 90\n2\n330\n17\n", "AcDbGeoData\n 90\n2\n330\n0\n")
                .Replace("301\n\n 93\n0\n 96\n0\n", "303\n<A>^J<B>\n301\n</B>^J</A>^J\n302\nrss\n 93\n3\n"
                    + " 13\n0.0\n 23\n0.0\n 14\n500000.0\n 24\n4100000.0\n 13\n1000.0\n 23\n0.0\n 14\n500001.0\n 24\n4100000.0\n"
                    + " 13\n0.0\n 23\n1000.0\n 14\n500000.0\n 24\n4100001.0\n 96\n1\n 97\n0\n 98\n1\n 99\n2\n");
            Assert.Contains("ACAD_GEOGRAPHICDATA\n350\n", text, StringComparison.Ordinal);
            Assert.Contains("AcDbGeoData\n 90\n2\n330\n0\n", text, StringComparison.Ordinal);
            Assert.Contains(" 99\n2\n", text, StringComparison.Ordinal);
            DxfDatabase ezdxfs = DxfDatabase.Parse(text);
            DxfGeoData geo = ezdxfs.GeoDataObject;

            Assert.Equal(handle, geo.Handle);
            Assert.Null(geo.BlockTableRecord);
            Assert.Equal(DxfTypeOfCoordinates.Local, geo.TypeOfCoordinates);
            Assert.Equal(Bits(new GeoPoint3(10, 20, 0)), Bits(geo.DesignPoint));
            Assert.Equal(Bits(new GeoPoint3(500000, 4100000, 0)), Bits(geo.ReferencePoint));
            Assert.Equal("<A>\n<B></B>\n</A>\n", geo.CoordinateSystem);
            Assert.Equal("rss", geo.GeoRSSTag);
            Assert.Equal(3, geo.NumMeshPoints);
            Assert.Equal(Bits(1000.0), Bits(geo.GetMeshPointMap(2).SourcePoint.Y));
            Assert.Equal(Bits(4100001.0), Bits(geo.GetMeshPointMap(2).DestPoint.Y));
            Assert.Equal(text, ezdxfs.ToText());

            // A point put in keeps the face as it is.
            geo.AddMeshPointMap(3, new GeoPoint2(1000, 1000), new GeoPoint2(500001, 4100001));
            string record = Records(ezdxfs.ToText()).Single(r => HandleOf(r) == handle);
            Assert.Contains(" 93\n4\n", record, StringComparison.Ordinal);
            Assert.Contains(" 14\n500001\n 24\n4100001\n 96\n1\n 97\n0\n 98\n1\n 99\n2\n", record, StringComparison.Ordinal);
            Assert.Equal(4, DxfDatabase.Parse(ezdxfs.ToText()).GeoDataObject.NumMeshPoints);
        }

        [Fact]
        public void ThePointsOfTheMesh_AreSetPutInAndTakenOut_TheFacesKeptAsTheyAre()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            var geo = new DxfGeoData { BlockTableRecord = db.ModelSpace };
            geo.SetMeshPointMaps(new[] { new GeoPoint2(0, 0), new GeoPoint2(1000, 0) }, new[] { new GeoPoint2(500000, 4100000), new GeoPoint2(500001, 4100000) });
            geo.PostToDb();
            Assert.Contains(" 93\n2\n 13\n0\n 23\n0\n 14\n500000\n 24\n4100000\n 13\n1000\n 23\n0\n 14\n500001\n 24\n4100000\n 96\n0\n", Records(db.ToText()).Single(r => HandleOf(r) == geo.Handle), StringComparison.Ordinal);

            // A face of the mesh written into the file, as AutoCAD writes one.
            db = DxfDatabase.Parse(db.ToText().Replace("4100000\n 96\n0\n", "4100000\n 96\n1\n 97\n0\n 98\n1\n 99\n0\n"));
            geo = db.GeoDataObject;
            Assert.Equal(2, geo.NumMeshPoints);
            Assert.Equal(Bits(1000.0), Bits(geo.GetMeshPointMap(1).SourcePoint.X));
            Assert.Equal(Bits(500001.0), Bits(geo.GetMeshPointMap(1).DestPoint.X));

            geo.AddMeshPointMap(1, new GeoPoint2(500, 500), new GeoPoint2(500000.5, 4100000.5));
            Assert.Equal(new[] { 0.0, 500.0, 1000.0 }.Select(Bits), geo.GetMeshPointMaps().Select(m => Bits(m.SourcePoint.X)));
            Assert.Contains(" 93\n3\n 13\n0\n 23\n0\n 14\n500000\n 24\n4100000\n 13\n500\n 23\n500\n 14\n500000.5\n 24\n4100000.5\n 13\n1000\n", Records(db.ToText()).Single(r => HandleOf(r) == geo.Handle), StringComparison.Ordinal);
            Assert.Contains(" 96\n1\n 97\n0\n 98\n1\n 99\n0\n", Records(db.ToText()).Single(r => HandleOf(r) == geo.Handle), StringComparison.Ordinal);

            geo.ResetMeshPointMaps();
            string record = Records(db.ToText()).Single(r => HandleOf(r) == geo.Handle);
            Assert.Contains("301\n\n 93\n0\n 96\n1\n 97\n0\n 98\n1\n 99\n0\n", record, StringComparison.Ordinal);
            Assert.Equal(0, DxfDatabase.Parse(db.ToText()).GeoDataObject.NumMeshPoints);
            Assert.Throws<ArgumentOutOfRangeException>(() => geo.GetMeshPointMap(0));
        }

        [Fact]
        public void AGeoDataOfVersion1_IsReadByTheGroupsEzdxfsCommentGivesIt_ItsNorthAndMeshNotWritten()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            DxfGeoData made = new DxfGeoData { BlockTableRecord = db.ModelSpace, TypeOfCoordinates = DxfTypeOfCoordinates.Grid };
            made.PostToDb();

            // Version 1 (R2009): no north direction, the points of the mesh in 12 and 22, those they are taken to in 13 and 23, as
            // ezdxf's comment maps them; ezdxf 1.4.5 itself reads no points there.
            string version1 = db.ToText().Replace("AcDbGeoData\n 90\n2\n", "AcDbGeoData\n 90\n1\n").Replace("230\n1.0\n 12\n0.0\n 22\n1.0\n 95\n", "230\n1.0\n 95\n")
                .Replace("301\n\n 93\n0\n", "301\n\n 93\n1\n 12\n5\n 22\n6\n 13\n7\n 23\n8\n");
            DxfDatabase old = DxfDatabase.Parse(version1);
            DxfGeoData geo = old.GeoDataObject;

            Assert.Equal(DxfTypeOfCoordinates.Grid, geo.TypeOfCoordinates);
            Assert.Equal(Bits(0.0), Bits(geo.NorthDirectionVector.X));
            Assert.Equal(Bits(1.0), Bits(geo.NorthDirectionVector.Y));
            Assert.Equal(1, geo.NumMeshPoints);
            Assert.Equal(Bits(5.0), Bits(geo.GetMeshPointMap(0).SourcePoint.X));
            Assert.Equal(Bits(6.0), Bits(geo.GetMeshPointMap(0).SourcePoint.Y));
            Assert.Equal(Bits(7.0), Bits(geo.GetMeshPointMap(0).DestPoint.X));
            Assert.Equal(Bits(8.0), Bits(geo.GetMeshPointMap(0).DestPoint.Y));

            Assert.Throws<InvalidOperationException>(() => geo.NorthDirectionVector = new GeoVector2(1, 0));
            Assert.Throws<InvalidOperationException>(() => geo.ResetMeshPointMaps());
            Assert.Throws<InvalidOperationException>(() => geo.AddMeshPointMap(0, new GeoPoint2(1, 1), new GeoPoint2(2, 2)));
            Assert.Equal(version1, old.ToText());

            geo.TypeOfCoordinates = DxfTypeOfCoordinates.Local;
            Assert.Equal(DxfTypeOfCoordinates.Local, DxfDatabase.Parse(old.ToText()).GeoDataObject.TypeOfCoordinates);
        }

        [Fact]
        public void ASecondGeoData_ErasesTheFirst_AndOneErasedTakesItsEntryWithIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            var first = new DxfGeoData { BlockTableRecord = db.ModelSpace };
            first.PostToDb();
            var second = new DxfGeoData { BlockTableRecord = db.ModelSpace, TypeOfCoordinates = DxfTypeOfCoordinates.Local };
            second.PostToDb();

            Assert.True(first.IsErased);
            Assert.Same(second, db.GeoDataObject);
            Assert.Single(Records(db.ToText()), r => r.StartsWith("  0\nGEODATA\n", StringComparison.Ordinal));
            Assert.Equal(1, db.ModelSpace.ExtensionDictionary.Count);

            second.Erase();
            Assert.True(second.IsErased);
            Assert.Null(db.GeoDataObject);
            Assert.Equal(0, db.ModelSpace.ExtensionDictionary.Count);
            Assert.DoesNotContain(Records(db.ToText()), r => r.StartsWith("  0\nGEODATA\n", StringComparison.Ordinal));
            Assert.DoesNotContain("ACAD_GEOGRAPHICDATA", db.ToText(), StringComparison.Ordinal);
            Assert.Throws<InvalidOperationException>(() => second.Erase());
        }

        [Fact]
        public void AGeoDataGivenToADrawingThatListsNoClassGeoData_ListsItOnce()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            Assert.DoesNotContain("\nAcDbGeoData\n", db.ToText(), StringComparison.Ordinal);

            new DxfGeoData { BlockTableRecord = db.ModelSpace }.PostToDb();
            new DxfGeoData { BlockTableRecord = db.ModelSpace }.PostToDb();

            // Listed last of the CLASSES section, as ezdxf lists it: ObjectDBX Classes, flags 4095.
            string saved = db.ToText();
            Assert.Single(Records(saved), r => r == GeoDataClass);
            int classes = saved.IndexOf("\nCLASSES\n", StringComparison.Ordinal);
            Assert.Equal(saved.IndexOf("  0\nENDSEC\n", classes, StringComparison.Ordinal), saved.IndexOf(GeoDataClass, StringComparison.Ordinal) + GeoDataClass.Length);
            Assert.Single(Records(DxfDatabase.Parse(saved).ToText()), r => r == GeoDataClass);
        }

        [Fact]
        public void AGeoDataPostedWrongly_OrSetOutsideItsRange_IsRefused_AndNothingIsWritten()
        {
            Assert.Throws<InvalidOperationException>(() => new DxfGeoData().PostToDb());
            Assert.Throws<ArgumentNullException>(() => new DxfGeoData { BlockTableRecord = null });

            // R2000 has no GEODATA; R12 has no OBJECTS section.
            DxfDatabase r2000 = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2000);
            Assert.Throws<InvalidOperationException>(() => new DxfGeoData { BlockTableRecord = r2000.ModelSpace }.PostToDb());
            Assert.Equal(DxfObjectsEzdxfDrawings.R2000, r2000.ToText());
            Assert.Null(r2000.GeoDataObject);
            Assert.Null(DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R12).GeoDataObject);

            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            Assert.Throws<InvalidOperationException>(() => new DxfGeoData { BlockTableRecord = new DxfBlockTableRecord { Name = "GH_LOOSE" } }.PostToDb());
            var geo = new DxfGeoData { BlockTableRecord = db.ModelSpace };
            geo.PostToDb();
            string text = db.ToText();

            Assert.Throws<InvalidOperationException>(() => geo.PostToDb());
            Assert.Throws<InvalidOperationException>(() => geo.BlockTableRecord = db.PaperSpace);
            Assert.Throws<ArgumentOutOfRangeException>(() => geo.TypeOfCoordinates = (DxfTypeOfCoordinates)4);
            Assert.Throws<ArgumentOutOfRangeException>(() => geo.ScaleEstimationMethod = (DxfScaleEstimationMethod)0);
            Assert.Throws<ArgumentOutOfRangeException>(() => geo.HorizontalUnits = (DxfUnits)99);
            Assert.Throws<ArgumentOutOfRangeException>(() => geo.HorizontalUnitsScale = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => geo.ScaleFactor = double.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => geo.SeaLevelElevation = double.PositiveInfinity);
            Assert.Throws<ArgumentException>(() => geo.DesignPoint = new GeoPoint3(double.NaN, 0, 0));
            Assert.Throws<ArgumentException>(() => geo.UpDirection = new GeoVector3(0, 0, 0));
            Assert.Throws<ArgumentException>(() => geo.NorthDirectionVector = new GeoVector2(0, 0));
            Assert.Throws<ArgumentNullException>(() => geo.CoordinateSystem = null);
            Assert.Throws<ArgumentNullException>(() => geo.GeoRSSTag = null);
            Assert.Throws<ArgumentException>(() => geo.ObservationToTag = "a\nb");
            Assert.Throws<ArgumentNullException>(() => geo.SetMeshPointMaps(null, new GeoPoint2[0]));
            Assert.Throws<ArgumentException>(() => geo.SetMeshPointMaps(new[] { new GeoPoint2(0, 0) }, new GeoPoint2[0]));
            Assert.Throws<ArgumentException>(() => geo.SetMeshPointMaps(new[] { new GeoPoint2(0, double.NaN) }, new[] { new GeoPoint2(0, 0) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => geo.AddMeshPointMap(1, new GeoPoint2(0, 0), new GeoPoint2(0, 0)));
            Assert.Equal(text, db.ToText());

            geo.Erase();
            Assert.Throws<InvalidOperationException>(() => geo.TypeOfCoordinates = DxfTypeOfCoordinates.Local);
        }
    }
}
