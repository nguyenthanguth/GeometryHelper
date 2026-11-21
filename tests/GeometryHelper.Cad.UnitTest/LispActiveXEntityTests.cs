using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The members of the kinds of entity of the object model (part O2 of L4): a line, a circle, an arc, a light-weight
    /// polyline, a text, an MText, a block reference with its attributes, an attribute definition, the box of a text, the
    /// <c>textbox</c> function and the <c>vlax-curve-*</c> functions given objects, typed at the command line of a headless
    /// session. The rows are those the full AutoCAD 2021 gave (probes g2 and g3 of the L4 spec, section 2b, marked
    /// <c>AutoCAD 2021 (GUI g2)</c>); a row the probes do not decide is marked <c>reference</c> (the ActiveX reference says
    /// so) or <c>ours: D2nn</c>.
    /// </summary>
    public sealed class LispActiveXEntityTests : IDisposable
    {
        public void Dispose() => WaitingRoutines.EndAll();

        // The drawing of the tests, in model space in this order: LINE (0 0 0)-(30 40 0), CIRCLE about (50 0 0) of radius 5,
        // ARC about (100 0 0) of radius 10 from 0 to 90 degrees, LWPOLYLINE (0 100) (100 100) (100 150), TEXT "abc" 2.5 high at
        // (0 20 0), MTEXT "mtext {\Lbody}" 20 wide held top left at (0 60 0), INSERT of TAGGED at (100 0 0) scaled 2 along x with
        // one attribute TAG1 = v30, INSERT of PLAIN (no attribute) at (200 0 0), and a LINE on the locked layer Locked. TAGGED is
        // the attribute definitions TAG1 (prompt "Prompt", default "dflt"), CTAG (constant, value CV) and a line.
        // In LISP: eX the entity name, X the object, X in line, circle, arc, poly, text, mtext, ins, plain, locked, att.
        private static readonly string[] Names =
        {
            "line",
            "circle",
            "arc",
            "poly",
            "text",
            "mtext",
            "ins",
            "plain",
            "locked",
            "att",
        };

        private static CadCommandSession Scene(
            out CadDocument document,
            FontFolder fonts = null,
            string textWords = "abc"
        )
        {
            document = CadDocument.From(new DxfDatabase("AC1027"), "blank.dxf");
            DxfDatabase database = document.Database;
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "Locked", ColorIndex = 3 });
            database.LayerTable["Locked"].IsLocked = true;

            var tagged = new DxfBlockTableRecord { Name = "TAGGED", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(tagged);
            tagged.AppendEntity(
                new DxfAttributeDefinition(new GeoPoint3(0, 0, 0), "dflt", "TAG1", "Prompt", null) { Height = 5.0 }
            );
            tagged.AppendEntity(
                new DxfAttributeDefinition(new GeoPoint3(0, 10, 0), "CV", "CTAG", "Const", null) { Constant = true }
            );
            tagged.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            var plain = new DxfBlockTableRecord { Name = "PLAIN", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(plain);
            plain.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));

            DxfBlockTableRecord model = database.ModelSpace;
            var handles = new Dictionary<string, string>();

            void Add(string name, DxfEntity entity)
            {
                model.AppendEntity(entity);
                handles[name] = entity.Handle;
            }

            Add("line", new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(30, 40, 0)));
            Add("circle", new DxfCircle(new GeoPoint3(50, 0, 0), new GeoVector3(0, 0, 1), 5.0));
            Add("arc", new DxfArc(new GeoPoint3(100, 0, 0), 10.0, 0.0, Math.PI / 2.0));
            var polyline = new DxfPolyline(3);
            Add("poly", polyline);
            polyline.AddVertexAt(0, new GeoPoint2(0, 100), 0, 0, 0);
            polyline.AddVertexAt(1, new GeoPoint2(100, 100), 0, 0, 0);
            polyline.AddVertexAt(2, new GeoPoint2(100, 150), 0, 0, 0);
            Add(
                "text",
                new DxfDBText
                {
                    Position = new GeoPoint3(0, 20, 0),
                    Height = 2.5,
                    TextString = textWords,
                }
            );
            Add(
                "mtext",
                new DxfMText
                {
                    Location = new GeoPoint3(0, 60, 0),
                    TextHeight = 2.5,
                    Contents = "mtext {\\Lbody}",
                    Width = 20.0,
                    Attachment = DxfAttachmentPoint.TopLeft,
                }
            );
            var insert = new DxfBlockReference(new GeoPoint3(100, 0, 0), tagged);
            Add("ins", insert);
            insert.ScaleFactors = new GeoVector3(2, 1, 1);
            var attribute = new DxfAttributeReference
            {
                Tag = "TAG1",
                TextString = "v30",
                Position = new GeoPoint3(100, 0, 0),
                Height = 5.0,
            };
            insert.AttributeCollection.AppendAttribute(attribute);
            handles["att"] = attribute.Handle;
            Add("plain", new DxfBlockReference(new GeoPoint3(200, 0, 0), plain));
            Add("locked", new DxfLine(new GeoPoint3(0, -50, 0), new GeoPoint3(100, -50, 0)) { Layer = "Locked" });

            if (fonts != null)
            {
                document.Database.TextStyleTable["Standard"].FileName = "test.shx";
                document.Fonts = new CadFonts(new[] { fonts.FullPath }, null, null);
            }

            CadCommandSession session = Session(document);
            Value(session, "(vl-load-com)");
            Value(session, "(setq app (vlax-get-acad-object) doc (vla-get-ActiveDocument app))");

            foreach (string name in Names)
            {
                Value(
                    session,
                    "(setq e"
                        + name
                        + " (handent \""
                        + handles[name]
                        + "\") "
                        + name
                        + " (vlax-ename->vla-object e"
                        + name
                        + "))"
                );
            }

            return session;
        }

        private static CadCommandSession Scene() => Scene(out _);

        // What a form gives, as the lines after it, the made-up addresses of the objects written @.
        private static string Value(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form.Length > 0 && form[0] != '(' && form[0] != '!' ? "!" + form : form);
            string text = string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
            return Regex.Replace(text, @"(#<VLA-OBJECT \w+) [0-9a-f]{16}>", "$1 @>");
        }

        private static string Error(CadCommandSession session, string form)
        {
            string text = Value(session, form);
            Assert.StartsWith("; error: ", text);
            return text.Substring("; error: ".Length);
        }

        // The group of an entity as entget gives it.
        private static string Group(CadCommandSession session, string name, int code) =>
            Value(session, "(cdr (assoc " + code + " (entget " + name + ")))");

        private static string Attributes(CadCommandSession session) =>
            Value(session, "(vlax-safearray->list (vlax-variant-value (vla-GetAttributes ins)))");

        #region Line

        [Fact]
        public void ALine_ReadsEveryMemberItHas()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): StartPoint a variant 8197, Length, angle, Delta a variant, Thickness 0.0.
            Assert.Equal("#<variant 8197 ...>", Value(session, "(vla-get-StartPoint line)"));
            Assert.Equal("8197", Value(session, "(vlax-variant-type (vla-get-StartPoint line))"));
            Assert.Equal(
                "(0.0 0.0 0.0)",
                Value(session, "(vlax-safearray->list (vlax-variant-value (vla-get-StartPoint line)))")
            );
            Assert.Equal("(30.0 40.0 0.0)", Value(session, "(vlax-get line 'EndPoint)"));
            Assert.Equal("50.0", Value(session, "(vla-get-Length line)"));
            Assert.Equal("0.927295", Value(session, "(vla-get-Angle line)"));
            Assert.Equal("#<variant 8197 ...>", Value(session, "(vla-get-Delta line)"));
            Assert.Equal("(30.0 40.0 0.0)", Value(session, "(vlax-get line 'Delta)"));
            Assert.Equal("0.0", Value(session, "(vla-get-Thickness line)"));
            Assert.Equal("\"AcDbLine\"", Value(session, "(vla-get-ObjectName line)"));
        }

        [Fact]
        public void ALinesPointsAreWrittenFromAVariantOrASafearrayOfThree()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): (vla-put-StartPoint o (vlax-3d-point 1 2 0)) gives 10 (1.0 2.0 0.0).
            Value(session, "(vla-put-StartPoint line (vlax-3d-point 1 2 0))");
            Assert.Equal("(1.0 2.0 0.0)", Group(session, "eline", 10));
            Assert.Equal("(1.0 2.0 0.0)", Value(session, "(vlax-get line 'StartPoint)"));

            // a safearray of three doubles puts them (g2: the empty one puts 0,0,0)
            Value(session, "(vla-put-EndPoint line (vlax-make-safearray vlax-vbDouble '(0 . 2)))");
            Assert.Equal("(0.0 0.0 0.0)", Group(session, "eline", 11));
            Value(
                session,
                "(vla-put-EndPoint line (vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 2)) '(7 8 9)))"
            );
            Assert.Equal("(7.0 8.0 9.0)", Group(session, "eline", 11));
        }

        [Fact]
        public void APointOfTwoOrAListIsRefusedAsAutoCadRefusesIt()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): a safearray of two, a list to vla-put and to vlax-put-property, vlax-put of any point.
            Assert.Equal(
                "Automation Error. Incorrect number of elements in SafeArray",
                Error(
                    session,
                    "(vla-put-StartPoint line (vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 1)) '(3 4)))"
                )
            );
            Assert.Equal(
                "lisp value has no coercion to VARIANT with this type:  (1 2 3)",
                Error(session, "(vla-put-StartPoint line '(1 2 3))")
            );
            Assert.Equal(
                "lisp value has no coercion to VARIANT with this type:  (0 0 0)",
                Error(session, "(vlax-put-property line 'StartPoint '(0 0 0))")
            );
            Assert.Equal("Exception occurred", Error(session, "(vlax-put line 'StartPoint '(0 0 0))"));
            Assert.Equal("Exception occurred", Error(session, "(vlax-put line 'StartPoint '(0 0))"));
            Assert.Equal("Exception occurred", Error(session, "(vlax-put line 'StartPoint (vlax-3d-point 1 0 0))"));
            Assert.Equal("(0.0 0.0 0.0)", Group(session, "eline", 10));
        }

        [Fact]
        public void ALinesLength_IsReadOnly()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): vlax-put-property of a line's Length is a type mismatch; vla-put-Length is a SUBR.
            Assert.Equal(
                "ActiveX Server returned an error: Type mismatch",
                Error(session, "(vlax-put-property line 'Length 5.0)")
            );
            Assert.Equal("T", Value(session, "(vlax-property-available-p line 'Length)"));
            Assert.Equal("nil", Value(session, "(vlax-property-available-p line 'Length T)"));
            Assert.Equal("T", Value(session, "(vlax-property-available-p line 'StartPoint T)"));
            Assert.Equal("50.0", Value(session, "(vla-get-Length line)"));
        }

        [Fact]
        public void ALinesThickness_IsWritten()
        {
            CadCommandSession session = Scene();

            // reference: Thickness is read and write; ours: D224.
            Value(session, "(vla-put-Thickness line 4)");
            Assert.Equal("4.0", Group(session, "eline", 39));
            Assert.Equal("4.0", Value(session, "(vla-get-Thickness line)"));
        }

        [Fact]
        public void AMemberAnotherKindHas_IsAnUnknownNameOnALine()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): Name and EffectiveName of a line are unknown names.
            Assert.Equal(
                "ActiveX Server returned the error: unknown name: Name",
                Error(session, "(vla-get-Name line)")
            );
            Assert.Equal(
                "ActiveX Server returned the error: unknown name: EffectiveName",
                Error(session, "(vla-get-EffectiveName line)")
            );
            Assert.Equal("nil", Value(session, "(vlax-property-available-p line 'EFFECTIVENAME)"));
            Assert.Equal("nil", Value(session, "(vlax-method-applicable-p line 'GETATTRIBUTES)"));
            Assert.Equal(
                "ActiveX Server returned the error: unknown name: Radius",
                Error(session, "(vla-get-Radius line)")
            );
        }

        [Fact]
        public void ALineOnALockedLayer_IsReadButNotWritten()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): a put on a locked layer is Automation Error. On locked layer, a read works.
            Assert.Equal("100.0", Value(session, "(vla-get-Length locked)"));
            Assert.Equal(
                "Automation Error. On locked layer",
                Error(session, "(vla-put-EndPoint locked (vlax-3d-point 1 1 0))")
            );
            Assert.Equal("(100.0 -50.0 0.0)", Group(session, "elocked", 11));
        }

        [Fact]
        public void ALineErased_IsTheErasedError()
        {
            CadCommandSession session = Scene();
            Value(session, "(vla-Delete line)");

            // AutoCAD 2021 (GUI g2): a get and a put of an erased object are Automation Error. Object was erased.
            Assert.Equal("Automation Error. Object was erased", Error(session, "(vla-get-Length line)"));
            Assert.Equal(
                "Automation Error. Object was erased",
                Error(session, "(vla-put-StartPoint line (vlax-3d-point 1 1 0))")
            );
        }

        #endregion

        #region Circle and arc

        [Fact]
        public void ACircle_ReadsEveryMemberItHas()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): Center, Radius 5.0, Diameter 10.0, Circumference 31.4159, Area 78.5398.
            Assert.Equal("(50.0 0.0 0.0)", Value(session, "(vlax-get circle 'Center)"));
            Assert.Equal("5.0", Value(session, "(vla-get-Radius circle)"));
            Assert.Equal("10.0", Value(session, "(vla-get-Diameter circle)"));
            Assert.Equal("31.4159", Value(session, "(vla-get-Circumference circle)"));
            Assert.Equal("78.5398", Value(session, "(vla-get-Area circle)"));
            Assert.Equal("\"AcDbCircle\"", Value(session, "(vla-get-ObjectName circle)"));
        }

        [Fact]
        public void ACircleAreaPut_SetsTheRadiusThatHoldsIt()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): (vla-put-Area c 100.0) gives Radius 5.6419.
            Value(session, "(vla-put-Area circle 100.0)");
            Assert.Equal("5.6419", Value(session, "(vla-get-Radius circle)"));
            Assert.Equal("5.6419", Group(session, "ecircle", 40));
            Assert.Equal("Automation Error. Invalid argument", Error(session, "(vla-put-Area circle -5.0)"));
        }

        [Fact]
        public void ACircleCentreRadiusDiameterAndCircumference_AreWritten()
        {
            CadCommandSession session = Scene();

            // reference: all four are read and write; ours: D224.
            Value(session, "(vla-put-Center circle (vlax-3d-point 60 5 0))");
            Assert.Equal("(60.0 5.0 0.0)", Group(session, "ecircle", 10));
            Value(session, "(vla-put-Radius circle 7)");
            Assert.Equal("7.0", Group(session, "ecircle", 40));
            Value(session, "(vla-put-Diameter circle 20)");
            Assert.Equal("10.0", Group(session, "ecircle", 40));
            Value(session, "(vla-put-Circumference circle 62.8319)");
            Assert.Equal("10.0", Value(session, "(vla-get-Radius circle)"));
        }

        [Fact]
        public void ACirclesNegativeRadius_IsAnAutomationError()
        {
            CadCommandSession session = Scene();

            // ours: D224 (the typed model refuses it; the reason is the error).
            Assert.StartsWith("Automation Error. ", Error(session, "(vla-put-Radius circle -3)"));
            Assert.Equal("5.0", Value(session, "(vla-get-Radius circle)"));
        }

        [Fact]
        public void AnArc_ReadsEveryMemberItHas()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): StartAngle, EndAngle in radians, TotalAngle, ArcLength, Area (the segment), the end points.
            Assert.Equal("(100.0 0.0 0.0)", Value(session, "(vlax-get arc 'Center)"));
            Assert.Equal("10.0", Value(session, "(vla-get-Radius arc)"));
            Assert.Equal("0.0", Value(session, "(vla-get-StartAngle arc)"));
            Assert.Equal("1.5708", Value(session, "(vla-get-EndAngle arc)"));
            Assert.Equal("1.5708", Value(session, "(vla-get-TotalAngle arc)"));
            Assert.Equal("15.708", Value(session, "(vla-get-ArcLength arc)"));
            Assert.Equal("28.5398", Value(session, "(vla-get-Area arc)"));
            Assert.Equal("(110.0 0.0 0.0)", Value(session, "(vlax-get arc 'StartPoint)"));
            Assert.Equal("(100.0 10.0 0.0)", Value(session, "(vlax-get arc 'EndPoint)"));
            Assert.Equal("\"AcDbArc\"", Value(session, "(vla-get-ObjectName arc)"));
        }

        [Fact]
        public void AnArcsEndPoints_AreReadOnly()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): (vla-put-StartPoint a (vlax-3d-point 0 0 0)) is a type mismatch.
            Assert.Equal(
                "ActiveX Server returned an error: Type mismatch",
                Error(session, "(vla-put-StartPoint arc (vlax-3d-point 0 0 0))")
            );
            Assert.Equal(
                "ActiveX Server returned an error: Type mismatch",
                Error(session, "(vla-put-EndPoint arc (vlax-3d-point 0 0 0))")
            );
            Assert.Equal("nil", Value(session, "(vlax-property-available-p arc 'StartPoint T)"));
        }

        [Fact]
        public void AnArcsAnglesCentreAndRadius_AreWritten()
        {
            CadCommandSession session = Scene();

            // reference: Center, Radius, StartAngle and EndAngle are read and write; the angles are radians (group 50 and 51 too).
            Value(session, "(vla-put-StartAngle arc 0.5)");
            Assert.Equal("0.5", Group(session, "earc", 50));
            Value(session, "(vla-put-EndAngle arc 2.0)");
            Assert.Equal("2.0", Group(session, "earc", 51));
            Assert.Equal("1.5", Value(session, "(vla-get-TotalAngle arc)"));
            Value(session, "(vla-put-Radius arc 20)");
            Assert.Equal("30.0", Value(session, "(vla-get-ArcLength arc)"));
            Value(session, "(vla-put-Center arc (vlax-3d-point 1 2 0))");
            Assert.Equal("(1.0 2.0 0.0)", Group(session, "earc", 10));
        }

        #endregion

        #region Light-weight polyline

        [Fact]
        public void APolyline_ReadsEveryMemberItHas()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): Coordinates a variant of 2n doubles, Closed :vlax-false, Elevation 0.0, Length, Area.
            Assert.Equal("#<variant 8197 ...>", Value(session, "(vla-get-Coordinates poly)"));
            Assert.Equal("(0.0 100.0 100.0 100.0 100.0 150.0)", Value(session, "(vlax-get poly 'Coordinates)"));
            Assert.Equal(":vlax-false", Value(session, "(vla-get-Closed poly)"));
            Assert.Equal("0", Value(session, "(vlax-get poly 'Closed)"));
            Assert.Equal("0.0", Value(session, "(vla-get-Elevation poly)"));
            Assert.Equal("150.0", Value(session, "(vla-get-Length poly)"));
            Assert.Equal("2500.0", Value(session, "(vla-get-Area poly)"));
            Assert.Equal("\"AcDbPolyline\"", Value(session, "(vla-get-ObjectName poly)"));
        }

        [Fact]
        public void ClosingAPolyline_ChangesItsLength()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): (vla-put-Closed p :vlax-true) gives 70 1, and the Length grows by the closing segment.
            Value(session, "(vla-put-Closed poly :vlax-true)");
            Assert.Equal("1", Group(session, "epoly", 70));
            Assert.Equal("261.803", Value(session, "(vla-get-Length poly)"));
            Value(session, "(vla-put-Closed poly :vlax-false)");
            Assert.Equal("0", Group(session, "epoly", 70));
        }

        [Fact]
        public void CoordinatesOfFourNumbers_MakeTwoVertices()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g3): a variant of 4 doubles (0 0 5 5) sets the vertices, group 90 becomes 2.
            Value(
                session,
                "(vla-put-Coordinates poly (vlax-make-variant (vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 3)) '(0 0 5 5))))"
            );
            Assert.Equal("2", Group(session, "epoly", 90));
            Assert.Equal("(0.0 0.0 5.0 5.0)", Value(session, "(vlax-get poly 'Coordinates)"));
        }

        [Fact]
        public void CoordinatesOfMoreNumbers_AddVerticesAndKeepTheBulgesOfTheOnesThatStay()
        {
            CadCommandSession session = Scene();
            Value(
                session,
                "(vla-put-Coordinates poly (vlax-make-variant (vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 7)) '(0 0 10 0 10 10 0 10))))"
            );

            // reference: Coordinates takes any count of vertices; ours: D244 (the new ones are straight).
            Assert.Equal("4", Group(session, "epoly", 90));
            Assert.Equal("(0.0 0.0 10.0 0.0 10.0 10.0 0.0 10.0)", Value(session, "(vlax-get poly 'Coordinates)"));
            Assert.Equal("30.0", Value(session, "(vla-get-Length poly)"));
        }

        [Theory]
        [InlineData(
            "(vlax-make-variant (vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 4)) '(0 0 5 5 7)))"
        )]
        [InlineData("(vlax-make-variant (vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 1)) '(0 0)))")]
        public void CoordinatesOfAnOddCountOrBelowTwoVertices_AreAnAutomationError(string value)
        {
            CadCommandSession session = Scene();

            // ours: D244 (AutoCAD's words are not known).
            Assert.Equal(
                "Automation Error. Invalid argument",
                Error(session, "(vla-put-Coordinates poly " + value + ")")
            );
            Assert.Equal("3", Group(session, "epoly", 90));
        }

        [Fact]
        public void CoordinatesOfNoArray_AreATypeMismatch()
        {
            CadCommandSession session = Scene();

            // ours: D215 (anything else is Type mismatch).
            Assert.Equal(
                "ActiveX Server returned an error: Type mismatch",
                Error(session, "(vla-put-Coordinates poly 5)")
            );
            Assert.Equal(
                "lisp value has no coercion to VARIANT with this type:  (1 2 3 4)",
                Error(session, "(vlax-put-property poly 'Coordinates '(1 2 3 4))")
            );
        }

        [Fact]
        public void APolylinesElevation_IsWritten()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g3): (vla-put-Elevation p 2) gives group 38 2.0.
            Value(session, "(vla-put-Elevation poly 2)");
            Assert.Equal("2.0", Group(session, "epoly", 38));
            Assert.Equal("2.0", Value(session, "(vla-get-Elevation poly)"));
        }

        #endregion

        #region Text

        [Fact]
        public void AText_ReadsEveryMemberItHas()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): TextString "abc", FieldCode "abc", Height 2.5, Rotation 0.0, StyleName "Standard",
            // InsertionPoint (0 20 0), TextAlignmentPoint (0 0 0) of a left-aligned text, Alignment 0, ScaleFactor 1.0, ObliqueAngle 0.0.
            Assert.Equal("\"AcDbText\"", Value(session, "(vla-get-ObjectName text)"));
            Assert.Equal("\"abc\"", Value(session, "(vla-get-TextString text)"));
            Assert.Equal("\"abc\"", Value(session, "(vla-FieldCode text)"));
            Assert.Equal("2.5", Value(session, "(vla-get-Height text)"));
            Assert.Equal("0.0", Value(session, "(vla-get-Rotation text)"));
            Assert.Equal("\"Standard\"", Value(session, "(vla-get-StyleName text)"));
            Assert.Equal("#<variant 8197 ...>", Value(session, "(vla-get-InsertionPoint text)"));
            Assert.Equal("(0.0 20.0 0.0)", Value(session, "(vlax-get text 'InsertionPoint)"));
            Assert.Equal("(0.0 0.0 0.0)", Value(session, "(vlax-get text 'TextAlignmentPoint)"));
            Assert.Equal("0", Value(session, "(vla-get-Alignment text)"));
            Assert.Equal("1.0", Value(session, "(vla-get-ScaleFactor text)"));
            Assert.Equal("0.0", Value(session, "(vla-get-ObliqueAngle text)"));
        }

        [Fact]
        public void ATextsStringHeightAndRotation_AreWrittenToItsGroups()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): TextString "abcd" is group 1, Height 5 is 40 5.0, Rotation 0.5 is 50 0.5.
            Value(session, "(vla-put-TextString text \"abcd\")");
            Assert.Equal("\"abcd\"", Group(session, "etext", 1));
            Value(session, "(vla-put-Height text 5)");
            Assert.Equal("5.0", Group(session, "etext", 40));
            Value(session, "(vla-put-Rotation text 0.5)");
            Assert.Equal("0.5", Group(session, "etext", 50));
            Assert.Equal("0.5", Value(session, "(vla-get-Rotation text)"));
        }

        [Fact]
        public void ATextsStyleName_MustBeAStyleOfTheDrawing()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): (vla-put-StyleName t "NOSUCH") is Automation Error. Key not found.
            Assert.Equal("Automation Error. Key not found", Error(session, "(vla-put-StyleName text \"NOSUCH\")"));
            Value(session, "(vla-put-StyleName text \"standard\")");
            Assert.Equal("\"Standard\"", Value(session, "(vla-get-StyleName text)"));
        }

        [Fact]
        public void ATextsInsertionPoint_MovesIt()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g3): (vla-put-InsertionPoint t (vlax-3d-point 5 20 0)) gives 10 (5.0 20.0 0.0).
            Value(session, "(vla-put-InsertionPoint text (vlax-3d-point 5 20 0))");
            Assert.Equal("(5.0 20.0 0.0)", Group(session, "etext", 10));
        }

        [Fact]
        public void TheAlignmentPointOfALeftAlignedText_IsNotApplicable()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g3): (vla-put-TextAlignmentPoint t (vlax-3d-point 9 20 0)) of a left-aligned text is
            // Automation Error. Not applicable, and group 11 stays (0 0 0).
            Assert.Equal(
                "Automation Error. Not applicable",
                Error(session, "(vla-put-TextAlignmentPoint text (vlax-3d-point 9 20 0))")
            );
            Assert.Equal("(0.0 0.0 0.0)", Group(session, "etext", 11));
        }

        [Fact]
        public void ScaleFactorAndObliqueAngle_AreWritten()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g3): (vla-put-ScaleFactor t 0.8), (vla-put-ObliqueAngle t 0.1) gives group 51 0.1 (radians).
            Value(session, "(vla-put-ScaleFactor text 0.8)");
            Assert.Equal("0.8", Group(session, "etext", 41));
            Assert.Equal("0.8", Value(session, "(vla-get-ScaleFactor text)"));
            Value(session, "(vla-put-ObliqueAngle text 0.1)");
            Assert.Equal("0.1", Group(session, "etext", 51));
            Assert.Equal("0.1", Value(session, "(vla-get-ObliqueAngle text)"));
        }

        [Fact]
        public void AlignmentCentre_SetsTheModeAndTakesTheAlignmentPointAsItIs()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g3): (vla-put-Alignment t 1) gives 72 1 and 11 (0 0 0), the point the text was never given; in
            // the fonts of the drawing the first point goes back by half the width of the words (without a font it stays).
            Value(session, "(vla-put-Alignment text 1)");
            Assert.Equal("1", Group(session, "etext", 72));
            Assert.Equal("(0.0 0.0 0.0)", Group(session, "etext", 11));
            Assert.Equal("1", Value(session, "(vla-get-Alignment text)"));
            Assert.Equal("(0.0 20.0 0.0)", Group(session, "etext", 10));

            // and now the second point is applicable
            Value(session, "(vla-put-TextAlignmentPoint text (vlax-3d-point 9 20 0))");
            Assert.Equal("(9.0 20.0 0.0)", Group(session, "etext", 11));
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(1, 1, 0)]
        [InlineData(2, 2, 0)]
        [InlineData(3, 3, 0)]
        [InlineData(4, 4, 0)]
        [InlineData(5, 5, 0)]
        [InlineData(6, 0, 3)]
        [InlineData(7, 1, 3)]
        [InlineData(8, 2, 3)]
        [InlineData(9, 0, 2)]
        [InlineData(10, 1, 2)]
        [InlineData(11, 2, 2)]
        [InlineData(12, 0, 1)]
        [InlineData(13, 1, 1)]
        [InlineData(14, 2, 1)]
        public void EachAlignment_IsAPairOfModesOfTheText(int alignment, int across, int up)
        {
            CadCommandSession session = Scene();

            // reference: acAlignment 0 left, 1 centre, 2 right, 3 aligned, 4 middle, 5 fit, 6-8 top, 9-11 middle, 12-14 bottom
            // of left, centre, right; the groups are 72 across and 73 up.
            Value(session, "(vla-put-Alignment text " + alignment + ")");
            Assert.Equal(across.ToString(), Group(session, "etext", 72));
            Assert.Equal(up.ToString(), up == 0 ? "0" : Group(session, "etext", 73));
            Assert.Equal(alignment.ToString(), Value(session, "(vla-get-Alignment text)"));
        }

        [Fact]
        public void AnAlignmentChangedFromOneToAnother_PassesNoModeAutoCadHasNoPlaceFor()
        {
            CadCommandSession session = Scene();

            // ours: D244 (middle is across 4 on the baseline only, so the order the modes are set in matters).
            Value(session, "(vla-put-Alignment text 4)");
            Value(session, "(vla-put-Alignment text 6)");
            Assert.Equal("6", Value(session, "(vla-get-Alignment text)"));
            Value(session, "(vla-put-Alignment text 5)");
            Assert.Equal("5", Value(session, "(vla-get-Alignment text)"));
            Value(session, "(vla-put-Alignment text 13)");
            Assert.Equal("13", Value(session, "(vla-get-Alignment text)"));
            Assert.Equal("Automation Error. Invalid argument", Error(session, "(vla-put-Alignment text 15)"));
            Assert.Equal("Automation Error. Invalid argument", Error(session, "(vla-put-Alignment text -1)"));
        }

        [Fact]
        public void ATextCentredInTheFontsOfTheDrawing_StartsBackByHalfItsWords()
        {
            using (var fonts = new FontFolder())
            {
                fonts.Write("test.shx", ShxFiles.Unifont());
                CadCommandSession session = Scene(out CadDocument document, fonts, "IOI");
                Value(session, "(vla-put-Height text 4)");

                // AutoCAD 2021 (GUI g3): the first point (10) of a text centred on (0 0 0) is half the width of its words back
                // (-3.33333 for abc in txt); ours: the DXF layer's AdjustAlignment in the document's fonts, whose box of "IOI" 4
                // high in the Unifont is the one GetBoundingBox gives.
                Value(session, "(vla-put-Alignment text 1)");
                DxfDBText text = (DxfDBText)document.FindEntity(HandleOfText(session));
                Assert.Equal((0.0, 0.0), (text.AlignmentPoint.X, text.AlignmentPoint.Y));
                Assert.True(text.Position.X < -1.0, "the first point went back: " + text.Position);
                Assert.Equal(0.0, text.Position.Y, 6);
            }
        }

        private static string HandleOfText(CadCommandSession session) =>
            Value(session, "(cdr (assoc 5 (entget etext)))").Trim('"');

        #endregion

        #region Fields

        [Fact]
        public void ATextStringWithFieldCodes_MakesAFieldThatShowsItsValue()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): (vla-put-TextString t "%<\AcVar Filename \f \"%fn2\">%") then TextString is the value
            // ("g1"), entget group 1 the value, FieldCode the codes; the drawing here is blank.dxf.
            Value(session, "(vla-put-TextString text \"%<\\\\AcVar Filename \\\\f \\\"%fn2\\\">%\")");
            Assert.Equal("\"blank\"", Value(session, "(vla-get-TextString text)"));
            Assert.Equal("\"blank\"", Group(session, "etext", 1));
            Assert.Equal("\"%<\\\\AcVar Filename \\\\f \\\"%fn2\\\">%\"", Value(session, "(vla-FieldCode text)"));
        }

        [Fact]
        public void APlainStringAfterAField_TakesTheFieldOut()
        {
            CadCommandSession session = Scene();
            Value(session, "(vla-put-TextString text \"%<\\\\AcVar Filename \\\\f \\\"%fn2\\\">%\")");

            // reference: a TextString with no field codes is plain words, as the Properties panel makes them.
            Value(session, "(vla-put-TextString text \"plain\")");
            Assert.Equal("\"plain\"", Value(session, "(vla-get-TextString text)"));
            Assert.Equal("\"plain\"", Value(session, "(vla-FieldCode text)"));
        }

        [Fact]
        public void TheFieldCodeOfAnMTextWithAField_IsItsCodes()
        {
            CadCommandSession session = Scene();
            Value(session, "(vla-put-TextString mtext \"%<\\\\AcVar Filename \\\\f \\\"%fn2\\\">%\")");

            // AutoCAD 2021 (GUI g2): FieldCode of an MText is the text with its codes, as for a TEXT.
            Assert.Equal("\"%<\\\\AcVar Filename \\\\f \\\"%fn2\\\">%\"", Value(session, "(vla-FieldCode mtext)"));
            Assert.Equal("\"blank\"", Group(session, "emtext", 1));
        }

        [Fact]
        public void FieldCodeOfATextWithNoField_IsItsWords()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): (vla-FieldCode t1) is "abc" for the text "abc", "mtext {\\Lbody}" for the MText.
            Assert.Equal("\"abc\"", Value(session, "(vla-FieldCode text)"));
            Assert.Equal("\"mtext {\\\\Lbody}\"", Value(session, "(vla-FieldCode mtext)"));
            Assert.Equal("T", Value(session, "(vlax-method-applicable-p text 'FieldCode)"));
            Assert.Equal("nil", Value(session, "(vlax-method-applicable-p line 'FieldCode)"));
        }

        #endregion

        #region MText

        [Fact]
        public void AnMText_ReadsEveryMemberItHas()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): TextString keeps its codes, Width 20.0, AttachmentPoint 1, Height 2.5, Rotation 0.0.
            Assert.Equal("\"AcDbMText\"", Value(session, "(vla-get-ObjectName mtext)"));
            Assert.Equal("\"mtext {\\\\Lbody}\"", Value(session, "(vla-get-TextString mtext)"));
            Assert.Equal("20.0", Value(session, "(vla-get-Width mtext)"));
            Assert.Equal("1", Value(session, "(vla-get-AttachmentPoint mtext)"));
            Assert.Equal("2.5", Value(session, "(vla-get-Height mtext)"));
            Assert.Equal("0.0", Value(session, "(vla-get-Rotation mtext)"));
            Assert.Equal("\"Standard\"", Value(session, "(vla-get-StyleName mtext)"));
            Assert.Equal("#<variant 8197 ...>", Value(session, "(vla-get-InsertionPoint mtext)"));
            Assert.Equal("(0.0 60.0 0.0)", Value(session, "(vlax-get mtext 'InsertionPoint)"));
        }

        [Fact]
        public void AnMTextIsWritten_ThroughItsGroups()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g3): TextString "new\Pline" keeps its code (group 1 "new\\Pline"), Width 30, AttachmentPoint 5 is 71 5.
            Value(session, "(vla-put-TextString mtext \"new\\\\Pline\")");
            Assert.Equal("\"new\\\\Pline\"", Group(session, "emtext", 1));
            Value(session, "(vla-put-Width mtext 30)");
            Assert.Equal("30.0", Group(session, "emtext", 41));
            Value(session, "(vla-put-AttachmentPoint mtext 5)");
            Assert.Equal("5", Group(session, "emtext", 71));
            Value(session, "(vla-put-Height mtext 4)");
            Assert.Equal("4.0", Group(session, "emtext", 40));
            Value(session, "(vla-put-InsertionPoint mtext (vlax-3d-point 1 61 0))");
            Assert.Equal("(1.0 61.0 0.0)", Group(session, "emtext", 10));
        }

        [Fact]
        public void AnMTextsStyleAndAttachment_AreChecked()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): a style that is not there is Key not found; ours: D244 for an attachment of 1 to 9 only.
            Assert.Equal("Automation Error. Key not found", Error(session, "(vla-put-StyleName mtext \"NOSUCH\")"));
            Assert.StartsWith("Automation Error. ", Error(session, "(vla-put-AttachmentPoint mtext 10)"));
            Assert.Equal("1", Value(session, "(vla-get-AttachmentPoint mtext)"));
        }

        #endregion

        #region Block references

        [Fact]
        public void ABlockReference_ReadsEveryMemberItHas()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): Name "GHB", EffectiveName "GHB", HasAttributes true, IsDynamicBlock false, XScaleFactor 2.0.
            Assert.Equal("\"AcDbBlockReference\"", Value(session, "(vla-get-ObjectName ins)"));
            Assert.Equal("\"TAGGED\"", Value(session, "(vla-get-Name ins)"));
            Assert.Equal("\"TAGGED\"", Value(session, "(vla-get-EffectiveName ins)"));
            Assert.Equal(":vlax-true", Value(session, "(vla-get-HasAttributes ins)"));
            Assert.Equal(":vlax-false", Value(session, "(vla-get-IsDynamicBlock ins)"));
            Assert.Equal("2.0", Value(session, "(vla-get-XScaleFactor ins)"));
            Assert.Equal("1.0", Value(session, "(vla-get-YScaleFactor ins)"));
            Assert.Equal("1.0", Value(session, "(vla-get-ZScaleFactor ins)"));
            Assert.Equal("0.0", Value(session, "(vla-get-Rotation ins)"));
            Assert.Equal("(100.0 0.0 0.0)", Value(session, "(vlax-get ins 'InsertionPoint)"));
            Assert.Equal("#<variant 8197 ...>", Value(session, "(vla-get-InsertionPoint ins)"));
        }

        [Fact]
        public void EffectiveName_IsAMemberOfAReferenceAndNotOfALine()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): (vlax-property-available-p BO 'EFFECTIVENAME) T, of a line nil.
            Assert.Equal("T", Value(session, "(vlax-property-available-p ins 'EFFECTIVENAME)"));
            Assert.Equal("nil", Value(session, "(vlax-property-available-p ins 'EFFECTIVENAME T)"));
            Assert.Equal("nil", Value(session, "(vlax-property-available-p line 'EFFECTIVENAME)"));
            Assert.Equal("T", Value(session, "(vlax-property-available-p plain 'EFFECTIVENAME)"));
            Assert.Equal("T", Value(session, "(vlax-method-applicable-p ins 'GETATTRIBUTES)"));
            Assert.Equal("T", Value(session, "(vlax-method-applicable-p ins 'GETCONSTANTATTRIBUTES)"));
        }

        [Fact]
        public void TheEffectiveNameOfADynamicBlocksReference_IsTheDynamicBlock()
        {
            var database = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "GH_PANEL", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(block);
            DxfLine[] lines =
            {
                new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0)),
            };

            foreach (DxfLine line in lines)
            {
                block.AppendEntity(line);
            }

            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                "Overall width",
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                DxfBlockParameterValueSet.List(new[] { 100.0, 150, 200 })
            );
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            var reference = new DxfBlockReference(new GeoPoint3(1000, 500, 0), block);
            database.ModelSpace.AppendEntity(reference);
            CadDocument document = CadDocument.From(database, "panel.dxf");
            Assert.True(CadDynamicBlocks.SetProperty(document, new[] { reference.Handle }, "Width", "150").Succeeded);
            CadCommandSession session = Session(document);
            Value(session, "(vl-load-com)");
            Value(session, "(setq app (vlax-get-acad-object) doc (vla-get-ActiveDocument app))");
            Value(session, "(setq d (vlax-ename->vla-object (handent \"" + reference.Handle + "\")))");

            // reference: Name is the anonymous block's (*U...), EffectiveName the dynamic block it is made from, IsDynamicBlock true.
            Assert.StartsWith("\"*U", Value(session, "(vla-get-Name d)"), StringComparison.Ordinal);
            Assert.Equal("\"GH_PANEL\"", Value(session, "(vla-get-EffectiveName d)"));
            Assert.Equal(":vlax-true", Value(session, "(vla-get-IsDynamicBlock d)"));
        }

        [Fact]
        public void ABlockReferencesScaleRotationAndPoint_AreWritten()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g3): XScaleFactor 3 is group 41 3.0, Rotation 0.5, InsertionPoint (110 0 0) is 10 (110.0 0.0 0.0).
            Value(session, "(vla-put-XScaleFactor ins 3)");
            Assert.Equal("3.0", Group(session, "eins", 41));
            Value(session, "(vla-put-YScaleFactor ins 4)");
            Assert.Equal("4.0", Group(session, "eins", 42));
            Value(session, "(vla-put-ZScaleFactor ins 5)");
            Assert.Equal("5.0", Group(session, "eins", 43));
            Assert.Equal("3.0", Value(session, "(vla-get-XScaleFactor ins)"));
            Value(session, "(vla-put-Rotation ins 0.5)");
            Assert.Equal("0.5", Group(session, "eins", 50));
            Value(session, "(vla-put-InsertionPoint ins (vlax-3d-point 110 0 0))");
            Assert.Equal("(110.0 0.0 0.0)", Group(session, "eins", 10));
        }

        [Fact]
        public void AScaleFactorOfNought_IsAnAutomationError()
        {
            CadCommandSession session = Scene();

            // ours: D224 (the typed model refuses a factor of nought; its reason is the error).
            Assert.StartsWith("Automation Error. ", Error(session, "(vla-put-XScaleFactor ins 0)"));
            Assert.Equal("2.0", Value(session, "(vla-get-XScaleFactor ins)"));
        }

        [Fact]
        public void MovingAReference_MovesItsAttributes()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g3): after (vla-put-InsertionPoint BO (vlax-3d-point 110 0 0)) the attribute's InsertionPoint is (110 0 0).
            Assert.Equal("(100.0 0.0 0.0)", Value(session, "(vlax-get att 'InsertionPoint)"));
            Value(session, "(vla-put-InsertionPoint ins (vlax-3d-point 110 0 0))");
            Assert.Equal(
                "(110.0 0.0 0.0)",
                Value(session, "(vlax-get (car (vlax-invoke ins 'GetAttributes)) 'InsertionPoint)")
            );
        }

        [Fact]
        public void ANameOfABlockThatIsNotThere_IsKeyNotFound()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g3): (vla-put-Name BO "NOSUCH") is Automation Error. Key not found.
            Assert.Equal("Automation Error. Key not found", Error(session, "(vla-put-Name ins \"NOSUCH\")"));
            Assert.Equal("\"TAGGED\"", Group(session, "eins", 2));
        }

        [Fact]
        public void ANameOfABlockThatIsThere_ReplacesTheBlockOfTheReference()
        {
            CadCommandSession session = Scene();

            // reference: Name is read and write; the block is found in any case; ours: D224.
            Value(session, "(vla-put-Name plain \"tagged\")");
            Assert.Equal("\"TAGGED\"", Group(session, "eplain", 2));
            Assert.Equal("\"TAGGED\"", Value(session, "(vla-get-Name plain)"));
        }

        #endregion

        #region GetAttributes

        [Fact]
        public void GetAttributes_IsAVariantOfObjects()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): a variant 8201; vlax-safearray->list of its value the attribute objects;
            // vlax-invoke the list of them; vlax-invoke-method the variant.
            Assert.Equal("#<variant 8201 ...>", Value(session, "(vla-GetAttributes ins)"));
            Assert.Equal("8201", Value(session, "(vlax-variant-type (vla-GetAttributes ins))"));
            Assert.Equal("(#<VLA-OBJECT IAcadAttributeReference @>)", Attributes(session));
            Assert.Equal(
                "(#<VLA-OBJECT IAcadAttributeReference @>)",
                Value(session, "(vlax-invoke ins 'GetAttributes)")
            );
            Assert.Equal("#<variant 8201 ...>", Value(session, "(vlax-invoke-method ins 'GetAttributes)"));
            Assert.Equal(
                "nil",
                Value(session, "(eq (car (vlax-invoke ins 'GetAttributes)) (car (vlax-invoke ins 'GetAttributes)))")
            );
            Assert.Equal("T", Value(session, "(equal (car (vlax-invoke ins 'GetAttributes)) att)"));
        }

        [Fact]
        public void GetAttributesOfAReferenceWithNone_IsAnEmptyArrayThatCannotBeListed()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): HasAttributes false; a variant 8201 whose array has upper bound -1;
            // vlax-safearray->list of it is Invalid index; vlax-invoke gives nil.
            Assert.Equal(":vlax-false", Value(session, "(vla-get-HasAttributes plain)"));
            Assert.Equal("8201", Value(session, "(vlax-variant-type (vla-GetAttributes plain))"));
            Assert.Equal("#<safearray...>", Value(session, "(vlax-variant-value (vla-GetAttributes plain))"));
            Assert.Equal(
                "-1",
                Value(session, "(vlax-safearray-get-u-bound (vlax-variant-value (vla-GetAttributes plain)) 1)")
            );
            Assert.Equal(
                "ActiveX Server returned an error: Invalid index",
                Error(session, "(vlax-safearray->list (vlax-variant-value (vla-GetAttributes plain)))")
            );
            Assert.Equal("nil", Value(session, "(vlax-invoke plain 'GetAttributes)"));
        }

        [Fact]
        public void GetConstantAttributes_IsTheConstantDefinitionsOfTheBlock()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): IAcadAttribute objects (CTAG, CV, Constant :vlax-true), ObjectName AcDbAttributeDefinition.
            Assert.Equal("8201", Value(session, "(vlax-variant-type (vla-GetConstantAttributes ins))"));
            Value(session, "(setq cl (vlax-safearray->list (vlax-variant-value (vla-GetConstantAttributes ins))))");
            Assert.Equal("1", Value(session, "(length cl)"));
            Assert.Equal("#<VLA-OBJECT IAcadAttribute @>", Value(session, "(car cl)"));
            Assert.Equal("\"AcDbAttributeDefinition\"", Value(session, "(vla-get-ObjectName (car cl))"));
            Assert.Equal("\"CTAG\"", Value(session, "(vla-get-TagString (car cl))"));
            Assert.Equal("\"CV\"", Value(session, "(vla-get-TextString (car cl))"));
            Assert.Equal(":vlax-true", Value(session, "(vla-get-Constant (car cl))"));
            Assert.Equal("nil", Value(session, "(vlax-invoke plain 'GetConstantAttributes)"));
        }

        #endregion

        #region Attributes

        [Fact]
        public void AnAttributeReference_ReadsEveryMemberItHas()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): ObjectName AcDbAttribute, TagString "TAG1", TextString, Constant false, Invisible false,
            // Height 5.0, OwnerID the reference's ObjectID.
            Value(session, "(setq a (car (vlax-invoke ins 'GetAttributes)))");
            Assert.Equal("\"AcDbAttribute\"", Value(session, "(vla-get-ObjectName a)"));
            Assert.Equal("\"TAG1\"", Value(session, "(vla-get-TagString a)"));
            Assert.Equal("\"v30\"", Value(session, "(vla-get-TextString a)"));
            Assert.Equal(":vlax-false", Value(session, "(vla-get-Constant a)"));
            Assert.Equal(":vlax-false", Value(session, "(vla-get-Invisible a)"));
            Assert.Equal("5.0", Value(session, "(vla-get-Height a)"));
            Assert.Equal("0.0", Value(session, "(vla-get-Rotation a)"));
            Assert.Equal("\"Standard\"", Value(session, "(vla-get-StyleName a)"));
            Assert.Equal("(100.0 0.0 0.0)", Value(session, "(vlax-get a 'InsertionPoint)"));
            Assert.Equal(Value(session, "(vla-get-ObjectID ins)"), Value(session, "(vla-get-OwnerID a)"));
        }

        [Fact]
        public void TheNameOfAnAttributesEntity_GivesItsAttributeReference()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g3): (vlax-ename->vla-object (entnext BO)) is an IAcadAttributeReference, ObjectName AcDbAttribute.
            Value(session, "(setq n (entnext eins))");
            Assert.Equal("\"ATTRIB\"", Value(session, "(cdr (assoc 0 (entget n)))"));
            Assert.Equal("#<VLA-OBJECT IAcadAttributeReference @>", Value(session, "(vlax-ename->vla-object n)"));
            Assert.Equal("\"AcDbAttribute\"", Value(session, "(vla-get-ObjectName (vlax-ename->vla-object n))"));
            Assert.Equal("T", Value(session, "(equal (vlax-ename->vla-object n) att)"));
            Assert.Equal("T", Value(session, "(equal (vlax-vla-object->ename att) n)"));
        }

        [Fact]
        public void AnAttributesTextString_IsWrittenToTheAttribOfItsInsert()
        {
            CadCommandSession session = Scene();
            Value(session, "(setq a (car (vlax-invoke ins 'GetAttributes)))");

            // AutoCAD 2021 (GUI g2): (vla-put-TextString a "NEW") gives group 1 "NEW" in entget of the ATTRIB.
            Value(session, "(vla-put-TextString a \"NEW\")");
            Assert.Equal("\"NEW\"", Value(session, "(cdr (assoc 1 (entget (entnext eins))))"));
            Assert.Equal("\"NEW\"", Value(session, "(vla-get-TextString att)"));
        }

        [Fact]
        public void AnAttributesTagAndInvisible_AreWritten()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g3): (vla-put-TagString A "TAGX") is group 2 "TAGX", (vla-put-Invisible A :vlax-true) is 70 bit 1.
            Value(session, "(vla-put-TagString att \"TAGX\")");
            Assert.Equal("\"TAGX\"", Value(session, "(cdr (assoc 2 (entget (entnext eins))))"));
            Value(session, "(vla-put-Invisible att :vlax-true)");
            Assert.Equal("1", Value(session, "(cdr (assoc 70 (entget (entnext eins))))"));
            Assert.Equal(":vlax-true", Value(session, "(vla-get-Invisible att)"));
            Value(session, "(vla-put-Invisible att :vlax-false)");
            Assert.Equal("0", Value(session, "(cdr (assoc 70 (entget (entnext eins))))"));
        }

        [Fact]
        public void AnAttributesHeightRotationStyleAndPoint_AreWritten()
        {
            CadCommandSession session = Scene();

            // reference: all four are read and write; ours: D224.
            Value(session, "(vla-put-Height att 3)");
            Assert.Equal("3.0", Value(session, "(cdr (assoc 40 (entget (entnext eins))))"));
            Value(session, "(vla-put-Rotation att 0.25)");
            Assert.Equal("0.25", Value(session, "(cdr (assoc 50 (entget (entnext eins))))"));
            Value(session, "(vla-put-InsertionPoint att (vlax-3d-point 101 2 0))");
            Assert.Equal("(101.0 2.0 0.0)", Value(session, "(cdr (assoc 10 (entget (entnext eins))))"));
            Assert.Equal("Automation Error. Key not found", Error(session, "(vla-put-StyleName att \"NOSUCH\")"));
            Value(session, "(vla-put-StyleName att \"Standard\")");
        }

        [Fact]
        public void AnAttributesWrite_IsOneUndoStepThatTakesTheValueBack()
        {
            CadCommandSession session = Scene();

            // ours: D226 (the evaluation's undo step); U takes the typed put back.
            Value(session, "(vla-put-TextString att \"NEW\")");
            Assert.Equal("\"NEW\"", Value(session, "(vla-get-TextString att)"));
            session.Feed("U");
            Assert.Equal("\"v30\"", Value(session, "(vla-get-TextString (vlax-ename->vla-object (entnext eins)))"));
        }

        [Fact]
        public void TheCanvasDrawsAnAttributeAndATextAgain_AfterTheyAreWrittenThroughTheirObjects()
        {
            CadCommandSession session = Scene(out CadDocument document);
            Assert.Contains(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>(), t =>
                t.Text == "v30"
            );

            // ours: D226 (an edit through an object is an edit of the session, which the canvas draws again): the attribute is
            // changed through its INSERT, so that the INSERT's picture is drawn again with it.
            Value(session, "(vla-put-TextString att \"NEW\")");
            Value(session, "(vla-put-TextString text \"abcd\")");
            DisplayText2[] texts = document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>().ToArray();
            Assert.Contains(texts, t => t.Text == "NEW");
            Assert.DoesNotContain(texts, t => t.Text == "v30");
            Assert.Contains(texts, t => t.Text == "abcd");
            Value(session, "(vla-put-InsertionPoint ins (vlax-3d-point 110 0 0))");
            Assert.Contains(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>(), t =>
                t.Text == "NEW" && Math.Abs(t.X - 110.0) < 1e-6
            );
        }

        [Fact]
        public void ACentredTextsString_MovesItsFirstPointAgainInTheFontsOfTheDrawing()
        {
            using (var fonts = new FontFolder())
            {
                fonts.Write("test.shx", ShxFiles.Unifont());
                CadCommandSession session = Scene(out CadDocument document, fonts, "IOI");
                Value(session, "(vla-put-Height text 4)");
                Value(session, "(vla-put-Alignment text 1)");
                DxfDBText text = (DxfDBText)document.FindEntity(HandleOfText(session));
                double short3 = text.Position.X;

                // ours: D224 (a write to the words of a text held by its middle works its first point out again: wider words,
                // further back; the second point stays).
                Value(session, "(vla-put-TextString text \"IOIOIOI\")");
                Assert.True(text.Position.X < short3 - 1.0, short3 + " then " + text.Position.X);
                Assert.Equal(0.0, text.AlignmentPoint.X, 6);
            }
        }

        [Fact]
        public void AnAttributeOnALockedLayer_IsReadButNotWritten()
        {
            CadCommandSession session = Scene(out CadDocument document);
            DxfBlockReference reference = (DxfBlockReference)
                document.FindEntity(Value(session, "(cdr (assoc 5 (entget eins)))").Trim('"'));
            reference.AttributeCollection[0].Layer = "Locked";

            // AutoCAD 2021 (GUI g2): a put on a locked layer is Automation Error. On locked layer; ours: D217 for the layer of the attribute.
            Assert.Equal("\"v30\"", Value(session, "(vla-get-TextString att)"));
            Assert.Equal("Automation Error. On locked layer", Error(session, "(vla-put-TextString att \"NEW\")"));
            Assert.Equal("\"v30\"", Value(session, "(vla-get-TextString att)"));
        }

        [Fact]
        public void AnAttributeDefinition_ReadsEveryMemberItHas()
        {
            CadCommandSession session = Scene();
            Value(session, "(setq bd (vla-Item (vla-get-Blocks doc) \"TAGGED\") ad (vla-Item bd 0))");

            // AutoCAD 2021 (GUI g2): IAcadAttribute, ObjectName AcDbAttributeDefinition, TagString "TAG1", PromptString "Prompt",
            // TextString "dflt", Constant false; the definition is the first of the block, in the order made.
            Assert.Equal("#<VLA-OBJECT IAcadAttribute @>", Value(session, "ad"));
            Assert.Equal("\"AcDbAttributeDefinition\"", Value(session, "(vla-get-ObjectName ad)"));
            Assert.Equal("\"TAG1\"", Value(session, "(vla-get-TagString ad)"));
            Assert.Equal("\"Prompt\"", Value(session, "(vla-get-PromptString ad)"));
            Assert.Equal("\"dflt\"", Value(session, "(vla-get-TextString ad)"));
            Assert.Equal(":vlax-false", Value(session, "(vla-get-Constant ad)"));
            Assert.Equal(":vlax-false", Value(session, "(vla-get-Invisible ad)"));
            Assert.Equal("5.0", Value(session, "(vla-get-Height ad)"));
            Assert.Equal("0.0", Value(session, "(vla-get-Rotation ad)"));
            Assert.Equal("(0.0 0.0 0.0)", Value(session, "(vlax-get ad 'InsertionPoint)"));
            Assert.Equal("\"Standard\"", Value(session, "(vla-get-StyleName ad)"));
        }

        [Fact]
        public void ABlockDefinitionIsWalkedInTheOrderMade_AttributeDefinitionsAndTheLine()
        {
            CadCommandSession session = Scene();
            Value(session, "(setq names nil bd (vla-Item (vla-get-Blocks doc) \"TAGGED\"))");
            Value(session, "(vlax-for e bd (setq names (cons (vla-get-ObjectName e) names)))");

            // AutoCAD 2021 (GUI g3): a definition is walked in the order made (ATTDEF, ATTDEF, LINE).
            Assert.Equal(
                "(\"AcDbAttributeDefinition\" \"AcDbAttributeDefinition\" \"AcDbLine\")",
                Value(session, "(reverse names)")
            );
        }

        [Fact]
        public void AnAttributeDefinitionOfABlock_IsWrittenInTheDefinition()
        {
            CadCommandSession session = Scene(out CadDocument document);
            Value(session, "(setq ad (vla-Item (vla-Item (vla-get-Blocks doc) \"TAGGED\") 0))");

            // reference: Prompt, Tag, default value and the rest are read and write; ours: D224.
            Value(session, "(vla-put-PromptString ad \"Say\")");
            Value(session, "(vla-put-TextString ad \"other\")");
            Value(session, "(vla-put-TagString ad \"TAG9\")");
            Value(session, "(vla-put-Invisible ad :vlax-true)");
            DxfAttributeDefinition definition = document
                .Database.BlockTable["TAGGED"]
                .OfType<DxfAttributeDefinition>()
                .First();
            Assert.Equal("Say", definition.Prompt);
            Assert.Equal("other", definition.TextString);
            Assert.Equal("TAG9", definition.Tag);
            Assert.True(definition.Invisible);
        }

        #endregion

        #region The box of a text

        [Fact]
        public void TheBoundingBoxOfALine_IsItsEnds()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (GUI g2): GetBoundingBox into two symbols of safearrays.
            Value(session, "(vla-GetBoundingBox line 'lo 'hi)");
            Assert.Equal("(0.0 0.0 0.0)", Value(session, "(vlax-safearray->list lo)"));
            Assert.Equal("(30.0 40.0 0.0)", Value(session, "(vlax-safearray->list hi)"));
        }

        [Fact]
        public void TheBoundingBoxOfAText_IsTheBoxOfItsWordsInTheFontsOfTheDrawing()
        {
            using (var fonts = new FontFolder())
            {
                fonts.Write("test.shx", ShxFiles.Unifont());
                CadCommandSession session = Scene(out CadDocument document, fonts, "IOI");
                DxfDBText text = (DxfDBText)document.FindEntity(HandleOfText(session));
                Value(session, "(vla-put-Height text 4)");
                Value(session, "(vla-put-InsertionPoint text (vlax-3d-point 0 20 0))");
                string searched = document.Database.FontSearchPath;
                document.Database.FontSearchPath = fonts.FullPath;
                GeoAabb3 expected = text.GeometricExtents;
                document.Database.FontSearchPath = searched;

                // AutoCAD 2021 (GUI g2): the box of "abc" 2.5 high in txt is (0 20 0) to (6.66667 22.5 0): the box of its words
                // at the insertion point. The test font is the Unifont; the box is the DXF layer's (GeometricExtents), measured in
                // the document's fonts, which are the ones the drawing is drawn in.
                Value(session, "(vla-GetBoundingBox text 'lo 'hi)");
                Assert.True((expected.Max - expected.Min).Length > 1.0, "the text is measured: " + expected);
                Assert.Equal(
                    expected.Min.X,
                    double.Parse(
                        Value(session, "(car (vlax-safearray->list lo))"),
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                    3
                );
                Assert.Equal(
                    expected.Min.Y,
                    double.Parse(
                        Value(session, "(cadr (vlax-safearray->list lo))"),
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                    3
                );
                Assert.Equal(
                    expected.Max.X,
                    double.Parse(
                        Value(session, "(car (vlax-safearray->list hi))"),
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                    3
                );
                Assert.Equal(
                    expected.Max.Y,
                    double.Parse(
                        Value(session, "(cadr (vlax-safearray->list hi))"),
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                    3
                );
                Assert.Equal("T", Value(session, "(< (car (vlax-safearray->list lo)) 1.0)"));
                Assert.Equal("T", Value(session, "(> (cadr (vlax-safearray->list hi)) 22.0)"));

                // vlax-invoke-method sets the symbols too (g2), the old vlax-invoke sets nothing.
                Value(session, "(vlax-invoke-method text 'GetBoundingBox 'lo2 'hi2)");
                Assert.Equal("safearray", Value(session, "(type lo2)"));
                Value(session, "(vlax-invoke text 'GetBoundingBox 'lo3 'hi3)");
                Assert.Equal("nil", Value(session, "lo3"));
            }
        }

        [Fact]
        public void TheBoundingBoxOfAnMText_IsTheBoxOfItsLayout()
        {
            using (var fonts = new FontFolder())
            {
                fonts.Write("test.shx", ShxFiles.Unifont());
                CadCommandSession session = Scene(out CadDocument document, fonts);
                DxfMText mtext = (DxfMText)
                    document.FindEntity(Value(session, "(cdr (assoc 5 (entget emtext)))").Trim('"'));
                Value(session, "(vla-put-TextString mtext \"IOI\")");
                Value(session, "(vla-put-Height mtext 4)");
                string searched = document.Database.FontSearchPath;
                document.Database.FontSearchPath = fonts.FullPath;
                GeoAabb3 expected = mtext.GeometricExtents;
                document.Database.FontSearchPath = searched;

                // AutoCAD 2021 (GUI g2): an MText's box is the box of its layout, as wide as its Width; ours: the DXF layer's.
                Value(session, "(vla-GetBoundingBox mtext 'lo 'hi)");
                Assert.Equal(
                    "T",
                    Value(
                        session,
                        "(< (distance (vlax-safearray->list lo) (list "
                            + expected.Min.X.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            + " "
                            + expected.Min.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            + " 0.0)) 0.001)"
                    )
                );
                Assert.Equal(
                    "T",
                    Value(
                        session,
                        "(< (distance (vlax-safearray->list hi) (list "
                            + expected.Max.X.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            + " "
                            + expected.Max.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            + " 0.0)) 0.001)"
                    )
                );
                Assert.True(expected.Max.X - expected.Min.X > 19.9, "as wide as its width: " + expected);
            }
        }

        [Fact]
        public void TheBoundingBoxOfATextWhoseFontIsNotFound_IsItsInsertionPoint()
        {
            CadCommandSession session = Scene();

            // ours: D237 (a text of a font that is not found is not measured: the box is the insertion point alone).
            Value(session, "(vla-GetBoundingBox text 'lo 'hi)");
            Assert.Equal("(0.0 20.0 0.0)", Value(session, "(vlax-safearray->list lo)"));
            Assert.Equal("(0.0 20.0 0.0)", Value(session, "(vlax-safearray->list hi)"));
        }

        [Fact]
        public void TextBox_IsTheBoxOfTheWordsBesideTheInsertionPoint()
        {
            using (var fonts = new FontFolder())
            {
                fonts.Write("test.shx", ShxFiles.Unifont());
                CadCommandSession session = Scene(out CadDocument document, fonts, "IOI");
                Value(session, "(vla-put-Height text 4)");
                Value(session, "(vla-GetBoundingBox text 'lo 'hi)");

                // AutoCAD 2021 (GUI g2): (textbox (entget t)) is ((0.0 0.0 0.0) (6.66667 2.5 0.0)) for abc in txt, and the
                // GetBoundingBox box is that box moved to the insertion point (0 20 0); ours for the Unifont.
                Value(session, "(setq tb (textbox (entget etext)))");
                Assert.Equal("2", Value(session, "(length tb)"));
                Assert.Equal(
                    "T",
                    Value(
                        session,
                        "(< (distance (mapcar '+ (car tb) '(0.0 20.0 0.0)) (vlax-safearray->list lo)) 0.001)"
                    )
                );
                Assert.Equal(
                    "T",
                    Value(
                        session,
                        "(< (distance (mapcar '+ (cadr tb) '(0.0 20.0 0.0)) (vlax-safearray->list hi)) 0.001)"
                    )
                );
                Assert.Equal("T", Value(session, "(> (car (cadr tb)) 1.0)"));
            }
        }

        [Fact]
        public void TextBoxOfATurnedText_IsTheBoxOfTheUnturnedText()
        {
            using (var fonts = new FontFolder())
            {
                fonts.Write("test.shx", ShxFiles.Unifont());
                CadCommandSession session = Scene(out _, fonts, "IOI");
                Value(session, "(vla-put-Height text 4)");
                Value(session, "(setq flat (textbox (entget etext)))");
                Value(session, "(vla-put-Rotation text 0.6)");

                // ours: AutoCAD's textbox ignores the rotation; the entity itself is not turned back.
                Assert.Equal("T", Value(session, "(equal flat (textbox (entget etext)) 0.0001)"));
                Assert.Equal("0.6", Value(session, "(vla-get-Rotation text)"));
            }
        }

        [Fact]
        public void TextBoxOfWordsNoFontMeasures_IsNil()
        {
            CadCommandSession session = Scene();

            // ours: nil where the words are not measured; of something that is no text, nil; of no list, a type error.
            Assert.Equal("nil", Value(session, "(textbox (entget etext))"));
            Assert.Equal("nil", Value(session, "(textbox (entget eline))"));
            Assert.Equal("nil", Value(session, "(textbox (list (cons 0 \"TEXT\")))"));
            Assert.Equal("bad argument type: consp 5", Error(session, "(textbox 5)"));
            Assert.Equal("bad argument type: consp nil", Error(session, "(textbox nil)"));
            Assert.Equal("SUBR", Value(session, "(type textbox)"));
        }

        #endregion

        #region vlax-curve-* given objects

        [Fact]
        public void TheCurveFunctions_TakeTheObjectOfAnEntityAsTheyTakeItsName()
        {
            CadCommandSession session = Scene();

            // reference: the vlax-curve functions take an entity name or its VLA-OBJECT (D208).
            foreach (string name in new[] { "line", "circle", "arc", "poly" })
            {
                Assert.Equal(
                    Value(session, "(vlax-curve-getEndParam e" + name + ")"),
                    Value(session, "(vlax-curve-getEndParam " + name + ")")
                );
                Assert.Equal(
                    Value(session, "(vlax-curve-getStartPoint e" + name + ")"),
                    Value(session, "(vlax-curve-getStartPoint " + name + ")")
                );
                Assert.Equal(
                    Value(session, "(vlax-curve-getEndPoint e" + name + ")"),
                    Value(session, "(vlax-curve-getEndPoint " + name + ")")
                );
                Assert.Equal(
                    Value(session, "(vlax-curve-getClosestPointTo e" + name + " '(60 7 0))"),
                    Value(session, "(vlax-curve-getClosestPointTo " + name + " '(60 7 0))")
                );
                Assert.Equal(
                    Value(session, "(vlax-curve-isClosed e" + name + ")"),
                    Value(session, "(vlax-curve-isClosed " + name + ")")
                );
            }

            Assert.Equal("(30.0 40.0 0.0)", Value(session, "(vlax-curve-getEndPoint line)"));
            Assert.Equal("50.0", Value(session, "(vlax-curve-getEndParam line)"));
            Assert.Equal("(15.0 20.0 0.0)", Value(session, "(vlax-curve-getPointAtDist line 25)"));
            Assert.Equal("25.0", Value(session, "(vlax-curve-getDistAtPoint line '(15 20 0))"));
        }

        [Fact]
        public void LengthAndAreaOfTheMembers_AgreeWithTheCurveFunctions()
        {
            CadCommandSession session = Scene();

            // reference: Length, ArcLength and Area of the ActiveX object are what AcDbCurve's length and area are.
            Assert.Equal(
                Value(session, "(vlax-curve-getDistAtParam line (vlax-curve-getEndParam line))"),
                Value(session, "(vla-get-Length line)")
            );
            Assert.Equal(
                Value(session, "(vlax-curve-getDistAtParam poly (vlax-curve-getEndParam poly))"),
                Value(session, "(vla-get-Length poly)")
            );
            Assert.Equal(
                Value(session, "(vlax-curve-getDistAtParam arc (vlax-curve-getEndParam arc))"),
                Value(session, "(vla-get-ArcLength arc)")
            );
            Assert.Equal(
                Value(session, "(vlax-curve-getDistAtParam circle (vlax-curve-getEndParam circle))"),
                Value(session, "(vla-get-Circumference circle)")
            );
            Assert.Equal(Value(session, "(vlax-curve-getArea circle)"), Value(session, "(vla-get-Area circle)"));
            Assert.Equal(Value(session, "(vlax-curve-getArea arc)"), Value(session, "(vla-get-Area arc)"));
            Assert.Equal(Value(session, "(vlax-curve-getArea poly)"), Value(session, "(vla-get-Area poly)"));
            Assert.Equal("0.0", Value(session, "(vlax-curve-getArea eline)"));
            Assert.Equal("0.0", Value(session, "(vlax-curve-getArea line)"));
        }

        [Fact]
        public void TheCurveFunctionsOfAnErasedObject_AreNotCurves()
        {
            CadCommandSession session = Scene();
            Value(session, "(vla-Delete line)");

            // AutoCAD 2021 (GUI g2): (vlax-curve-getStartPoint O2) of an erased line is bad argument value: AcDbCurve 45
            // (the counter is AutoCAD's own; ours the decimal value of the handle).
            string handle = Regex
                .Match(Error(session, "(vlax-curve-getStartPoint line)"), @"^bad argument value: AcDbCurve (\d+)$")
                .Groups[1]
                .Value;
            Assert.NotEqual(string.Empty, handle);

            // brought back by entdel, the old object is the curve again (g2)
            Value(session, "(entdel eline)");
            Assert.Equal("(0.0 0.0 0.0)", Value(session, "(vlax-curve-getStartPoint line)"));
        }

        [Fact]
        public void TheCurveFunctionsOfAnObjectThatIsNoCurve_AreAnErrorAsOfItsName()
        {
            CadCommandSession session = Scene();

            // AutoCAD 2021 (a4): a TEXT or an INSERT name is bad argument value: AcDbCurve n; the object is the same.
            Assert.Matches(@"^bad argument value: AcDbCurve \d+$", Error(session, "(vlax-curve-getStartPoint text)"));
            Assert.Matches(@"^bad argument value: AcDbCurve \d+$", Error(session, "(vlax-curve-getStartPoint etext)"));
            Assert.Matches(@"^bad argument value: AcDbCurve \d+$", Error(session, "(vlax-curve-getEndParam ins)"));
            Assert.Matches(@"^bad argument value: AcDbCurve \d+$", Error(session, "(vlax-curve-getArea att)"));

            // ours: a block or a layer record has a handle too, the application, the document, a collection and a set do not
            Assert.Matches(
                @"^bad argument value: AcDbCurve \d+$",
                Error(session, "(vlax-curve-getStartPoint (vla-Item (vla-get-Blocks doc) \"TAGGED\"))")
            );
            Assert.Equal(
                "unable to get ObjectID: #<VLA-OBJECT IAcadDocument @>",
                Error(session, "(vlax-curve-getStartPoint doc)")
            );
            Assert.Equal(
                "unable to get ObjectID: #<VLA-OBJECT IAcadApplication @>",
                Error(session, "(vlax-curve-getStartPoint app)")
            );
            Assert.Equal("unable to get ObjectID: nil", Error(session, "(vlax-curve-getStartPoint nil)"));
            Assert.Equal("unable to get ObjectID: 5", Error(session, "(vlax-curve-getStartPoint 5)"));
        }

        [Fact]
        public void TheCurveFunctionsOfAReleasedObject_AreTheReleasedError()
        {
            CadCommandSession session = Scene();
            Value(session, "(vlax-release-object circle)");

            // AutoCAD 2021 (GUI g2): a released value is null interface pointer in every use of it; ours for the curves.
            Assert.Equal(
                "null interface pointer: #<VLA-OBJECT 0000000000000000>",
                Error(session, "(vlax-curve-getStartPoint circle)")
            );
            Assert.Equal("(55.0 0.0 0.0)", Value(session, "(vlax-curve-getStartPoint ecircle)"));
        }

        [Fact]
        public void TheCurveFunctionsGivenAnObject_FollowAnEditMadeThroughIt()
        {
            CadCommandSession session = Scene();

            // reference: the object stands for the entity as it is now: a radius changed through it is the curve's.
            Value(session, "(vla-put-Radius circle 10)");
            Assert.Equal("(60.0 0.0 0.0)", Value(session, "(vlax-curve-getStartPoint circle)"));
            Assert.Equal("314.159", Value(session, "(vlax-curve-getArea circle)"));
            Value(session, "(vla-put-Closed poly :vlax-true)");
            Assert.Equal("T", Value(session, "(vlax-curve-isClosed poly)"));
            Assert.Equal("3.0", Value(session, "(vlax-curve-getEndParam poly)"));
        }

        #endregion
    }
}
