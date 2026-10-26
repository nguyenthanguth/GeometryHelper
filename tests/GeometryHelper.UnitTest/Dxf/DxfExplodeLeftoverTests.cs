using System;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The leftovers of exploding after A6, checked against ezdxf 1.4.5: a splined leader walked along the spline it explodes
    /// into, its box that spline's; a leader exploded in the look its dimension style draws it in; a MESH not smoothed broken into
    /// a 3DFACE for each face, and the MESHes it cannot break refused; a multileader copied into no drawing exploded as its
    /// original, or refused, naming the block it cannot find.
    /// </summary>
    public class DxfExplodeLeftoverTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        #region The splined leader

        [Fact]
        public void ASplinedLeader_IsWalkedAlongTheSplineItExplodesInto_ItsBoxBowingPastItsVertices()
        {
            // 89 splined: (0, 0, 0), (10, 10, 0), (20, 10, 0); the spline ezdxf fits rises to y = 10.433915879627 between the
            // second vertex and the last (its curve sampled at 200 001 points), its knots from 0 to 1.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfLeader leader = ByHandle<DxfLeader>(db, "89");
            leader.IsSplined = true;
            var drawn = (DxfSpline)leader.Explode()[0];

            GeoAabb3 box = leader.GeometricExtents;

            Assert.True(box.Min.IsEqualTo(new GeoPoint3(0, 0, 0), Tol), box.Min.ToString());
            Assert.Equal(20.0, box.Max.X, 9);
            Assert.Equal(10.433915879627, box.Max.Y, 6);
            Assert.Equal(0.0, box.Max.Z, 12);
            Assert.Equal(Bits(drawn.GeometricExtents.Max), Bits(box.Max));

            DxfSpline spline = leader.Spline;
            Assert.Equal(3, spline.NurbsData.Degree);
            Assert.Equal(drawn.NurbsData.GetKnots(), spline.NurbsData.GetKnots());
            Assert.Equal(drawn.NurbsData.GetControlPoints().Select(Bits), spline.NurbsData.GetControlPoints().Select(Bits));
            Assert.Equal(1.0, leader.EndParam);
            Assert.Equal(Bits(drawn.GetPointAtParameter(0.5)), Bits(leader.GetPointAtParameter(0.5)));
            Assert.True(leader.GetPointAtParameter(1.0).IsEqualTo(new GeoPoint3(20, 10, 0), Tol));

            double length = leader.GetDistanceAtParameter(1.0);
            Assert.Equal(drawn.GetDistanceAtParameter(1.0), length);
            GeoPoint3 middle = leader.GetPointAtDist(length / 2.0);
            Assert.Equal(length / 2.0, leader.GetDistAtPoint(middle), 6);
            Assert.Equal(leader.GetParameterAtDistance(length / 2.0), leader.GetParameterAtPoint(middle), 6);
            Assert.True(leader.GetClosestPointTo(new GeoPoint3(middle.X, middle.Y + 1E-4, 0), false).IsEqualTo(middle, Tol));
            GeoVector3 leaving = leader.GetFirstDerivative(0.0).Normalize();
            Assert.Equal(Math.Sqrt(0.5), leaving.X, 9);
            Assert.Equal(Math.Sqrt(0.5), leaving.Y, 9);
            Assert.Equal(Bits(drawn.GetSecondDerivative(0.25)), Bits(leader.GetSecondDerivative(0.25)));
            Assert.Throws<ArgumentOutOfRangeException>(() => leader.GetPointAtParameter(1.5));

            // Straight again, it is walked by its vertices as before.
            leader.IsSplined = false;
            Assert.Equal(2.0, leader.EndParam);
            Assert.Equal(Bits(new GeoPoint3(20, 10, 0)), Bits(leader.GeometricExtents.Max));
        }

        [Fact]
        public void ASplinedLeaderOfVerticesAllOnePoint_IsNoCurve_AndHasNoBox()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var leader = new DxfLeader();
            leader.AppendVertex(new GeoPoint3(5, 5, 0));
            leader.AppendVertex(new GeoPoint3(5, 5, 0));
            leader.IsSplined = true;
            db.ModelSpace.AppendEntity(leader);

            Assert.Throws<InvalidOperationException>(() => leader.GeometricExtents);
            Assert.Throws<InvalidOperationException>(() => leader.EndParam);
            Assert.Throws<InvalidOperationException>(() => leader.Spline);
            Assert.Throws<InvalidOperationException>(() => leader.GetPointAtParameter(0.0));
            Assert.Empty(leader.Explode());
        }

        #endregion

        #region The look of a leader exploded

        [Fact]
        public void ALeaderExploded_IsDrawnInTheColourLinetypeAndLineweightOfItsDimensionStyle_TheOverrideOverTheStyle()
        {
            // As ezdxf's virtual_entities draws it: DIMCLRD, DIMLTYPE and DIMLWD on the lines and the arrow, the override over
            // the style; the colour's true colour taken off, which ezdxf leaves on.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfLeader leader = ByHandle<DxfLeader>(db, "89");
            leader.ColorIndex = 3;
            leader.TrueColor = 0x112233;
            var dashed = new DxfLinetypeTableRecord { Name = "DASHED" };
            db.LinetypeTable.Add(dashed);
            DxfDimStyleTableRecord standard = db.DimStyleTable["Standard"];
            standard.Dimclrd = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 4);
            standard.Dimltype = dashed;

            DxfEntity[] styled = leader.Explode().ToArray();

            Assert.Equal(new[] { "DxfLine", "DxfLine", "DxfSolid" }, styled.Select(p => p.GetType().Name));
            Assert.All(styled, p => Assert.Equal(4, p.ColorIndex));
            Assert.All(styled, p => Assert.Null(p.TrueColor));
            Assert.All(styled, p => Assert.Equal("DASHED", p.Linetype));
            Assert.All(styled, p => Assert.Equal(leader.LineWeight, p.LineWeight));

            leader.Dimclrd = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);
            leader.Dimlwd = DxfLineWeight.LineWeight035;
            DxfEntity[] overridden = leader.Explode().ToArray();

            Assert.All(overridden, p => Assert.Equal(1, p.ColorIndex));
            Assert.All(overridden, p => Assert.Equal(DxfLineWeight.LineWeight035, p.LineWeight));
            Assert.All(overridden, p => Assert.Equal("DASHED", p.Linetype));

            // A splined one: its SPLINE so too, which ezdxf gives no look.
            leader.IsSplined = true;
            Assert.All(leader.Explode(), p => Assert.Equal(1, p.ColorIndex));
        }

        [Fact]
        public void ALeaderWhoseStyleDrawsItByBlock_KeepsItsOwnLook_OrTheColourItWritesForByBlock()
        {
            // B3 of DIMCLRD and DIMLWD BYBLOCK, as the style Standard has them: the leader's own look, as before; with 77 = 5, the
            // colour AutoCAD's reference gives the group, "the colour to use if DIMCLRD is BYBLOCK", as ezdxf reads it.
            const string Head = "LEADER\n  5\nB3\n330\n17\n100\nAcDbEntity\n  8\n0\n100\nAcDbLeader\n  3\nStandard\n";
            string text = DxfHatchLeaderDrawings.R2018.Replace("\r\n", "\n");
            Assert.Contains(Head, text, StringComparison.Ordinal);
            DxfLeader plain = ByHandle<DxfLeader>(DxfDatabase.Parse(text), "B3");
            DxfLeader withColour = ByHandle<DxfLeader>(DxfDatabase.Parse(text.Replace(Head, Head + " 77\n5\n")), "B3");

            Assert.True(plain.Dimclrd.IsByBlock);
            Assert.Equal(DxfLineWeight.ByBlock, plain.Dimlwd);
            Assert.All(plain.Explode(), p => Assert.Equal(plain.ColorIndex, p.ColorIndex));
            Assert.All(plain.Explode(), p => Assert.Equal(plain.LineWeight, p.LineWeight));
            Assert.All(plain.Explode(), p => Assert.Equal(plain.Linetype, p.Linetype));
            Assert.Equal(2, withColour.Explode().Count);
            Assert.All(withColour.Explode(), p => Assert.Equal(5, p.ColorIndex));
        }

        #endregion

        #region The MESH

        [Fact]
        public void TheMeshOfEzdxf_ExplodesIntoA3DFaceForEachOfItsFaces_EachTriangleEndingOnItsThird()
        {
            // 93: the tetrahedron of the corners (0, 0, 0), (1, 0, 0), (0, 1, 0) and (0, 0, 1), its faces (0, 1, 2), (0, 1, 3),
            // (1, 2, 3) and (0, 2, 3), as ezdxf's MeshBuilder renders them as 3DFACEs.
            foreach (string text in new[] { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfUnknownEntity mesh = ByHandle<DxfUnknownEntity>(db, "93");

                DxfFace[] faces = mesh.Explode().Cast<DxfFace>().ToArray();

                Assert.Equal(4, faces.Length);
                GeoPoint3[] corners = { new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0), new GeoPoint3(0, 1, 0), new GeoPoint3(0, 0, 1) };
                int[][] named = { new[] { 0, 1, 2, 2 }, new[] { 0, 1, 3, 3 }, new[] { 1, 2, 3, 3 }, new[] { 0, 2, 3, 3 } };

                for (int f = 0; f < 4; f++)
                {
                    Assert.Equal(named[f].Select(i => Bits(corners[i])), Enumerable.Range(0, 4).Select(i => Bits(faces[f].GetVertexAt((short)i))));
                    Assert.All(Enumerable.Range(0, 4), i => Assert.True(faces[f].IsEdgeVisibleAt((short)i)));
                    Assert.Equal(mesh.Layer, faces[f].Layer);
                    Assert.Equal(mesh.ColorIndex, faces[f].ColorIndex);
                }

                foreach (DxfFace face in faces)
                {
                    db.ModelSpace.AppendEntity(face);
                }

                Assert.Equal(4, DxfDatabase.Parse(db.ToText()).ModelSpace.Count(e => faces.Any(p => p.Handle == e.Handle)));
            }
        }

        [Fact]
        public void AMeshOfQuads_GivesA3DFaceOfEach_InTheOrderOfItsFaces_WithTheLookOfTheMesh()
        {
            // A cube 10 by 10 by 10 of eight vertices and six faces, on layer 0 in colour 5 with a true colour.
            DxfDatabase db = WithMeshes(Mesh("C01", 0, Cube(), "0 3 2 1|4 5 6 7|0 1 5 4|1 2 6 5|2 3 7 6|3 0 4 7", 0));
            DxfUnknownEntity mesh = ByHandle<DxfUnknownEntity>(db, "C01");

            DxfFace[] faces = mesh.Explode().Cast<DxfFace>().ToArray();

            Assert.Equal(6, faces.Length);
            Assert.Equal(new[] { "0,0,0", "0,10,0", "10,10,0", "10,0,0" }, Corners(faces[0]));
            Assert.Equal(new[] { "0,10,0", "0,0,0", "0,0,10", "0,10,10" }, Corners(faces[5]));
            Assert.All(faces, f => Assert.Equal(5, f.ColorIndex));
            Assert.All(faces, f => Assert.Equal(0x0AC81E, f.TrueColor));
            Assert.All(faces, f => Assert.Equal(DxfLineWeight.LineWeight035, f.LineWeight));
        }

        [Fact]
        public void AMeshSmoothed_OrOfAFaceOfMoreThanFourCorners_OrWithOverrides_IsRefusedSayingWhy()
        {
            DxfDatabase db = WithMeshes(
                Mesh("C01", 1, Cube(), "0 3 2 1|4 5 6 7|0 1 5 4|1 2 6 5|2 3 7 6|3 0 4 7", 0)
                + Mesh("C02", 0, new[] { "0 0 0", "10 0 0", "13 8 0", "5 13 0", "-3 8 0" }, "0 1 2 3 4", 0)
                + Mesh("C03", 0, Cube(), "0 3 2 1|4 5 6 7|0 1 5 4|1 2 6 5|2 3 7 6|3 0 4 7", 1)
                + Mesh("C04", 0, Cube(), "0 3 2 1|4 5 6 8", 0)
                + Mesh("C05", 0, Cube(), "0 3", 0)
                + Mesh("C06", 0, Cube(), "0 3 2 1", 0).Replace(" 93\n5\n", " 93\n7\n").Replace(" 94\n0\n", " 90\n4\n 90\n0\n 94\n0\n"));

            Assert.Contains("MESH C01 is smoothed to level 1", Assert.Throws<NotSupportedException>(() => ByHandle<DxfUnknownEntity>(db, "C01").Explode()).Message, StringComparison.Ordinal);
            Assert.Contains("5 corners", Assert.Throws<NotSupportedException>(() => ByHandle<DxfUnknownEntity>(db, "C02").Explode()).Message, StringComparison.Ordinal);
            Assert.Contains("MESH C03 gives 1 of", Assert.Throws<NotSupportedException>(() => ByHandle<DxfUnknownEntity>(db, "C03").Explode()).Message, StringComparison.Ordinal);
            Assert.Contains("the vertex 8", Assert.Throws<InvalidOperationException>(() => ByHandle<DxfUnknownEntity>(db, "C04").Explode()).Message, StringComparison.Ordinal);
            Assert.Contains("three at least", Assert.Throws<InvalidOperationException>(() => ByHandle<DxfUnknownEntity>(db, "C05").Explode()).Message, StringComparison.Ordinal);
            Assert.Contains("ends before them", Assert.Throws<InvalidOperationException>(() => ByHandle<DxfUnknownEntity>(db, "C06").Explode()).Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AMeshIsReadByItsCounts_AndOneMissingACountOrHoldingFewerThanItCounts_IsRefusedAsEzdxfRefusesIt()
        {
            // As AutoCAD writes a MESH, each list after its count: C07 has no count of its creases (95), C08 counts nine
            // vertices and holds eight, C09 has neither edges nor creases; C10 holds an edge and its crease, read past by their
            // counts to the count of its overrides, nought.
            const string Faces = "0 3 2 1|4 5 6 7|0 1 5 4|1 2 6 5|2 3 7 6|3 0 4 7";
            DxfDatabase db = WithMeshes(
                Mesh("C07", 0, Cube(), Faces, 0).Replace(" 95\n0\n", string.Empty)
                + Mesh("C08", 0, Cube(), Faces, 0).Replace(" 92\n8\n", " 92\n9\n")
                + Mesh("C09", 0, Cube(), Faces, 0).Replace(" 94\n0\n 95\n0\n", string.Empty)
                + Mesh("C10", 0, Cube(), Faces, 0).Replace(" 94\n0\n 95\n0\n", " 94\n1\n 90\n0\n 90\n1\n 95\n1\n140\n0.0\n"));

            Assert.Contains("MESH C07 holds no count of its creases (95)", Assert.Throws<InvalidOperationException>(() => ByHandle<DxfUnknownEntity>(db, "C07").Explode()).Message, StringComparison.Ordinal);
            Assert.Contains("MESH C08 holds fewer vertices", Assert.Throws<InvalidOperationException>(() => ByHandle<DxfUnknownEntity>(db, "C08").Explode()).Message, StringComparison.Ordinal);
            Assert.Contains("(94)", Assert.Throws<InvalidOperationException>(() => ByHandle<DxfUnknownEntity>(db, "C09").Explode()).Message, StringComparison.Ordinal);
            Assert.Equal(6, ByHandle<DxfUnknownEntity>(db, "C10").Explode().Count);
        }

        #endregion

        #region A multileader copied into no drawing

        [Fact]
        public void AMultileaderCopiedIntoNoDrawing_ExplodesAsItsOriginal_ItsBlockAndArrowsFoundInTheDrawingItCameFrom()
        {
            // BA places the block GKTAG with its attribute; 8A, its arrows set to GKTAG, draws them as INSERTs of it.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfMLeader block = ByHandle<DxfMLeader>(db, "BA");
            DxfMLeader words = ByHandle<DxfMLeader>(db, "8A");
            words.ArrowSymbol = db.BlockTable["GKTAG"];

            foreach (DxfMLeader original in new[] { block, words })
            {
                var copy = (DxfMLeader)original.Clone();
                DxfEntity[] expected = original.Explode().ToArray();
                DxfEntity[] parts = copy.Explode().ToArray();

                Assert.Equal(expected.Select(Described), parts.Select(Described));
            }

            Assert.Equal("GKTAG", Assert.IsType<DxfBlockReference>(block.Clone().Explode()[0]).Name);
            Assert.Equal(new[] { "NUM=42" }, Values(block.Clone()));
            Assert.Equal("GKTAG", Assert.IsType<DxfBlockReference>(words.Clone().Explode()[1]).Name);
        }

        [Fact]
        public void ACopiedMultileader_KeepsTheValuesOfItsAttributes_CopiedAgainMovedOrAppendedBack()
        {
            // BA gives NUM of GKTAG the value 42; its copy names the definition by its handle, as the original does.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfMLeader original = ByHandle<DxfMLeader>(db, "BA");
            var copy = (DxfMLeader)original.Clone();
            var back = (DxfMLeader)original.Clone();
            db.ModelSpace.AppendEntity(back);

            Assert.Equal(new[] { "NUM=42" }, Values(original));

            foreach (DxfEntity made in new DxfEntity[] { copy, copy.Clone(), original.GetTransformedCopy(GeoTransform3.Identity), back, ByHandle<DxfMLeader>(DxfDatabase.Parse(db.ToText()), back.Handle) })
            {
                Assert.Equal(new[] { "NUM=42" }, Values(made));
            }
        }

        [Fact]
        public void AMultileaderCopiedIntoAnotherDrawing_GivesItsValueToTheAttributeOfTheSameTagThere_OrLeavesItOut()
        {
            // Another drawing with a block GKTAG of its own, its attribute NUM of another handle: the value given to it. One with
            // no block GKTAG: the block and the value left out, as the block's pointer is.
            DxfMLeader original = ByHandle<DxfMLeader>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "BA");
            DxfDatabase other = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var gktag = new DxfBlockTableRecord { Name = "GKTAG" };
            other.BlockTable.Add(gktag);
            var num = new DxfAttributeDefinition(new GeoPoint3(0, 0, 0), "0", "NUM", "number", null) { Height = 2.5 };
            gktag.AppendEntity(num);
            var copy = (DxfMLeader)original.Clone();
            other.ModelSpace.AppendEntity(copy);

            Assert.Equal(new[] { "NUM=42" }, Values(copy));
            Assert.Equal(new[] { "NUM=42" }, Values(ByHandle<DxfMLeader>(DxfDatabase.Parse(other.ToText()), copy.Handle)));

            DxfDatabase bare = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var lost = (DxfMLeader)original.Clone();
            bare.ModelSpace.AppendEntity(lost);

            Assert.Null(lost.BlockContent);
            Assert.DoesNotContain("302\n42\n", bare.ToText(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMultileaderCopiedIntoNoDrawing_NamingABlockItsDrawingNeverHad_ExplodesAsItsOriginal()
        {
            // BA's block named by a handle the drawing never had (FFF): the original draws its arrow and lines alone, and so does
            // its copy, as a multileader of a drawing does; a copy is refused only for a block erased since.
            string text = DxfHatchLeaderDrawings.R2018.Replace("\r\n", "\n");
            int start = text.IndexOf("MULTILEADER\n  5\nBA\n", StringComparison.Ordinal);
            int end = text.IndexOf("\n  0\n", start, StringComparison.Ordinal);
            text = text.Substring(0, start) + text.Substring(start, end - start).Replace("\nB5\n", "\nFFF\n") + text.Substring(end);
            DxfMLeader original = ByHandle<DxfMLeader>(DxfDatabase.Parse(text), "BA");

            DxfEntity[] expected = original.Explode().ToArray();
            DxfEntity[] parts = original.Clone().Explode().ToArray();

            Assert.Equal(new[] { "DxfSolid", "DxfLine", "DxfLine" }, expected.Select(p => p.GetType().Name));
            Assert.Equal(expected.Select(Described), parts.Select(Described));
        }

        [Fact]
        public void ALeaderCopiedIntoNoDrawing_IsDrawnByTheDimensionStyleOfTheDrawingItCameFrom()
        {
            // 89 of colour 3 with a true colour, its style Standard of DIMCLRD BYLAYER, DIMASZ 2.5 and the arrow block GKTAG:
            // its copy reads them there, as the original does, and explodes as it does.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfLeader leader = ByHandle<DxfLeader>(db, "89");
            leader.ColorIndex = 3;
            leader.TrueColor = 0x112233;
            DxfDimStyleTableRecord standard = db.DimStyleTable["Standard"];
            standard.Dimclrd = DxfColor.ByLayer;
            standard.Dimldrblk = db.BlockTable["GKTAG"];
            var copy = (DxfLeader)leader.Clone();

            Assert.Equal(Bits(leader.Dimasz), Bits(copy.Dimasz));
            Assert.Equal("GKTAG", copy.Dimldrblk.Name);
            Assert.Equal(leader.Explode().Select(Described), copy.Explode().Select(Described));
            Assert.All(copy.Explode(), p => Assert.Equal(256, p.ColorIndex));
            Assert.All(copy.Explode(), p => Assert.Null(p.TrueColor));
            Assert.IsType<DxfBlockReference>(copy.Explode()[2]);
        }

        [Fact]
        public void ALeaderOfManyVertices_IsPlanedAndBoxedInTimeGrowingWithThem()
        {
            // 6 000 vertices on the floor: each read of a vertex reads them all, so that reading them once for each took seconds.
            var text = new StringBuilder("  0\nSECTION\n  2\nENTITIES\n  0\nLEADER\n  5\nC10\n100\nAcDbEntity\n  8\n0\n100\nAcDbLeader\n  3\nStandard\n 71\n1\n 72\n0\n 73\n3\n 76\n6000\n");

            for (int i = 0; i < 6000; i++)
            {
                text.Append(" 10\n" + (i * 10).ToString(CultureInfo.InvariantCulture) + "\n 20\n" + ((i % 2) * 5).ToString(CultureInfo.InvariantCulture) + "\n 30\n0.0\n");
            }

            DxfLeader leader = (DxfLeader)DxfDatabase.Parse(text.Append("  0\nENDSEC\n  0\nEOF\n").ToString()).ModelSpace.First();

            GeoPlane3 plane = leader.GetPlane();
            GeoAabb3 box = leader.GeometricExtents;

            Assert.True(plane.Normal.IsEqualTo(new GeoVector3(0, 0, 1), Tol));
            Assert.Equal(Bits(new GeoPoint3(59990, 5, 0)), Bits(box.Max));
        }

        [Fact]
        public void AMultileaderCopiedIntoNoDrawing_WhoseBlockItsDrawingNoLongerHas_IsRefusedNamingTheBlock()
        {
            // The copy of BA kept; BA and its block GKTAG (B5) erased from the drawing it came from.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfMLeader original = ByHandle<DxfMLeader>(db, "BA");
            var copy = (DxfMLeader)original.Clone();
            original.Erase();
            db.BlockTable["GKTAG"].Erase();

            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => copy.Explode());

            Assert.Contains("block of handle B5", refused.Message, StringComparison.Ordinal);
        }

        #endregion

        /// <summary>The type of a part, where it is and its look, to the bit.</summary>
        private static string Described(DxfEntity part)
        {
            string where;

            switch (part)
            {
                case DxfBlockReference insert: where = insert.Name + " " + Bits(insert.Position) + " " + Bits(insert.Rotation) + " " + string.Join(",", insert.AttributeCollection.Cast<DxfAttributeReference>().Select(a => a.Tag + "=" + a.TextString)); break;
                case DxfMText mtext: where = mtext.Contents + " " + Bits(mtext.Location) + " " + mtext.TextStyleName; break;
                case DxfLine line: where = Bits(line.StartPoint) + " " + Bits(line.EndPoint); break;
                case DxfSolid solid: where = Bits(solid.GetPointAt(0)) + " " + Bits(solid.GetPointAt(1)); break;
                default: where = string.Empty; break;
            }

            return part.GetType().Name + " " + where + " " + part.Layer + " " + part.ColorIndex + " " + part.Linetype + " " + part.LineWeight;
        }

        /// <summary>The attributes of the INSERT a multileader of a block explodes into, each its tag and value.</summary>
        private static string[] Values(DxfEntity multileader)
            => ((DxfBlockReference)multileader.Explode()[0]).AttributeCollection.Cast<DxfAttributeReference>().Select(a => a.Tag + "=" + a.TextString).ToArray();

        private static string[] Corners(DxfFace face)
            => Enumerable.Range(0, 4).Select(i => face.GetVertexAt((short)i)).Select(p => string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", p.X, p.Y, p.Z)).ToArray();

        private static string[] Cube() => new[] { "0 0 0", "10 0 0", "10 10 0", "0 10 0", "0 0 10", "10 0 10", "10 10 10", "0 10 10" };

        /// <summary>
        /// A MESH as ezdxf writes one, in colour 5 with the true colour (10, 200, 30) and the lineweight 0.35: its level of
        /// smoothing, its vertices ("x y z"), its faces (their indices, the faces apart by "|"), no edges or creases, and the
        /// count of its overrides.
        /// </summary>
        private static string Mesh(string handle, int level, string[] vertices, string faces, int overrides)
        {
            var text = new StringBuilder("  0\nMESH\n  5\n" + handle + "\n330\n17\n100\nAcDbEntity\n  8\n0\n 62\n5\n370\n35\n420\n706590\n100\nAcDbSubDMesh\n 71\n2\n 72\n0\n 91\n" + level + "\n 92\n" + vertices.Length + "\n");

            foreach (string vertex in vertices)
            {
                string[] xyz = vertex.Split(' ');
                text.Append(" 10\n" + xyz[0] + "\n 20\n" + xyz[1] + "\n 30\n" + xyz[2] + "\n");
            }

            string[][] list = faces.Split('|').Select(f => f.Split(' ')).ToArray();
            text.Append(" 93\n" + list.Sum(f => f.Length + 1) + "\n");

            foreach (string[] face in list)
            {
                text.Append(" 90\n" + face.Length + "\n");

                foreach (string index in face)
                {
                    text.Append(" 90\n" + index + "\n");
                }
            }

            text.Append(" 94\n0\n 95\n0\n 90\n" + overrides + "\n");

            for (int i = 0; i < overrides; i++)
            {
                text.Append(" 91\n" + i + "\n 92\n1\n 90\n0\n 63\n1\n");
            }

            return text.ToString();
        }

        /// <summary>The drawing of ezdxf's R2018 with entities added at the end of its ENTITIES, its handle seed raised past them.</summary>
        private static DxfDatabase WithMeshes(string entities)
        {
            string text = DxfFileEzdxfDrawings.R2018.Replace("\r\n", "\n").Replace("$HANDSEED\n  5\nA1\n", "$HANDSEED\n  5\nC00\n");
            int end = text.IndexOf("  0\nENDSEC\n", text.IndexOf("\nENTITIES\n", StringComparison.Ordinal), StringComparison.Ordinal);
            return DxfDatabase.Parse(text.Insert(end, entities));
        }
    }
}
