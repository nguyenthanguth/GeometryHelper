using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The DIMASSOC of an associative dimension, as AutoCAD 2021 writes one: under ACAD_DIMASSOC in the dimension's extension
    /// dictionary, its references to the objects snapped to, and the reactors on the dimension and the objects; read back from
    /// the file, written anew, and taken out again.
    /// </summary>
    public class DxfDimAssocTests
    {
        private static DxfDatabase Drawing(out DxfLine line, out DxfRotatedDimension dimension)
        {
            var db = new DxfDatabase("AC1027");
            line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            db.ModelSpace.AppendEntity(line);
            dimension = new DxfRotatedDimension(0.0, line.StartPoint, line.EndPoint, new GeoPoint3(100, 20, 0), string.Empty, null);
            dimension.DimBlock = Block(db);
            db.ModelSpace.AppendEntity(dimension);
            return db;
        }

        private static DxfBlockTableRecord Block(DxfDatabase db)
        {
            var block = new DxfBlockTableRecord { Name = "*D" };
            db.BlockTable.Add(block);
            return block;
        }

        private static DxfOsnapPointRef[] BothEnds(DxfLine line) => new[]
        {
            new DxfOsnapPointRef(1, DxfOsnapType.Endpoint, line.Handle, 0, 1.0, line.EndPoint, null),
            new DxfOsnapPointRef(0, DxfOsnapType.StartPoint, line.Handle, 0, 0.0, line.StartPoint, null),
        };

        [Fact]
        public void ADimensionTiedToBothEndsOfALine_IsWrittenAsAutoCadWritesIt_AndReadBack()
        {
            DxfDatabase db = Drawing(out DxfLine line, out DxfRotatedDimension dimension);
            Assert.Null(dimension.DimAssoc);

            DxfDimAssoc assoc = dimension.SetDimAssoc(BothEnds(line));

            Assert.Equal(dimension.Handle, assoc.DimensionHandle);
            Assert.Equal(3, assoc.AssociativityFlags);
            Assert.False(assoc.IsTransSpace);
            Assert.Equal(new[] { assoc.Handle }, line.GetPersistentReactorIds());
            Assert.Equal(new[] { assoc.Handle }, dimension.GetPersistentReactorIds());
            Assert.Equal(dimension.ExtensionDictionary.Handle, assoc.OwnerHandle);

            string text = db.ToText().Replace("\r\n", "\n");
            Assert.Contains("ACAD_DIMASSOC", text);
            Assert.Contains("  1\nDIMASSOC\n  2\nAcDbDimAssoc\n", text);
            Assert.Contains("100\nAcDbDimAssoc\n330\n" + dimension.Handle + "\n 90\n3\n 70\n0\n 71\n0\n  1\nAcDbOsnapPointRef\n 72\n13\n331\n" + line.Handle + "\n", text);

            DxfDatabase again = DxfDatabase.Parse(db.ToText());
            var read = (DxfRotatedDimension)again.GetObject(dimension.Handle);
            DxfOsnapPointRef[] references = read.DimAssoc.PointReferences.ToArray();
            Assert.Equal(2, references.Length);
            Assert.Equal(0, references[0].PointIndex);
            Assert.Equal(DxfOsnapType.StartPoint, references[0].OsnapType);
            Assert.Equal(1, references[1].PointIndex);
            Assert.Equal(DxfOsnapType.Endpoint, references[1].OsnapType);
            Assert.Equal(1.0, references[1].NearParameter);
            Assert.Equal(new GeoPoint3(100, 0, 0), references[1].Point);
            Assert.Equal(line.Handle, references[1].ObjectHandle);
            Assert.Equal(2, references[1].SubentType);
        }

        [Fact]
        public void ARadiusTiedToTheCenterOfACircle_HasTheSecondPointTied_AsAutoCadNumbersIt()
        {
            var db = new DxfDatabase("AC1027");
            var circle = new DxfCircle(new GeoPoint3(200, 0, 0), new GeoVector3(0, 0, 1), 25.0);
            db.ModelSpace.AppendEntity(circle);
            var radius = new DxfRadialDimension(circle.Center, new GeoPoint3(225, 0, 0), 10.0, string.Empty, null);
            radius.DimBlock = Block(db);
            db.ModelSpace.AppendEntity(radius);

            DxfDimAssoc assoc = radius.SetDimAssoc(new[] { new DxfOsnapPointRef(1, DxfOsnapType.Center, circle.Handle, 0, 0.0, circle.Center, null) });

            Assert.Equal(2, assoc.AssociativityFlags);
            Assert.Equal(DxfOsnapType.Center, Assert.Single(assoc.PointReferences).OsnapType);
            Assert.Equal(1, assoc.PointReferences[0].PointIndex);
        }

        [Fact]
        public void ADimensionTiedAnew_KeepsItsDimAssoc_AndTheObjectNoLongerNamedLosesTheReactor()
        {
            DxfDatabase db = Drawing(out DxfLine line, out DxfRotatedDimension dimension);
            var other = new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0));
            db.ModelSpace.AppendEntity(other);
            string first = dimension.SetDimAssoc(BothEnds(line)).Handle;

            DxfDimAssoc assoc = dimension.SetDimAssoc(new[] { new DxfOsnapPointRef(0, DxfOsnapType.StartPoint, other.Handle, 0, 0.0, other.StartPoint, null) });

            Assert.Equal(first, assoc.Handle);
            Assert.Equal(1, assoc.AssociativityFlags);
            Assert.Empty(line.GetPersistentReactorIds());
            Assert.Equal(new[] { first }, other.GetPersistentReactorIds());
            Assert.Equal(other.Handle, Assert.Single(DxfDatabase.Parse(db.ToText()).ModelSpace.OfType<DxfRotatedDimension>()).DimAssoc.PointReferences[0].ObjectHandle);
        }

        [Fact]
        public void ADimensionReleased_HasNoExtensionDictionaryLeft_AndTheLineNoReactor()
        {
            DxfDatabase db = Drawing(out DxfLine line, out DxfRotatedDimension dimension);
            DxfDimAssoc assoc = dimension.SetDimAssoc(BothEnds(line));

            dimension.ReleaseDimAssoc();

            Assert.True(assoc.IsErased);
            Assert.Null(dimension.DimAssoc);
            Assert.Null(dimension.ExtensionDictionary);
            Assert.Empty(line.GetPersistentReactorIds());
            Assert.Empty(dimension.GetPersistentReactorIds());
            Assert.DoesNotContain("DIMASSOC\n  5", db.ToText().Replace("\r\n", "\n"));
            Assert.Equal(new GeoPoint3(100, 0, 0), dimension.XLine2Point);
        }

        [Fact]
        public void ReferencesThatTieOnePointTwice_OrNameNoObject_AreRefused()
        {
            DxfDatabase db = Drawing(out DxfLine line, out DxfRotatedDimension dimension);

            Assert.Throws<ArgumentException>(() => dimension.SetDimAssoc(new[]
            {
                new DxfOsnapPointRef(0, DxfOsnapType.StartPoint, line.Handle, 0, 0.0, line.StartPoint, null),
                new DxfOsnapPointRef(0, DxfOsnapType.Endpoint, line.Handle, 0, 1.0, line.EndPoint, null),
            }));
            Assert.Throws<ArgumentException>(() => dimension.SetDimAssoc(new[] { new DxfOsnapPointRef(0, DxfOsnapType.StartPoint, "FFFF", 0, 0.0, line.StartPoint, null) }));
            Assert.Throws<ArgumentException>(() => dimension.SetDimAssoc(Array.Empty<DxfOsnapPointRef>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxfOsnapPointRef(4, DxfOsnapType.Endpoint, line.Handle, 0, 1.0, line.EndPoint, null));
            Assert.Null(dimension.DimAssoc);
        }
    }
}
