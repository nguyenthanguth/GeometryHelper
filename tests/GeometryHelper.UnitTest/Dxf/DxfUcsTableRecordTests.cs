using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The UCS table: a named system added as AutoCAD writes one, read back, changed, renamed and erased with the header
    /// variables that name it following.
    /// </summary>
    public class DxfUcsTableRecordTests
    {
        private const string Head = "100\nAcDbSymbolTableRecord\n100\nAcDbUCSTableRecord\n";

        [Fact]
        public void ASystemAdded_IsWrittenAsAutoCadWritesIt_AndReadBack()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            Assert.Empty(db.UcsTable);

            // A quarter turn about Z at (100, 50, 0).
            db.UcsTable.Add(new DxfUcsTableRecord { Name = "Slab", Origin = new GeoPoint3(100.0, 50.0, 0.0), XAxis = new GeoVector3(0.0, 1.0, 0.0), YAxis = new GeoVector3(-1.0, 0.0, 0.0) });
            db.Ucsname = "Slab";
            string saved = db.ToText();

            Assert.Contains(Head + "  2\nSlab\n 70\n0\n 10\n100\n 20\n50\n 30\n0\n 11\n0\n 21\n1\n 31\n0\n 12\n-1\n 22\n0\n 32\n0\n 79\n0\n146\n0\n  0\n", saved, StringComparison.Ordinal);
            Assert.Contains("  2\nUCS\n  5\n", saved, StringComparison.Ordinal);
            Assert.Contains("AcDbSymbolTable\n 70\n1\n", saved.Substring(saved.IndexOf("  2\nUCS\n", StringComparison.Ordinal)), StringComparison.Ordinal);

            DxfDatabase again = DxfDatabase.Parse(saved);
            DxfUcsTableRecord slab = again.UcsTable["SLAB"];
            Assert.Equal("Slab", slab.Name);
            Assert.Equal(new GeoPoint3(100.0, 50.0, 0.0), slab.Origin);
            Assert.Equal(new GeoVector3(0.0, 1.0, 0.0), slab.XAxis);
            Assert.Equal(new GeoVector3(-1.0, 0.0, 0.0), slab.YAxis);
            Assert.Equal("Slab", again.Ucsname);
            Assert.Single(again.UcsTable);
        }

        [Fact]
        public void ASystemOfAFile_IsChangedRenamedAndErased_TheHeaderFollowing()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            db.UcsTable.Add(new DxfUcsTableRecord { Name = "A" });
            db.UcsTable.Add(new DxfUcsTableRecord { Name = "B" });
            db.Ucsname = "A";
            db.UcsBase = "A";
            DxfDatabase file = DxfDatabase.Parse(db.ToText());
            string before = file.ToText();

            // Setting the values a system has changes no byte.
            DxfUcsTableRecord a = file.UcsTable["A"];
            a.Origin = GeoPoint3.Origin;
            a.XAxis = GeoVector3.XAxis;
            Assert.Equal(before, file.ToText());

            // Turned 30 degrees about Z at (100, 50, 0).
            double c = Math.Cos(Math.PI / 6.0);
            double s = Math.Sin(Math.PI / 6.0);
            a.Origin = new GeoPoint3(100.0, 50.0, 0.0);
            a.XAxis = new GeoVector3(c, s, 0.0);
            a.YAxis = new GeoVector3(-s, c, 0.0);
            a.Name = "Turned";
            DxfDatabase renamed = DxfDatabase.Parse(file.ToText());
            Assert.Equal("Turned", renamed.Ucsname);
            Assert.Equal("Turned", renamed.UcsBase);
            Assert.Equal(new GeoVector3(c, s, 0.0), renamed.UcsTable["Turned"].XAxis);
            Assert.Equal(new GeoVector3(-s, c, 0.0), renamed.UcsTable["Turned"].YAxis);
            Assert.Equal(new GeoPoint3(100.0, 50.0, 0.0), renamed.UcsTable["Turned"].Origin);
            Assert.Throws<ArgumentException>(() => renamed.UcsTable["Turned"].Name = "b");

            renamed.UcsTable["Turned"].Erase();
            DxfDatabase erased = DxfDatabase.Parse(renamed.ToText());
            Assert.Equal(new[] { "B" }, erased.UcsTable.Select(r => r.Name).ToArray());
            Assert.Null(erased.Ucsname);
            Assert.Null(erased.UcsBase);
            Assert.False(erased.UcsTable.Has("Turned"));
        }

        [Fact]
        public void ASystemAdded_ToADrawingOfR12_HasNoMarkersAndNoGroupsOfR2000()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            db.UcsTable.Add(new DxfUcsTableRecord { Name = "R12", Origin = new GeoPoint3(1.0, 2.0, 3.0) });
            string saved = db.ToText();
            int at = saved.IndexOf("  2\nR12\n", StringComparison.Ordinal);

            Assert.True(at > 0);
            Assert.DoesNotContain("AcDbUCSTableRecord", saved, StringComparison.Ordinal);
            Assert.StartsWith("  2\nR12\n 70\n0\n 10\n1\n 20\n2\n 30\n3\n 11\n1\n 21\n0\n 31\n0\n 12\n0\n 22\n1\n 32\n0\n  0\n", saved.Substring(at), StringComparison.Ordinal);
            Assert.Equal(new GeoPoint3(1.0, 2.0, 3.0), DxfDatabase.Parse(saved).UcsTable["R12"].Origin);
            Assert.Throws<ArgumentException>(() => db.UcsTable.Add(new DxfUcsTableRecord { Name = "r12" }));
        }
    }
}
