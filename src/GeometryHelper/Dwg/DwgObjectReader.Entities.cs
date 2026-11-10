using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dwg
{
    /// <content>The entities: each reads its data in the order of the file and adds its groups in the order of DXF.</content>
    internal sealed partial class DwgObjectReader
    {
        /// <summary>The pseudo group code of the text of an MTEXT, which DXF writes in pieces of 250 under 3 and the rest under 1.</summary>
        public const int LongText = -1;

        private double[] Point2Default(double[] basis)
        {
            double x = _data.BitDoubleDefault(basis[0]);
            return new[] { x, _data.BitDoubleDefault(basis[1]) };
        }

        private double[] Point3Default(double[] basis)
        {
            double x = _data.BitDoubleDefault(basis[0]);
            double y = _data.BitDoubleDefault(basis[1]);
            return new[] { x, y, _data.BitDoubleDefault(basis[2]) };
        }

        private void ReadLine()
        {
            bool flat = _data.Bit();
            double x1 = _data.RawDouble();
            double x2 = _data.BitDoubleDefault(x1);
            double y1 = _data.RawDouble();
            double y2 = _data.BitDoubleDefault(y1);
            double z1 = 0.0;
            double z2 = 0.0;

            if (!flat)
            {
                z1 = _data.RawDouble();
                z2 = _data.BitDoubleDefault(z1);
            }

            double thickness = _data.BitThickness();
            double[] extrusion = _data.BitExtrusion();
            _object.Add(100, "AcDbLine");
            AddNonZero(39, thickness);
            _object.AddPoint(10, new[] { x1, y1, z1 });
            _object.AddPoint(11, new[] { x2, y2, z2 });
            AddExtrusion(extrusion);
        }

        private void ReadCircle(bool arc)
        {
            double[] centre = _data.Point3();
            double radius = _data.BitDouble();
            double thickness = _data.BitThickness();
            double[] extrusion = _data.BitExtrusion();
            _object.Add(100, "AcDbCircle");
            AddNonZero(39, thickness);
            _object.AddPoint(10, centre);
            _object.Add(40, radius);
            AddExtrusion(extrusion);

            if (arc)
            {
                double start = _data.BitDouble();
                double end = _data.BitDouble();
                _object.Add(100, "AcDbArc");
                _object.Add(50, Degrees(start));
                _object.Add(51, Degrees(end));
            }
        }

        private void ReadPoint()
        {
            double[] point = _data.Point3();
            double thickness = _data.BitThickness();
            double[] extrusion = _data.BitExtrusion();
            double angle = _data.BitDouble();
            _object.Add(100, "AcDbPoint");
            _object.AddPoint(10, point);
            AddNonZero(39, thickness);
            AddExtrusion(extrusion);
            AddNonZero(50, Degrees(angle));
        }

        private void ReadEllipse()
        {
            double[] centre = _data.Point3();
            double[] major = _data.Point3();
            double[] normal = _data.Point3();
            _object.Add(100, "AcDbEllipse");
            _object.AddPoint(10, centre);
            _object.AddPoint(11, major);
            _object.AddPoint(210, normal);
            _object.Add(40, _data.BitDouble());
            _object.Add(41, _data.BitDouble());
            _object.Add(42, _data.BitDouble());
        }

        private void ReadRay(string subclass)
        {
            _object.Add(100, subclass);
            _object.AddPoint(10, _data.Point3());
            _object.AddPoint(11, _data.Point3());
        }

        private void ReadSpline()
        {
            int scenario = _data.BitLong();
            DxfKnotParameterizationEnum parameterization = DxfKnotParameterizationEnum.Chord;

            if (R2013Plus)
            {
                // From R2013 the flags and the knot parameterisation decide whether the spline is kept by its fit points.
                int splineFlags = _data.BitLong();
                int knotKind = _data.BitLong();
                scenario = knotKind == 15 || (splineFlags & 8) == 0 ? 1 : 2;

                if (knotKind == 1 || knotKind == 2)
                {
                    parameterization = (DxfKnotParameterizationEnum)knotKind;
                }
            }

            int degree = _data.BitLong();
            int flags = 0;
            int knots = 0;
            int controls = 0;
            int fits = 0;
            bool weighted = false;
            double knotTolerance = 1E-9;
            double controlTolerance = 1E-10;
            double fitTolerance = 1E-10;
            double[] startTangent = null;
            double[] endTangent = null;

            if (scenario == 2)
            {
                fitTolerance = _data.BitDouble();
                startTangent = _data.Point3();
                endTangent = _data.Point3();
                fits = Count(_data.BitLong());
            }
            else if (scenario == 1)
            {
                flags |= _data.Bit() ? 4 : 0;
                flags |= _data.Bit() ? 1 : 0;
                flags |= _data.Bit() ? 2 : 0;
                knotTolerance = _data.BitDouble();
                controlTolerance = _data.BitDouble();
                knots = Count(_data.BitLong());
                controls = Count(_data.BitLong());
                weighted = _data.Bit();
            }
            else
            {
                throw new FormatException("The spline is of the unknown kind " + scenario + ".");
            }

            var knotValues = new List<double>(knots);
            var controlPoints = new List<GeoPoint3>(controls);
            var weights = new List<double>();
            var fitPoints = new List<GeoPoint3>(fits);

            for (int i = 0; i < knots; i++)
            {
                knotValues.Add(_data.BitDouble());
            }

            for (int i = 0; i < controls; i++)
            {
                double[] point = _data.Point3();
                controlPoints.Add(new GeoPoint3(point[0], point[1], point[2]));

                if (weighted)
                {
                    weights.Add(_data.BitDouble());
                }
            }

            for (int i = 0; i < fits; i++)
            {
                double[] point = _data.Point3();
                fitPoints.Add(new GeoPoint3(point[0], point[1], point[2]));
            }

            if (R2013Plus && (flags & 1) != 0)
            {
                // From R2013 AutoCAD keeps a periodic spline clamped and marked closed alone, and writes a closed one to
                // DXF as periodic too.
                flags |= 2;
            }

            bool startGiven = false;
            bool endGiven = false;

            if (scenario == 2)
            {
                // A spline through fit points is kept in a DWG as those points alone. DXF carries the curve as well, so
                // it is fitted here the way AutoCAD fits it: a cubic over the lengths of the chords.
                var start = new GeoVector3(startTangent[0], startTangent[1], startTangent[2]);
                var end = new GeoVector3(endTangent[0], endTangent[1], endTangent[2]);
                startGiven = start.IsValid && start.Length > 0.0;
                endGiven = end.IsValid && end.Length > 0.0;
                flags |= 1024 | 32;

                if (fitPoints.Count >= 2 && Finite(fitPoints) && DxfNurbsEdit.InterpolateCubic(fitPoints, startGiven ? start : default(GeoVector3),
                    endGiven ? end : default(GeoVector3), parameterization, out List<GeoPoint3> kept, out List<double> fitted, out List<GeoPoint3> points))
                {
                    fitPoints = kept;
                    knotValues = fitted;
                    controlPoints = points;
                    degree = 3;
                }
            }

            _object.Add(100, "AcDbSpline");
            List<GeoPoint3> shape = controlPoints.Count > 0 ? controlPoints : fitPoints;

            if (Finite(shape) && DxfCurveTools.TryPlane(shape, default(GeoVector3), out GeoVector3 normal))
            {
                flags |= 8;
                _object.AddPoint(210, new[] { normal.X, normal.Y, normal.Z });
            }
            else if (Finite(shape) && FarApart(shape))
            {
                // Points so far apart that the distance between them is past the largest double give no plane to tell,
                // and no drawing has them: broken bytes, one broken object.
                throw new FormatException("The spline has points too far apart for their plane to be worked out.");
            }

            _object.Add(70, flags);
            _object.Add(71, degree);
            _object.Add(72, knotValues.Count);
            _object.Add(73, controlPoints.Count);
            _object.Add(74, fitPoints.Count);
            _object.Add(42, knotTolerance);
            _object.Add(43, controlTolerance);

            if (scenario == 2)
            {
                _object.Add(44, fitTolerance);

                if (startGiven)
                {
                    _object.AddPoint(12, startTangent);
                }

                if (endGiven)
                {
                    _object.AddPoint(13, endTangent);
                }
            }

            foreach (double knot in knotValues)
            {
                _object.Add(40, knot);
            }

            for (int i = 0; i < controlPoints.Count; i++)
            {
                _object.AddPoint(10, new[] { controlPoints[i].X, controlPoints[i].Y, controlPoints[i].Z });

                if (i < weights.Count)
                {
                    _object.Add(41, weights[i]);
                }
            }

            foreach (GeoPoint3 fit in fitPoints)
            {
                _object.AddPoint(11, new[] { fit.X, fit.Y, fit.Z });
            }
        }

        private static bool Finite(List<GeoPoint3> points)
        {
            foreach (GeoPoint3 point in points)
            {
                if (!point.IsValid)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Whether finite points lie so far apart that the distance from the first to one of them is past the largest double.</summary>
        private static bool FarApart(List<GeoPoint3> points)
        {
            foreach (GeoPoint3 point in points)
            {
                if (double.IsInfinity(points[0].GetVectorTo(point).Length))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A TEXT (<paramref name="kind"/> 0), an ATTRIB (1) or an ATTDEF (2); false for an attribute of several lines, which is not read yet.</summary>
        private bool ReadText(int kind)
        {
            int flags = _data.RawChar();
            double elevation = (flags & 1) == 0 ? _data.RawDouble() : 0.0;
            double[] insertion = _data.RawPoint2();
            double[] alignment = (flags & 2) == 0 ? Point2Default(insertion) : new[] { 0.0, 0.0 };
            double[] extrusion = _data.BitExtrusion();
            double thickness = _data.BitThickness();
            double oblique = (flags & 4) == 0 ? _data.RawDouble() : 0.0;
            double rotation = (flags & 8) == 0 ? _data.RawDouble() : 0.0;
            double height = _data.RawDouble();
            double widthFactor = (flags & 0x10) == 0 ? _data.RawDouble() : 1.0;
            byte[] text = T();
            int generation = (flags & 0x20) == 0 ? _data.BitShort() : 0;
            int horizontal = (flags & 0x40) == 0 ? _data.BitShort() : 0;
            int vertical = (flags & 0x80) == 0 ? _data.BitShort() : 0;
            byte[] tag = null;
            byte[] prompt = null;
            int fieldLength = 0;
            int attributeFlags = 0;
            int version = 0;
            int locked = 0;

            ulong style = 0;
            List<DwgGroup> embedded = null;
            int attributeType = 0;
            int reallyLocked = 0;

            if (kind != 0)
            {
                version = R2010Plus ? _data.RawChar() : 0;
                attributeType = R2018Plus ? _data.RawChar() : 0;

                if (attributeType == 2 || attributeType == 4)
                {
                    // From R2018 an attribute of several lines holds an MTEXT of its own, its common entity data and all;
                    // the handles of the text come first.
                    style = H();
                    embedded = ReadEmbeddedMText(out reallyLocked);
                }

                tag = T();
                fieldLength = _data.BitShort();
                attributeFlags = _data.RawChar();
                locked = R2007Plus && _data.Bit() ? 1 : 0;
            }

            if (kind == 2)
            {
                if (R2010Plus)
                {
                    _data.RawChar();
                }

                prompt = T();
            }

            if (embedded == null)
            {
                style = H();
            }

            _object.Add(100, "AcDbText");
            AddNonZero(39, thickness);
            _object.AddPoint(10, new[] { insertion[0], insertion[1], elevation });
            _object.Add(40, height);
            _object.Add(1, text);
            AddNonZero(50, Degrees(rotation));

            if (widthFactor != 1.0)
            {
                _object.Add(41, widthFactor);
            }

            AddNonZero(51, Degrees(oblique));
            _object.Add(7, new DwgName(style, "Standard", "Standard"));
            AddNonZero(71, generation);
            AddNonZero(72, horizontal);

            if (horizontal != 0 || vertical != 0)
            {
                _object.AddPoint(11, new[] { alignment[0], alignment[1], elevation });
            }

            AddExtrusion(extrusion);

            if (kind == 0)
            {
                _object.Add(100, "AcDbText");
                AddNonZero(73, vertical);
                return true;
            }

            _object.Add(100, kind == 2 ? "AcDbAttributeDefinition" : "AcDbAttribute");

            if (R2010Plus)
            {
                _object.Add(280, version);
            }

            if (kind == 2)
            {
                _object.Add(3, prompt);
            }

            _object.Add(2, tag);
            _object.Add(70, attributeFlags);
            AddNonZero(73, fieldLength);
            AddNonZero(74, vertical);

            if (R2007Plus)
            {
                _object.Add(280, locked);
            }

            if (embedded != null)
            {
                _object.Add(71, attributeType);
                _object.Add(72, reallyLocked);
                _object.AddPoint(11, new[] { alignment[0], alignment[1], elevation });
                _object.Add(101, "Embedded Object");
                _object.Body.AddRange(embedded);
            }

            return true;
        }

        /// <summary>
        /// The MTEXT of an attribute of several lines (R2018): its common entity data, read past as the file has it, and its
        /// own data, whose groups DXF writes after <c>101 Embedded Object</c>; then the annotative data, whose last value
        /// DXF writes as 72 of the attribute.
        /// </summary>
        private List<DwgGroup> ReadEmbeddedMText(out int reallyLocked)
        {
            if (_data.Bits2() == 0)
            {
                H();
            }

            int reactors = Count(_data.BitLong());
            bool noDictionary = ReadDictionaryFlags();

            for (int i = 0; i < reactors; i++)
            {
                H();
            }

            if (!noDictionary)
            {
                H();
            }

            if (ReadEntityColour().HasBook)
            {
                H();
            }

            _data.BitDouble();
            H();

            if (_data.Bits2() == 3)
            {
                H();
            }

            if (_data.Bits2() == 3)
            {
                H();
            }

            _data.RawChar();

            if (_data.Bits2() == 3)
            {
                H();
            }

            for (int i = 0; i < 3; i++)
            {
                if (_data.Bit())
                {
                    H();
                }
            }

            _data.BitShort();
            _data.RawChar();

            // The groups of the MTEXT as its own record has them, but its subclass.
            List<DwgGroup> body = _object.Body;
            _object.Body = new List<DwgGroup>();
            ReadMText();
            List<DwgGroup> groups = _object.Body;
            _object.Body = body;
            groups.RemoveAll(group => group.Code == 100);
            reallyLocked = 0;
            int annotative = _data.BitShort();

            if (annotative > 0)
            {
                if (annotative * 8L > _data.Remaining)
                {
                    throw new FormatException("The annotative data of the attribute run past its end.");
                }

                _data.Bytes(annotative);
                H();
                reallyLocked = _data.BitShort();
            }

            return groups;
        }

        private void ReadMText()
        {
            double[] insertion = _data.Point3();
            double[] normal = _data.Point3();
            double[] direction = _data.Point3();
            double width = _data.BitDouble();
            double rectangleHeight = R2007Plus ? _data.BitDouble() : 0.0;
            double height = _data.BitDouble();
            int attachment = _data.BitShort();
            int flow = _data.BitShort();
            _object.TextExtents = new[] { _data.BitDouble(), _data.BitDouble() };
            byte[] text = T();
            int spacingStyle = _data.BitShort();
            double spacing = _data.BitDouble();
            ulong style = H();
            int fill = 0;
            double fillScale = 0.0;
            DwgColour fillColour = null;
            int fillTransparency = 0;

            if (R2004Plus)
            {
                _data.Bit();
                fill = _data.BitLong();

                if ((fill & 1) != 0 || (R2018Plus && (fill & 0x10) != 0))
                {
                    fillScale = _data.BitDouble();
                    fillColour = ReadColour();
                    fillTransparency = _data.BitLong();
                }
            }

            if (R2018Plus)
            {
                ReadMTextColumns();
            }

            _object.Add(100, "AcDbMText");
            _object.AddPoint(10, insertion);
            _object.Add(40, height);
            _object.Add(41, width);

            if (R2007Plus)
            {
                _object.Add(46, rectangleHeight);
            }

            _object.Add(71, attachment);
            _object.Add(72, flow);
            _object.Add(LongText, text);
            _object.Add(7, new DwgName(style, "Standard", "Standard"));

            if (direction[0] != 1.0 || direction[1] != 0.0 || direction[2] != 0.0)
            {
                _object.AddPoint(11, direction);
            }

            AddExtrusion(normal);
            _object.Add(73, spacingStyle);
            _object.Add(44, spacing);

            if (fill != 0)
            {
                _object.Add(90, fill);

                if (fillColour != null)
                {
                    AddColour(_object.Body, fillColour, false, 63, 421);
                    _object.Add(45, fillScale);
                    _object.Add(441, fillTransparency);
                }
            }
        }

        /// <summary>
        /// The columns of an MTEXT of R2018, read past: a bit, where it is set a version, a flag, the handle of an
        /// application, the attachment, two points, four numbers and the kind of columns, with their sizes and heights.
        /// </summary>
        private void ReadMTextColumns()
        {
            if (!_data.Bit())
            {
                return;
            }

            _data.BitShort();
            _data.Bit();
            H();
            _data.BitLong();
            _data.Point3();
            _data.Point3();

            for (int i = 0; i < 4; i++)
            {
                _data.BitDouble();
            }

            int kind = _data.BitShort();

            if (kind == 0)
            {
                return;
            }

            int count = Count(_data.BitLong());
            _data.BitDouble();
            _data.BitDouble();
            bool autoHeight = _data.Bit();
            _data.Bit();

            if (!autoHeight && kind == 2)
            {
                for (int i = 0; i < count; i++)
                {
                    _data.BitDouble();
                }
            }
        }

        private void ReadInsert(bool array)
        {
            double[] insertion = _data.Point3();
            double[] scale = { 1.0, 1.0, 1.0 };

            switch (_data.Bits2())
            {
                case 0:
                    scale[0] = _data.RawDouble();
                    scale[1] = _data.BitDoubleDefault(scale[0]);
                    scale[2] = _data.BitDoubleDefault(scale[0]);
                    break;
                case 1:
                    scale[1] = _data.BitDoubleDefault(1.0);
                    scale[2] = _data.BitDoubleDefault(1.0);
                    break;
                case 2:
                    scale[0] = _data.RawDouble();
                    scale[1] = scale[0];
                    scale[2] = scale[0];
                    break;
            }

            double rotation = _data.BitDouble();
            double[] extrusion = _data.Point3();
            bool attributes = _data.Bit();
            int owned = R2004Plus && attributes ? Count(_data.BitLong()) : 0;
            int columns = 1;
            int rows = 1;
            double columnSpacing = 0.0;
            double rowSpacing = 0.0;

            if (array)
            {
                columns = _data.BitShort();
                rows = _data.BitShort();
                columnSpacing = _data.BitDouble();
                rowSpacing = _data.BitDouble();
            }

            ulong block = H();

            if (attributes)
            {
                ReadOwnedHandles(owned);
                _object.SeqEnd = H();
            }

            _object.Add(100, array ? "AcDbMInsertBlock" : "AcDbBlockReference");

            if (attributes)
            {
                _object.Add(66, 1);
            }

            _object.Add(2, new DwgName(block));
            _object.AddPoint(10, insertion);

            for (int i = 0; i < 3; i++)
            {
                if (scale[i] != 1.0)
                {
                    _object.Add(41 + i, scale[i]);
                }
            }

            AddNonZero(50, Degrees(rotation));

            if (array)
            {
                _object.Add(70, columns);
                _object.Add(71, rows);
                _object.Add(44, columnSpacing);
                _object.Add(45, rowSpacing);
            }

            AddExtrusion(extrusion);
        }

        private void ReadVertex2D()
        {
            int flags = _data.RawChar();
            double[] point = _data.Point3();
            double startWidth = _data.BitDouble();
            double endWidth;

            if (startWidth < 0.0)
            {
                startWidth = -startWidth;
                endWidth = startWidth;
            }
            else
            {
                endWidth = _data.BitDouble();
            }

            double bulge = _data.BitDouble();
            double tangent = _data.BitDouble();
            _object.Add(100, "AcDbVertex");
            _object.Add(100, "AcDb2dVertex");
            _object.AddPoint(10, point);

            // Written only where they are not the widths of the polyline, which the vertex does not know here.
            _object.Add(40, startWidth);
            _object.Add(41, endWidth);

            AddNonZero(42, bulge);
            AddNonZero(70, flags);
            AddNonZero(50, Degrees(tangent));
        }

        private void ReadVertex3D(string subclass)
        {
            int flags = _data.RawChar();
            double[] point = _data.Point3();
            _object.Add(100, "AcDbVertex");
            _object.Add(100, subclass);
            _object.AddPoint(10, point);
            AddNonZero(70, flags);
        }

        private void ReadFaceRecord()
        {
            _object.Add(100, "AcDbFaceRecord");
            _object.AddPoint(10, new[] { 0.0, 0.0, 0.0 });
            _object.Add(70, 128);

            for (int i = 0; i < 4; i++)
            {
                AddNonZero(71 + i, _data.BitShort());
            }
        }

        /// <summary>The entities a polyline owns: before R2004 its first and last vertex, from R2004 a count in the data and that many handles; then its SEQEND.</summary>
        private void ReadOwned()
        {
            ReadOwnedHandles(R2004Plus ? Count(_data.BitLong()) : 0);
            _object.SeqEnd = H();
        }

        /// <summary>Before R2004 the first and the last owned entity, which lead one to the next; from R2004 the <paramref name="count"/> owned entities.</summary>
        private void ReadOwnedHandles(int count)
        {
            if (!R2004Plus)
            {
                _object.First = H();
                _object.Last = H();
                return;
            }

            _object.Owned = new List<ulong>(Math.Min(count, 1024));

            for (int i = 0; i < count; i++)
            {
                _object.Owned.Add(H());
            }

            if (count > 0)
            {
                _object.First = _object.Owned[0];
                _object.Last = _object.Owned[count - 1];
            }
        }

        private void ReadPolyline2D()
        {
            int flags = _data.BitShort();
            int curve = _data.BitShort();
            double startWidth = _data.BitDouble();
            double endWidth = _data.BitDouble();
            double thickness = _data.BitThickness();
            double elevation = _data.BitDouble();
            double[] extrusion = _data.BitExtrusion();
            ReadOwned();
            _object.Add(100, "AcDb2dPolyline");
            _object.Add(66, 1);
            _object.AddPoint(10, new[] { 0.0, 0.0, elevation });
            AddNonZero(39, thickness);
            AddNonZero(70, flags);

            if (startWidth != 0.0 || endWidth != 0.0)
            {
                _object.Add(40, startWidth);
                _object.Add(41, endWidth);
            }

            AddNonZero(75, curve);
            AddExtrusion(extrusion);
        }

        private void ReadPolyline3D()
        {
            int spline = _data.RawChar();
            int closed = _data.RawChar();
            ReadOwned();
            _object.Add(100, "AcDb3dPolyline");
            _object.Add(66, 1);
            _object.AddPoint(10, new[] { 0.0, 0.0, 0.0 });
            _object.Add(70, 8 | (closed & 1) | ((spline & 3) != 0 ? 4 : 0));
            AddNonZero(75, (spline & 1) != 0 ? 5 : (spline & 2) != 0 ? 6 : 0);
        }

        private void ReadPolygonMesh()
        {
            int flags = _data.BitShort();
            int curve = _data.BitShort();
            int m = _data.BitShort();
            int n = _data.BitShort();
            int mDensity = _data.BitShort();
            int nDensity = _data.BitShort();
            ReadOwned();
            _object.Add(100, "AcDbPolygonMesh");
            _object.Add(66, 1);
            _object.AddPoint(10, new[] { 0.0, 0.0, 0.0 });
            _object.Add(70, flags | 16);
            _object.Add(71, m);
            _object.Add(72, n);
            AddNonZero(73, mDensity);
            AddNonZero(74, nDensity);
            AddNonZero(75, curve);
        }

        private void ReadPolyfaceMesh()
        {
            int vertices = _data.BitShort();
            int faces = _data.BitShort();
            ReadOwned();
            _object.Add(100, "AcDbPolyFaceMesh");
            _object.Add(66, 1);
            _object.AddPoint(10, new[] { 0.0, 0.0, 0.0 });
            _object.Add(70, 64);
            _object.Add(71, vertices);
            _object.Add(72, faces);
        }

        private void ReadLwPolyline()
        {
            int flags = _data.BitShort();
            double width = (flags & 4) != 0 ? _data.BitDouble() : 0.0;
            double elevation = (flags & 8) != 0 ? _data.BitDouble() : 0.0;
            double thickness = (flags & 2) != 0 ? _data.BitDouble() : 0.0;
            double[] normal = (flags & 1) != 0 ? _data.Point3() : new[] { 0.0, 0.0, 1.0 };
            int count = Count(_data.BitLong());
            int bulgeCount = (flags & 0x10) != 0 ? Count(_data.BitLong()) : 0;
            int idCount = (flags & 0x400) != 0 ? Count(_data.BitLong()) : 0;
            int widthCount = (flags & 0x20) != 0 ? Count(_data.BitLong()) : 0;
            var points = new double[count][];

            for (int i = 0; i < count; i++)
            {
                points[i] = i == 0 ? _data.RawPoint2() : Point2Default(points[i - 1]);
            }

            var bulges = new double[bulgeCount];

            for (int i = 0; i < bulgeCount; i++)
            {
                bulges[i] = _data.BitDouble();
            }

            for (int i = 0; i < idCount; i++)
            {
                _data.BitLong();
            }

            var widths = new double[widthCount][];

            for (int i = 0; i < widthCount; i++)
            {
                double start = _data.BitDouble();
                widths[i] = new[] { start, _data.BitDouble() };
            }

            _object.Add(100, "AcDbPolyline");
            _object.Add(90, count);
            _object.Add(70, ((flags & 0x200) != 0 ? 1 : 0) | ((flags & 0x100) != 0 ? 128 : 0));

            if (widthCount == 0)
            {
                _object.Add(43, width);
            }

            AddNonZero(38, elevation);
            AddNonZero(39, thickness);

            for (int i = 0; i < count; i++)
            {
                _object.AddPoint(10, points[i]);

                if (i < widthCount)
                {
                    _object.Add(40, widths[i][0]);
                    _object.Add(41, widths[i][1]);
                }

                if (i < bulgeCount)
                {
                    AddNonZero(42, bulges[i]);
                }
            }

            AddExtrusion(normal);
        }

        private void ReadSolid()
        {
            double thickness = _data.BitThickness();
            double elevation = _data.BitDouble();
            var corners = new double[4][];

            for (int i = 0; i < 4; i++)
            {
                double[] corner = _data.RawPoint2();
                corners[i] = new[] { corner[0], corner[1], elevation };
            }

            double[] extrusion = _data.BitExtrusion();
            _object.Add(100, "AcDbTrace");

            for (int i = 0; i < 4; i++)
            {
                _object.AddPoint(10 + i, corners[i]);
            }

            AddNonZero(39, thickness);
            AddExtrusion(extrusion);
        }

        private void ReadFace()
        {
            bool noFlags = _data.Bit();
            bool flat = _data.Bit();
            var corners = new double[4][];
            double x = _data.RawDouble();
            double y = _data.RawDouble();
            corners[0] = new[] { x, y, flat ? 0.0 : _data.RawDouble() };

            for (int i = 1; i < 4; i++)
            {
                corners[i] = Point3Default(corners[i - 1]);
            }

            int flags = noFlags ? 0 : _data.BitShort();
            _object.Add(100, "AcDbFace");

            for (int i = 0; i < 4; i++)
            {
                _object.AddPoint(10 + i, corners[i]);
            }

            AddNonZero(70, flags);
        }

        /// <summary>
        /// A VIEWPORT of a layout. Its number (group 69) and status (68) are not in the file: the transcoder gives them from
        /// the order of the viewports of its layout, so the groups carry a <see cref="DwgViewportNumber"/> here.
        /// </summary>
        private void ReadViewport()
        {
            double[] centre = _data.Point3();
            double width = _data.BitDouble();
            double height = _data.BitDouble();
            double[] target = _data.Point3();
            double[] direction = _data.Point3();
            double twist = _data.BitDouble();
            double viewHeight = _data.BitDouble();
            double lens = _data.BitDouble();
            double front = _data.BitDouble();
            double back = _data.BitDouble();
            double snapAngle = _data.BitDouble();
            double[] viewCentre = _data.RawPoint2();
            double[] snapBase = _data.RawPoint2();
            double[] snapSpacing = _data.RawPoint2();
            double[] gridSpacing = _data.RawPoint2();
            int circleZoom = _data.BitShort();
            int gridMajor = R2007Plus ? _data.BitShort() : 0;
            int frozen = Count(_data.BitLong());
            int status = _data.BitLong();
            byte[] styleSheet = T();
            int renderMode = _data.RawChar();
            bool ucsAtOrigin = _data.Bit();
            bool ucsPerViewport = _data.Bit();
            double[] ucsOrigin = _data.Point3();
            double[] ucsX = _data.Point3();
            double[] ucsY = _data.Point3();
            double elevation = _data.BitDouble();
            int orthographic = _data.BitShort();
            int shadePlot = R2004Plus ? _data.BitShort() : 0;
            bool defaultLights = false;
            int lightingType = 0;
            double brightness = 0.0;
            double contrast = 0.0;
            DwgColour ambient = null;

            if (R2007Plus)
            {
                defaultLights = _data.Bit();
                lightingType = _data.RawChar();
                brightness = _data.BitDouble();
                contrast = _data.BitDouble();
                ambient = ReadColour();
            }

            var layers = new List<ulong>(Math.Min(frozen, 1024));

            for (int i = 0; i < frozen; i++)
            {
                layers.Add(H());
            }

            ulong boundary = H();

            if (!R2004Plus)
            {
                H();
            }

            ulong namedUcs = H();
            ulong baseUcs = H();
            ulong background = 0;
            ulong visualStyle = 0;
            ulong shadePlotId = 0;
            ulong sun = 0;

            if (R2007Plus)
            {
                background = H();
                visualStyle = H();
                shadePlotId = H();
                sun = H();
            }

            _object.Add(100, "AcDbViewport");
            _object.AddPoint(10, centre);
            _object.Add(40, width);
            _object.Add(41, height);
            _object.Add(68, new DwgViewportNumber(_object.Handle, true));
            _object.Add(69, new DwgViewportNumber(_object.Handle, false));
            _object.AddPoint(12, viewCentre);
            _object.AddPoint(13, snapBase);
            _object.AddPoint(14, snapSpacing);
            _object.AddPoint(15, gridSpacing);
            _object.AddPoint(16, direction);
            _object.AddPoint(17, target);
            _object.Add(42, lens);
            _object.Add(43, front);
            _object.Add(44, back);
            _object.Add(45, viewHeight);
            _object.Add(50, Degrees(snapAngle));
            _object.Add(51, Degrees(twist));
            _object.Add(72, circleZoom);

            foreach (ulong layer in layers)
            {
                _object.Add(R2004Plus ? 331 : 341, layer);
            }

            _object.Add(90, status);

            if (boundary != 0)
            {
                _object.Add(340, boundary);
            }

            _object.Add(1, styleSheet);
            _object.Add(281, renderMode);
            _object.Add(71, ucsPerViewport ? 1 : 0);
            _object.Add(74, ucsAtOrigin ? 1 : 0);
            _object.AddPoint(110, ucsOrigin);
            _object.AddPoint(111, ucsX);
            _object.AddPoint(112, ucsY);

            if (namedUcs != 0)
            {
                _object.Add(345, namedUcs);
            }

            if (baseUcs != 0)
            {
                _object.Add(346, baseUcs);
            }

            _object.Add(79, orthographic);
            _object.Add(146, elevation);

            if (R2004Plus)
            {
                _object.Add(170, shadePlot);
            }

            if (!R2007Plus)
            {
                return;
            }

            _object.Add(61, gridMajor);

            if (background != 0)
            {
                _object.Add(332, background);
            }

            if (shadePlotId != 0)
            {
                _object.Add(333, shadePlotId);
            }

            if (visualStyle != 0)
            {
                _object.Add(348, visualStyle);
            }

            _object.Add(292, defaultLights ? 1 : 0);
            _object.Add(282, lightingType);
            _object.Add(141, brightness);
            _object.Add(142, contrast);
            AddColour(_object.Body, ambient, false, 63, 421);

            if (sun != 0)
            {
                _object.Add(361, sun);
            }
        }

        /// <summary>
        /// A HATCH: from R2004 its gradient, then its plane, its pattern name, its boundary paths (a polyline with or
        /// without bulges, or edges: lines, arcs, elliptic arcs and splines, with fit points from R2010), the objects each
        /// path follows, its style and pattern lines, and its seed points. DXF writes the gradient at the end, and from
        /// R2004 only for a solid fill.
        /// </summary>
        private void ReadHatch()
        {
            var gradient = new List<DwgGroup>();

            if (R2004Plus)
            {
                gradient.Add(new DwgGroup(450, _data.BitLong()));
                gradient.Add(new DwgGroup(451, _data.BitLong()));
                gradient.Add(new DwgGroup(460, _data.BitDouble()));
                gradient.Add(new DwgGroup(461, _data.BitDouble()));
                gradient.Add(new DwgGroup(452, _data.BitLong()));
                gradient.Add(new DwgGroup(462, _data.BitDouble()));
                int colours = Count(_data.BitLong());
                gradient.Add(new DwgGroup(453, colours));

                for (int i = 0; i < colours; i++)
                {
                    gradient.Add(new DwgGroup(463, _data.BitDouble()));
                    AddColour(gradient, ReadColour(), false, 63, 421);
                }

                gradient.Add(new DwgGroup(470, T()));
            }

            double elevation = _data.BitDouble();
            double[] normal = _data.Point3();
            byte[] name = T();
            bool solid = _data.Bit();
            bool associative = _data.Bit();
            int paths = Count(_data.BitLong());
            bool derived = false;
            _object.Add(100, "AcDbHatch");
            _object.AddPoint(10, new[] { 0.0, 0.0, elevation });
            _object.AddPoint(210, normal);
            _object.Add(2, name);
            _object.Add(70, solid ? 1 : 0);
            _object.Add(71, associative ? 1 : 0);
            _object.Add(91, paths);

            for (int i = 0; i < paths; i++)
            {
                int flags = _data.BitLong();
                derived |= (flags & 4) != 0;
                _object.Add(92, flags);

                if ((flags & 2) != 0)
                {
                    bool bulges = _data.Bit();
                    bool closed = _data.Bit();
                    int count = Count(_data.BitLong());
                    _object.Add(72, bulges ? 1 : 0);
                    _object.Add(73, closed ? 1 : 0);
                    _object.Add(93, count);

                    for (int j = 0; j < count; j++)
                    {
                        _object.AddPoint(10, _data.RawPoint2());

                        if (bulges)
                        {
                            _object.Add(42, _data.BitDouble());
                        }
                    }
                }
                else
                {
                    int edges = Count(_data.BitLong());
                    _object.Add(93, edges);

                    for (int j = 0; j < edges; j++)
                    {
                        ReadHatchEdge();
                    }
                }

                int sources = Count(_data.BitLong());
                _object.Add(97, sources);

                for (int j = 0; j < sources; j++)
                {
                    _object.Add(330, H());
                }
            }

            _object.Add(75, _data.BitShort());
            _object.Add(76, _data.BitShort());

            if (!solid)
            {
                _object.Add(52, Degrees(_data.BitDouble()));
                _object.Add(41, _data.BitDouble());
                _object.Add(77, _data.Bit() ? 1 : 0);
                int lines = _data.BitShort();

                if (lines < 0 || lines > _data.Remaining)
                {
                    throw new FormatException("The hatch names more pattern lines than it has room for.");
                }

                _object.Add(78, lines);

                for (int i = 0; i < lines; i++)
                {
                    _object.Add(53, Degrees(_data.BitDouble()));
                    _object.Add(43, _data.BitDouble());
                    _object.Add(44, _data.BitDouble());
                    _object.Add(45, _data.BitDouble());
                    _object.Add(46, _data.BitDouble());
                    int dashes = _data.BitShort();

                    if (dashes < 0 || dashes > _data.Remaining)
                    {
                        throw new FormatException("The hatch names more dashes than it has room for.");
                    }

                    _object.Add(79, dashes);

                    for (int j = 0; j < dashes; j++)
                    {
                        _object.Add(49, _data.BitDouble());
                    }
                }
            }

            if (derived)
            {
                _object.Add(47, _data.BitDouble());
            }

            int seeds = Count(_data.BitLong());
            _object.Add(98, seeds);

            for (int i = 0; i < seeds; i++)
            {
                _object.AddPoint(10, _data.RawPoint2());
            }

            if (solid)
            {
                _object.Body.AddRange(gradient);
            }
        }

        /// <summary>One edge of a boundary path of a HATCH: its kind (group 72) and its data.</summary>
        private void ReadHatchEdge()
        {
            int kind = _data.RawChar();
            _object.Add(72, kind);

            switch (kind)
            {
                case 1:
                    _object.AddPoint(10, _data.RawPoint2());
                    _object.AddPoint(11, _data.RawPoint2());
                    break;
                case 2:
                    _object.AddPoint(10, _data.RawPoint2());
                    _object.Add(40, _data.BitDouble());
                    _object.Add(50, Degrees(_data.BitDouble()));
                    _object.Add(51, Degrees(_data.BitDouble()));
                    _object.Add(73, _data.Bit() ? 1 : 0);
                    break;
                case 3:
                    _object.AddPoint(10, _data.RawPoint2());
                    _object.AddPoint(11, _data.RawPoint2());
                    _object.Add(40, _data.BitDouble());
                    _object.Add(50, Degrees(_data.BitDouble()));
                    _object.Add(51, Degrees(_data.BitDouble()));
                    _object.Add(73, _data.Bit() ? 1 : 0);
                    break;
                case 4:
                    _object.Add(94, _data.BitLong());
                    bool rational = _data.Bit();
                    _object.Add(73, rational ? 1 : 0);
                    _object.Add(74, _data.Bit() ? 1 : 0);
                    int knots = Count(_data.BitLong());
                    int controls = Count(_data.BitLong());
                    _object.Add(95, knots);
                    _object.Add(96, controls);

                    for (int i = 0; i < knots; i++)
                    {
                        _object.Add(40, _data.BitDouble());
                    }

                    for (int i = 0; i < controls; i++)
                    {
                        _object.AddPoint(10, _data.RawPoint2());

                        if (rational)
                        {
                            _object.Add(42, _data.BitDouble());
                        }
                    }

                    if (R2010Plus)
                    {
                        int fits = Count(_data.BitLong());
                        _object.Add(97, fits);

                        for (int i = 0; i < fits; i++)
                        {
                            _object.AddPoint(11, _data.RawPoint2());
                        }

                        if (fits > 0)
                        {
                            _object.AddPoint(12, _data.RawPoint2());
                            _object.AddPoint(13, _data.RawPoint2());
                        }
                    }

                    break;
                default:
                    throw new FormatException("The hatch has an edge of the unknown kind " + kind + ".");
            }
        }

        /// <summary>
        /// A LEADER: the kind of its annotation and path, its points, its plane and offsets, the size of the box of its
        /// text, its hook line and arrow, and the handles of its annotation and dimension style. DXF leaves the arrow out
        /// where it is on, the path where it is straight, the annotation kind and the box where there is none.
        /// </summary>
        private void ReadLeader()
        {
            _data.Bit();
            int annotation = _data.BitShort();
            int path = _data.BitShort();
            int count = Count(_data.BitLong());
            var points = new List<double[]>(Math.Min(count, 1024));

            for (int i = 0; i < count; i++)
            {
                points.Add(_data.Point3());
            }

            _data.Point3();
            double[] normal = _data.Point3();
            double[] horizontal = _data.Point3();
            double[] blockOffset = _data.Point3();
            double[] annotationOffset = _data.Point3();
            // From R2010 the box of the text is not kept; AutoCAD writes the extents its MTEXT keeps, which the transcoder
            // puts in place of these.
            double boxHeight = R2010Plus ? double.NaN : _data.BitDouble();
            double boxWidth = R2010Plus ? double.NaN : _data.BitDouble();
            _data.Bit();
            bool arrow = _data.Bit();
            _data.BitShort();
            _data.Bit();
            _data.Bit();
            ulong annotationHandle = H();
            ulong style = H();
            _object.Annotation = annotationHandle;
            _object.Add(100, "AcDbLeader");
            _object.Add(3, new DwgName(style, "Standard"));

            if (!arrow)
            {
                _object.Add(71, 0);
            }

            AddNonZero(72, path);

            if (annotation != 3)
            {
                _object.Add(73, annotation);
                _object.Add(40, boxHeight);
                _object.Add(41, boxWidth);
            }

            _object.Add(76, count);

            foreach (double[] point in points)
            {
                _object.AddPoint(10, point);
            }

            if (annotationHandle != 0)
            {
                _object.Add(340, annotationHandle);
            }

            AddExtrusion(normal);

            if (horizontal[0] != 1.0 || horizontal[1] != 0.0 || horizontal[2] != 0.0)
            {
                _object.AddPoint(211, horizontal);
            }

            if (blockOffset[0] != 0.0 || blockOffset[1] != 0.0 || blockOffset[2] != 0.0)
            {
                _object.AddPoint(212, blockOffset);
            }

            if (annotationOffset[0] != 0.0 || annotationOffset[1] != 0.0 || annotationOffset[2] != 0.0)
            {
                _object.AddPoint(213, annotationOffset);
            }
        }

        /// <summary>A TOLERANCE, a feature control frame: its point, direction, plane and text, and its dimension style.</summary>
        private void ReadTolerance()
        {
            double[] insertion = _data.Point3();
            double[] direction = _data.Point3();
            double[] normal = _data.Point3();
            byte[] text = T();
            ulong style = H();
            _object.Add(100, "AcDbFcf");
            _object.Add(3, new DwgName(style, "Standard"));
            _object.AddPoint(10, insertion);
            _object.Add(1, text);
            AddExtrusion(normal);

            if (direction[0] != 1.0 || direction[1] != 0.0 || direction[2] != 0.0)
            {
                _object.AddPoint(11, direction);
            }
        }

        /// <summary>
        /// An IMAGE or a WIPEOUT (<paramref name="subclass"/>): its corner and pixel vectors, its size, its display flags and
        /// levels, and its clip boundary, two corners or a polygon. A wipeout names no image definition and no reactor, and
        /// DXF writes both as 0.
        /// </summary>
        private void ReadRasterImage(string subclass)
        {
            int version = _data.BitLong();
            double[] insertion = _data.Point3();
            double[] u = _data.Point3();
            double[] v = _data.Point3();
            double[] size = _data.RawPoint2();
            int flags = _data.BitShort();
            bool clipped = _data.Bit();
            int brightness = _data.RawChar();
            int contrast = _data.RawChar();
            int fade = _data.RawChar();
            bool inside = R2010Plus && _data.Bit();
            int clipType = _data.BitShort();
            var vertices = new List<double[]>();

            if (clipType == 1)
            {
                vertices.Add(_data.RawPoint2());
                vertices.Add(_data.RawPoint2());
            }
            else if (clipType == 2)
            {
                int count = Count(_data.BitLong());

                for (int i = 0; i < count; i++)
                {
                    vertices.Add(_data.RawPoint2());
                }

                // The file keeps the polygon open; DXF closes it with its first vertex again.
                if (count > 0 && (vertices[0][0] != vertices[count - 1][0] || vertices[0][1] != vertices[count - 1][1]))
                {
                    vertices.Add(vertices[0]);
                }
            }

            ulong definition = H();
            ulong reactor = H();
            _object.Add(100, subclass);
            _object.Add(90, version);
            _object.AddPoint(10, insertion);
            _object.AddPoint(11, u);
            _object.AddPoint(12, v);
            _object.AddPoint(13, size);
            _object.Add(340, definition);
            _object.Add(70, flags);
            _object.Add(280, clipped ? 1 : 0);
            _object.Add(281, brightness);
            _object.Add(282, contrast);
            _object.Add(283, fade);

            if (R2010Plus)
            {
                _object.Add(290, inside ? 1 : 0);
            }

            _object.Add(360, reactor);
            _object.Add(71, clipType);
            _object.Add(91, vertices.Count);

            foreach (double[] vertex in vertices)
            {
                _object.AddPoint(14, vertex);
            }
        }

        /// <summary>
        /// An MLINE: its scale, justification, start, plane and flags, then for each vertex its point, direction and mitre
        /// and for each line of the style its segment and fill parameters; the style by handle, and DXF names it too.
        /// </summary>
        private void ReadMLine()
        {
            double scale = _data.BitDouble();
            int justification = _data.RawChar();
            double[] start = _data.Point3();
            double[] normal = _data.Point3();
            int flags = _data.BitShort();
            int lines = _data.RawChar();
            int count = _data.BitShort();

            if (count < 0 || count > _data.Remaining)
            {
                throw new FormatException("The MLINE names more vertices than it has room for.");
            }

            var vertices = new List<DwgGroup>();

            for (int i = 0; i < count; i++)
            {
                AddPoint(vertices, 11, _data.Point3());
                AddPoint(vertices, 12, _data.Point3());
                AddPoint(vertices, 13, _data.Point3());

                for (int j = 0; j < lines; j++)
                {
                    ReadMLineParameters(vertices, 74, 41);
                    ReadMLineParameters(vertices, 75, 42);
                }
            }

            ulong style = H();
            _object.Add(100, "AcDbMline");
            _object.Add(2, new DwgName(style, "STANDARD"));
            _object.Add(340, style);
            _object.Add(40, scale);
            _object.Add(70, justification);
            _object.Add(71, flags);
            _object.Add(72, count);
            _object.Add(73, lines);
            _object.AddPoint(10, start);
            _object.AddPoint(210, normal);
            _object.Body.AddRange(vertices);
        }

        /// <summary>A count of parameters (a bit short) under <paramref name="countCode"/> and the parameters under <paramref name="code"/>.</summary>
        private void ReadMLineParameters(List<DwgGroup> groups, int countCode, int code)
        {
            int count = _data.BitShort();

            if (count < 0 || count > _data.Remaining)
            {
                throw new FormatException("The MLINE names more parameters than it has room for.");
            }

            groups.Add(new DwgGroup(countCode, count));

            for (int i = 0; i < count; i++)
            {
                groups.Add(new DwgGroup(code, _data.BitDouble()));
            }
        }

        /// <summary>
        /// A REGION, a 3DSOLID or a BODY (<paramref name="solid"/> for a 3DSOLID): its ACIS data as DXF carries it. Before
        /// R2013 the data is SAT text in blocks, each character but a line end kept as 0x9F less it, and DXF writes it so,
        /// a line a group 1 (pieces past 255 under 3). From R2013 the data is in the data store of the file, which DXF
        /// writes in its own section; the entity carries its GUID, after the wireframe and silhouettes, which are read past.
        /// False where the data is SAB (version 2), which DXF before R2013 does not carry.
        /// </summary>
        private bool ReadModelerGeometry(bool solid)
        {
            _object.Add(100, "AcDbModelerGeometry");

            if (!R2013Plus)
            {
                bool empty = _data.Bit();
                var text = new List<byte>();

                if (!empty)
                {
                    _data.Bit();
                    int version = _data.BitShort();

                    if (version != 1)
                    {
                        return false;
                    }

                    for (int size = _data.BitLong(); size != 0; size = _data.BitLong())
                    {
                        if (size < 0 || size * 8L > _data.Remaining)
                        {
                            throw new FormatException("The ACIS data of the entity run past its end.");
                        }

                        text.AddRange(_data.Bytes(size));
                    }
                }

                _object.Add(70, 1);
                AddSatLines(text);
            }
            else
            {
                ReadWireframe();

                int materials = Count(_data.BitLong());

                for (int i = 0; i < materials; i++)
                {
                    _data.BitLong();
                    _data.BitLong();
                    H();
                }

                _object.Add(290, _data.Bit() ? 1 : 0);
                int a = _data.BitLong();
                short b = (short)_data.BitShort();
                short c = (short)_data.BitShort();
                byte[] rest = _data.Bytes(8);
                _data.BitLong();
                _object.Add(2, new Guid(a, b, c, rest).ToString("B"));
            }

            if (solid && R2007Plus)
            {
                _object.Add(100, "AcDb3dSolid");
                _object.Add(350, H());
            }

            return true;
        }

        /// <summary>The SAT text of an ACIS entity, a line a group 1 and its pieces past 255 bytes under 3.</summary>
        private void AddSatLines(List<byte> text)
        {
            int start = 0;

            for (int i = 0; i <= text.Count; i++)
            {
                if (i < text.Count && text[i] != (byte)'\n')
                {
                    continue;
                }

                // A line of SAT ends in a carriage return and a line feed, which DXF does not keep.
                int end = i > start && text[i - 1] == (byte)'\r' ? i - 1 : i;

                if (i > start || i < text.Count)
                {
                    for (int at = start, code = 1; at < end || code == 1; at += 255, code = 3)
                    {
                        _object.Add(code, text.GetRange(at, Math.Min(255, end - at)).ToArray());
                    }
                }

                start = i + 1;
            }
        }

        /// <summary>The wireframe and the silhouettes of an ACIS entity from R2013, read past.</summary>
        private void ReadWireframe()
        {
            if (!_data.Bit())
            {
                return;
            }

            if (_data.Bit())
            {
                _data.Point3();
            }

            _data.BitLong();

            if (_data.Bit())
            {
                ReadWires();
            }

            int silhouettes = Count(_data.BitLong());

            for (int i = 0; i < silhouettes; i++)
            {
                _data.BitLongLong();
                _data.Point3();
                _data.Point3();
                _data.Point3();
                _data.Bit();

                if (_data.Bit())
                {
                    ReadWires();
                }
            }

            _data.Bit();
        }

        private void ReadWires()
        {
            int wires = Count(_data.BitLong());

            for (int i = 0; i < wires; i++)
            {
                _data.RawChar();
                _data.BitLong();
                _data.BitShort();
                _data.BitLong();
                int points = Count(_data.BitLong());

                for (int j = 0; j < points; j++)
                {
                    _data.Point3();
                }

                if (_data.Bit())
                {
                    for (int j = 0; j < 4; j++)
                    {
                        _data.Point3();
                    }

                    _data.BitDouble();
                    _data.Bit();
                    _data.Bit();
                    _data.Bit();
                }
            }
        }

        private void ReadDimension(int type)
        {
            int version = R2010Plus ? _data.RawChar() : 0;
            double[] normal = _data.Point3();
            double[] middle = _data.RawPoint2();
            double elevation = _data.BitDouble();
            int flags = _data.RawChar();
            byte[] text = T();
            double textRotation = _data.BitDouble();
            double horizontal = _data.BitDouble();
            _data.Point3();
            _data.BitDouble();
            int attachment = _data.BitShort();
            int spacingStyle = _data.BitShort();
            double spacing = _data.BitDouble();
            double measurement = _data.BitDouble();
            var flips = new int[3];

            if (R2007Plus)
            {
                for (int i = 0; i < 3; i++)
                {
                    flips[i] = _data.Bit() ? 1 : 0;
                }
            }

            _data.RawPoint2();

            // The points of the kind, under their DXF codes 10 and 13 to 16; an angle of 50 or 52 and the leader length.
            var points = new double[17][];
            double rotation = 0.0;
            double oblique = 0.0;
            double leader = 0.0;
            int kind;
            string subclass;
            int extra = 0;

            switch (type)
            {
                case 0x14:
                    kind = 6;
                    subclass = "AcDbOrdinateDimension";
                    points[10] = _data.Point3();
                    points[13] = _data.Point3();
                    points[14] = _data.Point3();
                    extra = (_data.RawChar() & 1) != 0 ? 64 : 0;
                    break;
                case 0x15:
                case 0x16:
                    kind = type == 0x15 ? 0 : 1;
                    subclass = "AcDbAlignedDimension";
                    points[13] = _data.Point3();
                    points[14] = _data.Point3();
                    points[10] = _data.Point3();
                    oblique = _data.BitDouble();

                    if (type == 0x15)
                    {
                        rotation = _data.BitDouble();
                    }

                    break;
                case 0x17:
                    kind = 5;
                    subclass = "AcDb3PointAngularDimension";
                    points[10] = _data.Point3();
                    points[13] = _data.Point3();
                    points[14] = _data.Point3();
                    points[15] = _data.Point3();
                    break;
                case 0x18:
                    kind = 2;
                    subclass = "AcDb2LineAngularDimension";
                    double[] arc = _data.RawPoint2();
                    points[16] = new[] { arc[0], arc[1], elevation };
                    points[13] = _data.Point3();
                    points[14] = _data.Point3();
                    points[15] = _data.Point3();
                    points[10] = _data.Point3();
                    break;
                case 0x19:
                    kind = 4;
                    subclass = "AcDbRadialDimension";
                    points[10] = _data.Point3();
                    points[15] = _data.Point3();
                    leader = _data.BitDouble();
                    break;
                default:
                    kind = 3;
                    subclass = "AcDbDiametricDimension";
                    points[15] = _data.Point3();
                    points[10] = _data.Point3();
                    leader = _data.BitDouble();
                    break;
            }

            ulong style = H();
            ulong block = H();
            _object.Add(100, "AcDbDimension");

            if (R2010Plus)
            {
                _object.Add(280, version);
            }

            _object.Add(2, new DwgName(block));
            _object.AddPoint(10, points[10]);
            _object.AddPoint(11, new[] { middle[0], middle[1], elevation });
            _object.Add(70, kind | 32 | ((flags & 1) == 0 ? 128 : 0) | extra);

            if (text.Length > 0)
            {
                _object.Add(1, text);
            }

            _object.Add(71, attachment);

            if (spacingStyle != 1 || spacing != 1.0)
            {
                _object.Add(72, spacingStyle);
                _object.Add(41, spacing);
            }

            _object.Add(42, measurement);

            if (R2007Plus)
            {
                _object.Add(73, flips[0]);
                _object.Add(74, flips[1]);
                _object.Add(75, flips[2]);
            }

            AddNonZero(53, Degrees(textRotation));
            AddNonZero(51, Degrees(horizontal));
            _object.Add(3, new DwgName(style, "Standard"));
            AddExtrusion(normal);
            _object.Add(100, subclass);

            for (int code = 13; code <= 16; code++)
            {
                if (points[code] != null)
                {
                    _object.AddPoint(code, points[code]);
                }
            }

            if (type == 0x19 || type == 0x1A)
            {
                _object.Add(40, leader);
            }

            AddNonZero(50, Degrees(rotation));
            AddNonZero(52, Degrees(oblique));

            if (type == 0x15)
            {
                _object.Add(100, "AcDbRotatedDimension");
            }
        }
    }
}
