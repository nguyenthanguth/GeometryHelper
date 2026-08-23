using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using PlaneGeometry.Core;
using PlaneGeometry.Geometry;

namespace ArrangeAlgorithms.CadTest
{
    public interface ISplitHandler
    {
        bool Execute(DBObject subject, object cutter, BlockTableRecord modelSpace, Transaction tr, Editor ed);
    }

    // 1. Line split by Point
    public class LineSplitByPointHandler : ISplitHandler
    {
        public bool Execute(DBObject subject, object cutter, BlockTableRecord modelSpace, Transaction tr, Editor ed)
        {
            if (subject is Line line && cutter is GeoPoint2 point)
            {
                ed.WriteMessage($"\n  [Handler] Đang xử lý: Cắt Line (Chiều dài gốc: {line.Length:F2}) bằng Điểm ({point.X:F2}, {point.Y:F2}).");
                GeoLine2 geoLine = line.ToGeoLine();

                ed.WriteMessage("\n  [Handler] Đang tính toán vị trí cắt...");
                if (Splition2.TrySplitBy(geoLine, point, out GeoLine2 first, out GeoLine2 second))
                {
                    Line acadFirst = first.ToAcadLine();
                    Line acadSecond = second.ToAcadLine();

                    Database db = modelSpace.Database;
                    acadFirst.LayerId = SplitTestRunner.EnsureLayer(db, tr, "Split_Part1");
                    acadSecond.LayerId = SplitTestRunner.EnsureLayer(db, tr, "Split_Part2");

                    acadFirst.ColorIndex = 1; // Red
                    acadSecond.ColorIndex = 3; // Green

                    modelSpace.AppendEntity(acadFirst);
                    tr.AddNewlyCreatedDBObject(acadFirst, true);

                    modelSpace.AppendEntity(acadSecond);
                    tr.AddNewlyCreatedDBObject(acadSecond, true);

                    line.Erase();
                    ed.WriteMessage($"\n  [Handler] Cắt thành công! Đoạn 1 (Màu đỏ, Layer: Split_Part1) dài: {first.Length:F2}, Đoạn 2 (Màu xanh, Layer: Split_Part2) dài: {second.Length:F2}.");
                    return true;
                }
                else
                {
                    ed.WriteMessage("\n  [Handler] Cắt thất bại: Điểm chọn không nằm trên đường thẳng (Line) hoặc trùng với điểm đầu/cuối.");
                }
            }
            else
            {
                ed.WriteMessage("\n  [Handler] Lỗi: Đối tượng chính không phải Line hoặc điểm cắt không hợp lệ.");
            }
            return false;
        }
    }

    // 2. Polyline split by Point
    public class PolylineSplitByPointHandler : ISplitHandler
    {
        public bool Execute(DBObject subject, object cutter, BlockTableRecord modelSpace, Transaction tr, Editor ed)
        {
            if (subject is Polyline polyline && cutter is GeoPoint2 point)
            {
                ed.WriteMessage($"\n  [Handler] Đang xử lý: Cắt Polyline (Chiều dài gốc: {polyline.Length:F2}) bằng Điểm ({point.X:F2}, {point.Y:F2}).");
                GeoPolyline2 geoPolyline = polyline.ToGeoPolyline();

                ed.WriteMessage("\n  [Handler] Đang tính toán vị trí cắt...");
                if (Splition2.TrySplitBy(geoPolyline, point, out GeoPolyline2 first, out GeoPolyline2 second))
                {
                    Polyline acadFirst = first.ToAcadPolyline();
                    Polyline acadSecond = second.ToAcadPolyline();

                    Database db = modelSpace.Database;
                    acadFirst.LayerId = SplitTestRunner.EnsureLayer(db, tr, "Split_Part1");
                    acadSecond.LayerId = SplitTestRunner.EnsureLayer(db, tr, "Split_Part2");

                    acadFirst.ColorIndex = 1; // Red
                    acadSecond.ColorIndex = 3; // Green

                    modelSpace.AppendEntity(acadFirst);
                    tr.AddNewlyCreatedDBObject(acadFirst, true);

                    modelSpace.AppendEntity(acadSecond);
                    tr.AddNewlyCreatedDBObject(acadSecond, true);

                    polyline.Erase();
                    ed.WriteMessage($"\n  [Handler] Cắt thành công! Đoạn 1 (Màu đỏ, Layer: Split_Part1) dài: {first.Length:F2}, Đoạn 2 (Màu xanh, Layer: Split_Part2) dài: {second.Length:F2}.");
                    return true;
                }
                else
                {
                    ed.WriteMessage("\n  [Handler] Cắt thất bại: Điểm chọn không nằm trên Polyline hoặc trùng với các điểm nút.");
                }
            }
            else
            {
                ed.WriteMessage("\n  [Handler] Lỗi: Đối tượng chính không phải Polyline hoặc điểm cắt không hợp lệ.");
            }
            return false;
        }
    }

