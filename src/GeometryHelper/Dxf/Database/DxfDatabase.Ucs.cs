using System;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The user coordinate systems of model space and paper space the header keeps, as AutoCAD's <c>Database</c> has them: the
    /// current one's origin and axes (<c>$UCSORG</c>, <c>$UCSXDIR</c>, <c>$UCSYDIR</c>, and <c>$PUCS</c> of paper space), its
    /// name, the UCS the orthographic ones are taken from, and the origins of those six. A UCS is given by the name of its
    /// record of the UCS table, which the library has no class for.
    /// </summary>
    public sealed partial class DxfDatabase
    {
        /// <summary>Gets the origin of the current UCS of model space (<c>$UCSORG</c>), as AutoCAD's <c>Ucsorg</c>, in world coordinates; the origin where the header gives none.</summary>
        public GeoPoint3 Ucsorg => Point("$UCSORG");

        /// <summary>Gets the X axis of the current UCS of model space (<c>$UCSXDIR</c>), as AutoCAD's <c>Ucsxdir</c>; the world's X axis where the header gives none.</summary>
        public GeoVector3 Ucsxdir => Direction("$UCSXDIR", GeoVector3.XAxis);

        /// <summary>Gets the Y axis of the current UCS of model space (<c>$UCSYDIR</c>), as AutoCAD's <c>Ucsydir</c>; the world's Y axis where the header gives none.</summary>
        public GeoVector3 Ucsydir => Direction("$UCSYDIR", GeoVector3.YAxis);

        /// <summary>Gets the origin of the current UCS of paper space (<c>$PUCSORG</c>), as AutoCAD's <c>Pucsorg</c>; the origin where the header gives none.</summary>
        public GeoPoint3 Pucsorg => Point("$PUCSORG");

        /// <summary>Gets the X axis of the current UCS of paper space (<c>$PUCSXDIR</c>), as AutoCAD's <c>Pucsxdir</c>; the world's X axis where the header gives none.</summary>
        public GeoVector3 Pucsxdir => Direction("$PUCSXDIR", GeoVector3.XAxis);

        /// <summary>Gets the Y axis of the current UCS of paper space (<c>$PUCSYDIR</c>), as AutoCAD's <c>Pucsydir</c>; the world's Y axis where the header gives none.</summary>
        public GeoVector3 Pucsydir => Direction("$PUCSYDIR", GeoVector3.YAxis);

        /// <summary>
        /// Sets the current UCS of model space, as AutoCAD's <c>Ucs</c>: its origin and axes written to <c>$UCSORG</c>,
        /// <c>$UCSXDIR</c> and <c>$UCSYDIR</c>, read back by <see cref="Ucsorg"/>, <see cref="Ucsxdir"/> and
        /// <see cref="Ucsydir"/>. A system rotated a quarter turn about Z at (100, 0, 0) writes the axes (0, 1, 0) and (-1, 0, 0).
        /// The name of the UCS, <see cref="Ucsname"/>, is left as it is. A <see cref="GeoCoordinateSystem3"/> makes its axes
        /// of unit length and square, so a header whose axes are not, read into one and set back, is written with them so:
        /// <c>$UCSXDIR</c> of (2, 0, 0) is written (1, 0, 0).
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the system set is not of a finite origin and unit axes at right angles, as <c>default(GeoCoordinateSystem3)</c> is not.</exception>
        public GeoCoordinateSystem3 Ucs
        {
            set => PutUcs("$UCS", value);
        }

        /// <summary>Sets the current UCS of paper space, as AutoCAD's <c>Pucs</c>: its origin and axes written to <c>$PUCSORG</c>, <c>$PUCSXDIR</c> and <c>$PUCSYDIR</c>, as <see cref="Ucs"/> writes those of model space.</summary>
        /// <exception cref="ArgumentException">Thrown when the system set is not of a finite origin and unit axes at right angles.</exception>
        public GeoCoordinateSystem3 Pucs
        {
            set => PutUcs("$PUCS", value);
        }

        /// <summary>Gets or sets the name of the current UCS of model space (<c>$UCSNAME</c>), as AutoCAD's <c>Ucsname</c> gives its id: a record of the UCS table; null where the header names none.</summary>
        /// <value>Set, the name of a record of the UCS table, or null for none.</value>
        /// <exception cref="ArgumentException">Thrown when the UCS table has no record of the name set.</exception>
        public string Ucsname
        {
            get => Name("$UCSNAME", 2, null);
            set => PutUcsName("$UCSNAME", value, 1009);
        }

        /// <summary>Gets or sets the name of the current UCS of paper space (<c>$PUCSNAME</c>), as AutoCAD's <c>Pucsname</c> gives its id; null where the header names none.</summary>
        /// <value>Set, the name of a record of the UCS table, or null for none.</value>
        /// <exception cref="ArgumentException">Thrown when the UCS table has no record of the name set.</exception>
        public string Pucsname
        {
            get => Name("$PUCSNAME", 2, null);
            set => PutUcsName("$PUCSNAME", value, 1009);
        }

        /// <summary>Gets or sets the name of the UCS the orthographic systems of model space are taken from (<c>$UCSBASE</c>), as AutoCAD's <c>UcsBase</c> gives its id; null, the world, where the header names none.</summary>
        /// <value>Set, the name of a record of the UCS table, or null for the world.</value>
        /// <exception cref="ArgumentException">Thrown when the UCS table has no record of the name set.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public string UcsBase
        {
            get => Name("$UCSBASE", 2, null);
            set => PutUcsName("$UCSBASE", value, 1015);
        }

        /// <summary>Gets or sets the name of the UCS the orthographic systems of paper space are taken from (<c>$PUCSBASE</c>), as AutoCAD's <c>PucsBase</c> gives its id; null, the world, where the header names none.</summary>
        /// <value>Set, the name of a record of the UCS table, or null for the world.</value>
        /// <exception cref="ArgumentException">Thrown when the UCS table has no record of the name set.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public string PucsBase
        {
            get => Name("$PUCSBASE", 2, null);
            set => PutUcsName("$PUCSBASE", value, 1015);
        }

        /// <summary>Gets which orthographic system the current UCS of model space is (<c>$UCSORTHOVIEW</c>), as AutoCAD's <c>UcsOrthographic</c>; <see cref="DxfOrthographicView.NonOrthoView"/> where the header gives none.</summary>
        public DxfOrthographicView UcsOrthographic => (DxfOrthographicView)Whole("$UCSORTHOVIEW", 70, 0);

        /// <summary>Gets which orthographic system the current UCS of paper space is (<c>$PUCSORTHOVIEW</c>), as AutoCAD's <c>PucsOrthographic</c>; <see cref="DxfOrthographicView.NonOrthoView"/> where the header gives none.</summary>
        public DxfOrthographicView PucsOrthographic => (DxfOrthographicView)Whole("$PUCSORTHOVIEW", 70, 0);

        /// <summary>
        /// Gets the origin of an orthographic UCS of model space when the base UCS is the world (<c>$UCSORGTOP</c> to
        /// <c>$UCSORGBACK</c>), as AutoCAD's <c>WorldUcsBaseOrigin</c>; the origin where the header gives none.
        /// </summary>
        /// <param name="orthoView">The orthographic system, top to right.</param>
        /// <returns>The origin, in world coordinates.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the system is <see cref="DxfOrthographicView.NonOrthoView"/> or not one of <see cref="DxfOrthographicView"/>.</exception>
        public GeoPoint3 WorldUcsBaseOrigin(DxfOrthographicView orthoView) => Point("$UCSORG" + Side(orthoView, nameof(orthoView)));

        /// <summary>Sets the origin of an orthographic UCS of model space when the base UCS is the world, as AutoCAD's <c>SetWorldUcsBaseOrigin</c>: <c>$UCSORGTOP</c> for the top.</summary>
        /// <param name="origin">The origin, in world coordinates.</param>
        /// <param name="orthoView">The orthographic system, top to right.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate of the origin is not a finite number.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the system is <see cref="DxfOrthographicView.NonOrthoView"/> or not one of <see cref="DxfOrthographicView"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the origin changes in a file older than R2000 (AC1015), which has no such variables.</exception>
        public void SetWorldUcsBaseOrigin(GeoPoint3 origin, DxfOrthographicView orthoView) => PutBaseOrigin("$UCSORG" + Side(orthoView, nameof(orthoView)), origin);

        /// <summary>Gets the origin of an orthographic UCS of paper space when the base UCS is the world (<c>$PUCSORGTOP</c> to <c>$PUCSORGBACK</c>), as AutoCAD's <c>WorldPaperspaceUcsBaseOrigin</c>; the origin where the header gives none.</summary>
        /// <param name="orthoView">The orthographic system, top to right.</param>
        /// <returns>The origin, in world coordinates.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the system is <see cref="DxfOrthographicView.NonOrthoView"/> or not one of <see cref="DxfOrthographicView"/>.</exception>
        public GeoPoint3 WorldPaperspaceUcsBaseOrigin(DxfOrthographicView orthoView) => Point("$PUCSORG" + Side(orthoView, nameof(orthoView)));

        /// <summary>Sets the origin of an orthographic UCS of paper space when the base UCS is the world, as AutoCAD's <c>SetWorldPaperspaceUcsBaseOrigin</c>: <c>$PUCSORGTOP</c> for the top.</summary>
        /// <param name="origin">The origin, in world coordinates.</param>
        /// <param name="orthoView">The orthographic system, top to right.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate of the origin is not a finite number.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the system is <see cref="DxfOrthographicView.NonOrthoView"/> or not one of <see cref="DxfOrthographicView"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the origin changes in a file older than R2000 (AC1015), which has no such variables.</exception>
        public void SetWorldPaperspaceUcsBaseOrigin(GeoPoint3 origin, DxfOrthographicView orthoView) => PutBaseOrigin("$PUCSORG" + Side(orthoView, nameof(orthoView)), origin);

        /// <summary>The end of the name of the variable of an orthographic system: TOP for the top.</summary>
        private static string Side(DxfOrthographicView view, string parameter)
        {
            switch (view)
            {
                case DxfOrthographicView.TopView: return "TOP";
                case DxfOrthographicView.BottomView: return "BOTTOM";
                case DxfOrthographicView.FrontView: return "FRONT";
                case DxfOrthographicView.BackView: return "BACK";
                case DxfOrthographicView.LeftView: return "LEFT";
                case DxfOrthographicView.RightView: return "RIGHT";
                default: throw new ArgumentOutOfRangeException(parameter, view, "An orthographic system is the top, bottom, front, back, left or right.");
            }
        }

        private void PutBaseOrigin(string name, GeoPoint3 origin)
        {
            if (!origin.IsValid)
            {
                throw new ArgumentException("A point has to be of finite numbers: " + origin + " is not.", nameof(origin));
            }

            GeoPoint3 now = Point(name);

            if (!(Same(origin.X, now.X) && Same(origin.Y, now.Y) && Same(origin.Z, now.Z)))
            {
                Since(1015, name);
                SetPoint(name, origin);
            }
        }

        /// <summary>A direction of the header: its groups 10, 20 and 30, nought for one not given; the fallback where none is given.</summary>
        private GeoVector3 Direction(string name, GeoVector3 fallback)
        {
            double[] values = new double[3];
            bool any = false;

            for (int i = 0; i < 3; i++)
            {
                bool given = Numbers.TryRead(First(name, 10 + 10 * i), out double value) && Guard.IsFinite(value);
                values[i] = given ? value : 0.0;
                any |= given;
            }

            return any ? new GeoVector3(values[0], values[1], values[2]) : fallback;
        }

        private void PutUcs(string prefix, GeoCoordinateSystem3 value)
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("A UCS has to have a finite origin and unit axes at right angles: " + value + " has not.", nameof(value));
            }

            SetPoint(prefix + "ORG", value.Origin);
            PutDirection(prefix + "XDIR", value.XAxis, Direction(prefix + "XDIR", GeoVector3.XAxis));
            PutDirection(prefix + "YDIR", value.YAxis, Direction(prefix + "YDIR", GeoVector3.YAxis));
        }

        private void PutDirection(string name, GeoVector3 value, GeoVector3 now)
        {
            if (!(Same(value.X, now.X) && Same(value.Y, now.Y) && Same(value.Z, now.Z)))
            {
                File.SetHeader(name, new[]
                {
                    new DxfTypedValue(10, Numbers.Exact(value.X + 0.0)),
                    new DxfTypedValue(20, Numbers.Exact(value.Y + 0.0)),
                    new DxfTypedValue(30, Numbers.Exact(value.Z + 0.0)),
                });
            }
        }

        private void PutUcsName(string name, string value, int since)
        {
            string now = Name(name, 2, null);

            if (string.Equals(value ?? string.Empty, now ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            if (!string.IsNullOrEmpty(value) && !UcsListed(value))
            {
                throw new ArgumentException("The UCS table has no record named " + value + ".", nameof(value));
            }

            Since(since, name);
            Set(name, 2, value ?? string.Empty);
        }

        /// <summary>Whether the UCS table has a record of a name, letters of either case the same.</summary>
        private bool UcsListed(string name)
        {
            foreach (DxfItem item in File.TableItems("UCS"))
            {
                if (!item.IsRemoved && string.Equals(DxfReader.NameOf(File.ValueOf(item, 2) ?? string.Empty), name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
