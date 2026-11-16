using System;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The variables of wipeouts as AutoCAD 2021 keeps them (probe draw1): the entry ACAD_WIPEOUT_VARS is made when WIPEOUT
    /// Frames is first used, frames shown (70 of 1); Off writes 70 of 0, and "Display but not plot" writes 70 of 1.
    /// </summary>
    public class DxfWipeoutVariablesTests
    {
        [Fact]
        public void TheVariablesOfWipeouts_AreMadeOnce_FramesShown_WithTheirClassListed()
        {
            var database = new DxfDatabase("AC1032");
            Assert.False(database.NamedObjectsDictionary.Contains("ACAD_WIPEOUT_VARS"));

            DxfWipeoutVariables variables = DxfWipeoutVariables.GetWipeoutVariables(database);

            Assert.True(variables.ShowFrames);
            string saved = database.ToText();
            Assert.Contains("  0\nWIPEOUTVARIABLES\n", saved, StringComparison.Ordinal);
            Assert.Contains("100\nAcDbWipeoutVariables\n 70\n1\n", saved, StringComparison.Ordinal);
            Assert.Contains("  1\nWIPEOUTVARIABLES\n  2\nAcDbWipeoutVariables\n", saved, StringComparison.Ordinal);
            Assert.Same(variables, DxfWipeoutVariables.GetWipeoutVariables(database));
            Assert.Equal(saved, database.ToText());
        }

        [Fact]
        public void FramesHidden_AreWrittenAsNought_AndReadBack()
        {
            var database = new DxfDatabase("AC1032");
            DxfWipeoutVariables.GetWipeoutVariables(database).ShowFrames = false;

            string saved = database.ToText();
            Assert.Contains("100\nAcDbWipeoutVariables\n 70\n0\n", saved, StringComparison.Ordinal);

            DxfWipeoutVariables read = DxfWipeoutVariables.GetWipeoutVariables(DxfDatabase.Parse(saved));
            Assert.False(read.ShowFrames);

            read.ShowFrames = true;
            Assert.True(read.ShowFrames);
        }

        [Fact]
        public void AWipeoutAppended_MakesTheVariables_AsFramesShown()
        {
            var database = new DxfDatabase("AC1032");
            var wipeout = new DxfWipeout();
            wipeout.SetFrom(
                new[]
                {
                    new GeometryHelper.Geometry.GeoPoint2(0, 0),
                    new GeometryHelper.Geometry.GeoPoint2(10, 0),
                    new GeometryHelper.Geometry.GeoPoint2(10, 10),
                    new GeometryHelper.Geometry.GeoPoint2(0, 0),
                },
                new GeometryHelper.Geometry.GeoVector3(0, 0, 1)
            );
            database.ModelSpace.AppendEntity(wipeout);

            Assert.True(database.NamedObjectsDictionary.Contains("ACAD_WIPEOUT_VARS"));
            Assert.True(DxfWipeoutVariables.GetWipeoutVariables(database).ShowFrames);
        }

        [Fact]
        public void ADrawingWithNoObjects_RefusesTheVariables_AndNullIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => DxfWipeoutVariables.GetWipeoutVariables(null));
            Assert.Throws<InvalidOperationException>(() =>
                DxfWipeoutVariables.GetWipeoutVariables(DxfDatabase.Parse(DxfFileEzdxfDrawings.R12))
            );
        }
    }
}