    // 3. Line split by Line
    public class LineSplitByLineHandler : ISplitHandler
    {
        public bool Execute(DBObject subject, object cutter, BlockTableRecord modelSpace, Transaction tr, Editor ed)
        {
            if (subject is Line line && cutter is GeoLine2 cutterLine)
            {
                ed.WriteMessage($"\n  [Handler] Đang xử lý: Cắt Line (Chiều dài gốc: {line.Length:F2}) bằng Line cắt.");
                GeoLine2 geoLine = line.ToGeoLine();

                ed.WriteMessage("\n  [Handler] Đang tính toán giao điểm giữa 2 đường thẳng...");
                if (Splition2.TrySplitBy(geoLine, cutterLine, out GeoLine2 first, out GeoLine2 second))
                {
                    Line acadFirst = first.ToAcadLine();
                    Line acadSecond = second.ToAcadLine();

                    Database db = modelSpace.Database;
                    acadFirst.LayerId = SplitTestRunner.EnsureLayer(db, tr, "Split_Part1");
                    acadSecond.LayerId = SplitTestRunner.EnsureLayer(db, tr, "Split_Part2");

                    acadFirst.ColorIndex = 1; // Red
                    acadSecond.ColorIndex = 3; // Green

                    modelSpace.AppendEntity(acadFirst);
                    tr.AddNewlyCreatedDBObject(acadFirst, true);

                    modelSpace.AppendEntity(acadSecond);
                    tr.AddNewlyCreatedDBObject(acadSecond, true);

                    line.Erase();
                    ed.WriteMessage($"\n  [Handler] Cắt thành công! Đoạn 1 (Màu đỏ, Layer: Split_Part1) dài: {first.Length:F2}, Đoạn 2 (Màu xanh, Layer: Split_Part2) dài: {second.Length:F2}.");
                    return true;
                }
                else
                {
                    ed.WriteMessage("\n  [Handler] Cắt thất bại: Hai đường thẳng không giao nhau hoặc giao điểm không nằm trong đoạn thẳng chính.");
                }
            }
            else
            {
                ed.WriteMessage("\n  [Handler] Lỗi: Đối tượng chính không phải Line hoặc đường cắt không hợp lệ.");
            }
            return false;
        }
    }

    // 4. Polyline split by Line
    public class PolylineSplitByLineHandler : ISplitHandler
    {
        public bool Execute(DBObject subject, object cutter, BlockTableRecord modelSpace, Transaction tr, Editor ed)
        {
            if (subject is Polyline polyline && cutter is GeoLine2 cutterLine)
            {
                ed.WriteMessage($"\n  [Handler] Đang xử lý: Cắt Polyline (Chiều dài gốc: {polyline.Length:F2}) bằng Line cắt.");
                GeoPolyline2 geoPolyline = polyline.ToGeoPolyline();

                ed.WriteMessage("\n  [Handler] Đang tính toán giao điểm giữa Polyline và Line...");
                if (geoPolyline.TrySplitBy(cutterLine, out GeoPolyline2[] pieces))
                {
                    Database db = modelSpace.Database;
                    ObjectId layer1 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part1");
                    ObjectId layer2 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part2");

                    for (int i = 0; i < pieces.Length; i++)
                    {
                        Polyline acadPiece = pieces[i].ToAcadPolyline();
                        // Xen kẽ layer và màu để trực quan hóa
                        if (i % 2 == 0)
                        {
                            acadPiece.LayerId = layer1;
                            acadPiece.ColorIndex = 1; // Red
                        }
                        else
                        {
                            acadPiece.LayerId = layer2;
                            acadPiece.ColorIndex = 3; // Green
                        }

                        modelSpace.AppendEntity(acadPiece);
                        tr.AddNewlyCreatedDBObject(acadPiece, true);
                    }

                    polyline.Erase();
                    ed.WriteMessage($"\n  [Handler] Cắt thành công! Đã chia Polyline thành {pieces.Length} phần (Xen kẽ Layer: Split_Part1/Split_Part2 và màu Đỏ/Xanh).");
                    return true;
                }
                else
                {
                    ed.WriteMessage("\n  [Handler] Cắt thất bại: Đường thẳng không giao cắt với Polyline.");
                }
            }
            else
            {
                ed.WriteMessage("\n  [Handler] Lỗi: Đối tượng chính không phải Polyline hoặc đường cắt không hợp lệ.");
            }
            return false;
        }
    }

    // 5. Line split by Polygon
    public class LineSplitByPolygonHandler : ISplitHandler
    {
        public bool Execute(DBObject subject, object cutter, BlockTableRecord modelSpace, Transaction tr, Editor ed)
        {
            if (subject is Line line && cutter is GeoPolygon2 polygon)
            {
                ed.WriteMessage($"\n  [Handler] Đang xử lý: Cắt Line (Chiều dài gốc: {line.Length:F2}) bằng Polygon.");
                GeoLine2 geoLine = line.ToGeoLine();

                ed.WriteMessage("\n  [Handler] Đang tính toán các đoạn cắt nằm trong/ngoài Polygon...");
                if (Splition2.TrySplitBy(geoLine, polygon, out GeoLine2[] inside, out GeoLine2[] outside))
                {
                    Database db = modelSpace.Database;
                    ObjectId insideLayerId = SplitTestRunner.EnsureLayer(db, tr, "Split_Inside");
                    ObjectId outsideLayerId = SplitTestRunner.EnsureLayer(db, tr, "Split_Outside");

                    foreach (var piece in inside)
                    {
                        Line acadPiece = piece.ToAcadLine();
                        acadPiece.LayerId = insideLayerId;
                        acadPiece.ColorIndex = 4; // Cyan (Inside)
                        modelSpace.AppendEntity(acadPiece);
                        tr.AddNewlyCreatedDBObject(acadPiece, true);
                    }

                    foreach (var piece in outside)
                    {
                        Line acadPiece = piece.ToAcadLine();
                        acadPiece.LayerId = outsideLayerId;
                        acadPiece.ColorIndex = 6; // Magenta (Outside)
                        modelSpace.AppendEntity(acadPiece);
                        tr.AddNewlyCreatedDBObject(acadPiece, true);
                    }

                    line.Erase();
                    ed.WriteMessage($"\n  [Handler] Cắt thành công! Số đoạn bên trong Polygon (Màu Cyan, Layer: Split_Inside): {inside.Length}, Số đoạn bên ngoài (Màu Magenta, Layer: Split_Outside): {outside.Length}.");
                    return true;
                }
                else
                {
                    ed.WriteMessage("\n  [Handler] Cắt thất bại: Đường thẳng không giao cắt với biên Polygon (toàn bộ nằm trong hoặc toàn bộ nằm ngoài).");
                }
            }
            else
            {
                ed.WriteMessage("\n  [Handler] Lỗi: Đối tượng chính không phải Line hoặc Polygon cắt không hợp lệ.");
            }
            return false;
        }
    }

