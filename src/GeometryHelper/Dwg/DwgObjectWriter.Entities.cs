using System;
using System.Collections.Generic;

namespace GeometryHelper.Dwg
{
    /// <content>The entities: each writes its data in the order of the file from the groups of its DXF record.</content>
    internal sealed partial class DwgObjectWriter
    {
        private static readonly double[] ZAxis = { 0.0, 0.0, 1.0 };

        private void Point2Default(double[] point, double[] basis)
        {
            _data.BitDoubleDefault(point[0], basis[0]);
            _data.BitDoubleDefault(point[1], basis[1]);
        }

        private void Point3Default(double[] point, double[] basis)
        {
            _data.BitDoubleDefault(point[0], basis[0]);
            _data.BitDoubleDefault(point[1], basis[1]);
            _data.BitDoubleDefault(point[2], basis[2]);
        }

        /// <summary>
        /// BE: the extrusion, a set bit alone for (0, 0, 1). AutoCAD writes some of those whole too, a clear bit and three
        /// bit doubles, by what it keeps of how the entity was made, which DXF does not say; both read the same.
        /// </summary>
        private void Extrusion(double[] extrusion) => _data.BitExtrusion(extrusion);

        private void WriteLine()
        {
            DwgGroups line = _record.Of("AcDbLine");
            double[] start = line.Point(10);
            double[] end = line.Point(11);
            bool flat = start[2] == 0.0 && end[2] == 0.0;
            _data.Bit(flat);
            _data.RawDouble(start[0]);
            _data.BitDoubleDefault(end[0], start[0]);
            _data.RawDouble(start[1]);
            _data.BitDoubleDefault(end[1], start[1]);

            if (!flat)
            {
                _data.RawDouble(start[2]);
                _data.BitDoubleDefault(end[2], start[2]);
            }

            _data.BitThickness(line.Double(39));
            Extrusion(line.Point(210, 0, 0, 1));
        }

        private void WriteCircle(bool arc)
        {
            DwgGroups circle = _record.Of("AcDbCircle");
            _data.Point3(circle.Point(10));
            _data.BitDouble(circle.Double(40));
            _data.BitThickness(circle.Double(39));
            Extrusion(circle.Point(210, 0, 0, 1));

            if (arc)
            {
                DwgGroups angles = _record.Of("AcDbArc");
                _data.BitDouble(Radians(angles.Double(50)));
                _data.BitDouble(Radians(angles.Double(51)));
            }
        }

        private void WritePoint()
        {
            DwgGroups point = _record.Of("AcDbPoint");
            _data.Point3(point.Point(10));
            _data.BitThickness(point.Double(39));
            Extrusion(point.Point(210, 0, 0, 1));
            _data.BitDouble(Radians(point.Double(50)));
        }

        private void WriteEllipse()
        {
            DwgGroups ellipse = _record.Of("AcDbEllipse");
            _data.Point3(ellipse.Point(10));
            _data.Point3(ellipse.Point(11, 1));
            _data.Point3(ellipse.Point(210, 0, 0, 1));
            _data.BitDouble(ellipse.Double(40, 1.0));
            _data.BitDouble(ellipse.Double(41));
            _data.BitDouble(ellipse.Double(42, 2 * Math.PI));
        }

        private void WriteRay()
        {
            DwgGroups ray = _record.Of(_record.Type == "RAY" ? "AcDbRay" : "AcDbXline");
            _data.Point3(ray.Point(10));
            _data.Point3(ray.Point(11, 1));
        }

        /// <summary>The points of <paramref name="code"/> in the order of the record, each its x, then the y and z that follow it.</summary>
        private static List<double[]> Points(DwgGroups groups, int code)
        {
            var points = new List<double[]>();

            foreach (KeyValuePair<int, string> group in groups.All)
            {
                if (group.Key == code)
                {
                    points.Add(new[] { DwgRecord.Double(group.Value), 0.0, 0.0 });
                }
                else if (points.Count > 0 && group.Key == code + 10)
                {
                    points[points.Count - 1][1] = DwgRecord.Double(group.Value);
                }
                else if (points.Count > 0 && group.Key == code + 20)
                {
                    points[points.Count - 1][2] = DwgRecord.Double(group.Value);
                }
            }

            return points;
        }

