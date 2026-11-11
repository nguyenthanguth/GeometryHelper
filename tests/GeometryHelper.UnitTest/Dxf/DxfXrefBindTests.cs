using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// External references bound, as AutoCAD 2021's -XREF Bind binds them (probe xref-bind): Bind renames the records of the
    /// drawing referenced to <c>x$0$WALL</c>, the number raised past a name taken, the records the reference brought renamed
    /// in their places with what the drawing set on them; Insert merges them by name, the drawing's own kept; the block made
    /// an ordinary one about the base point of its drawing, its path kept.
    /// </summary>
    public class DxfXrefBindTests
    {
        /// <summary>
        /// The drawing x: its base point (100, 50, 0), a line on WALL (green, DASHED) from (0, 0) to (1 000, 0), a line on layer 0,
        /// the text "n" in the style NOTE on the layer NOTE, and an INSERT of the block BLK, a circle of radius 10 on WALL.
        /// </summary>
        private static DxfDatabase Referenced()
        {
            var x = new DxfDatabase("AC1032");
            x.Insbase = new GeoPoint3(100, 50, 0);
            var dashed = new DxfLinetypeTableRecord
            {
                Name = "DASHED",
                AsciiDescription = "Dashed __ __ __",
                NumDashes = 2,
            };
            dashed.SetDashLengthAt(0, 12.7);
            dashed.SetDashLengthAt(1, -6.35);
            x.LinetypeTable.Add(dashed);
            x.LayerTable.Add(
                new DxfLayerTableRecord
                {
                    Name = "WALL",
                    ColorIndex = 3,
                    LinetypeName = "DASHED",
                }
            );
            x.LayerTable.Add(new DxfLayerTableRecord { Name = "NOTE", ColorIndex = 1 });
            x.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "NOTE", FileName = "romans.shx" });
            var blk = new DxfBlockTableRecord { Name = "BLK" };
            x.BlockTable.Add(blk);
            blk.AppendEntity(new DxfCircle(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 10) { Layer = "WALL" });
            x.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)) { Layer = "WALL" }
            );
            x.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 200, 0)));
            x.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    Position = new GeoPoint3(0, 100, 0),
                    TextString = "n",
                    Height = 10,
                    TextStyleName = "NOTE",
                    Layer = "NOTE",
                }
            );
            x.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(500, 500, 0), blk));
            return x;
        }

        /// <summary>
        /// A drawing that attaches x and places it at (1 000, 0), with the records AutoCAD 2021 writes for it: x|DASHED and
        /// x|WALL flagged 48, x|WALL made red here (its override data 1071 of 1); and the layers given.
        /// </summary>
        private static DxfDatabase Host(out DxfBlockTableRecord x, params string[] layers)
        {
            var db = new DxfDatabase("AC1032");
            x = db.AttachXref("x.dxf", "x");
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(1000, 0, 0), x));
            var dashed = new DxfLinetypeTableRecord
            {
                Name = "x|DASHED",
                AsciiDescription = "Dashed __ __ __",
                NumDashes = 2,
            };
            dashed.SetDashLengthAt(0, 12.7);
            dashed.SetDashLengthAt(1, -6.35);
            dashed.SetDependent(true, true);
            db.LinetypeTable.Add(dashed);
            var wall = new DxfLayerTableRecord
            {
                Name = "x|WALL",
                ColorIndex = 1,
                LinetypeName = "x|DASHED",
            };
            wall.SetDependent(true, true);
            db.LayerTable.Add(wall);
            wall.XData = new[] { new DxfTypedValue(1001, "AcXRefLayerPropertyOverride"), new DxfTypedValue(1071, "1") };

            foreach (string layer in layers)
            {
                db.LayerTable.Add(new DxfLayerTableRecord { Name = layer, ColorIndex = 6 });
            }

            return db;
        }

        [Fact]
        public void ABoundReference_RenamesItsRecordsAfterIt_KeepsWhatTheDrawingSetOnThem_AndBecomesAnOrdinaryBlock()
        {
            DxfDatabase referenced = Referenced();
            string before = referenced.ToText();
            DxfDatabase db = Host(out DxfBlockTableRecord x, "x$0$NOTE");

            db.BindXref(x, referenced, false);

            Assert.False(x.IsFromExternalReference);
            Assert.Equal(new GeoPoint3(100, 50, 0), x.Origin);
            Assert.Equal("x.dxf", x.PathName);
            Assert.Equal(4, x.Count);
            DxfEntity[] inside = x.Cast<DxfEntity>().ToArray();
            Assert.Equal("x$0$WALL", inside[0].Layer);
            Assert.Equal("0", inside[1].Layer);
            Assert.Equal("x$1$NOTE", inside[2].Layer);
            Assert.Equal("x$0$NOTE", ((DxfDBText)inside[2]).TextStyleName);
            Assert.Equal("x$0$BLK", ((DxfBlockReference)inside[3]).Name);
            Assert.Equal("x$0$WALL", db.BlockTable["x$0$BLK"].Cast<DxfEntity>().Single().Layer);

            // x|WALL renamed in its place, red as the drawing made it, on the linetype renamed with it, of no reference now.
            DxfLayerTableRecord wall = db.LayerTable["x$0$WALL"];
            Assert.Equal(1, wall.ColorIndex);
            Assert.Equal("x$0$DASHED", wall.LinetypeName);
            Assert.False(wall.IsDependent);
            Assert.False(wall.IsResolved);
            Assert.Null(wall.GetXDataForApplication("AcXRefLayerPropertyOverride"));
            Assert.Equal(6, db.LayerTable["x$0$NOTE"].ColorIndex);
            Assert.Equal(1, db.LayerTable["x$1$NOTE"].ColorIndex);
            Assert.False(db.LinetypeTable["x$0$DASHED"].IsDependent);
            Assert.True(db.TextStyleTable.Has("x$0$Standard"));
            Assert.True(db.DimStyleTable.Has("x$0$Standard"));
            Assert.False(db.LayerTable.Has("x$0$0"));
            Assert.False(db.LinetypeTable.Has("x$0$Continuous"));

            string saved = db.ToText();
            Assert.DoesNotContain("x|", saved, StringComparison.Ordinal);
            DxfDatabase read = DxfDatabase.Parse(saved);
            Assert.False(read.BlockTable["x"].IsFromExternalReference);
            Assert.Equal(4, read.BlockTable["x"].Count);
            Assert.Equal(before, referenced.ToText());
        }

        [Fact]
        public void AnInsertedReference_MergesItsRecordsByName_TheDrawingsOwnKept()
        {
            DxfDatabase db = Host(out DxfBlockTableRecord x, "NOTE");
            var blk = new DxfBlockTableRecord { Name = "BLK" };
            db.BlockTable.Add(blk);
            blk.AppendEntity(new DxfCircle(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 99));

            db.BindXref(x, Referenced(), true);

            DxfEntity[] inside = x.Cast<DxfEntity>().ToArray();
            Assert.Equal("WALL", inside[0].Layer);
            Assert.Equal("NOTE", inside[2].Layer);
            Assert.Equal("NOTE", ((DxfDBText)inside[2]).TextStyleName);
            Assert.Equal("BLK", ((DxfBlockReference)inside[3]).Name);
            Assert.Equal(99, ((DxfCircle)Assert.Single(db.BlockTable["BLK"].Cast<DxfEntity>())).Radius);
            Assert.Equal(1, db.LayerTable["WALL"].ColorIndex);
            Assert.Equal("DASHED", db.LayerTable["WALL"].LinetypeName);
            Assert.Equal(6, db.LayerTable["NOTE"].ColorIndex);
            Assert.False(db.LayerTable.Has("x|WALL"));
            Assert.False(db.LinetypeTable.Has("x|DASHED"));
            Assert.DoesNotContain("x$0$", db.ToText(), StringComparison.Ordinal);
        }

        [Fact]
        public void AnInsertedReference_WhoseLinetypeTheDrawingHas_DrawsItsLayerByTheDrawingsOwn()
        {
            DxfDatabase db = Host(out DxfBlockTableRecord x);
            var dashed = new DxfLinetypeTableRecord
            {
                Name = "DASHED",
                AsciiDescription = "Ours",
                NumDashes = 2,
            };
            dashed.SetDashLengthAt(0, 5);
            dashed.SetDashLengthAt(1, -5);
            db.LinetypeTable.Add(dashed);

            db.BindXref(x, Referenced(), true);

            Assert.Equal("DASHED", db.LayerTable["WALL"].LinetypeName);
            Assert.Equal("Ours", db.LinetypeTable["DASHED"].AsciiDescription);
            Assert.False(db.LinetypeTable.Has("x|DASHED"));
        }

        [Fact]
        public void TheReferencesOfTheDrawingBound_ComeAsReferences_ItsOverlaysNot()
        {
            DxfDatabase referenced = Referenced();
            DxfBlockTableRecord nested = referenced.AttachXref("n.dxf", "N");
            DxfBlockTableRecord over = referenced.OverlayXref("o.dxf", "O");
            referenced.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(300, 0, 0), nested));
            referenced.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(400, 0, 0), over));
            DxfDatabase db = Host(out DxfBlockTableRecord x);

            db.BindXref(x, referenced, false);

            Assert.Equal(5, x.Count);
            Assert.True(db.BlockTable["N"].IsFromExternalReference);
            Assert.Equal("n.dxf", db.BlockTable["N"].PathName);
            Assert.False(db.BlockTable.Has("O"));
        }

        [Fact]
        public void ABlockThatIsNoReference_OrTheDrawingItself_IsRefused_AndNothingIsWritten()
        {
            DxfDatabase db = Host(out DxfBlockTableRecord x);
            string before = db.ToText();

            Assert.Throws<ArgumentNullException>(() => db.BindXref(null, Referenced(), false));
            Assert.Throws<ArgumentNullException>(() => db.BindXref(x, null, false));
            Assert.Throws<ArgumentException>(() => db.BindXref(db.BlockTable["*Model_Space"], Referenced(), false));
            Assert.Throws<ArgumentException>(() => db.BindXref(x, db, false));
            DxfDatabase selfNamed = Referenced();
            selfNamed.BlockTable.Add(new DxfBlockTableRecord { Name = "x" });
            Assert.Throws<ArgumentException>(() => db.BindXref(x, selfNamed, true));
            Assert.Equal(before, db.ToText());
        }
    }
}
