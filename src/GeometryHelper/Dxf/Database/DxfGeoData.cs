using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A GEODATA of a DXF file, as AutoCAD's <c>GeoLocationData</c>: where a drawing stands on the earth, kept in the extension
    /// dictionary of its block, model space's, under the name ACAD_GEOGRAPHICDATA; its design point and the reference point of
    /// the coordinate system it stands for, the units and scales that take the drawing's units to metres, the up and north
    /// directions, the coordinate system itself and the points of the mesh that fit the one to the other, read and set as the
    /// groups of AcDbGeoData of the DXF reference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>new DxfGeoData { BlockTableRecord = db.ModelSpace, TypeOfCoordinates = DxfTypeOfCoordinates.Local, DesignPoint = new GeoPoint3(0, 0, 0), ReferencePoint = new GeoPoint3(500000, 4100000, 0), HorizontalUnits = DxfUnits.Millimeters, HorizontalUnitsScale = 0.001 }.PostToDb()</c>
    /// places a drawing in millimetres on a local grid, its origin at 500 000 east and 4 100 000 north: a GEODATA at the end of
    /// the OBJECTS section owned by model space's extension dictionary, made where it had none, and the class GEODATA listed
    /// where the drawing lists none. <see cref="DxfDatabase.GeoDataObject"/> reads it back.
    /// </para>
    /// <para>
    /// A GEODATA made in code is AutoCAD 2021's new one, written in the order of ezdxf's: of version 2, of no known type of
    /// coordinates, its two points at the origin, its units metres of scale 1, up along Z and north along Y, its scale judged
    /// at the reference point, a coordinate system of no text and no mesh. It is posted to model space alone, as AutoCAD's
    /// <c>BlockTableRecordId</c> takes no other block. A GEODATA came with R2010 (AC1024), version 1 of R2009 having no DXF
    /// version of its own: one of version 1 is read by the groups a comment of ezdxf gives it, those of version 2 but the north
    /// direction, the points of its mesh in 12 and 13 there; ezdxf 1.4.5 itself reads no points of the mesh in one of version
    /// 1, and only warns of it.
    /// </para>
    /// <para>
    /// The faces of the mesh (96 to 99) are kept as they are and not given. Taking a point to latitude and longitude, AutoCAD's
    /// <c>TransformToLonLatAlt</c> and <c>TransformFromLonLatAlt</c>, needs the coordinate system's projection, which AutoCAD
    /// works out by its own library: neither is here.
    /// </para>
    /// </remarks>
    public sealed class DxfGeoData : DxfDBObject
    {
        // The groups of AcDbGeoData in the order AutoCAD writes them, those of version 2.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[]
        {
            100, 90, 330, 70, 10, 20, 30, 11, 21, 31, 40, 91, 41, 92, 210, 220, 230, 12, 22, 95, 141, 294, 142, 143, 303, 301, 302,
            305, 306, 307, 93, 13, 23, 14, 24, 96, 97, 98, 99,
        });

        // The groups of the coordinate system's definition, its pieces (303) and its last (301).
        private static readonly int[] Definition = { 303, 301 };

        // The groups a GEODATA made in code is written with after its subclass marker, in the order ezdxf writes a new one, the
        // values those of AutoCAD 2021's new GeoLocationData: of no known type (70 of 0), in metres (91 and 92 of 6), its scale
        // judged at the reference point (95 of 3). The host's handle (330) is put after the version when it is posted.
        private readonly List<KeyValuePair<int, string>> _made = new List<KeyValuePair<int, string>>
        {
            Pair(90, "2"), Pair(70, "0"), Pair(10, "0.0"), Pair(20, "0.0"), Pair(30, "0.0"), Pair(11, "0.0"), Pair(21, "0.0"),
            Pair(31, "0.0"), Pair(40, "1.0"), Pair(91, "6"), Pair(41, "1.0"), Pair(92, "6"), Pair(210, "0.0"), Pair(220, "0.0"),
            Pair(230, "1.0"), Pair(12, "0.0"), Pair(22, "1.0"), Pair(95, "3"), Pair(141, "1.0"), Pair(294, "0"), Pair(142, "0.0"),
            Pair(143, "0.0"), Pair(301, string.Empty), Pair(93, "0"), Pair(96, "0"),
        };

        private DxfBlockTableRecord _block;

        /// <summary>
        /// Initializes a GEODATA made in code, as AutoCAD's <c>GeoLocationData()</c>, with AutoCAD 2021's defaults: of version
        /// 2, of no known type of coordinates, its points at the origin, metres of scale 1, up along Z and north along Y, its
        /// scale judged at the reference point; given a <see cref="BlockTableRecord"/> and added to its drawing by
        /// <see cref="PostToDb"/>.
        /// </summary>
        public DxfGeoData()
        {
        }

        internal DxfGeoData(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets or sets the block whose geographic location the GEODATA gives (group 330 of AcDbGeoData), as AutoCAD's
        /// <c>BlockTableRecordId</c>: <see cref="DxfDatabase.ModelSpace"/> for the drawing's own; null where the drawing has no
        /// block of the handle, and for one made in code not given one.
        /// </summary>
        /// <value>Set, on a GEODATA made in code, the block it is posted to: model space, as AutoCAD 2021 refuses paper space
        /// (eInvalidInput) and every other block.</value>
        /// <exception cref="ArgumentNullException">Thrown when the block set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the block set is of a drawing and is not its model space.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block is set on a GEODATA of a file: it stays with the
        /// block whose extension dictionary holds it.</exception>
        public DxfBlockTableRecord BlockTableRecord
        {
            get
            {
                if (Item == null)
                {
                    return _block;
                }

                string handle = Value(330)?.Trim();
                DxfDatabase database = Item.File.Database;

                if (handle == null || database == null)
                {
                    return null;
                }

                foreach (DxfBlockTableRecord block in database.BlockTable)
                {
                    if (string.Equals(block.Handle, handle, StringComparison.OrdinalIgnoreCase))
                    {
                        return block;
                    }
                }

                return null;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (Item != null)
                {
                    throw new InvalidOperationException("The GEODATA " + (Handle ?? string.Empty) + " is in a file, and stays with the block whose extension dictionary holds it: post a new one to another block.");
                }

                if (value.Item != null && !DxfBlockTableRecord.IsModelName(value.Name))
                {
                    throw new ArgumentException("A GEODATA is posted to model space alone, as AutoCAD's BlockTableRecordId takes no other block: " + value.Name + " is not.", nameof(value));
                }

                _block = value;
            }
        }

        /// <summary>
        /// Gets or sets what the coordinates of the location are (group 70), as AutoCAD's <c>TypeOfCoordinates</c>: a local
        /// grid, a projected grid or latitude and longitude; geographic where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is not one of the four.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased.</exception>
        public DxfTypeOfCoordinates TypeOfCoordinates
        {
            get => (DxfTypeOfCoordinates)Whole(70, 3);

            set
            {
                if (value == TypeOfCoordinates)
                {
                    return;
                }

                if (value < DxfTypeOfCoordinates.Unknown || value > DxfTypeOfCoordinates.Geographic)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The coordinates of a geographic location are unknown (0), of a local grid (1), of a projected grid (2) or geographic (3).");
                }

                Set(Pair(70, ((int)value).ToString(CultureInfo.InvariantCulture)));
            }
        }

        /// <summary>
        /// Gets or sets the design point (groups 10, 20 and 30), as AutoCAD's <c>DesignPoint</c>: the point of the drawing, in
        /// its world coordinates, that stands at the reference point; the origin where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the point set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased.</exception>
        public GeoPoint3 DesignPoint
        {
            get => new GeoPoint3(Real(10, 0.0), Real(20, 0.0), Real(30, 0.0));
            set => SetPoint(10, value.X, value.Y, value.Z, DesignPoint, nameof(value));
        }

        /// <summary>
        /// Gets or sets the reference point (groups 11, 21 and 31), as AutoCAD's <c>ReferencePoint</c>: the point of the
        /// coordinate system the design point stands at, east and north on a grid, longitude and latitude where the coordinates
        /// are geographic; the origin where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the point set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased.</exception>
        public GeoPoint3 ReferencePoint
        {
            get => new GeoPoint3(Real(11, 0.0), Real(21, 0.0), Real(31, 0.0));
            set => SetPoint(11, value.X, value.Y, value.Z, ReferencePoint, nameof(value));
        }

        /// <summary>
        /// Gets or sets the scale that takes a horizontal length of the drawing to metres (group 40), as AutoCAD's
        /// <c>HorizontalUnitsScale</c>: 0.001 for a drawing in millimetres; 1 where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased.</exception>
        public double HorizontalUnitsScale
        {
            get => Real(40, 1.0);
            set => SetReal(40, value, HorizontalUnitsScale, true);
        }

        /// <summary>
        /// Gets or sets the unit of the drawing's horizontal lengths (group 91), as AutoCAD's <c>HorizontalUnits</c>, numbered
        /// as <c>$INSUNITS</c> is; inches where the record gives none, as ezdxf reads it.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the unit set is not one of <see cref="DxfUnits"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased.</exception>
        public DxfUnits HorizontalUnits
        {
            get => (DxfUnits)Whole(91, 1);
            set => SetUnits(91, value, HorizontalUnits);
        }

        /// <summary>
        /// Gets or sets the scale that takes a height of the drawing to metres (group 41), as AutoCAD's
        /// <c>VerticalUnitsScale</c>; 1 where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased.</exception>
        public double VerticalUnitsScale
        {
            get => Real(41, 1.0);
            set => SetReal(41, value, VerticalUnitsScale, false);
        }

        /// <summary>
        /// Gets or sets the unit of the drawing's heights (group 92), as AutoCAD's <c>VerticalUnits</c>; inches where the record
        /// gives none, as ezdxf reads it.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the unit set is not one of <see cref="DxfUnits"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased.</exception>
        public DxfUnits VerticalUnits
        {
            get => (DxfUnits)Whole(92, 1);
            set => SetUnits(92, value, VerticalUnits);
        }

        /// <summary>
        /// Gets or sets the direction up (groups 210, 220 and 230), as AutoCAD's <c>UpDirection</c>, as written, not made of
        /// length 1; along Z where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the direction set is not finite or is of no length.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased.</exception>
        public GeoVector3 UpDirection
        {
            get => new GeoVector3(Real(210, 0.0), Real(220, 0.0), Real(230, 1.0));

            set
            {
                if (value.X == 0.0 && value.Y == 0.0 && value.Z == 0.0)
                {
                    throw new ArgumentException("The direction up has a length.", nameof(value));
                }

                SetPoint(210, value.X, value.Y, value.Z, new GeoPoint3(UpDirection.X, UpDirection.Y, UpDirection.Z), nameof(value));
            }
        }

        /// <summary>
        /// Gets or sets the direction north in the drawing (groups 12 and 22), as AutoCAD's <c>NorthDirectionVector</c>, as
        /// written; along Y where the record gives none, and for a GEODATA of version 1, whose group 12 is a point of its mesh.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the direction set is not finite or is of no length.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased, or is of version 1.</exception>
        public GeoVector2 NorthDirectionVector
        {
            get => IsFirstVersion ? new GeoVector2(0.0, 1.0) : new GeoVector2(Real(12, 0.0), Real(22, 1.0));

            set
            {
                if (value.X == 0.0 && value.Y == 0.0)
                {
                    throw new ArgumentException("The direction north has a length.", nameof(value));
                }

                if (!Guard.IsFinite(value.X) || !Guard.IsFinite(value.Y))
                {
                    throw new ArgumentException("The direction north is of finite numbers.", nameof(value));
                }

                GeoVector2 now = NorthDirectionVector;

                if (Same(value.X, now.X) && Same(value.Y, now.Y))
                {
                    return;
                }

                NotFirstVersion("a north direction");
                Set(Pair(12, Numbers.Exact(value.X + 0.0)), Pair(22, Numbers.Exact(value.Y + 0.0)));
            }
        }

        /// <summary>
        /// Gets the angle of the direction north, in radians, as AutoCAD's <c>NorthDirection</c> gives it: from the Y axis of
        /// the drawing, positive towards X, from -π to π. North along Y is 0, along X a quarter turn (90 degrees), and along -X
        /// -90 degrees, as AutoCAD 2021 reads the three.
        /// </summary>
        public double NorthDirection
        {
            get
            {
                GeoVector2 north = NorthDirectionVector;
                return Math.Atan2(north.X, north.Y);
            }
        }

        /// <summary>
        /// Gets or sets how the scale is judged (group 95), as AutoCAD's <c>ScaleEstimationMethod</c>; none, a scale of 1, where
        /// the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the method set is not one of the four.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased.</exception>
        public DxfScaleEstimationMethod ScaleEstimationMethod
        {
            get => (DxfScaleEstimationMethod)Whole(95, 1);

            set
            {
                if (value == ScaleEstimationMethod)
                {
                    return;
                }

                if (value < DxfScaleEstimationMethod.Unity || value > DxfScaleEstimationMethod.Prismoidal)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The scale of a geographic location is judged by none (1), the user's factor (2), the grid at the reference point (3) or the prismoidal method (4).");
                }

                Set(Pair(95, ((int)value).ToString(CultureInfo.InvariantCulture)));
            }
        }

        /// <summary>
        /// Gets or sets the scale factor the user gives (group 141), as AutoCAD's <c>ScaleFactor</c>, read where the scale is
        /// judged by it; 1 where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the factor set is nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased.</exception>
        public double ScaleFactor
        {
            get => Real(141, 1.0);
            set => SetReal(141, value, ScaleFactor, true);
        }

        /// <summary>
        /// Gets or sets whether lengths are corrected to the level of the sea (group 294), as AutoCAD's
        /// <c>DoSeaLevelCorrection</c>; false where the record gives none.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased.</exception>
        public bool DoSeaLevelCorrection
        {
            get => Whole(294, 0) != 0;

            set
            {
                if (value != DoSeaLevelCorrection)
                {
                    Set(Pair(294, value ? "1" : "0"));
                }
            }
        }

        /// <summary>
        /// Gets or sets the height of the sea's level (group 142), as AutoCAD's <c>SeaLevelElevation</c>, in the drawing's
        /// units; nought where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased.</exception>
        public double SeaLevelElevation
        {
            get => Real(142, 0.0);
            set => SetReal(142, value, SeaLevelElevation, false);
        }

        /// <summary>
        /// Gets or sets the radius of the coordinate system's projection (group 143), as AutoCAD's
        /// <c>CoordinateProjectionRadius</c>; nought where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased.</exception>
        public double CoordinateProjectionRadius
        {
            get => Real(143, 0.0);
            set => SetReal(143, value, CoordinateProjectionRadius, false);
        }

        /// <summary>
        /// Gets or sets the definition of the coordinate system (groups 303 and 301), as AutoCAD's <c>CoordinateSystem</c>: the
        /// XML of its projection, datum and ellipsoid, as ezdxf reads it, the pieces of 303 then the last of 301 joined, each
        /// <c>^J</c> a line feed; empty where the record gives none.
        /// </summary>
        /// <value>Set, written as ezdxf writes it, each line feed as <c>^J</c> and in pieces of 255 characters, the last in group
        /// 301, a piece not ending on a caret or on the first half of a letter of two units of UTF-16; a carriage return, alone or
        /// before a line feed, is written as a line feed.</value>
        /// <exception cref="ArgumentNullException">Thrown when the definition set is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased.</exception>
        public string CoordinateSystem
        {
            get
            {
                var text = new StringBuilder();

                foreach (KeyValuePair<int, string> group in Groups())
                {
                    if (group.Key == 303 || group.Key == 301)
                    {
                        text.Append(DxfReader.Decode(group.Value));
                    }
                }

                return text.ToString().Replace("^J", "\n");
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                string plain = value.Replace("\r\n", "\n").Replace('\r', '\n');

                if (string.Equals(plain, CoordinateSystem, StringComparison.Ordinal))
                {
                    return;
                }

                string written = plain.Replace("\n", "^J");
                var groups = new List<KeyValuePair<int, string>>();
                int from = 0;

                while (written.Length - from > 255)
                {
                    int piece = written[from + 254] == '^' || char.IsHighSurrogate(written[from + 254]) ? 254 : 255;
                    groups.Add(Pair(303, written.Substring(from, piece)));
                    from += piece;
                }

                groups.Add(Pair(301, written.Substring(from)));
                Replace(Definition, groups);
            }
        }

        /// <summary>Gets or sets the GeoRSS tag (group 302), as AutoCAD's <c>GeoRSSTag</c>; empty where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the tag set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the tag set has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased.</exception>
        public string GeoRSSTag
        {
            get => Text(302);
            set => SetText(302, value);
        }

        /// <summary>Gets or sets the tag of the observation from (group 305), as the DXF reference names it; empty where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the tag set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the tag set has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased.</exception>
        public string ObservationFromTag
        {
            get => Text(305);
            set => SetText(305, value);
        }

        /// <summary>Gets or sets the tag of the observation to (group 306), as the DXF reference names it; empty where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the tag set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the tag set has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased.</exception>
        public string ObservationToTag
        {
            get => Text(306);
            set => SetText(306, value);
        }

        /// <summary>Gets or sets the tag of the observation's coverage (group 307), as the DXF reference names it; empty where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the tag set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the tag set has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased.</exception>
        public string ObservationCoverageTag
        {
            get => Text(307);
            set => SetText(307, value);
        }

        /// <summary>
        /// Gets how many points the mesh has, as AutoCAD's <c>NumMeshPoints</c>: the pairs of a point of the drawing (13 and 23)
        /// and the point it is taken to (14 and 24), 12 and 13 in a GEODATA of version 1; a point of the drawing with no point to
        /// take it to is not counted.
        /// </summary>
        public int NumMeshPoints => MeshPoints().Count;

        /// <summary>Gets a point of the mesh, as AutoCAD's <c>GetMeshPointMap</c>.</summary>
        /// <param name="index">Which point, from nought.</param>
        /// <returns>The point of the drawing and the point it is taken to.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is below nought or not below <see cref="NumMeshPoints"/>.</exception>
        public DxfMeshPointMap GetMeshPointMap(int index)
        {
            List<DxfMeshPointMap> maps = MeshPoints();

            if (index < 0 || index >= maps.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The mesh has " + maps.Count.ToString(CultureInfo.InvariantCulture) + " points.");
            }

            return maps[index];
        }

        /// <summary>
        /// Gets the points of the mesh, as AutoCAD's <c>GetMeshPointMaps</c>, in the order of the record; here a list of
        /// pairs where AutoCAD gives two lists.
        /// </summary>
        /// <returns>The points, a new list; empty where the mesh has none.</returns>
        public IReadOnlyList<DxfMeshPointMap> GetMeshPointMaps() => MeshPoints().AsReadOnly();

        /// <summary>
        /// Puts a point in the mesh, as AutoCAD's <c>AddMeshPointMap</c>: before the point of the index, or last for the index
        /// <see cref="NumMeshPoints"/>; the count (93) written anew, the faces of the mesh kept as they are.
        /// </summary>
        /// <param name="index">Where the point goes, from nought to <see cref="NumMeshPoints"/>.</param>
        /// <param name="sourcePt">The point of the drawing.</param>
        /// <param name="destPt">The point of the coordinate system it is taken to.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is below nought or above <see cref="NumMeshPoints"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when a point is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased, or is of version 1.</exception>
        public void AddMeshPointMap(int index, GeoPoint2 sourcePt, GeoPoint2 destPt)
        {
            List<DxfMeshPointMap> maps = MeshPoints();

            if (index < 0 || index > maps.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "A point goes into the mesh from nought to its count, " + maps.Count.ToString(CultureInfo.InvariantCulture) + ".");
            }

            CheckFinite(sourcePt, nameof(sourcePt));
            CheckFinite(destPt, nameof(destPt));
            maps.Insert(index, new DxfMeshPointMap(sourcePt, destPt));
            WriteMesh(maps);
        }

        /// <summary>
        /// Gives the mesh its points, as AutoCAD's <c>SetMeshPointMaps</c>: each point of the drawing with the point at its place
        /// among those it is taken to, in place of the points it had, the faces of the mesh kept as they are; the points it has
        /// change nothing.
        /// </summary>
        /// <param name="sourcePts">The points of the drawing.</param>
        /// <param name="destPts">The points of the coordinate system they are taken to, one for each.</param>
        /// <exception cref="ArgumentNullException">Thrown when either list is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the lists are not of one length, or a point is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is erased, or is of version 1.</exception>
        public void SetMeshPointMaps(IEnumerable<GeoPoint2> sourcePts, IEnumerable<GeoPoint2> destPts)
        {
            if (sourcePts == null)
            {
                throw new ArgumentNullException(nameof(sourcePts));
            }

            if (destPts == null)
            {
                throw new ArgumentNullException(nameof(destPts));
            }

            var sources = new List<GeoPoint2>(sourcePts);
            var dests = new List<GeoPoint2>(destPts);

            if (sources.Count != dests.Count)
            {
                throw new ArgumentException("The mesh takes one point of the coordinate system for each of the drawing: " + sources.Count.ToString(CultureInfo.InvariantCulture) + " and " + dests.Count.ToString(CultureInfo.InvariantCulture) + " are given.", nameof(destPts));
            }

            var maps = new List<DxfMeshPointMap>(sources.Count);

            for (int i = 0; i < sources.Count; i++)
            {
                CheckFinite(sources[i], nameof(sourcePts));
                CheckFinite(dests[i], nameof(destPts));
                maps.Add(new DxfMeshPointMap(sources[i], dests[i]));
            }

            WriteMesh(maps);
        }

        /// <summary>
        /// Takes every point out of the mesh, as AutoCAD's <c>ResetMeshPointMaps</c>: the count (93) written 0, the faces of the
        /// mesh kept as they are; a mesh of no points changes nothing.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA has points and is erased, or is of version 1.</exception>
        public void ResetMeshPointMaps() => WriteMesh(new List<DxfMeshPointMap>());

        /// <summary>
        /// Adds the GEODATA made in code to the drawing of its <see cref="BlockTableRecord"/>, as AutoCAD's <c>PostToDb</c>:
        /// at the end of the OBJECTS section, owned by the block's extension dictionary, which is made where the block has none,
        /// under the name ACAD_GEOGRAPHICDATA, the GEODATA it held erased, as ezdxf replaces it; the block's handle written as
        /// its group 330 and the class GEODATA listed where the drawing lists none, as ezdxf lists it (flags 4095).
        /// </summary>
        /// <remarks>
        /// The class is listed in the drawing's CLASSES section: a file of R2010 or later written with no CLASSES section is
        /// given the GEODATA and no class for it, and whether AutoCAD opens such a file is not confirmed.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is in a file already, has no block, or its block
        /// is in no file, is erased, is not model space, or is of a file older than R2010, which has no GEODATA.</exception>
        public void PostToDb()
        {
            if (Item != null)
            {
                throw new InvalidOperationException("The GEODATA " + (Handle ?? string.Empty) + " is in a file already.");
            }

            DxfBlockTableRecord block = _block ?? throw new InvalidOperationException("The GEODATA is posted to the drawing of its block: give it its BlockTableRecord first, as AutoCAD's BlockTableRecordId.");

            if (block.Item == null || !block.IsResident || block.HasNoRecord || block.IsErased)
            {
                throw new InvalidOperationException("The block " + block.Name + " of the GEODATA is in no file, or is erased.");
            }

            if (!DxfBlockTableRecord.IsModelName(block.Name))
            {
                throw new InvalidOperationException("A GEODATA is posted to model space alone, as AutoCAD's BlockTableRecordId takes no other block: " + block.Name + " is not.");
            }

            DxfFile file = block.Item.File;
            file.Requires(1024, "GEODATA", "R2010 (AC1024)");
            block.CreateExtensionDictionary();
            string host = block.Handle;
            List<KeyValuePair<int, string>> made = _made;
            DxfItem item = block.ExtensionDictionary.Add("ACAD_GEOGRAPHICDATA", "GEODATA", b =>
            {
                b.Group(100, "AcDbGeoData");

                foreach (KeyValuePair<int, string> group in made)
                {
                    b.Group(group.Key, group.Value);

                    if (group.Key == 90)
                    {
                        b.Group(330, host);
                    }
                }
            });

            file.AddClass("GEODATA", "AcDbGeoData", 4095);
            _block = null;
            Bind(item);
        }

        /// <summary>
        /// Erases the GEODATA, as AutoCAD's <c>EraseFromDb</c>: it is taken out of the file, and the entry of the extension
        /// dictionary that names it with it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the GEODATA is in no file, or is erased.</exception>
        public override void Erase() => DxfDBDictionary.EraseFromOwner(this, InFile("be erased"));

        /// <summary>Gives the GEODATA as a line for debugging: its type, its handle and what its coordinates are.</summary>
        /// <returns>The line, such as <c>GEODATA 2F, Local</c>.</returns>
        public override string ToString() => "GEODATA " + (Handle ?? string.Empty) + ", " + TypeOfCoordinates;

        private static KeyValuePair<int, string> Pair(int code, string value) => new KeyValuePair<int, string>(code, value);

        private static bool Same(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        /// <summary>Whether the GEODATA is of version 1 (group 90 below 2), whose groups 12 and 13 are the points of its mesh.</summary>
        private bool IsFirstVersion => Whole(90, 2) < 2;

        /// <summary>The groups of AcDbGeoData after its subclass marker, as made in code or as the file writes them, in their order.</summary>
        private List<KeyValuePair<int, string>> Groups()
        {
            if (Item == null)
            {
                return new List<KeyValuePair<int, string>>(_made);
            }

            var groups = new List<KeyValuePair<int, string>>();

            foreach (DxfTypedValue group in Item.File.ObjectGroupsOf(Item))
            {
                if (group.TypeCode != 100)
                {
                    groups.Add(Pair(group.TypeCode, group.Value));
                }
            }

            return groups;
        }

        /// <summary>The value of the first group of a code of AcDbGeoData, as made in code or as the file writes it; null where it has none.</summary>
        private string Value(int code)
        {
            if (Item != null)
            {
                return Item.File.ValueOf(Item, code);
            }

            foreach (KeyValuePair<int, string> group in _made)
            {
                if (group.Key == code)
                {
                    return group.Value;
                }
            }

            return null;
        }

        /// <summary>A whole number of AcDbGeoData; the fallback where it has none, or not a whole number.</summary>
        private int Whole(int code, int fallback)
            => int.TryParse(Value(code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

        /// <summary>A number of AcDbGeoData; the fallback where it has none, or not a finite number.</summary>
        private double Real(int code, double fallback)
            => Numbers.TryRead(Value(code), out double value) && Guard.IsFinite(value) ? value : fallback;

        /// <summary>A text of AcDbGeoData, its escapes read as letters; empty where it has none.</summary>
        private string Text(int code)
        {
            string value = Value(code);
            return value == null ? string.Empty : DxfReader.Decode(value);
        }

        /// <summary>Sets a number of AcDbGeoData; nothing where it is the one it has, to the bit.</summary>
        private void SetReal(int code, double value, double now, bool nonZero)
        {
            Guard.Finite(value, nameof(value), "The value is a finite number.");

            if (nonZero && value == 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The scale is a finite number other than nought.");
            }

            if (!Same(value, now))
            {
                Set(Pair(code, Numbers.Exact(value + 0.0)));
            }
        }

        /// <summary>Sets a unit of AcDbGeoData; nothing where it is the one it has.</summary>
        private void SetUnits(int code, DxfUnits value, DxfUnits now)
        {
            if (value == now)
            {
                return;
            }

            if (!Enum.IsDefined(typeof(DxfUnits), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "A unit of a geographic location is one of AutoCAD's UnitsValue, 0 to 20.");
            }

            Set(Pair(code, ((int)value).ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>Sets a text of AcDbGeoData; nothing where it is the one it has.</summary>
        private void SetText(int code, string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("A tag of a geographic location has no line break in it.", nameof(value));
            }

            if (!string.Equals(value, Text(code), StringComparison.Ordinal))
            {
                Set(Pair(code, value));
            }
        }

        /// <summary>Sets the three groups of a point or a direction from a code on (10, 11, 210); nothing where each is the one it has, to the bit.</summary>
        private void SetPoint(int code, double x, double y, double z, GeoPoint3 now, string parameter)
        {
            if (!Guard.IsFinite(x) || !Guard.IsFinite(y) || !Guard.IsFinite(z))
            {
                throw new ArgumentException("A point or a direction of a geographic location is of finite numbers.", parameter);
            }

            if (Same(x, now.X) && Same(y, now.Y) && Same(z, now.Z))
            {
                return;
            }

            Set(Pair(code, Numbers.Exact(x + 0.0)), Pair(code + 10, Numbers.Exact(y + 0.0)), Pair(code + 20, Numbers.Exact(z + 0.0)));
        }

        private static void CheckFinite(GeoPoint2 point, string parameter)
        {
            if (!Guard.IsFinite(point.X) || !Guard.IsFinite(point.Y))
            {
                throw new ArgumentException("A point of the mesh is of finite numbers.", parameter);
            }
        }

        /// <summary>Refuses a change a GEODATA of version 1 cannot be given, its groups as ezdxf's comment gives them not confirmed for it.</summary>
        private void NotFirstVersion(string what)
        {
            if (IsFirstVersion)
            {
                throw new InvalidOperationException("A GEODATA of version 1 (R2009) holds the points of its mesh in its groups 12 and 13, and is not given " + what + ".");
            }
        }

        /// <summary>
        /// The points of the mesh, in the order of the record: the points of the drawing (13 and 23, 12 and 22 in version 1) each
        /// with the point at its place among those it is taken to (14 and 24, 13 and 23 in version 1).
        /// </summary>
        private List<DxfMeshPointMap> MeshPoints()
        {
            bool first = IsFirstVersion;
            int source = first ? 12 : 13;
            int dest = first ? 13 : 14;
            List<KeyValuePair<int, string>> groups = Groups();
            var sources = new List<GeoPoint2>();
            var dests = new List<GeoPoint2>();

            for (int i = 0; i < groups.Count; i++)
            {
                int code = groups[i].Key;

                if (code != source && code != dest)
                {
                    continue;
                }

                double x = Read(groups[i].Value);
                double y = i + 1 < groups.Count && groups[i + 1].Key == code + 10 ? Read(groups[++i].Value) : 0.0;
                (code == source ? sources : dests).Add(new GeoPoint2(x, y));
            }

            var maps = new List<DxfMeshPointMap>(Math.Min(sources.Count, dests.Count));

            for (int i = 0; i < sources.Count && i < dests.Count; i++)
            {
                maps.Add(new DxfMeshPointMap(sources[i], dests[i]));
            }

            return maps;
        }

        /// <summary>A number of the mesh; nought where it is not a finite number.</summary>
        private static double Read(string text) => Numbers.TryRead(text, out double value) && Guard.IsFinite(value) ? value : 0.0;

        /// <summary>
        /// Writes the points of the mesh, the count (93) then each point of the drawing and the point it is taken to, as ezdxf
        /// writes them, in place of those it has; nothing where the points are those it has, to the bit, with their count.
        /// </summary>
        private void WriteMesh(List<DxfMeshPointMap> maps)
        {
            List<DxfMeshPointMap> now = MeshPoints();
            bool same = now.Count == maps.Count;

            for (int i = 0; i < maps.Count && same; i++)
            {
                same = Same(now[i].SourcePoint.X, maps[i].SourcePoint.X) && Same(now[i].SourcePoint.Y, maps[i].SourcePoint.Y)
                    && Same(now[i].DestPoint.X, maps[i].DestPoint.X) && Same(now[i].DestPoint.Y, maps[i].DestPoint.Y);
            }

            // A mesh of no points with no count written is as one of a count of nought.
            if (same && Whole(93, maps.Count == 0 ? 0 : -1) == maps.Count)
            {
                return;
            }

            NotFirstVersion("points of its mesh");
            var groups = new List<KeyValuePair<int, string>> { Pair(93, maps.Count.ToString(CultureInfo.InvariantCulture)) };

            foreach (DxfMeshPointMap map in maps)
            {
                groups.Add(Pair(13, Numbers.Exact(map.SourcePoint.X + 0.0)));
                groups.Add(Pair(23, Numbers.Exact(map.SourcePoint.Y + 0.0)));
                groups.Add(Pair(14, Numbers.Exact(map.DestPoint.X + 0.0)));
                groups.Add(Pair(24, Numbers.Exact(map.DestPoint.Y + 0.0)));
            }

            Replace(new[] { 93, 13, 23, 14, 24 }, groups);
        }

        /// <summary>
        /// Sets groups of AcDbGeoData, each the first of its code, in its place, one it has not put where AutoCAD's order puts
        /// it: those a GEODATA made in code is to be written with, or the record's.
        /// </summary>
        private void Set(params KeyValuePair<int, string>[] values)
        {
            if (Item != null)
            {
                InFile("be changed").SetValues(Item, values, Order, false);
                return;
            }

            foreach (KeyValuePair<int, string> value in values)
            {
                int at = _made.FindIndex(g => g.Key == value.Key);

                if (at >= 0)
                {
                    _made[at] = value;
                }
                else
                {
                    _made.Insert(Place(value.Key), value);
                }
            }
        }

        /// <summary>
        /// Replaces every group of some codes of AcDbGeoData by the groups given, put where the first of those was, or where
        /// AutoCAD's order puts the first given: the pieces of the coordinate system, the points of the mesh.
        /// </summary>
        private void Replace(int[] codes, List<KeyValuePair<int, string>> groups)
        {
            if (Item != null)
            {
                InFile("be changed").ReplaceOwn(Item, 0, codes, groups, Order, false);
                return;
            }

            int at = _made.FindIndex(g => Array.IndexOf(codes, g.Key) >= 0);
            _made.RemoveAll(g => Array.IndexOf(codes, g.Key) >= 0);
            _made.InsertRange(at >= 0 ? at : Place(groups[0].Key), groups);
        }

        /// <summary>Where a group of a code goes among those a GEODATA made in code is written with, by AutoCAD's order.</summary>
        private int Place(int code)
        {
            int rank = Rank(code);
            int put = _made.FindIndex(g => Rank(g.Key) > rank);
            return put < 0 ? _made.Count : put;
        }

        /// <summary>The place of a code in AutoCAD's order of the groups of AcDbGeoData.</summary>
        private static int Rank(int code)
        {
            for (int i = 1; i < Order.Count; i++)
            {
                if (Order[i] == code)
                {
                    return i;
                }
            }

            return Order.Count;
        }
    }
}