        private void WriteSpline()
        {
            DwgGroups spline = _record.Of("AcDbSpline");
            int flags = spline.Int(70);
            List<double[]> fits = Points(spline, 11);
            List<double[]> controls = Points(spline, 10);
            List<string> knots = spline.Each(40);
            List<string> weights = spline.Each(41);
            int scenario = fits.Count > 0 ? 2 : 1;

            if (R2013Plus)
            {
                // From R2013 AutoCAD writes the kind 1 and says by the flags and the knot parameterisation which it is:
                // fit points 9 and the parameterisation, control points 0 and 15.
                _data.BitLong(1);
                _data.BitLong(scenario == 2 ? 1 | 8 : 0);
                _data.BitLong(scenario == 2 ? 0 : 15);
            }
            else
            {
                _data.BitLong(scenario);
            }

            _data.BitLong(spline.Int(71, 3));

            if (scenario == 2)
            {
                _data.BitDouble(spline.Double(44, 1E-10));
                _data.Point3(spline.Point(12));
                _data.Point3(spline.Point(13));
                _data.BitLong(fits.Count);

                foreach (double[] fit in fits)
                {
                    _data.Point3(fit);
                }

                return;
            }

            bool weighted = weights.Count > 0;
            _data.Bit((flags & 4) != 0);
            _data.Bit((flags & 1) != 0);
            _data.Bit((flags & 2) != 0 && !(R2013Plus && (flags & 1) != 0));
            _data.BitDouble(spline.Double(42, 1E-9));
            _data.BitDouble(spline.Double(43, 1E-10));
            _data.BitLong(knots.Count);
            _data.BitLong(controls.Count);
            _data.Bit(weighted);

            foreach (string knot in knots)
            {
                _data.BitDouble(DwgRecord.Double(knot));
            }

            for (int i = 0; i < controls.Count; i++)
            {
                _data.Point3(controls[i]);

                if (weighted)
                {
                    _data.BitDouble(i < weights.Count ? DwgRecord.Double(weights[i]) : 1.0);
                }
            }
        }