    // 6. Polyline split by Polygon
    public class PolylineSplitByPolygonHandler : ISplitHandler
    {
        public bool Execute(DBObject subject, object cutter, BlockTableRecord modelSpace, Transaction tr, Editor ed)
        {
            if (subject is Polyline polyline && cutter is GeoPolygon2 polygon)
            {
                ed.WriteMessage($"\n  [Handler] Đang xử lý: Cắt Polyline (Chiều dài gốc: {polyline.Length:F2}) bằng Polygon.");
                GeoPolyline2 geoPolyline = polyline.ToGeoPolyline();

                ed.WriteMessage("\n  [Handler] Đang tính toán các đoạn cắt nằm trong/ngoài Polygon...");
                if (Splition2.TrySplitBy(geoPolyline, polygon, out GeoPolyline2[] inside, out GeoPolyline2[] outside))
                {
                    Database db = modelSpace.Database;
                    ObjectId insideLayerId = SplitTestRunner.EnsureLayer(db, tr, "Split_Inside");
                    ObjectId outsideLayerId = SplitTestRunner.EnsureLayer(db, tr, "Split_Outside");

                    foreach (var piece in inside)
                    {
                        Polyline acadPiece = piece.ToAcadPolyline();
                        acadPiece.LayerId = insideLayerId;
                        acadPiece.ColorIndex = 4; // Cyan (Inside)
                        modelSpace.AppendEntity(acadPiece);
                        tr.AddNewlyCreatedDBObject(acadPiece, true);
                    }

                    foreach (var piece in outside)
                    {
                        Polyline acadPiece = piece.ToAcadPolyline();
                        acadPiece.LayerId = outsideLayerId;
                        acadPiece.ColorIndex = 6; // Magenta (Outside)
                        modelSpace.AppendEntity(acadPiece);
                        tr.AddNewlyCreatedDBObject(acadPiece, true);
                    }

                    polyline.Erase();
                    ed.WriteMessage($"\n  [Handler] Cắt thành công! Số đoạn bên trong Polygon (Màu Cyan, Layer: Split_Inside): {inside.Length}, Số đoạn bên ngoài (Màu Magenta, Layer: Split_Outside): {outside.Length}.");
                    return true;
                }
                else
                {
                    ed.WriteMessage("\n  [Handler] Cắt thất bại: Polyline không giao cắt với biên Polygon.");
                }
            }
            else
            {
                ed.WriteMessage("\n  [Handler] Lỗi: Đối tượng chính không phải Polyline hoặc Polygon cắt không hợp lệ.");
            }
            return false;
        }
    }


    // 9. Line split by Multiple Points
    public class LineSplitByMultiplePointsHandler : ISplitHandler
    {
        public bool Execute(DBObject subject, object cutter, BlockTableRecord modelSpace, Transaction tr, Editor ed)
        {
            if (subject is Line line && cutter is GeoPoint2[] points)
            {
                ed.WriteMessage($"\n  [Handler] Đang xử lý: Cắt Line bằng danh sách {points.Length} điểm.");
                GeoLine2 geoLine = line.ToGeoLine();

                if (geoLine.TrySplitBy(points, out GeoLine2[] pieces))
                {
                    Database db = modelSpace.Database;
                    ObjectId layer1 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part1");
                    ObjectId layer2 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part2");

                    for (int i = 0; i < pieces.Length; i++)
                    {
                        Line acadPiece = pieces[i].ToAcadLine();
                        if (i % 2 == 0)
                        {
                            acadPiece.LayerId = layer1;
                            acadPiece.ColorIndex = 1; // Red
                        }
                        else
                        {
                            acadPiece.LayerId = layer2;
                            acadPiece.ColorIndex = 3; // Green
                        }

                        modelSpace.AppendEntity(acadPiece);
                        tr.AddNewlyCreatedDBObject(acadPiece, true);
                    }

                    line.Erase();
                    ed.WriteMessage($"\n  [Handler] Cắt thành công! Đã chia Line thành {pieces.Length} phần.");
                    return true;
                }
                else
                {
                    ed.WriteMessage("\n  [Handler] Cắt thất bại: Không có điểm nào nằm trên Line.");
                }
            }
            return false;
        }
    }

    // 10. Line split by Multiple Polygons
    public class LineSplitByMultiplePolygonsHandler : ISplitHandler
    {
        public bool Execute(DBObject subject, object cutter, BlockTableRecord modelSpace, Transaction tr, Editor ed)
        {
            if (subject is Line line && cutter is GeoPolygon2[] polygons)
            {
                ed.WriteMessage($"\n  [Handler] Đang xử lý: Cắt Line bằng danh sách {polygons.Length} Polygon.");
                GeoLine2 geoLine = line.ToGeoLine();

                if (geoLine.TrySplitBy(polygons, out GeoLine2[] inside, out GeoLine2[] outside))
                {
                    Database db = modelSpace.Database;
                    ObjectId layer1 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part1");
                    ObjectId layer2 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part2");

                    foreach (var piece in inside)
                    {
                        Line acadPiece = piece.ToAcadLine();
                        acadPiece.LayerId = layer2;
                        acadPiece.ColorIndex = 3; // Green
                        modelSpace.AppendEntity(acadPiece);
                        tr.AddNewlyCreatedDBObject(acadPiece, true);
                    }

                    foreach (var piece in outside)
                    {
                        Line acadPiece = piece.ToAcadLine();
                        acadPiece.LayerId = layer1;
                        acadPiece.ColorIndex = 1; // Red
                        modelSpace.AppendEntity(acadPiece);
                        tr.AddNewlyCreatedDBObject(acadPiece, true);
                    }

                    line.Erase();
                    ed.WriteMessage($"\n  [Handler] Cắt thành công! Đã chia Line thành {inside.Length} phần trong và {outside.Length} phần ngoài.");
                    return true;
                }
                else
                {
                    ed.WriteMessage("\n  [Handler] Cắt thất bại: Các Polygon không cắt qua Line.");
                }
            }
            return false;
        }
    }

