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

            if (kind != 0)
            {
                version = R2010Plus ? _data.RawChar() : 0;

                if (R2018Plus)
                {
                    // An attribute of several lines holds an MTEXT of its own, which is not read yet.
                    int attributeType = _data.RawChar();

                    if (attributeType == 2 || attributeType == 4)
                    {
                        return false;
                    }
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

            ulong style = H();
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

            return true;
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
            _data.BitDouble();
            _data.BitDouble();
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