        /// <summary>A TEXT (<paramref name="kind"/> 0), an ATTRIB (1) or an ATTDEF (2), of one line or, from R2018, of several.</summary>
        private void WriteText(int kind)
        {
            DwgGroups text = _record.Of("AcDbText");
            int embedded = kind == 0 ? -1 : _record.Body.FindIndex(g => g.Key == 101);

            // The groups of an attribute of several lines end where those of its MTEXT begin, after 101.
            DwgGroups attribute = new DwgGroups(
                embedded < 0 ? _record.Body : _record.Body.GetRange(0, embedded),
                kind == 2 ? "AcDbAttributeDefinition" : "AcDbAttribute"
            );
            DwgGroups second = kind == 0 ? new DwgGroups(_record.Body.GetRange(0, 0), null) : attribute;

            if (kind == 0)
            {
                // A TEXT names its subclass twice; its vertical alignment is in the second.
                int first = _record.Body.FindIndex(g => g.Key == 100 && g.Value.Trim() == "AcDbText");
                int again =
                    first < 0
                        ? -1
                        : _record.Body.FindIndex(first + 1, g => g.Key == 100 && g.Value.Trim() == "AcDbText");
                second =
                    again < 0
                        ? second
                        : new DwgGroups(_record.Body.GetRange(again + 1, _record.Body.Count - again - 1), null);
            }

            double[] insertion = text.Point(10);
            bool hasAlignment = text.Has(11);
            double[] alignment = text.Point(11);
            double oblique = Radians(text.Double(51));
            double rotation = Radians(text.Double(50));
            double width = text.Double(41, 1.0);
            int generation = text.Int(71);
            int horizontal = text.Int(72);
            int vertical = second.Int(kind == 0 ? 73 : 74);
            int flags =
                (insertion[2] == 0.0 ? 1 : 0)
                | (hasAlignment ? 0 : 2)
                | (oblique == 0.0 ? 4 : 0)
                | (rotation == 0.0 ? 8 : 0)
                | (width == 1.0 ? 0x10 : 0)
                | (generation == 0 ? 0x20 : 0)
                | (horizontal == 0 ? 0x40 : 0)
                | (vertical == 0 ? 0x80 : 0);
            _data.RawChar(flags);

            if ((flags & 1) == 0)
            {
                _data.RawDouble(insertion[2]);
            }

            _data.RawDouble(insertion[0]);
            _data.RawDouble(insertion[1]);

            if ((flags & 2) == 0)
            {
                Point2Default(alignment, insertion);
            }

            Extrusion(text.Point(210, 0, 0, 1));
            _data.BitThickness(text.Double(39));

            if ((flags & 4) == 0)
            {
                _data.RawDouble(oblique);
            }

            if ((flags & 8) == 0)
            {
                _data.RawDouble(rotation);
            }

            _data.RawDouble(text.Double(40));

            if ((flags & 0x10) == 0)
            {
                _data.RawDouble(width);
            }

            T(text.Text(1));

            if ((flags & 0x20) == 0)
            {
                _data.BitShort(generation);
            }

            if ((flags & 0x40) == 0)
            {
                _data.BitShort(horizontal);
            }

            if ((flags & 0x80) == 0)
            {
                _data.BitShort(vertical);
            }

            ulong style = _drawing.HandleOf("STYLE", text.Text(7, "Standard"));

            if (kind != 0)
            {
                List<string> versions = attribute.Each(280);

                if (R2010Plus)
                {
                    _data.RawChar(versions.Count > 0 ? DwgRecord.Int(versions[0]) : 0);
                }

                if (R2018Plus && embedded >= 0)
                {
                    // An attribute of several lines (2, or 4 for a definition) holds an MTEXT of its own after its text's
                    // style: the MTEXT's entity data as the attribute's, its groups those after 101, no annotative data.
                    _data.RawChar(attribute.Int(71, kind == 2 ? 4 : 2));
                    H(5, style);
                    WriteEmbeddedEntity();
                    var groups = _record.Body.GetRange(embedded + 1, _record.Body.Count - embedded - 1);
                    WriteMText(new DwgGroups(groups, null), true);
                    _data.BitShort(0);
                }
                else if (R2018Plus)
                {
                    _data.RawChar(1);
                }

                T(attribute.Text(2));
                _data.BitShort(attribute.Int(73));
                _data.RawChar(attribute.Int(70));

                if (R2007Plus)
                {
                    _data.Bit(versions.Count > 0 && DwgRecord.Int(versions[versions.Count - 1]) != 0);
                }

                if (kind == 2)
                {
                    // An ATTDEF repeats the version before its prompt.
                    if (R2010Plus)
                    {
                        _data.RawChar(versions.Count > 0 ? DwgRecord.Int(versions[0]) : 0);
                    }

                    T(attribute.Text(3));
                }

                if (R2018Plus && embedded >= 0)
                {
                    return;
                }
            }

            H(5, style);
        }

        /// <summary>
        /// The entity data of the MTEXT of an attribute of several lines, as AutoCAD 2021 writes it: no proxy graphics, owner,
        /// reactors or extension dictionary, the attribute's colour, layer and lineweight, its linetype named by its record
        /// (ByLayer too) and its material where that is written.
        /// </summary>
        private void WriteEmbeddedEntity()
        {
            DwgGroups all = _record.Of("AcDbEntity");
            _data.Bits2(0);
            _handles.Handle(4, 0);
            _data.BitLong(0);
            _data.Bit(true);
            _data.Bit(false);
            WriteEntityColour(all);
            _data.BitDouble(all.Double(48, 1.0));
            H(5, _drawing.HandleOf("LAYER", all.Text(8, "0")));
            ulong linetype = _drawing.HandleOf("LTYPE", all.Text(6, "ByLayer").Trim());
            bool named = _written.Contains(linetype);
            _data.Bits2(named ? 3 : 0);

            if (named)
            {
                H(5, linetype);
            }

            ulong material = all.Handle(347);

            if (material == 0)
            {
                DwgRecord byLayer = _drawing.Objects.Find(record =>
                    record.Type == "MATERIAL"
                    && string.Equals(record.Of("AcDbMaterial").Text(1).Trim(), "ByLayer", StringComparison.Ordinal)
                );
                material = byLayer?.Handle ?? 0;
            }

            bool hasMaterial = _written.Contains(material);
            _data.Bits2(hasMaterial ? 3 : 0);

            if (hasMaterial)
            {
                H(5, material);
            }

            _data.RawChar(0);
            _data.Bits2(0);
            _data.Bit(false);
            _data.Bit(false);
            _data.Bit(false);
            _data.BitShort(all.Int(60, 0));
            _data.RawChar(LineWeightIndex(all.Int(370, -1)));
        }