    // 11. Line split by Multiple Polylines
    public class LineSplitByMultiplePolylinesHandler : ISplitHandler
    {
        public bool Execute(DBObject subject, object cutter, BlockTableRecord modelSpace, Transaction tr, Editor ed)
        {
            if (subject is Line line && cutter is GeoPolyline2[] polylines)
            {
                ed.WriteMessage($"\n  [Handler] Đang xử lý: Cắt Line bằng danh sách {polylines.Length} Polyline.");
                GeoLine2 geoLine = line.ToGeoLine();

                if (geoLine.TrySplitBy(polylines, out GeoLine2[] pieces))
                {
                    Database db = modelSpace.Database;
                    ObjectId layer1 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part1");
                    ObjectId layer2 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part2");

                    for (int i = 0; i < pieces.Length; i++)
                    {
                        Line acadPiece = pieces[i].ToAcadLine();
                        if (i % 2 == 0)
                        {
                            acadPiece.LayerId = layer1;
                            acadPiece.ColorIndex = 1; // Red
                        }
                        else
                        {
                            acadPiece.LayerId = layer2;
                            acadPiece.ColorIndex = 3; // Green
                        }

                        modelSpace.AppendEntity(acadPiece);
                        tr.AddNewlyCreatedDBObject(acadPiece, true);
                    }

                    line.Erase();
                    ed.WriteMessage($"\n  [Handler] Cắt thành công! Đã chia Line thành {pieces.Length} phần.");
                    return true;
                }
                else
                {
                    ed.WriteMessage("\n  [Handler] Cắt thất bại: Các Polyline không giao cắt với Line.");
                }
            }
            return false;
        }
    }

    // 7. Line split by Multiple Lines
    public class LineSplitByMultipleLinesHandler : ISplitHandler
    {
        public bool Execute(DBObject subject, object cutter, BlockTableRecord modelSpace, Transaction tr, Editor ed)
        {
            if (subject is Line line && cutter is GeoLine2[] cutters)
            {
                ed.WriteMessage($"\n  [Handler] Đang xử lý: Cắt Line bằng danh sách {cutters.Length} Line.");
                GeoLine2 geoLine = line.ToGeoLine();

                if (geoLine.TrySplitBy(cutters, out GeoLine2[] pieces))
                {
                    Database db = modelSpace.Database;
                    ObjectId layer1 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part1");
                    ObjectId layer2 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part2");

                    for (int i = 0; i < pieces.Length; i++)
                    {
                        Line acadPiece = pieces[i].ToAcadLine();
                        if (i % 2 == 0)
                        {
                            acadPiece.LayerId = layer1;
                            acadPiece.ColorIndex = 1; // Red
                        }
                        else
                        {
                            acadPiece.LayerId = layer2;
                            acadPiece.ColorIndex = 3; // Green
                        }

                        modelSpace.AppendEntity(acadPiece);
                        tr.AddNewlyCreatedDBObject(acadPiece, true);
                    }

                    line.Erase();
                    ed.WriteMessage($"\n  [Handler] Cắt thành công! Đã chia Line thành {pieces.Length} phần.");
                    return true;
                }
                else
                {
                    ed.WriteMessage("\n  [Handler] Cắt thất bại: Không có giao điểm nào hợp lệ.");
                }
            }
            return false;
        }
    }

    // 8. Line split by Polyline
    public class LineSplitByPolylineHandler : ISplitHandler
    {
        public bool Execute(DBObject subject, object cutter, BlockTableRecord modelSpace, Transaction tr, Editor ed)
        {
            if (subject is Line line && cutter is GeoPolyline2 cutterPolyline)
            {
                ed.WriteMessage("\n  [Handler] Đang xử lý: Cắt Line bằng Polyline.");
                GeoLine2 geoLine = line.ToGeoLine();

                if (geoLine.TrySplitBy(cutterPolyline, out GeoLine2[] pieces))
                {
                    Database db = modelSpace.Database;
                    ObjectId layer1 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part1");
                    ObjectId layer2 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part2");

                    for (int i = 0; i < pieces.Length; i++)
                    {
                        Line acadPiece = pieces[i].ToAcadLine();
                        if (i % 2 == 0)
                        {
                            acadPiece.LayerId = layer1;
                            acadPiece.ColorIndex = 1; // Red
                        }
                        else
                        {
                            acadPiece.LayerId = layer2;
                            acadPiece.ColorIndex = 3; // Green
                        }

                        modelSpace.AppendEntity(acadPiece);
                        tr.AddNewlyCreatedDBObject(acadPiece, true);
                    }

                    line.Erase();
                    ed.WriteMessage($"\n  [Handler] Cắt thành công! Đã chia Line thành {pieces.Length} phần.");
                    return true;
                }
                else
                {
                    ed.WriteMessage("\n  [Handler] Cắt thất bại: Polyline không giao cắt với Line.");
                }
            }
            return false;
        }
    }

