using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// <see cref="DxfEntity.DxfName"/>: the type an entity is written as, the group 0 of its record. Each class made in code
    /// gives its own; each entity of a drawing gives the type its file holds, in every version the drawing is saved as, where
    /// an LWPOLYLINE of R2000 is a POLYLINE of R12.
    /// </summary>
    public class DxfEntityDxfNameTests
    {
        private static readonly string[] Versions =
        {
            "AC1009",
            "AC1015",
            "AC1018",
            "AC1021",
            "AC1024",
            "AC1027",
            "AC1032",
        };

        /// <summary>The type each public class of entity that is made in code with no argument is written as; a class that is not here fails the test that lists them.</summary>
        private static readonly SortedDictionary<string, string> MadeInCode = new SortedDictionary<string, string>(
            StringComparer.Ordinal
        )
        {
            { nameof(DxfAlignedDimension), "DIMENSION" },
            { nameof(DxfArc), "ARC" },
            { nameof(DxfAttributeDefinition), "ATTDEF" },
            { nameof(DxfAttributeReference), "ATTRIB" },
            { nameof(DxfCircle), "CIRCLE" },
            { nameof(DxfDBPoint), "POINT" },
            { nameof(DxfDBText), "TEXT" },
            { nameof(DxfDiametricDimension), "DIMENSION" },
            { nameof(DxfEllipse), "ELLIPSE" },
            { nameof(DxfFace), "3DFACE" },
            { nameof(DxfFaceRecord), "VERTEX" },
            { nameof(DxfHatch), "HATCH" },
            { nameof(DxfLeader), "LEADER" },
            { nameof(DxfLine), "LINE" },
            { nameof(DxfLineAngularDimension2), "DIMENSION" },
            { nameof(DxfMInsertBlock), "INSERT" },
            { nameof(DxfMLeader), "MULTILEADER" },
            { nameof(DxfMText), "MTEXT" },
            { nameof(DxfOrdinateDimension), "DIMENSION" },
            { nameof(DxfPoint3AngularDimension), "DIMENSION" },
            { nameof(DxfPolyFaceMesh), "POLYLINE" },
            { nameof(DxfPolyFaceMeshVertex), "VERTEX" },
            { nameof(DxfPolygonMesh), "POLYLINE" },
            { nameof(DxfPolygonMeshVertex), "VERTEX" },
            { nameof(DxfPolyline), "LWPOLYLINE" },
            { nameof(DxfPolyline2d), "POLYLINE" },
            { nameof(DxfPolyline3d), "POLYLINE" },
            { nameof(DxfPolylineVertex3d), "VERTEX" },
            { nameof(DxfRadialDimension), "DIMENSION" },
            { nameof(DxfRadialDimensionLarge), "LARGE_RADIAL_DIMENSION" },
            { nameof(DxfRay), "RAY" },
            { nameof(DxfRotatedDimension), "DIMENSION" },
            { nameof(DxfSolid), "SOLID" },
            { nameof(DxfSpline), "SPLINE" },
            { nameof(DxfTolerance), "TOLERANCE" },
            { nameof(DxfVertex2d), "VERTEX" },
            { nameof(DxfViewport), "VIEWPORT" },
            { nameof(DxfWipeout), "WIPEOUT" },
            { nameof(DxfXline), "XLINE" },
        };

        /// <summary>The public classes of entity the library has that are made in code with no argument, by name.</summary>
        private static SortedDictionary<string, ConstructorInfo> NoArgument()
        {
            var found = new SortedDictionary<string, ConstructorInfo>(StringComparer.Ordinal);

            foreach (Type type in typeof(DxfEntity).Assembly.GetExportedTypes())
            {
                ConstructorInfo constructor =
                    type.IsAbstract || !typeof(DxfEntity).IsAssignableFrom(type)
                        ? null
                        : type.GetConstructor(Type.EmptyTypes);

                if (constructor != null)
                {
                    found.Add(type.Name, constructor);
                }
            }

            return found;
        }

        /// <summary>The types of the records of the BLOCKS and ENTITIES sections of a text, in the order of the file, but for the markers of a block and the vertices and end of a polyline.</summary>
        private static List<string> TypesOf(string text)
        {
            var types = new List<string>();
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            string section = null;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                string value = lines[i + 1];

                if (lines[i].Trim() == "2" && i >= 2 && lines[i - 2].Trim() == "0" && lines[i - 1] == "SECTION")
                {
                    section = value;
                }
                else if (lines[i].Trim() == "0" && value == "ENDSEC")
                {
                    section = null;
                }
                else if (
                    lines[i].Trim() == "0"
                    && (section == "BLOCKS" || section == "ENTITIES")
                    && value != "BLOCK"
                    && value != "ENDBLK"
                    && value != "VERTEX"
                    && value != "SEQEND"
                )
                {
                    types.Add(value);
                }
            }

            types.Sort(StringComparer.Ordinal);
            return types;
        }

        private static List<string> NamesOf(DxfDatabase db)
        {
            List<string> names = Everything(db).Select(e => e.DxfName).ToList();
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        [Fact]
        public void EveryClassOfEntityMadeInCodeWithNoArgument_IsInTheTable_AndNamesItsType()
        {
            SortedDictionary<string, ConstructorInfo> classes = NoArgument();

            Assert.Equal(MadeInCode.Keys, classes.Keys);

            foreach (KeyValuePair<string, ConstructorInfo> one in classes)
            {
                var entity = (DxfEntity)one.Value.Invoke(null);
                Assert.Equal(MadeInCode[one.Key], entity.DxfName);
            }
        }

        [Fact]
        public void AnEntityMadeInCodeWithItsShape_NamesItsTypeBeforeItIsInAnyDrawing()
        {
            Assert.Equal("LINE", new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)).DxfName);
            Assert.Equal("CIRCLE", new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 5).DxfName);
        }

        [Fact]
        public void TheEntitiesTheWriterWrites_NameTheTypesTheFileHolds()
        {
            string text = DxfReaderRoundTripTests.OneOfEach().ToString();
            DxfDatabase db = DxfDatabase.Parse(text);

            Assert.Equal(TypesOf(text), NamesOf(db));
            Assert.Contains("LWPOLYLINE", NamesOf(db));
            Assert.Contains("POLYLINE", NamesOf(db));
        }

        [Fact]
        public void TheEntitiesTheWriterWrites_NameTheTypesTheFileHoldsInEveryVersionItIsSavedAs()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfReaderRoundTripTests.OneOfEach().ToString());

            foreach (string version in Versions)
            {
                using (var stream = new MemoryStream())
                {
                    db.Save(stream, version);
                    DxfDatabase saved = Opened(stream.ToArray());
                    string text = saved.ToText();

                    Assert.Equal(TypesOf(text), NamesOf(saved));
                    Assert.Equal(version == "AC1009", !NamesOf(saved).Contains("LWPOLYLINE"));
                    Assert.Contains("POLYLINE", NamesOf(saved));
                }
            }
        }

        [Fact]
        public void ATextAnMTextAndAnInsertAddedToADrawing_NameTheTypeItIsWrittenAsInEveryVersion()
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "B" };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(5, 0, 0)));

            var text = new DxfDBText
            {
                TextString = "steel",
                Position = new GeoPoint3(0, 0, 0),
                Height = 2.5,
            };
            var mtext = new DxfMText
            {
                Contents = "plate 10 by 20",
                Location = new GeoPoint3(0, 10, 0),
                TextHeight = 2.5,
            };
            var insert = new DxfBlockReference(new GeoPoint3(20, 0, 0), block);
            db.ModelSpace.AppendEntity(text);
            db.ModelSpace.AppendEntity(mtext);
            db.ModelSpace.AppendEntity(insert);

            Assert.Equal("TEXT", text.DxfName);
            Assert.Equal("MTEXT", mtext.DxfName);
            Assert.Equal("INSERT", insert.DxfName);

            foreach (string version in Versions)
            {
                using (var stream = new MemoryStream())
                {
                    db.Save(stream, version);
                    DxfDatabase saved = Opened(stream.ToArray());
                    List<string> names = NamesOf(saved);

                    Assert.Equal(TypesOf(saved.ToText()), names);
                    Assert.Contains("INSERT", names);
                    Assert.Equal(version == "AC1009", !names.Contains("MTEXT"));
                }
            }
        }
    }
}