        private void WriteMText() => WriteMText(_record.Of("AcDbMText"), false);

        /// <summary>The data of an MTEXT of <paramref name="text"/>, its own or, <paramref name="embedded"/>, an attribute's.</summary>
        private void WriteMText(DwgGroups text, bool embedded)
        {
            double[] insertion = text.Point(10);
            double[] direction = text.Point(11, 1);

            if (!text.Has(11) && text.Has(50))
            {
                double angle = Radians(text.Double(50));
                direction = new[] { Math.Cos(angle), Math.Sin(angle), 0.0 };
            }

            _data.Point3(insertion);
            _data.Point3(text.Point(210, 0, 0, 1));
            _data.Point3(direction);
            _data.BitDouble(text.Double(41));

            if (R2007Plus)
            {
                _data.BitDouble(text.Double(46));
            }

            _data.BitDouble(text.Double(40));
            _data.BitShort(text.Int(71, 1));
            _data.BitShort(text.Int(72, 1));

            // The extents of the text: a DXF of AutoCAD gives them for a leader's annotation as the leader's box (40, 41).
            double extentsHeight = text.Double(43);
            double extentsWidth = text.Double(42);

            if (!embedded && !text.Has(43) && LeaderOf(_record.Handle) is DwgRecord leader)
            {
                extentsHeight = leader.Of("AcDbLeader").Double(40);
                extentsWidth = leader.Of("AcDbLeader").Double(41);
            }

            _data.BitDouble(extentsHeight);
            _data.BitDouble(extentsWidth);
            T(string.Concat(string.Concat(text.Each(3)), text.Text(1)));
            _data.BitShort(text.Int(73, 1));
            _data.BitDouble(text.Double(44, 1.0));
            H(5, _drawing.HandleOf("STYLE", text.Text(7, "Standard")));

            if (R2004Plus)
            {
                _data.Bit(false);
                int fill = text.Int(90);
                _data.BitLong(fill);

                if ((fill & 1) != 0 || (R2018Plus && (fill & 0x10) != 0))
                {
                    _data.BitDouble(text.Double(45, 1.5));
                    WriteColour(text.Int(63, 256), text.Has(421) ? text.Int(421) : -1);
                    _data.BitLong(text.Int(441));
                }
            }

            if (R2018Plus)
            {
                _data.Bit(true);
                _data.BitShort(4);
                _data.Bit(true);
                H(5, 0);
                _data.BitLong(text.Int(71, 1));
                _data.Point3(direction);
                _data.Point3(insertion);
                _data.BitDouble(text.Double(41));
                _data.BitDouble(text.Double(46));
                _data.BitDouble(extentsWidth);
                _data.BitDouble(extentsHeight);
                _data.BitShort(0);
            }
        }

        /// <summary>The LEADER whose annotation is the object of <paramref name="annotation"/>; null for none.</summary>
        private DwgRecord LeaderOf(ulong annotation)
        {
            if (_leaders == null)
            {
                _leaders = new Dictionary<ulong, DwgRecord>();

                foreach (DwgRecord record in _drawing.All)
                {
                    ulong target = record.Type == "LEADER" ? record.Of("AcDbLeader").Handle(340) : 0;

                    if (target != 0 && !_leaders.ContainsKey(target))
                    {
                        _leaders[target] = record;
                    }
                }
            }

            return _leaders.TryGetValue(annotation, out DwgRecord leader) ? leader : null;
        }