    // 12. Polyline split by Multiple Points
    public class PolylineSplitByMultiplePointsHandler : ISplitHandler
    {
        public bool Execute(DBObject subject, object cutter, BlockTableRecord modelSpace, Transaction tr, Editor ed)
        {
            if (subject is Polyline polyline && cutter is GeoPoint2[] points)
            {
                ed.WriteMessage($"\n  [Handler] Đang xử lý: Cắt Polyline bằng danh sách {points.Length} điểm.");
                GeoPolyline2 geoPolyline = polyline.ToGeoPolyline();

                if (geoPolyline.TrySplitBy(points, out GeoPolyline2[] pieces))
                {
                    Database db = modelSpace.Database;
                    ObjectId layer1 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part1");
                    ObjectId layer2 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part2");

                    for (int i = 0; i < pieces.Length; i++)
                    {
                        Polyline acadPiece = pieces[i].ToAcadPolyline();
                        if (i % 2 == 0)
                        {
                            acadPiece.LayerId = layer1;
                            acadPiece.ColorIndex = 1; // Red
                        }
                        else
                        {
                            acadPiece.LayerId = layer2;
                            acadPiece.ColorIndex = 3; // Green
                        }

                        modelSpace.AppendEntity(acadPiece);
                        tr.AddNewlyCreatedDBObject(acadPiece, true);
                    }

                    polyline.Erase();
                    ed.WriteMessage($"\n  [Handler] Cắt thành công! Đã chia Polyline thành {pieces.Length} phần.");
                    return true;
                }
                else
                {
                    ed.WriteMessage("\n  [Handler] Cắt thất bại: Không có điểm nào nằm trên Polyline.");
                }
            }
            return false;
        }
    }

    // 13. Polyline split by Multiple Lines
    public class PolylineSplitByMultipleLinesHandler : ISplitHandler
    {
        public bool Execute(DBObject subject, object cutter, BlockTableRecord modelSpace, Transaction tr, Editor ed)
        {
            if (subject is Polyline polyline && cutter is GeoLine2[] cutters)
            {
                ed.WriteMessage($"\n  [Handler] Đang xử lý: Cắt Polyline bằng danh sách {cutters.Length} Line.");
                GeoPolyline2 geoPolyline = polyline.ToGeoPolyline();

                if (geoPolyline.TrySplitBy(cutters, out GeoPolyline2[] pieces))
                {
                    Database db = modelSpace.Database;
                    ObjectId layer1 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part1");
                    ObjectId layer2 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part2");

                    for (int i = 0; i < pieces.Length; i++)
                    {
                        Polyline acadPiece = pieces[i].ToAcadPolyline();
                        if (i % 2 == 0)
                        {
                            acadPiece.LayerId = layer1;
                            acadPiece.ColorIndex = 1; // Red
                        }
                        else
                        {
                            acadPiece.LayerId = layer2;
                            acadPiece.ColorIndex = 3; // Green
                        }

                        modelSpace.AppendEntity(acadPiece);
                        tr.AddNewlyCreatedDBObject(acadPiece, true);
                    }

                    polyline.Erase();
                    ed.WriteMessage($"\n  [Handler] Cắt thành công! Đã chia Polyline thành {pieces.Length} phần.");
                    return true;
                }
                else
                {
                    ed.WriteMessage("\n  [Handler] Cắt thất bại: Các Line không giao cắt với Polyline.");
                }
            }
            return false;
        }
    }

    // 14. Polyline split by Multiple Polylines
    public class PolylineSplitByMultiplePolylinesHandler : ISplitHandler
    {
        public bool Execute(DBObject subject, object cutter, BlockTableRecord modelSpace, Transaction tr, Editor ed)
        {
            if (subject is Polyline polyline && cutter is GeoPolyline2[] polylines)
            {
                ed.WriteMessage($"\n  [Handler] Đang xử lý: Cắt Polyline bằng danh sách {polylines.Length} Polyline.");
                GeoPolyline2 geoPolyline = polyline.ToGeoPolyline();

                if (geoPolyline.TrySplitBy(polylines, out GeoPolyline2[] pieces))
                {
                    Database db = modelSpace.Database;
                    ObjectId layer1 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part1");
                    ObjectId layer2 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part2");

                    for (int i = 0; i < pieces.Length; i++)
                    {
                        Polyline acadPiece = pieces[i].ToAcadPolyline();
                        if (i % 2 == 0)
                        {
                            acadPiece.LayerId = layer1;
                            acadPiece.ColorIndex = 1; // Red
                        }
                        else
                        {
                            acadPiece.LayerId = layer2;
                            acadPiece.ColorIndex = 3; // Green
                        }

                        modelSpace.AppendEntity(acadPiece);
                        tr.AddNewlyCreatedDBObject(acadPiece, true);
                    }

                    polyline.Erase();
                    ed.WriteMessage($"\n  [Handler] Cắt thành công! Đã chia Polyline thành {pieces.Length} phần.");
                    return true;
                }
                else
                {
                    ed.WriteMessage("\n  [Handler] Cắt thất bại: Các Polyline không giao cắt với Polyline.");
                }
            }
            return false;
        }
    }

    // 15. Polyline split by Multiple Polygons
    public class PolylineSplitByMultiplePolygonsHandler : ISplitHandler
    {
        public bool Execute(DBObject subject, object cutter, BlockTableRecord modelSpace, Transaction tr, Editor ed)
        {
            if (subject is Polyline polyline && cutter is GeoPolygon2[] polygons)
            {
                ed.WriteMessage($"\n  [Handler] Đang xử lý: Cắt Polyline bằng danh sách {polygons.Length} Polygon.");
                GeoPolyline2 geoPolyline = polyline.ToGeoPolyline();

                if (geoPolyline.TrySplitBy(polygons, out GeoPolyline2[] inside, out GeoPolyline2[] outside))
                {
                    Database db = modelSpace.Database;
                    ObjectId layer1 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part1");
                    ObjectId layer2 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part2");

                    foreach (var piece in inside)
                    {
                        Polyline acadPiece = piece.ToAcadPolyline();
                        acadPiece.LayerId = layer2;
                        acadPiece.ColorIndex = 3; // Green
                        modelSpace.AppendEntity(acadPiece);
                        tr.AddNewlyCreatedDBObject(acadPiece, true);
                    }

                    foreach (var piece in outside)
                    {
                        Polyline acadPiece = piece.ToAcadPolyline();
                        acadPiece.LayerId = layer1;
                        acadPiece.ColorIndex = 1; // Red
                        modelSpace.AppendEntity(acadPiece);
                        tr.AddNewlyCreatedDBObject(acadPiece, true);
                    }

                    polyline.Erase();
                    ed.WriteMessage($"\n  [Handler] Cắt thành công! Đã chia Polyline thành {inside.Length} phần trong và {outside.Length} phần ngoài.");
                    return true;
                }
                else
                {
                    ed.WriteMessage("\n  [Handler] Cắt thất bại: Các Polygon không giao cắt với Polyline.");
                }
            }
            return false;
        }
    }

