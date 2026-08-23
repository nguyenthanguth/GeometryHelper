using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using PlaneGeometry.Geometry;

[assembly: CommandClass(typeof(ArrangeAlgorithms.CadTest.ArrangeCommands))]

namespace ArrangeAlgorithms.CadTest
{
    /// <summary>
    /// Test runner command for label arrangement algorithms in AutoCAD.
    /// Scans and selects GeoLine2 and LWPOLYLINE entities, assumes each object has a corresponding rotated label (GeoRectangle2 OBB),
    /// runs label arrangement to avoid overlaps, and draws the results on the drawing.
    /// </summary>
    public class ArrangeTestRunner
    {
        private const double BoxWidth = 2000.0;
        private const double BoxHeight = 1000.0;

        private const string BoxFromLayer = "BoxFrom";
        private const string BoxToLayer = "BoxTo";
        private const string LineMoveLayer = "LineMove";

        private const short OriginalColour = 8; // Grey: Box at initial assumed position
        private const short PlacedColour = 3; // Green: Found empty space and successfully arranged
        private const short FallbackColour = 1; // Red: Collision encountered, forced to use fallback position
        private const short LeaderColour = 253; // Light grey: Leader connecting from object to the new label

        /// <summary>
        /// Executes common label arrangement logic for all algorithm types.
        /// </summary>
        public void RunArrangeTest(ArrangeAlgorithmType algorithmType, string algorithmName)
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            Editor editor = document.Editor;
            Database database = document.Database;

            try
            {
                PromptSelectionResult selection = editor.GetSelection();
                if (selection.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\nCancelled: no objects selected.");
                    return;
                }

                var arranges = new List<Arrange>();
                var skipped = 0;

                using Transaction transaction = database.TransactionManager.StartTransaction();
                var selectedLines = new List<GeoLine2>();
                var entities = new List<GeoLine2>();

                foreach (ObjectId id in selection.Value.GetObjectIds())
                {
                    DBObject dbObject = transaction.GetObject(id, OpenMode.ForRead);

                    if (dbObject is Entity entity)
                    {
                        if (!TryGetLeaderLine(entity, out GeoLine2 leader))
                        {
                            skipped++;
                            continue;
                        }

                        selectedLines.Add(leader);
                        entities.Add(leader);
                    }
                }

                foreach (GeoLine2 leader in entities)
                {
                    arranges.Add(new Arrange
                    {
                        GeoLine2 = leader,
                        // Create rotated label box along the direction of the guide object
                        GeoRectangle2 = MakeBox(leader, BoxWidth, BoxHeight),
                        BlockPolygons = new List<GeoPolygon2>(),
                        // Avoid all other selected guide segments
                        BlockLines = selectedLines.FindAll(l => !l.Equals(leader))
                    });
                }

                if (arranges.Count == 0)
                {
                    editor.WriteMessage("\nNo valid objects to arrange.");
                    transaction.Commit();
                    return;
                }

                var options = new ArrangeOptions
                {
                    Algorithm = algorithmType
                };

                var stopwatch = Stopwatch.StartNew();
                Arrange.Run(arranges, options);
                stopwatch.Stop();

                DrawResult(database, transaction, arranges, algorithmName, stopwatch.ElapsedMilliseconds);
                transaction.Commit();

                Report(editor, arranges, skipped, stopwatch.ElapsedMilliseconds, algorithmName);
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage(string.Format(
                    CultureInfo.CurrentCulture,
                    "\nError executing label arrangement [{0}]: {1}\nStack Trace: {2}",
                    algorithmName, ex.Message, ex.StackTrace));
            }
        }