        private void WriteInsert(bool array)
        {
            DwgGroups insert = _record.Of(array ? "AcDbMInsertBlock" : "AcDbBlockReference");
            _data.Point3(insert.Point(10));
            double x = insert.Double(41, 1.0);
            double y = insert.Double(42, 1.0);
            double z = insert.Double(43, 1.0);

            if (x == 1.0 && y == 1.0 && z == 1.0)
            {
                _data.Bits2(3);
            }
            else if (x == 1.0)
            {
                _data.Bits2(1);
                _data.BitDoubleDefault(y, 1.0);
                _data.BitDoubleDefault(z, 1.0);
            }
            else if (x == y && x == z)
            {
                _data.Bits2(2);
                _data.RawDouble(x);
            }
            else
            {
                _data.Bits2(0);
                _data.RawDouble(x);
                _data.BitDoubleDefault(y, x);
                _data.BitDoubleDefault(z, x);
            }

            _data.BitDouble(Radians(insert.Double(50)));
            _data.Point3(insert.Point(210, 0, 0, 1));
            List<DwgRecord> attributes = OwnedEntities();
            bool hasAttributes = attributes.Count > 0;
            _data.Bit(hasAttributes);

            if (R2004Plus && hasAttributes)
            {
                _data.BitLong(attributes.Count);
            }

            if (array)
            {
                _data.BitShort(insert.Int(70, 1));
                _data.BitShort(insert.Int(71, 1));
                _data.BitDouble(insert.Double(44));
                _data.BitDouble(insert.Double(45));
            }

            H(5, _drawing.HandleOf("BLOCK_RECORD", insert.Text(2)));

            if (hasAttributes)
            {
                WriteOwnedHandles(attributes);
            }
        }

        /// <summary>The written vertices or attributes of the record, in order.</summary>
        private List<DwgRecord> OwnedEntities()
        {
            return _record.Owned == null
                ? new List<DwgRecord>()
                : _record.Owned.FindAll(entity => _written.Contains(entity.Handle));
        }

        /// <summary>The handles of owned entities: before R2004 the first and the last, from R2004 each; then the SEQEND.</summary>
        private void WriteOwnedHandles(List<DwgRecord> owned)
        {
            if (!R2004Plus)
            {
                H(4, owned.Count > 0 ? owned[0].Handle : 0);
                H(4, owned.Count > 0 ? owned[owned.Count - 1].Handle : 0);
            }
            else
            {
                foreach (DwgRecord entity in owned)
                {
                    H(3, entity.Handle);
                }
            }

            H(3, _record.SeqEnd?.Handle ?? 0);
        }

        private void WriteOwnedCount()
        {
            if (R2004Plus)
            {
                _data.BitLong(OwnedEntities().Count);
            }
        }

        private void WriteVertex2D()
        {
            DwgGroups vertex = _record.Of("AcDb2dVertex");
            DwgGroups polyline = (_drawing.Find(_record.Container) ?? _record).Of("AcDb2dPolyline");
            _data.RawChar(vertex.Int(70));
            _data.Point3(vertex.Point(10));
            double start = vertex.Double(40, polyline.Double(40));
            double end = vertex.Double(41, polyline.Double(41));

            if (start == end && start != 0.0)
            {
                _data.BitDouble(-start);
            }
            else
            {
                _data.BitDouble(start);
                _data.BitDouble(end);
            }

            _data.BitDouble(vertex.Double(42));
            _data.BitDouble(Radians(vertex.Double(50)));

            if (R2010Plus)
            {
                // The vertex id, 0 where the vertex has none.
                _data.BitLong(vertex.Int(91));
            }
        }

        private void WriteVertex3D()
        {
            var vertex = new DwgGroups(_record.Body, null);
            _data.RawChar(vertex.Int(70));
            _data.Point3(vertex.Point(10));
        }

        private void WriteFaceRecord()
        {
            DwgGroups face = _record.Of("AcDbFaceRecord");

            for (int i = 0; i < 4; i++)
            {
                _data.BitShort(face.Int(71 + i));
            }
        }

        private void WritePolyline2D()
        {
            DwgGroups polyline = _record.Of("AcDb2dPolyline");
            _data.BitShort(polyline.Int(70));
            _data.BitShort(polyline.Int(75));
            _data.BitDouble(polyline.Double(40));
            _data.BitDouble(polyline.Double(41));
            _data.BitThickness(polyline.Double(39));
            _data.BitDouble(polyline.Double(30));
            Extrusion(polyline.Point(210, 0, 0, 1));
            WriteOwnedCount();
            WriteOwnedHandles(OwnedEntities());
        }

        private void WritePolyline3D()
        {
            DwgGroups polyline = _record.Of("AcDb3dPolyline");
            int flags = polyline.Int(70);
            int curve = polyline.Int(75);
            _data.RawChar(
                curve == 5 ? 1
                : curve == 6 ? 2
                : 0
            );
            _data.RawChar(flags & 1);
            WriteOwnedCount();
            WriteOwnedHandles(OwnedEntities());
        }