    // 16. Polyline split by Polyline
    public class PolylineSplitByPolylineHandler : ISplitHandler
    {
        public bool Execute(DBObject subject, object cutter, BlockTableRecord modelSpace, Transaction tr, Editor ed)
        {
            if (subject is Polyline polyline && cutter is GeoPolyline2 cutterPolyline)
            {
                ed.WriteMessage("\n  [Handler] Đang xử lý: Cắt Polyline bằng Polyline.");
                GeoPolyline2 geoPolyline = polyline.ToGeoPolyline();

                if (geoPolyline.TrySplitBy(new[] { cutterPolyline }, out GeoPolyline2[] pieces))
                {
                    Database db = modelSpace.Database;
                    ObjectId layer1 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part1");
                    ObjectId layer2 = SplitTestRunner.EnsureLayer(db, tr, "Split_Part2");

                    for (int i = 0; i < pieces.Length; i++)
                    {
                        Polyline acadPiece = pieces[i].ToAcadPolyline();
                        if (i % 2 == 0)
                        {
                            acadPiece.LayerId = layer1;
                            acadPiece.ColorIndex = 1; // Red
                        }
                        else
                        {
                            acadPiece.LayerId = layer2;
                            acadPiece.ColorIndex = 3; // Green
                        }

                        modelSpace.AppendEntity(acadPiece);
                        tr.AddNewlyCreatedDBObject(acadPiece, true);
                    }

                    polyline.Erase();
                    ed.WriteMessage($"\n  [Handler] Cắt thành công! Đã chia Polyline thành {pieces.Length} phần.");
                    return true;
                }
                else
                {
                    ed.WriteMessage("\n  [Handler] Cắt thất bại: Polyline dao cắt không giao với Polyline chính.");
                }
            }
            return false;
        }
    }

    public class SplitTestRunner
    {
        private readonly Dictionary<string, ISplitHandler> _handlers;

        public SplitTestRunner()
        {
            _handlers = new Dictionary<string, ISplitHandler>
            {
                { "Line_Point", new LineSplitByPointHandler() },
                { "Polyline_Point", new PolylineSplitByPointHandler() },
                { "Line_Line", new LineSplitByLineHandler() },
                { "Polyline_Line", new PolylineSplitByLineHandler() },
                { "Line_Polygon", new LineSplitByPolygonHandler() },
                { "Polyline_Polygon", new PolylineSplitByPolygonHandler() },
                { "Line_MultipleLines", new LineSplitByMultipleLinesHandler() },
                { "Line_Polyline", new LineSplitByPolylineHandler() },
                { "Line_MultiplePoints", new LineSplitByMultiplePointsHandler() },
                { "Line_MultiplePolygons", new LineSplitByMultiplePolygonsHandler() },
                { "Line_MultiplePolylines", new LineSplitByMultiplePolylinesHandler() },
                { "Polyline_MultiplePoints", new PolylineSplitByMultiplePointsHandler() },
                { "Polyline_MultipleLines", new PolylineSplitByMultipleLinesHandler() },
                { "Polyline_MultiplePolylines", new PolylineSplitByMultiplePolylinesHandler() },
                { "Polyline_MultiplePolygons", new PolylineSplitByMultiplePolygonsHandler() },
                { "Polyline_Polyline", new PolylineSplitByPolylineHandler() }
            };
        }

        public static ObjectId EnsureLayer(Database database, Transaction transaction, string name)
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

        public void RunSplitTest()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            ed.WriteMessage("\n--- BẮT ĐẦU KIỂM THỬ CẮT (SPLIT TEST) ---");

