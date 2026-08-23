using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using GeometryHelper.PlaneGeometry.Core;
using GeometryHelper.PlaneGeometry.Geometry;
using GeometryHelper.CadConvert;

namespace GeometryHelper.ArrangeAlgorithms.CadTest
{
    public class ClosestPointTestRunner
    {
        public void RunClosestPointTest()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            // 1. Chọn đối tượng biên (Line, Polyline/Rectangle hoặc Circle)
            var selectOpts = new PromptEntityOptions("\nChọn đối tượng biên (Line, Polyline/Rectangle hoặc Circle):");
            selectOpts.SetRejectMessage("\nChỉ chấp nhận đối tượng Line, Polyline hoặc Circle.");
            selectOpts.AddAllowedClass(typeof(Line), exactMatch: true);
            selectOpts.AddAllowedClass(typeof(Polyline), exactMatch: true);
            selectOpts.AddAllowedClass(typeof(Circle), exactMatch: true);

            PromptEntityResult selectRes = ed.GetEntity(selectOpts);
            if (selectRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nKhông chọn đối tượng biên. Lệnh bị hủy.");
                return;
            }

            // 2. Pick điểm cần tìm hình chiếu
            var pointOpts = new PromptPointOptions("\nChọn điểm cần tìm điểm gần nhất trên biên:");
            PromptPointResult pointRes = ed.GetPoint(pointOpts);
            if (pointRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nKhông chọn điểm. Lệnh bị hủy.");
                return;
            }

            Point3d pickedPoint3d = pointRes.Value;
            GeoPoint2 pickedGeoPoint = pickedPoint3d.ToGeoPoint2();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                        SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);

                    DBObject selectedObj = tr.GetObject(selectRes.ObjectId, OpenMode.ForRead);

                    GeoPoint2 closestGeoPoint = default(GeoPoint2);
                    string objTypeStr = "";

                    if (selectedObj is Line line)
                    {
                        objTypeStr = "Line";
                        GeoLine2 geoLine = line.ToGeoLine2();
                        closestGeoPoint = geoLine.GetClosestPointOnBoundary(pickedGeoPoint);
                    }
                    else if (selectedObj is Polyline polyline)
                    {
                        if (polyline.TryToGeoRectangle2(out GeoRectangle2 geoRectangle))
                        {
                            objTypeStr = "Rectangle (Hình chữ nhật)";
                            closestGeoPoint = geoRectangle.GetClosestPointOnBoundary(pickedGeoPoint);
                        }
                        else if (polyline.Closed)
                        {
                            objTypeStr = "Polygon (Polyline khép kín)";
                            GeoPolygon2 geoPolygon = polyline.ToGeoPolygon2();
                            closestGeoPoint = geoPolygon.GetClosestPointOnBoundary(pickedGeoPoint);
                        }
                        else
                        {
                            objTypeStr = "Polyline (Polyline mở)";
                            GeoPolyline2 geoPolyline = polyline.ToGeoPolyline2();
                            closestGeoPoint = geoPolyline.GetClosestPointOnBoundary(pickedGeoPoint);
                        }
                    }
                    else if (selectedObj is Circle circle)
                    {
                        objTypeStr = "Circle";
                        GeoCircle2 geoCircle = circle.ToGeoCircle2();
                        closestGeoPoint = geoCircle.GetClosestPointOnBoundary(pickedGeoPoint);
                    }

                    // Convert kết quả về Point3d của AutoCAD
                    Point3d closestPoint3d = closestGeoPoint.ToAcadPoint3();

                    // Vẽ Line nối từ điểm picked đến điểm closest
                    Line connectLine = new Line(pickedPoint3d, closestPoint3d);
                    connectLine.LayerId = SplitTestRunner.EnsureLayer(db, tr, "ClosestPoint_Connection");
                    connectLine.ColorIndex = 2; // Yellow

                    // Vẽ Point tại closest point
                    DBPoint targetPoint = new DBPoint(closestPoint3d);
                    targetPoint.LayerId = SplitTestRunner.EnsureLayer(db, tr, "ClosestPoint_Target");
                    targetPoint.ColorIndex = 1; // Red

                    modelSpace.AppendEntity(connectLine);
                    tr.AddNewlyCreatedDBObject(connectLine, true);

                    modelSpace.AppendEntity(targetPoint);
                    tr.AddNewlyCreatedDBObject(targetPoint, true);

                    double distance = pickedPoint3d.DistanceTo(closestPoint3d);
                    ed.WriteMessage($"\n[Test] Đối tượng biên được chọn: {objTypeStr}");
                    ed.WriteMessage($"\n[Test] Điểm gần nhất trên biên tìm được tại: ({closestPoint3d.X:F2}, {closestPoint3d.Y:F2})");
                    ed.WriteMessage($"\n[Test] Khoảng cách ngắn nhất đến biên: {distance:F2}");

                    tr.Commit();
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\nLỗi trong quá trình chạy test: {ex.Message}");
                    tr.Abort();
                }
            }
        }
    }
}