        private void WritePolygonMesh()
        {
            DwgGroups mesh = _record.Of("AcDbPolygonMesh");
            _data.BitShort(mesh.Int(70));
            _data.BitShort(mesh.Int(75));
            _data.BitShort(mesh.Int(71));
            _data.BitShort(mesh.Int(72));
            _data.BitShort(mesh.Int(73));
            _data.BitShort(mesh.Int(74));
            WriteOwnedCount();
            WriteOwnedHandles(OwnedEntities());
        }

        private void WritePolyfaceMesh()
        {
            DwgGroups mesh = _record.Of("AcDbPolyFaceMesh");
            _data.BitShort(mesh.Int(71));
            _data.BitShort(mesh.Int(72));
            WriteOwnedCount();
            WriteOwnedHandles(OwnedEntities());
        }

        private void WriteLwPolyline()
        {
            DwgGroups polyline = _record.Of("AcDbPolyline");

            // The vertices: each starts at its 10, its widths and bulge up to the next.
            var points = new List<double[]>();
            var widths = new List<double[]>();
            var bulges = new List<double>();
            bool anyWidth = false;
            bool anyBulge = false;

            foreach (KeyValuePair<int, string> group in polyline.All)
            {
                switch (group.Key)
                {
                    case 10:
                        points.Add(new[] { DwgRecord.Double(group.Value), 0.0 });
                        widths.Add(new[] { 0.0, 0.0 });
                        bulges.Add(0.0);
                        break;
                    case 20 when points.Count > 0:
                        points[points.Count - 1][1] = DwgRecord.Double(group.Value);
                        break;
                    case 40 when points.Count > 0:
                        widths[widths.Count - 1][0] = DwgRecord.Double(group.Value);
                        anyWidth = true;
                        break;
                    case 41 when points.Count > 0:
                        widths[widths.Count - 1][1] = DwgRecord.Double(group.Value);
                        anyWidth = true;
                        break;
                    case 42 when points.Count > 0:
                        bulges[bulges.Count - 1] = DwgRecord.Double(group.Value);
                        anyBulge |= bulges[bulges.Count - 1] != 0.0;
                        break;
                }
            }

            double width = polyline.Double(43);
            double elevation = polyline.Double(38);
            double thickness = polyline.Double(39);
            double[] normal = polyline.Point(210, 0, 0, 1);
            int dxf = polyline.Int(70);
            int flags =
                (width != 0.0 ? 4 : 0)
                | (elevation != 0.0 ? 8 : 0)
                | (thickness != 0.0 ? 2 : 0)
                | (IsDefaultExtrusion(normal) ? 0 : 1)
                | (anyBulge ? 0x10 : 0)
                | (anyWidth ? 0x20 : 0)
                | ((dxf & 1) != 0 ? 0x200 : 0)
                | ((dxf & 128) != 0 ? 0x100 : 0);
            _data.BitShort(flags);

            if ((flags & 4) != 0)
            {
                _data.BitDouble(width);
            }

            if ((flags & 8) != 0)
            {
                _data.BitDouble(elevation);
            }

            if ((flags & 2) != 0)
            {
                _data.BitDouble(thickness);
            }

            if ((flags & 1) != 0)
            {
                _data.Point3(normal);
            }

            _data.BitLong(points.Count);

            if (anyBulge)
            {
                _data.BitLong(points.Count);
            }

            if (anyWidth)
            {
                _data.BitLong(points.Count);
            }

            for (int i = 0; i < points.Count; i++)
            {
                if (i == 0)
                {
                    _data.RawPoint2(points[i]);
                }
                else
                {
                    Point2Default(points[i], points[i - 1]);
                }
            }

            if (anyBulge)
            {
                foreach (double bulge in bulges)
                {
                    _data.BitDouble(bulge);
                }
            }

            if (anyWidth)
            {
                foreach (double[] pair in widths)
                {
                    _data.BitDouble(pair[0]);
                    _data.BitDouble(pair[1]);
                }
            }
        }