        /// <summary>
        /// Extracts geometric guide segment from AutoCAD entity.
        /// For LWPOLYLINE, the longest line segment is taken.
        /// </summary>
        private static bool TryGetLeaderLine(Entity entity, out GeoLine2 leader)
        {
            leader = new GeoLine2(new GeoPoint2(), new GeoPoint2());

            if (entity is Line lineEntity)
            {
                leader = new GeoLine2(
                    lineEntity.StartPoint.X, lineEntity.StartPoint.Y,
                    lineEntity.EndPoint.X, lineEntity.EndPoint.Y);

                return leader.Length > 0.0;
            }

            if (!(entity is Polyline polyline) || polyline.NumberOfVertices < 2)
            {
                return false;
            }

            double longest = 0.0;
            int segmentCount = polyline.Closed ? polyline.NumberOfVertices : polyline.NumberOfVertices - 1;

            for (int i = 0; i < segmentCount; i++)
            {
                Point2d start = polyline.GetPoint2dAt(i);
                Point2d end = polyline.GetPoint2dAt((i + 1) % polyline.NumberOfVertices);

                var candidate = new GeoLine2(start.X, start.Y, end.X, end.Y);
                if (candidate.Length > longest)
                {
                    longest = candidate.Length;
                    leader = candidate;
                }
            }

            return longest > 0.0;
        }

        /// <summary>
        /// Creates a rotated rectangle OBB parallel to the guide line segment direction,
        /// with its center at the midpoint of the guide segment.
        /// </summary>
        private static GeoRectangle2 MakeBox(GeoLine2 leader, double width, double height)
        {
            GeoPoint2 centre = leader.MidPoint;

            if (!leader.Direction.TryGetNormal(out GeoVector2 along))
            {
                along = GeoVector2.XAxis;
            }

            double angleRad = Math.Atan2(along.Y, along.X);
            return new GeoRectangle2(centre, width, height, angleRad);
        }

