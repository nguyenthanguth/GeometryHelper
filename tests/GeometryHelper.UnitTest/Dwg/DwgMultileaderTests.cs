using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// A multileader made in code, written as DWG and read back. The drawing's CLASSES section lists the class of the style
    /// (the library adds it) and not that of the multileader, and the writer left the multileader out for it, so a note
    /// drawn with MLEADER vanished from the file. AutoCAD 2021 saves the multileader in a file of R2000 too, as its own
    /// drawing <c>leaders_AC1015</c> shows.
    /// </summary>
    public class DwgMultileaderTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-9, 1E-9);

        private static DxfDatabase NoteInModelSpace()
        {
            var database = new DxfDatabase("AC1032");
            database.NamedObjectsDictionary.SetAt("ACAD_MLEADERSTYLE", new DxfDBDictionary());
            database.MLeaderStyleDictionary.SetAt("Standard", new DxfMLeaderStyle());

            // As a drawing program makes one: the words 4 high at (150, 150), a leader from the arrow at (0, 0), landed 8 short.
            var note = new DxfMLeader { Scale = 2.0 };
            note.MText = new DxfMText
            {
                Contents = "SEE DETAIL 3",
                Location = new GeoPoint3(150, 150, 0),
                TextHeight = 4.0,
            };
            note.AddLeaderLine(new GeoPoint3(0, 0, 0));
            database.ModelSpace.AppendEntity(note);
            return database;
        }

        [Theory]
        [InlineData("AC1032")]
        [InlineData("AC1027")]
        [InlineData("AC1024")]
        [InlineData("AC1021")]
        [InlineData("AC1018")]
        [InlineData("AC1015")]
        public void AMultileaderWrittenAsDwg_IsNotLeftOut(string version)
        {
            DwgWriter.ToDwg(NoteInModelSpace().ToText(), version, out IDictionary<string, int> leftOut);

            Assert.Empty(leftOut);
        }

        [Theory]
        [InlineData("AC1032")]
        [InlineData("AC1027")]
        [InlineData("AC1024")]
        [InlineData("AC1021")]
        [InlineData("AC1018")]
        [InlineData("AC1015")]
        public void AMultileaderWrittenAsDwgAndReadBack_HasItsWordsItsLeaderAndItsScale(string version)
        {
            DxfDatabase before = NoteInModelSpace();
            var stream = new MemoryStream();
            DwgWriter.Write(before, stream, version);
            stream.Position = 0;
            DxfDatabase after = DwgReader.Open(stream);

            DxfMLeader was = before.ModelSpace.OfType<DxfMLeader>().Single();
            DxfMLeader now = Assert.Single(after.ModelSpace.OfType<DxfMLeader>());

            Assert.Equal(was.ContentType, now.ContentType);
            Assert.Equal("SEE DETAIL 3", now.MText.Contents);
            Assert.True(was.MText.Location.IsEqualTo(now.MText.Location, Tol));
            Assert.Equal(was.MText.TextHeight, now.MText.TextHeight, 9);
            Assert.Equal(2.0, now.Scale, 9);
            Assert.Equal(was.LeaderCount, now.LeaderCount);
            Assert.Equal(was.LeaderLineCount, now.LeaderLineCount);
            Assert.True(was.GetFirstVertex(0).IsEqualTo(now.GetFirstVertex(0), Tol));
            Assert.True(was.GetLastVertex(0).IsEqualTo(now.GetLastVertex(0), Tol));
            Assert.True(was.GetDogleg(0).IsEqualTo(now.GetDogleg(0), Tol));
            Assert.Equal(was.MLeaderStyle, now.MLeaderStyle);
            Assert.Equal(was.ArrowSize, now.ArrowSize, 9);
        }
    }
}