        private void WriteDimension(int type)
        {
            DwgGroups dimension = _record.Of("AcDbDimension");
            string subclass =
                type == 0x14 ? "AcDbOrdinateDimension"
                : type == 0x15 || type == 0x16 ? "AcDbAlignedDimension"
                : type == 0x17 ? "AcDb3PointAngularDimension"
                : type == 0x18 ? "AcDb2LineAngularDimension"
                : type == 0x19 ? "AcDbRadialDimension"
                : "AcDbDiametricDimension";
            DwgGroups kind = _record.Of(subclass);
            int flags = dimension.Int(70);
            double[] middle = dimension.Point(11);

            if (R2010Plus)
            {
                _data.RawChar(dimension.Int(280));
            }

            _data.Point3(dimension.Point(210, 0, 0, 1));
            _data.RawPoint2(middle);
            _data.BitDouble(middle[2]);
            // Bit 1 where the text is where the dimension puts it; AutoCAD sets 2 and 8 besides in every dimension it wrote.
            _data.RawChar(((flags & 128) != 0 ? 0 : 1) | 2 | 8);
            T(dimension.Text(1));
            _data.BitDouble(Radians(dimension.Double(53)));
            _data.BitDouble(Radians(dimension.Double(51)));
            _data.Point3(new[] { 1.0, 1.0, 1.0 });
            _data.BitDouble(0.0);
            _data.BitShort(dimension.Int(71, 5));
            _data.BitShort(dimension.Int(72, 1));
            _data.BitDouble(dimension.Double(41, 1.0));
            _data.BitDouble(dimension.Double(42));

            if (R2007Plus)
            {
                _data.Bit(dimension.Int(73) != 0);
                _data.Bit(dimension.Int(74) != 0);
                _data.Bit(dimension.Int(75) != 0);
            }

            _data.RawPoint2(dimension.Point(12));

            switch (type)
            {
                case 0x14:
                    _data.Point3(dimension.Point(10));
                    _data.Point3(kind.Point(13));
                    _data.Point3(kind.Point(14));
                    _data.RawChar((flags & 64) != 0 ? 1 : 0);
                    break;
                case 0x15:
                case 0x16:
                    _data.Point3(kind.Point(13));
                    _data.Point3(kind.Point(14));
                    _data.Point3(dimension.Point(10));
                    _data.BitDouble(Radians(kind.Double(52)));

                    if (type == 0x15)
                    {
                        _data.BitDouble(Radians(kind.Double(50)));
                    }

                    break;
                case 0x17:
                    _data.Point3(dimension.Point(10));
                    _data.Point3(kind.Point(13));
                    _data.Point3(kind.Point(14));
                    _data.Point3(kind.Point(15));
                    break;
                case 0x18:
                    _data.RawPoint2(kind.Point(16));
                    _data.Point3(kind.Point(13));
                    _data.Point3(kind.Point(14));
                    _data.Point3(kind.Point(15));
                    _data.Point3(dimension.Point(10));
                    break;
                case 0x19:
                    _data.Point3(dimension.Point(10));
                    _data.Point3(kind.Point(15));
                    _data.BitDouble(kind.Double(40));
                    break;
                default:
                    _data.Point3(kind.Point(15));
                    _data.Point3(dimension.Point(10));
                    _data.BitDouble(kind.Double(40));
                    break;
            }

            H(5, _drawing.HandleOf("DIMSTYLE", dimension.Text(3, "Standard")));
            H(5, _drawing.HandleOf("BLOCK_RECORD", dimension.Text(2)));
        }

        private void WriteSolid()
        {
            DwgGroups solid = _record.Of("AcDbTrace");
            double[] first = solid.Point(10);
            _data.BitThickness(solid.Double(39));
            _data.BitDouble(first[2]);

            for (int i = 0; i < 4; i++)
            {
                _data.RawPoint2(solid.Point(10 + i));
            }

            Extrusion(solid.Point(210, 0, 0, 1));
        }

        private void WriteFace()
        {
            DwgGroups face = _record.Of("AcDbFace");
            int flags = face.Int(70);
            var corners = new double[4][];

            for (int i = 0; i < 4; i++)
            {
                corners[i] = face.Point(10 + i);
            }

            bool flat = corners[0][2] == 0.0;
            _data.Bit(flags == 0);
            _data.Bit(flat);
            _data.RawDouble(corners[0][0]);
            _data.RawDouble(corners[0][1]);

            if (!flat)
            {
                _data.RawDouble(corners[0][2]);
            }

            for (int i = 1; i < 4; i++)
            {
                Point3Default(corners[i], corners[i - 1]);
            }

            if (flags != 0)
            {
                _data.BitShort(flags);
            }
        }
    }
}
