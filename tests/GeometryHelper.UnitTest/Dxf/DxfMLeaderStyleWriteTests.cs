using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Multileader styles (MLEADERSTYLE) made in code and added to a drawing, and the values of a style set, written as
    /// AutoCAD 2021 holds them (checked by entget on styles its API made: angles in radians, no limit as 0, scale to layout as 0).
    /// </summary>
    public class DxfMLeaderStyleWriteTests
    {
        /// <summary>A drawing of R2013 in millimetres with a dictionary of multileader styles, made in code.</summary>
        private static DxfDatabase Drawing(string version = "AC1027")
        {
            var db = new DxfDatabase(version);
            db.SetHeader("$MEASUREMENT", new DxfTypedValue(70, "1"));

            if (db.MLeaderStyleDictionary == null)
            {
                db.NamedObjectsDictionary.SetAt("ACAD_MLEADERSTYLE", new DxfDBDictionary());
            }

            return db;
        }

        private static string Lf(string text) => text.Replace("\r\n", "\n");

        [Fact]
        public void AStyleMadeAnnotative_HasItsGroup296Set_AndNoExtendedData_AsAutoCad2021Writes()
        {
            // AutoCAD 2021's MLeaderStyle.Annotative = True, dumped with entget and every application's data: 296 is 1 and
            // there is no AcadAnnotative, as on the style Annotative of acad.dwt; Standard has 0.
            DxfDatabase db = Drawing();
            var style = new DxfMLeaderStyle();
            db.MLeaderStyleDictionary.SetAt("GH_NOTES", style);
            Assert.Equal(DxfAnnotativeStates.False, style.Annotative);

            style.Annotative = DxfAnnotativeStates.True;

            string record = DxfModel.Records(Lf(db.ToText())).Single(r => r.StartsWith("  0\nMLEADERSTYLE\n", StringComparison.Ordinal) && r.Contains("\n  5\n" + style.Handle + "\n"));
            Assert.Contains("\n296\n1\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("1001", record, StringComparison.Ordinal);
            var again = (DxfMLeaderStyle)DxfDatabase.Parse(db.ToText()).MLeaderStyleDictionary["GH_NOTES"];
            Assert.Equal(DxfAnnotativeStates.True, again.Annotative);

            style.Annotative = DxfAnnotativeStates.False;
            Assert.Equal(DxfAnnotativeStates.False, style.Annotative);
            Assert.Contains("\n296\n0\n", DxfModel.Records(Lf(db.ToText())).Single(r => r.Contains("\n  5\n" + style.Handle + "\n")), StringComparison.Ordinal);
            Assert.Throws<InvalidOperationException>(() => new DxfMLeaderStyle().Annotative = DxfAnnotativeStates.True);
        }

        [Fact]
        public void ANewStyleAdded_IsWrittenWithEveryGroupAutoCadHolds_InItsOrder_AndReadsBack()
        {
            DxfDatabase db = Drawing();
            var style = new DxfMLeaderStyle { TextHeight = 2.5, ArrowSize = 2.5 };
            db.MLeaderStyleDictionary.SetAt("Standard", style);

            Assert.Equal("Standard", style.Name);
            Assert.NotNull(style.Handle);

            string text = Lf(db.ToText());
            Assert.Contains("100\nAcDbMLeaderStyle\n179\n2\n170\n2\n171\n1\n172\n0\n 90\n2\n 40\n0.0\n 41\n0.0\n173\n1\n 91\n-1056964608\n 92\n-2\n290\n1\n 42\n2.0\n291\n1\n 43\n8.0\n  3\n\n 44\n2.5\n300\n\n174\n1\n178\n1\n175\n1\n176\n0\n 93\n-1056964608\n 45\n2.5\n", text, StringComparison.Ordinal);
            Assert.Contains("142\n1.0\n295\n0\n296\n0\n143\n3.75\n271\n0\n272\n9\n273\n9\n298\n0\n", text, StringComparison.Ordinal);

            var read = (DxfMLeaderStyle)DxfDatabase.Parse(text).MLeaderStyleDictionary["Standard"];
            Assert.Equal(2.5, read.TextHeight);
            Assert.Equal(2.5, read.ArrowSize);
            Assert.Equal(8.0, read.DoglegLength);
            Assert.Equal(3.75, read.BreakSize);
        }

        [Fact]
        public void ANewStyleAdded_ListsItsClass_CountedOnce()
        {
            DxfDatabase db = Drawing();
            db.MLeaderStyleDictionary.SetAt("Standard", new DxfMLeaderStyle());

            string text = Lf(db.ToText());
            Assert.Contains("  0\nCLASS\n  1\nMLEADERSTYLE\n  2\nAcDbMLeaderStyle\n  3\nACDB_MLEADERSTYLE_CLASS\n 90\n4095\n 91\n1\n", text, StringComparison.Ordinal);
        }

        [Fact]
        public void TheValuesSetOnAStyleOfADrawing_AreWrittenAsAutoCad2021HoldsThem()
        {
            DxfDatabase db = Drawing();
            var style = new DxfMLeaderStyle();
            db.MLeaderStyleDictionary.SetAt("GH_NOTES", style);

            style.FirstSegmentAngleConstraint = DxfAngleConstraint.Degrees045;
            style.SecondSegmentAngleConstraint = DxfAngleConstraint.Degrees090;
            style.MaxLeaderSegmentsPoints = 0;
            style.Scale = 0;
            style.ExtendLeaderToText = true;
            style.DefaultMText = new DxfMText { Contents = "Default Text" };
            style.LeaderLineType = DxfLeaderType.SplineLeader;
            style.LeaderLineColor = DxfColor.OfIndex(1);
            style.LeaderLineWeight = DxfLineWeight.LineWeight035;
            style.EnableLanding = false;
            style.EnableDogleg = false;
            style.DoglegLength = 6;
            style.LandingGap = 1;
            style.BreakSize = 2;
            style.TextColor = DxfColor.OfIndex(3);
            style.EnableFrameText = true;
            style.TextAlignAlwaysLeft = true;
            style.TextAngleType = DxfTextAngleType.InsertAngle;
            style.TextAttachmentDirection = DxfTextAttachmentDirection.AttachmentVertical;
            style.SetTextAttachmentType(DxfTextAttachmentType.AttachmentLinedCenter, DxfLeaderDirectionType.TopLeader);
            style.SetTextAttachmentType(DxfTextAttachmentType.AttachmentBottomOfTop, DxfLeaderDirectionType.RightLeader);
            style.ContentType = DxfContentType.NoneContent;

            string text = Lf(db.ToText());
            Assert.Contains("\n 40\n0.7853981633974483\n 41\n1.5707963267948966\n", text, StringComparison.Ordinal);

            var read = (DxfMLeaderStyle)DxfDatabase.Parse(text).MLeaderStyleDictionary["GH_NOTES"];
            Assert.Equal(DxfAngleConstraint.Degrees045, read.FirstSegmentAngleConstraint);
            Assert.Equal(DxfAngleConstraint.Degrees090, read.SecondSegmentAngleConstraint);
            Assert.Equal(0, read.MaxLeaderSegmentsPoints);
            Assert.Equal(0.0, read.Scale);
            Assert.True(read.ExtendLeaderToText);
            Assert.Equal("Default Text", read.DefaultMText.Contents);
            Assert.Equal(DxfLeaderType.SplineLeader, read.LeaderLineType);
            Assert.Equal(1, read.LeaderLineColor.ColorIndex);
            Assert.Equal(DxfLineWeight.LineWeight035, read.LeaderLineWeight);
            Assert.False(read.EnableLanding);
            Assert.False(read.EnableDogleg);
            Assert.Equal(6.0, read.DoglegLength);
            Assert.Equal(1.0, read.LandingGap);
            Assert.Equal(2.0, read.BreakSize);
            Assert.Equal(3, read.TextColor.ColorIndex);
            Assert.True(read.EnableFrameText);
            Assert.True(read.TextAlignAlwaysLeft);
            Assert.Equal(DxfTextAngleType.InsertAngle, read.TextAngleType);
            Assert.Equal(DxfTextAttachmentDirection.AttachmentVertical, read.TextAttachmentDirection);
            Assert.Equal(DxfTextAttachmentType.AttachmentLinedCenter, read.GetTextAttachmentType(DxfLeaderDirectionType.TopLeader));
            Assert.Equal(DxfTextAttachmentType.AttachmentBottomOfTop, read.GetTextAttachmentType(DxfLeaderDirectionType.RightLeader));
            Assert.Equal(DxfContentType.NoneContent, read.ContentType);
        }

        [Fact]
        public void TheRecordsOfAStyle_AreThoseOfItsDrawing_ByHandle()
        {
            DxfDatabase db = Drawing();
            var style = new DxfMLeaderStyle();
            db.MLeaderStyleDictionary.SetAt("GH_NOTES", style);
            DxfLinetypeTableRecord byBlock = db.LinetypeTable["ByBlock"];
            DxfTextStyleTableRecord standard = db.TextStyleTable["Standard"];

            style.LeaderLineLinetype = byBlock;
            style.TextStyle = standard;

            var read = (DxfMLeaderStyle)DxfDatabase.Parse(db.ToText()).MLeaderStyleDictionary["GH_NOTES"];
            Assert.Equal("ByBlock", read.LeaderLineLinetype.Name);
            Assert.Equal("Standard", read.TextStyle.Name);

            style.LeaderLineLinetype = null;
            Assert.Null(style.LeaderLineLinetype);
            Assert.Throws<ArgumentException>(() => style.TextStyle = Drawing().TextStyleTable["Standard"]);
        }

        [Fact]
        public void AStyleInNoFile_GivenRecordsOfTheDrawing_IsWrittenWithThem_AndOthersLeftOut()
        {
            DxfDatabase db = Drawing();
            var style = new DxfMLeaderStyle { TextStyle = db.TextStyleTable["Standard"], LeaderLineLinetype = db.LinetypeTable["ByBlock"] };
            db.MLeaderStyleDictionary.SetAt("GH_NOTES", style);

            Assert.Equal("Standard", style.TextStyle.Name);
            Assert.Equal("ByBlock", style.LeaderLineLinetype.Name);

            DxfDatabase other = Drawing();
            var foreign = new DxfMLeaderStyle { TextStyle = db.TextStyleTable["Standard"] };
            other.MLeaderStyleDictionary.SetAt("GH_NOTES", foreign);
            Assert.Null(foreign.TextStyle);
        }

        [Fact]
        public void AValueOutOfAutoCadsRange_IsRefused()
        {
            var style = new DxfMLeaderStyle();

            Assert.Throws<ArgumentOutOfRangeException>(() => style.TextHeight = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => style.ArrowSize = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => style.MaxLeaderSegmentsPoints = 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => style.FirstSegmentAngleConstraint = (DxfAngleConstraint)5);
            Assert.Throws<ArgumentOutOfRangeException>(() => style.ContentType = DxfContentType.ToleranceContent);
            Assert.Throws<ArgumentOutOfRangeException>(() => style.SetTextAttachmentType((DxfTextAttachmentType)11, DxfLeaderDirectionType.LeftLeader));
        }

        [Fact]
        public void TheExtensionOfTheLeaders_HasNoPlaceInAFileOlderThanR2013()
        {
            DxfDatabase db = Drawing("AC1024");
            var style = new DxfMLeaderStyle();
            db.MLeaderStyleDictionary.SetAt("Standard", style);

            Assert.DoesNotContain("\n298\n", Lf(db.ToText()), StringComparison.Ordinal);
            Assert.Throws<InvalidOperationException>(() => style.ExtendLeaderToText = true);
        }

        [Fact]
        public void AMultileaderAdded_TakesAStyleMadeInCode_AndItsValuesFromIt()
        {
            // CMLEADERSTYLE is not set in the drawing: the multileader takes Standard.
            DxfDatabase db = Drawing();
            db.MLeaderStyleDictionary.SetAt("Standard", new DxfMLeaderStyle { TextHeight = 2.5, ArrowSize = 2.5, LandingGap = 2, DoglegLength = 8 });
            db.MLeaderStyleDictionary.SetAt("GH_NOTES", new DxfMLeaderStyle { TextHeight = 3.5, ArrowSize = 3 });

            var mleader = new DxfMLeader { MText = new DxfMText { Contents = "ABC", Location = new GeoPoint3(100, 50, 0), TextHeight = 2.5 } };
            mleader.AddLeaderLine(new GeoPoint3(80, 30, 0));
            db.ModelSpace.AppendEntity(mleader);

            Assert.Equal("Standard", mleader.MLeaderStyle);
            Assert.True(mleader.SetFromStyle());
            Assert.Equal(2.5, mleader.TextHeight);
            Assert.Equal(2.5, mleader.ArrowSize);

            mleader.MLeaderStyle = "GH_NOTES";
            Assert.True(mleader.SetFromStyle());
            Assert.Equal(3.5, mleader.TextHeight);
            Assert.Equal(3.0, mleader.ArrowSize);

            DxfMLeader read = DxfDatabase.Parse(db.ToText()).ModelSpace.OfType<DxfMLeader>().Single();
            Assert.Equal("GH_NOTES", read.MLeaderStyle);
            Assert.Equal(3.0, read.ArrowSize);
        }

        [Fact]
        public void AStyleSetInADrawing_ChangesNothingButItsOwnRecord()
        {
            DxfDatabase db = Drawing();
            db.MLeaderStyleDictionary.SetAt("Standard", new DxfMLeaderStyle());
            var style = (DxfMLeaderStyle)db.MLeaderStyleDictionary["Standard"];
            string before = Lf(db.ToText());

            style.ArrowSize = style.ArrowSize;
            Assert.Equal(before, Lf(db.ToText()));

            style.ArrowSize = 3;
            string after = Lf(db.ToText());
            Assert.Equal(before.Replace("\n 44\n4.0\n", "\n 44\n3.0\n"), after);
        }
    }
}
