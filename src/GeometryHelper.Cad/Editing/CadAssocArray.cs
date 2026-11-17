using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>The three kinds of associative array AutoCAD 2021 makes: by rows and columns, round a centre, or along a path curve.</summary>
    internal enum CadArrayKind
    {
        /// <summary>ARRAYRECT: columns along the X axis of the array, rows along its Y axis, levels along Z.</summary>
        Rectangular,

        /// <summary>ARRAYPOLAR: items round a centre, rows outward from it, levels along Z.</summary>
        Polar,

        /// <summary>ARRAYPATH: items along a path curve, rows to the left of the way it runs, levels along Z.</summary>
        Path,
    }

    /// <summary>One item of an array: its place in the array and the transformation that takes the source objects, as the anonymous block holds them, to it.</summary>
    internal sealed class CadArrayItem
    {
        /// <summary>Gets or sets the index along the columns, round the centre or along the path.</summary>
        internal int Index { get; set; }

        /// <summary>Gets or sets the row.</summary>
        internal int Row { get; set; }

        /// <summary>Gets or sets the level.</summary>
        internal int Level { get; set; }

        /// <summary>Gets or sets the transformation from the source block to the item, in the frame of the array's INSERT.</summary>
        internal GeoTransform3 Matrix { get; set; }

        /// <summary>Gets or sets the handle of the INSERT in the array's block that draws the item.</summary>
        internal string Handle { get; set; }

        /// <summary>Whether the matrix only moves: AutoCAD stores such an item as a vector, the others as a 4 by 4 matrix.</summary>
        internal bool IsTranslation =>
            Math.Abs(Matrix[0, 0] - 1.0) <= 1E-12
            && Math.Abs(Matrix[1, 1] - 1.0) <= 1E-12
            && Math.Abs(Matrix[2, 2] - 1.0) <= 1E-12
            && Math.Abs(Matrix[0, 1]) <= 1E-12
            && Math.Abs(Matrix[0, 2]) <= 1E-12
            && Math.Abs(Matrix[1, 0]) <= 1E-12
            && Math.Abs(Matrix[1, 2]) <= 1E-12
            && Math.Abs(Matrix[2, 0]) <= 1E-12
            && Math.Abs(Matrix[2, 1]) <= 1E-12;
    }

    /// <summary>
    /// The parameters of one associative array as AutoCAD 2021 keeps them (an <c>AcDbAssocArray*Parameters</c> held in an
    /// action of the drawing's associative network) and the items they make. The array's INSERT stands at <see cref="Origin"/>:
    /// the base point of a rectangular array, the centre of a polar one, the origin of the drawing for a path array; the
    /// source objects are held in an anonymous block as they lie from the <see cref="BasePoint"/>, and each item is the source
    /// block taken by a matrix in the frame of the INSERT. A rectangular array of 3 columns 20 apart and 2 rows 30 apart has
    /// six items, the second at (0, 30).
    /// </summary>
    internal sealed class CadAssocArray
    {
        /// <summary>Gets or sets the kind.</summary>
        internal CadArrayKind Kind { get; set; }

        /// <summary>Gets or sets where the array's INSERT stands.</summary>
        internal GeoPoint3 Origin { get; set; }

        /// <summary>Gets or sets the base point of the source objects: the point of them that stands where the first item is.</summary>
        internal GeoPoint3 BasePoint { get; set; }

        /// <summary>Gets or sets the number of rows.</summary>
        internal int Rows { get; set; } = 1;

        /// <summary>Gets or sets the distance between rows.</summary>
        internal double RowSpacing { get; set; } = 1.0;

        /// <summary>Gets or sets how much higher each row is than the one before.</summary>
        internal double RowElevation { get; set; }

        /// <summary>Gets or sets the number of levels.</summary>
        internal int Levels { get; set; } = 1;

        /// <summary>Gets or sets the distance between levels.</summary>
        internal double LevelSpacing { get; set; } = 1.0;

        /// <summary>Gets or sets the number of columns of a rectangular array.</summary>
        internal int Columns { get; set; } = 1;

        /// <summary>Gets or sets the distance between columns.</summary>
        internal double ColumnSpacing { get; set; } = 1.0;

        /// <summary>Gets or sets the angle between the row axis and the column axis, a right angle unless the array was made otherwise.</summary>
        internal double AxesAngle { get; set; } = Math.PI / 2.0;

        /// <summary>Gets or sets the angle of the column axis in the plan, the row axis angle of ARRAYRECT.</summary>
        internal double RowAngle { get; set; }

        /// <summary>Gets or sets the number of items round the centre or along the path.</summary>
        internal int Items { get; set; } = 1;

        /// <summary>Gets or sets the angle between the items of a polar array, always above nought; <see cref="Clockwise"/> says which way.</summary>
        internal double Between { get; set; }

        /// <summary>Gets or sets whether a polar array runs clockwise.</summary>
        internal bool Clockwise { get; set; }

        /// <summary>Gets or sets whether the items of a polar array are turned with it.</summary>
        internal bool Rotate { get; set; } = true;

        /// <summary>Gets or sets the distance between the items along the path, or the share of it for Divide.</summary>
        internal double Spacing { get; set; } = 1.0;

        /// <summary>Gets the angle a polar array fills: a whole turn its items share, else as far as its last item.</summary>
        internal double Fill
        {
            get
            {
                double whole = Between * Items;
                return Math.Abs(whole - 2.0 * Math.PI) <= 1E-9 ? whole : Between * Math.Max(0, Items - 1);
            }
        }

        /// <summary>Gets or sets the path method: Divide shares the path among the items, Measure sets them a distance apart.</summary>
        internal bool Divide { get; set; }

        /// <summary>Gets or sets whether the items begin at the end of the path nearest where it was picked.</summary>
        internal bool FromEnd { get; set; }

        /// <summary>Gets or sets whether the items of a path array are turned to follow the path.</summary>
        internal bool Align { get; set; } = true;

        /// <summary>Gets or sets whether the items of a path array keep their Z direction.</summary>
        internal bool MaintainZ { get; set; } = true;

        /// <summary>Gets or sets how the items of a path array are turned.</summary>
        internal CadPathTangent Tangent { get; set; }

        /// <summary>Gets or sets the angle of the direction the user gave for <see cref="CadPathTangent.Vector"/>.</summary>
        internal double TangentAngle { get; set; }

        /// <summary>Gets or sets the path curve of a path array.</summary>
        internal DxfCurve Path { get; set; }

        /// <summary>Gets or sets the length of the path.</summary>
        internal double PathLength { get; set; }

        /// <summary>Gets or sets whether the path is closed.</summary>
        internal bool PathClosed { get; set; }

        /// <summary>The most items an array holds: AutoCAD 2021 refuses a larger one, which would also fill the drawing's memory with INSERTs.</summary>
        internal const int MostItems = 100000;

        /// <summary>Gets the number of items the parameters make: columns, items round the centre or along the path, by rows by levels; a path array counts the items its path holds.</summary>
        internal long TotalItems
        {
            get
            {
                long across =
                    Kind == CadArrayKind.Rectangular ? Columns
                    : Kind == CadArrayKind.Polar ? Items
                    : Path == null ? Items
                    : Math.Min(Items, MostAlongPath(Spacing));
                return Math.Max(1L, across) * Math.Max(1, Rows) * Math.Max(1, Levels);
            }
        }

        /// <summary>Gets the radius of a polar array: the distance from its centre to its base point.</summary>
        internal double Radius => Math.Sqrt(Dx * Dx + Dy * Dy);

        /// <summary>Gets the angle from the centre of a polar array to its base point.</summary>
        internal double StartAngle => Math.Atan2(Dy, Dx);

        private double Dx => BasePoint.X - Origin.X;

        private double Dy => BasePoint.Y - Origin.Y;

        /// <summary>
        /// The most items a Measure distance holds along a path, as <see cref="CadPathArray.MaxItems"/> says it.
        /// </summary>
        internal int MostAlongPath(double spacing) => PathArray().MaxItems(spacing);

        /// <summary>The path array that places the items of a path: the parameters here handed to the object that works them out.</summary>
        internal CadPathArray PathArray()
        {
            if (Path == null)
            {
                throw new InvalidOperationException("The array has no path curve.");
            }

            return new CadPathArray(Path, PathLength, PathClosed, FromEnd)
            {
                Divide = Divide,
                Spacing = Spacing,
                Items = Items,
                Base = BasePoint,
                Tangent = Tangent,
                TangentAngle = TangentAngle,
                Align = Align,
                Rows = Rows,
                RowSpacing = RowSpacing,
                Elevation = RowElevation,
                Levels = Levels,
                LevelSpacing = LevelSpacing,
            };
        }

        /// <summary>
        /// The angle the source objects are turned by before they are held in the anonymous block: of a path array whose items
        /// follow the path as its first item stands, the turn back from the way the path runs there, so that the first item
        /// is not turned; nought for every other array.
        /// </summary>
        internal double SourceTurn()
        {
            if (Kind != CadArrayKind.Path || Tangent != CadPathTangent.Path || Path == null)
            {
                return 0.0;
            }

            return -FirstAngle();
        }

        /// <summary>The angle of the way the path runs at the first item.</summary>
        internal double FirstAngle()
        {
            CadPathArray array = PathArray();
            double at = FromEnd ? PathLength : 0.0;
            at = Math.Min(Math.Max(at, 0.0), PathLength);
            GeoVector3 along = Path.GetFirstDerivative(Path.GetParameterAtDistance(at));
            double flat = Math.Sqrt(along.X * along.X + along.Y * along.Y);

            if (!(flat > 1E-12))
            {
                return 0.0;
            }

            return FromEnd ? Math.Atan2(-along.Y, -along.X) : Math.Atan2(along.Y, along.X);
        }

        /// <summary>
        /// The items in order (along the columns, round the centre or along the path first, then rows, then levels), each with
        /// the matrix that takes the source block to it in the frame of the array's INSERT. A rectangular array of 3 columns
        /// and 2 rows has six; a polar one of 6 items 60 degrees apart has an item at 60 degrees whose matrix turns by 60.
        /// </summary>
        internal List<CadArrayItem> MakeItems()
        {
            switch (Kind)
            {
                case CadArrayKind.Rectangular:
                    return RectangularItems();
                case CadArrayKind.Polar:
                    return PolarItems();
                default:
                    return PathItems();
            }
        }

        private List<CadArrayItem> RectangularItems()
        {
            var items = new List<CadArrayItem>();
            double ux = Math.Cos(RowAngle);
            double uy = Math.Sin(RowAngle);
            double vx = Math.Cos(RowAngle + AxesAngle);
            double vy = Math.Sin(RowAngle + AxesAngle);

            for (int i = 0; i < Math.Max(1, Columns); i++)
            {
                for (int j = 0; j < Math.Max(1, Rows); j++)
                {
                    for (int k = 0; k < Math.Max(1, Levels); k++)
                    {
                        double x = i * ColumnSpacing * ux + j * RowSpacing * vx;
                        double y = i * ColumnSpacing * uy + j * RowSpacing * vy;
                        double z = j * RowElevation + k * LevelSpacing;
                        items.Add(
                            new CadArrayItem
                            {
                                Index = i,
                                Row = j,
                                Level = k,
                                Matrix = GeoTransform3.Translation(new GeoVector3(x, y, z)),
                            }
                        );
                    }
                }
            }

            return items;
        }

        private List<CadArrayItem> PolarItems()
        {
            var items = new List<CadArrayItem>();
            double radius = Radius;
            double ux = radius > 1E-9 ? Dx / radius : 1.0;
            double uy = radius > 1E-9 ? Dy / radius : 0.0;
            double sign = Clockwise ? -1.0 : 1.0;

            for (int i = 0; i < Math.Max(1, Items); i++)
            {
                double angle = sign * i * Between;
                double cos = Math.Cos(angle);
                double sin = Math.Sin(angle);

                for (int j = 0; j < Math.Max(1, Rows); j++)
                {
                    double px = Dx + ux * j * RowSpacing;
                    double py = Dy + uy * j * RowSpacing;
                    double x = px * cos - py * sin;
                    double y = px * sin + py * cos;

                    for (int k = 0; k < Math.Max(1, Levels); k++)
                    {
                        double z = j * RowElevation + k * LevelSpacing;
                        GeoTransform3 matrix = Rotate
                            ? new GeoTransform3(
                                new[,]
                                {
                                    { cos, -sin, 0.0, x },
                                    { sin, cos, 0.0, y },
                                    { 0.0, 0.0, 1.0, z },
                                    { 0.0, 0.0, 0.0, 1.0 },
                                }
                            )
                            : GeoTransform3.Translation(new GeoVector3(x, y, z));
                        items.Add(
                            new CadArrayItem
                            {
                                Index = i,
                                Row = j,
                                Level = k,
                                Matrix = matrix,
                            }
                        );
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// The items of a path array: the moves <see cref="CadPathArray.Moves"/> works out for the objects where they lie in the
        /// drawing, put in the frame of the source block, which holds them from the base point and turned by
        /// <see cref="SourceTurn"/>: an item takes the base point to its place and turns by what the path turns past the first.
        /// </summary>
        private List<CadArrayItem> PathItems()
        {
            var items = new List<CadArrayItem>();
            CadPathArray path = PathArray();
            List<GeoTransform3> moves = path.Moves();
            GeoTransform3 back = GeoTransform3.RotationZ(-SourceTurn());
            int rows = Math.Max(1, Rows);
            int levels = Math.Max(1, Levels);
            GeoPoint3 start = path.Base ?? FirstPoint();
            int at = 0;

            for (int i = 0; at < moves.Count; i++)
            {
                for (int j = 0; j < rows; j++)
                {
                    for (int k = 0; k < levels; k++)
                    {
                        if (at >= moves.Count)
                        {
                            break;
                        }

                        GeoTransform3 world = moves[at++];
                        GeoPoint3 to = world.Transform(start);
                        GeoTransform3 turn = world * back;
                        var matrix = new double[4, 4];

                        for (int r = 0; r < 3; r++)
                        {
                            for (int c = 0; c < 3; c++)
                            {
                                matrix[r, c] = turn[r, c];
                            }
                        }

                        matrix[0, 3] = to.X;
                        matrix[1, 3] = to.Y;
                        matrix[2, 3] = to.Z;
                        matrix[3, 3] = 1.0;
                        items.Add(
                            new CadArrayItem
                            {
                                Index = i,
                                Row = j,
                                Level = k,
                                Matrix = new GeoTransform3(matrix),
                            }
                        );
                    }
                }
            }

            return items;
        }

        /// <summary>The point of the path where the items begin.</summary>
        internal GeoPoint3 FirstPoint() => Path.GetPointAtDist(FromEnd ? PathLength : 0.0);
    }
}