        private static void DrawResult(
            Database database, Transaction transaction, List<Arrange> arranges, string algorithmName, long elapsedMilliseconds)
        {
            ObjectId boxFromLayerId = EnsureLayer(database, transaction, BoxFromLayer);
            ObjectId boxToLayerId = EnsureLayer(database, transaction, BoxToLayer);
            ObjectId lineMoveLayerId = EnsureLayer(database, transaction, LineMoveLayer);
            ObjectId connectionLayerId = EnsureLayer(database, transaction, "connection");

            var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            var space = (BlockTableRecord)transaction.GetObject(
                blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            var createdEntities = new List<Entity>();
            double maxConnectionLength = 0.0;
            double sumConnectionLength = 0.0;

            for (int i = 0; i < arranges.Count; i++)
            {
                Arrange arrange = arranges[i];

                // Draw assumed box at initial position (grey)
                Polyline original = ToAcadPolyline(arrange.GeoRectangle2);
                original.LayerId = boxFromLayerId;
                original.ColorIndex = OriginalColour;

                space.AppendEntity(original);
                transaction.AddNewlyCreatedDBObject(original, true);
                createdEntities.Add(original);

                // Translate label box according to result translation GeoVector2
                GeoRectangle2 movedRect = new GeoRectangle2(
                    arrange.GeoRectangle2.Center + arrange.TranslationVector,
                    arrange.GeoRectangle2.Width,
                    arrange.GeoRectangle2.Height,
                    arrange.GeoRectangle2.AngleRad);

                // Draw box after arrangement (green if successful, red if overlapped/failed)
                Polyline moved = ToAcadPolyline(movedRect);
                moved.LayerId = boxToLayerId;
                moved.ColorIndex = arrange.Placed ? PlacedColour : FallbackColour;

                space.AppendEntity(moved);
                transaction.AddNewlyCreatedDBObject(moved, true);
                createdEntities.Add(moved);

                // Draw leader connecting from guide segment midpoint to new label center
                var leader = new Line(
                    new Point3d(arrange.GeoLine2.MidPoint.X, arrange.GeoLine2.MidPoint.Y, 0.0),
                    new Point3d(movedRect.Center.X, movedRect.Center.Y, 0.0))
                {
                    LayerId = lineMoveLayerId,
                    ColorIndex = LeaderColour
                };

                space.AppendEntity(leader);
                transaction.AddNewlyCreatedDBObject(leader, true);
                createdEntities.Add(leader);

                // Draw connection line from BoxTo (new label center) to the closest point on GeoLine2
                GeoPoint2 closestOnLine = arrange.GeoLine2.GetClosestPointOnBoundary(movedRect.Center);
                var connection = new Line(
                    new Point3d(movedRect.Center.X, movedRect.Center.Y, 0.0),
                    new Point3d(closestOnLine.X, closestOnLine.Y, 0.0))
                {
                    LayerId = connectionLayerId,
                    ColorIndex = 4 // Cyan
                };

                space.AppendEntity(connection);
                transaction.AddNewlyCreatedDBObject(connection, true);
                createdEntities.Add(connection);

                double connLen = movedRect.Center.DistanceTo(closestOnLine);
                sumConnectionLength += connLen;
                if (connLen > maxConnectionLength)
                {
                    maxConnectionLength = connLen;
                }
            }

            if (createdEntities.Count > 0)
            {
                Extents3d totalExtents = new Extents3d();
                bool first = true;

                foreach (Entity ent in createdEntities)
                {
                    try
                    {
                        Extents3d ext = ent.GeometricExtents;
                        if (first)
                        {
                            totalExtents = ext;
                            first = false;
                        }
                        else
                        {
                            totalExtents.AddExtents(ext);
                        }
                    }
                    catch
                    {
                        // Ignore if entity cannot get Extents
                    }
                }

                if (!first)
                {
                    double minX = totalExtents.MinPoint.X;
                    double maxX = totalExtents.MaxPoint.X;
                    double minY = totalExtents.MinPoint.Y;

                    double centerX = (minX + maxX) * 0.5;
                    double textY = minY - 2000.0;

                    int placedCount = 0;
                    for (int i = 0; i < arranges.Count; i++)
                    {
                        if (arranges[i].Placed) placedCount++;
                    }

                    double avgConnectionLength = arranges.Count > 0 ? sumConnectionLength / arranges.Count : 0.0;

                    var dbText = new DBText
                    {
                        TextString = $"{algorithmName} ({placedCount}/{arranges.Count} placed, {elapsedMilliseconds} ms) - Max Conn: {maxConnectionLength:F1}, Avg Conn: {avgConnectionLength:F1}",
                        Height = 800.0,
                        LayerId = boxToLayerId,
                        ColorIndex = PlacedColour
                    };
                    dbText.Justify = AttachmentPoint.BaseCenter;
                    dbText.AlignmentPoint = new Point3d(centerX, textY, 0.0);

                    space.AppendEntity(dbText);
                    transaction.AddNewlyCreatedDBObject(dbText, true);
                }
            }
        }

        /// <summary>
        /// Converts rotated rectangle GeoRectangle2 into a closed 4-vertex AutoCAD lwpolyline.
        /// </summary>
        private static Polyline ToAcadPolyline(GeoRectangle2 rect)
        {
            var result = new Polyline();
            var vertices = rect.GetVertices(); // Returns 4 vertices array: LowerLeft, LowerRight, UpperRight, UpperLeft

            for (int i = 0; i < vertices.Length; i++)
            {
                result.AddVertexAt(i, new Point2d(vertices[i].X, vertices[i].Y), 0.0, 0.0, 0.0);
            }

            result.Closed = true;
            return result;
        }

        /// <summary>
        /// Ensures layer exists in drawing, automatically creates it if not present.
        /// </summary>
        private static ObjectId EnsureLayer(Database database, Transaction transaction, string name)
        {
            var table = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
            if (table.Has(name))
            {
                return table[name];
            }

            table.UpgradeOpen();
            var record = new LayerTableRecord { Name = name };
            ObjectId id = table.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);

            return id;
        }

        /// <summary>
        /// Prints statistics report of arrangement results to AutoCAD command line.
        /// </summary>
        private static void Report(Editor editor, List<Arrange> arranges, int skipped, long milliseconds, string algorithmName)
        {
            int placed = 0;
            foreach (Arrange arrange in arranges)
            {
                if (arrange.Placed)
                {
                    placed++;
                }
            }

            editor.WriteMessage(string.Format(
                CultureInfo.CurrentCulture,
                "\nArranged {0} labels using [{1}] algorithm in {2} ms." +
                "\n  Placed         : {3}" +
                "\n  Fallback (red) : {4}" +
                "\n  Skipped        : {5}" +
                "\nLayer {6}: box {7}x{8} at initial assumed position (grey)." +
                "\nLayer {9}: box after arrangement (green/red)." +
                "\nLayer {10}: connection line showing movement.",
                arranges.Count, algorithmName, milliseconds, placed, arranges.Count - placed, skipped,
                BoxFromLayer, BoxWidth, BoxHeight, BoxToLayer, LineMoveLayer));
        }
    }
}