            try
            {
                // 1. Select the main object (Subject)
                PromptEntityOptions optSubject = new PromptEntityOptions("\nChọn đối tượng chính cần cắt (Line hoặc Polyline):");
                optSubject.SetRejectMessage("\nChỉ chấp nhận chọn Line hoặc Polyline.");
                optSubject.AddAllowedClass(typeof(Line), exactMatch: false);
                optSubject.AddAllowedClass(typeof(Polyline), exactMatch: false);

                PromptEntityResult resSubject = ed.GetEntity(optSubject);
                if (resSubject.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n[HỦY] Người dùng đã hủy chọn đối tượng chính.");
                    return;
                }
                ObjectId subjectId = resSubject.ObjectId;

                string sType = subjectId.ObjectClass.DxfName;
                ed.WriteMessage($"\n-> Đã chọn đối tượng chính: {sType}");

                // 2. Select Split Type Option
                PromptKeywordOptions optType = new PromptKeywordOptions("\nChọn loại đối tượng dùng làm dao cắt:");
                optType.Keywords.Add("Point", "PO", "Point");
                optType.Keywords.Add("Line", "L", "Line");
                optType.Keywords.Add("MLines", "ML", "MLines");
                optType.Keywords.Add("Polyline", "PL", "Polyline");
                optType.Keywords.Add("Polygon", "PG", "Polygon");
                optType.Keywords.Add("MPoints", "MP", "MPoints");
                optType.Keywords.Add("MPolylines", "MPL", "MPolylines");
                optType.Keywords.Add("MPolygons", "MPG", "MPolygons");
                optType.Keywords.Default = "Line";

                PromptResult resType = ed.GetKeywords(optType);
                if (resType.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n[HỦY] Người dùng đã hủy chọn loại dao cắt.");
                    return;
                }

                string splitType = resType.StringResult;
                if (splitType.Equals("Point", StringComparison.OrdinalIgnoreCase) || splitType.Equals("PO", StringComparison.OrdinalIgnoreCase))
                {
                    splitType = "Point";
                }
                else if (splitType.Equals("Line", StringComparison.OrdinalIgnoreCase) || splitType.Equals("L", StringComparison.OrdinalIgnoreCase))
                {
                    splitType = "Line";
                }
                else if (splitType.Equals("MLines", StringComparison.OrdinalIgnoreCase) || splitType.Equals("ML", StringComparison.OrdinalIgnoreCase))
                {
                    splitType = "MultipleLines";
                }
                else if (splitType.Equals("Polyline", StringComparison.OrdinalIgnoreCase) || splitType.Equals("PL", StringComparison.OrdinalIgnoreCase))
                {
                    splitType = "Polyline";
                }
                else if (splitType.Equals("Polygon", StringComparison.OrdinalIgnoreCase) || splitType.Equals("PG", StringComparison.OrdinalIgnoreCase))
                {
                    splitType = "Polygon";
                }
                else if (splitType.Equals("MPoints", StringComparison.OrdinalIgnoreCase) || splitType.Equals("MP", StringComparison.OrdinalIgnoreCase))
                {
                    splitType = "MultiplePoints";
                }
                else if (splitType.Equals("MPolylines", StringComparison.OrdinalIgnoreCase) || splitType.Equals("MPL", StringComparison.OrdinalIgnoreCase))
                {
                    splitType = "MultiplePolylines";
                }
                else if (splitType.Equals("MPolygons", StringComparison.OrdinalIgnoreCase) || splitType.Equals("MPG", StringComparison.OrdinalIgnoreCase))
                {
                    splitType = "MultiplePolygons";
                }
                ed.WriteMessage($"\n-> Đã chọn loại dao cắt: {splitType}");

                object cutter = null;

                // 3. Get Cutter based on option
                if (splitType == "Point")
                {
                    PromptPointOptions optPt = new PromptPointOptions("\nChọn một điểm để cắt trên đối tượng chính:");
                    PromptPointResult resPt = ed.GetPoint(optPt);
                    if (resPt.Status != PromptStatus.OK)
                    {
                        ed.WriteMessage("\n[HỦY] Người dùng hủy chọn điểm.");
                        return;
                    }
                    cutter = resPt.Value.ToGeoPoint();
                    ed.WriteMessage($"\n-> Đã chọn điểm cắt: ({resPt.Value.X:F2}, {resPt.Value.Y:F2})");
                }
                else if (splitType == "Line")
                {
                    PromptEntityOptions optCutter = new PromptEntityOptions("\nChọn Line làm đường cắt:");
                    optCutter.SetRejectMessage("\nChỉ chấp nhận chọn Line.");
                    optCutter.AddAllowedClass(typeof(Line), exactMatch: false);

                    PromptEntityResult resCutter = ed.GetEntity(optCutter);
                    if (resCutter.Status != PromptStatus.OK)
                    {
                        ed.WriteMessage("\n[HỦY] Người dùng hủy chọn Line cắt.");
                        return;
                    }

                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        if (tr.GetObject(resCutter.ObjectId, OpenMode.ForRead) is Line lineCutter)
                        {
                            cutter = lineCutter.ToGeoLine();
                            ed.WriteMessage($"\n-> Đã chọn Line làm đường cắt (Chiều dài: {lineCutter.Length:F2})");
                        }
                    }
                }
                else if (splitType == "Polygon")
                {
                    PromptEntityOptions optCutter = new PromptEntityOptions("\nChọn Polyline làm Polygon cắt:");
                    optCutter.SetRejectMessage("\nChỉ chấp nhận Polyline.");
                    optCutter.AddAllowedClass(typeof(Polyline), exactMatch: false);

                    PromptEntityResult resCutter = ed.GetEntity(optCutter);
                    if (resCutter.Status != PromptStatus.OK)
                    {
                        ed.WriteMessage("\n[HỦY] Người dùng hủy chọn Polygon cắt.");
                        return;
                    }

                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        if (tr.GetObject(resCutter.ObjectId, OpenMode.ForRead) is Polyline polylineCutter)
                        {
                            if (!polylineCutter.Closed)
                            {
                                ed.WriteMessage("\n[LỖI] Polyline đã chọn không khép kín. Không thể làm Polygon dao cắt.");
                                return;
                            }
                            cutter = polylineCutter.ToGeoPolygon();
                            ed.WriteMessage($"\n-> Đã chọn Polyline làm Polygon cắt (Chu vi: {polylineCutter.Length:F2}, Số đỉnh: {polylineCutter.NumberOfVertices})");
                        }
                    }
                }
                else if (splitType == "MultipleLines")
                {
                    var optSel = new PromptSelectionOptions();
                    optSel.MessageForAdding = "\nChọn các Line làm dao cắt:";
                    var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LINE") });

                    PromptSelectionResult resSel = ed.GetSelection(optSel, filter);
                    if (resSel.Status != PromptStatus.OK)
                    {
                        ed.WriteMessage("\n[HỦY] Người dùng hủy chọn Line cắt.");
                        return;
                    }

                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        var cutterList = new List<GeoLine2>();
                        foreach (SelectedObject selObj in resSel.Value)
                        {
                            if (tr.GetObject(selObj.ObjectId, OpenMode.ForRead) is Line lineCutter)
                            {
                                cutterList.Add(lineCutter.ToGeoLine());
                            }
                        }
                        cutter = cutterList.ToArray();
                        ed.WriteMessage($"\n-> Đã chọn {cutterList.Count} Line làm dao cắt.");
                    }
                }
                else if (splitType == "Polyline")
                {
                    PromptEntityOptions optCutter = new PromptEntityOptions("\nChọn Polyline làm dao cắt:");
                    optCutter.SetRejectMessage("\nChỉ chấp nhận Polyline.");
                    optCutter.AddAllowedClass(typeof(Polyline), exactMatch: false);

                    PromptEntityResult resCutter = ed.GetEntity(optCutter);
                    if (resCutter.Status != PromptStatus.OK)
                    {
                        ed.WriteMessage("\n[HỦY] Người dùng hủy chọn Polyline cắt.");
                        return;
                    }

                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        if (tr.GetObject(resCutter.ObjectId, OpenMode.ForRead) is Polyline polylineCutter)
                        {
                            cutter = polylineCutter.ToGeoPolyline();
                            ed.WriteMessage($"\n-> Đã chọn Polyline làm dao cắt (Chiều dài: {polylineCutter.Length:F2}, Số đỉnh: {polylineCutter.NumberOfVertices})");
                        }
                    }
                }
                else if (splitType == "MultiplePoints")
                {
                    var pointList = new List<GeoPoint2>();
                    while (true)
                    {
                        PromptPointOptions optPt = new PromptPointOptions($"\nChọn điểm thứ {pointList.Count + 1} để cắt (Nhấn Enter để kết thúc chọn):");
                        optPt.AllowNone = true;
                        PromptPointResult resPt = ed.GetPoint(optPt);
                        if (resPt.Status == PromptStatus.None)
                        {
                            break;
                        }
                        if (resPt.Status != PromptStatus.OK)
                        {
                            ed.WriteMessage("\n[HỦY] Người dùng hủy chọn điểm.");
                            return;
                        }
                        pointList.Add(resPt.Value.ToGeoPoint());
                    }
                    if (pointList.Count == 0)
                    {
                        ed.WriteMessage("\n[LỖI] Chưa chọn điểm nào để cắt.");
                        return;
                    }
                    cutter = pointList.ToArray();
                    ed.WriteMessage($"\n-> Đã chọn {pointList.Count} điểm làm dao cắt.");
                }
                else if (splitType == "MultiplePolygons")
                {
                    var optSel = new PromptSelectionOptions();
                    optSel.MessageForAdding = "\nChọn các Polyline khép kín làm các Polygon cắt:";
                    var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE,POLYLINE") });

                    PromptSelectionResult resSel = ed.GetSelection(optSel, filter);
                    if (resSel.Status != PromptStatus.OK)
                    {
                        ed.WriteMessage("\n[HỦY] Người dùng hủy chọn Polygon cắt.");
                        return;
                    }

                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        var polyList = new List<GeoPolygon2>();
                        foreach (SelectedObject selObj in resSel.Value)
                        {
                            if (tr.GetObject(selObj.ObjectId, OpenMode.ForRead) is Polyline polylineCutter)
                            {
                                if (!polylineCutter.Closed)
                                {
                                    ed.WriteMessage($"\n[CẢNH BÁO] Polyline ID {selObj.ObjectId} không khép kín. Bỏ qua không dùng làm Polygon cắt.");
                                    continue;
                                }
                                polyList.Add(polylineCutter.ToGeoPolygon());
                            }
                        }
                        if (polyList.Count == 0)
                        {
                            ed.WriteMessage("\n[LỖI] Không có Polyline khép kín nào được chọn.");
                            return;
                        }
                        cutter = polyList.ToArray();
                        ed.WriteMessage($"\n-> Đã chọn {polyList.Count} Polygon làm dao cắt.");
                    }
                }
                else if (splitType == "MultiplePolylines")
                {
                    var optSel = new PromptSelectionOptions();
                    optSel.MessageForAdding = "\nChọn các Polyline làm dao cắt:";
                    var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE,POLYLINE") });

                    PromptSelectionResult resSel = ed.GetSelection(optSel, filter);
                    if (resSel.Status != PromptStatus.OK)
                    {
                        ed.WriteMessage("\n[HỦY] Người dùng hủy chọn Polyline cắt.");
                        return;
                    }

                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        var polylineList = new List<GeoPolyline2>();
                        foreach (SelectedObject selObj in resSel.Value)
                        {
                            if (tr.GetObject(selObj.ObjectId, OpenMode.ForRead) is Polyline polylineCutter)
                            {
                                polylineList.Add(polylineCutter.ToGeoPolyline());
                            }
                        }
                        cutter = polylineList.ToArray();
                        ed.WriteMessage($"\n-> Đã chọn {polylineList.Count} Polyline làm dao cắt.");
                    }
                }

                if (cutter == null)
                {
                    ed.WriteMessage("\n[LỖI] Không thể khởi tạo đối tượng dao cắt.");
                    return;
                }

                // 4. Execute split through handlers
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    DBObject objSubject = tr.GetObject(subjectId, OpenMode.ForWrite);
                    BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    string handlerKey = GetHandlerKey(objSubject, splitType);
                    ed.WriteMessage($"\n-> Đang gọi Handler: {handlerKey}");

                    if (_handlers.TryGetValue(handlerKey, out ISplitHandler handler))
                    {
                        bool success = handler.Execute(objSubject, cutter, modelSpace, tr, ed);
                        if (success)
                        {
                            tr.Commit();
                            ed.WriteMessage("\n-> Giao dịch hoàn tất (Transaction Committed).");
                        }
                        else
                        {
                            ed.WriteMessage("\n-> Giao dịch bị hủy bỏ (Transaction Aborted).");
                            tr.Abort();
                        }
                    }
                    else
                    {
                        ed.WriteMessage($"\n[LỖI] Không tìm thấy Handler phù hợp cho sự kết hợp: {handlerKey}.");
                        tr.Abort();
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[LỖI NGOẠI LỆ] Có lỗi xảy ra trong quá trình split test: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                ed.WriteMessage("\n--- KẾT THÚC KIỂM THỬ CẮT ---");
            }
        }

        private string GetHandlerKey(DBObject subject, string splitType)
        {
            string subjectType = subject is Line ? "Line" : "Polyline";
            return $"{subjectType}_{splitType}";
        }
    }
}
