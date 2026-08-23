using System;
using System.Collections.Generic;
using System.Diagnostics;
using CommonGeometry;
// AutoCAD ships its own Autodesk.AutoCAD.Geometry.Tolerance. Before the split this file found
// ArrangeAlgorithms.Tolerance through the enclosing namespace, which outranks a using; now that
// it arrives through a using of its own the two tie, so the one meant here is named outright.
using Tolerance = CommonGeometry.Tolerance;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using PlaneGeometry.Core;
using PlaneGeometry.Geometry;

namespace ArrangeAlgorithms.CadTest
{
    public class JoinTestRunner
    {
        /// <summary>
        /// Chạy test JOIN bằng <see cref="Merge2.Join"/>, bản dùng lưới không gian.
        /// </summary>
        public void RunJoinTest()
        {
            Run("Merge2.Join", Merge2.Join, "Joined_Lines", 2);
        }

        /// <summary>
        /// Chạy test JOIN bằng <see cref="Merge2.JoinBackup"/>, bản so từng cặp giữ lại để đối chiếu.
        /// </summary>
        /// <remarks>
        /// Kết quả phải trùng với <see cref="RunJoinTest"/> về số polyline và tổng chiều dài; chiều của
        /// từng polyline thì không, vì không bản nào cam kết chiều. Chi phí của bản này tăng theo bình
        /// phương số Line, nên với bản vẽ lớn nó chậm hơn hẳn — đó là điều lệnh này để cho thấy.
        /// <para>
        /// Vẽ ra layer riêng và màu riêng để chạy hai lệnh trên cùng một bản vẽ mà vẫn phân biệt được.
        /// </para>
        /// </remarks>
        public void RunJoinBackupTest()
        {
            Run("Merge2.JoinBackup", Merge2.JoinBackup, "Joined_Lines_Backup", 4);
        }

        /// <summary>
        /// Thân chung của hai lệnh: chọn Line, gộp bằng hàm được truyền vào, vẽ polyline kết quả rồi
        /// xóa các Line gốc.
        /// </summary>
        /// <param name="label">Tên hàm, dùng trong thông báo.</param>
        /// <param name="join">Hàm gộp.</param>
        /// <param name="layerName">Layer chứa kết quả.</param>
        /// <param name="colorIndex">Màu của polyline kết quả.</param>
        private void Run(
            string label,
            Func<IEnumerable<GeoLine2>, Tolerance, GeoPolyline2[]> join,
            string layerName,
            short colorIndex)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            // 1. Yêu cầu người dùng chọn tập hợp các đối tượng Line
            TypedValue[] filter = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Start, "LINE")
            };
            SelectionFilter selectionFilter = new SelectionFilter(filter);

            PromptSelectionOptions selOpts = new PromptSelectionOptions();
            selOpts.MessageForAdding = $"\nChọn các Line để thực hiện gộp ({label}):";
            PromptSelectionResult selRes = ed.GetSelection(selOpts, selectionFilter);

            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nKhông chọn đối tượng. Lệnh bị hủy.");
                return;
            }

            SelectionSet selSet = selRes.Value;
            if (selSet.Count == 0)
            {
                ed.WriteMessage("\nKhông có Line nào được chọn.");
                return;
            }

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                        SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);

                    var geoLines = new List<GeoLine2>();
                    var lineObjects = new List<Line>();

                    // Thu thập các đối tượng Line
                    foreach (SelectedObject selObj in selSet)
                    {
                        if (selObj != null)
                        {
                            Line line = tr.GetObject(selObj.ObjectId, OpenMode.ForWrite) as Line;
                            if (line != null)
                            {
                                geoLines.Add(line.ToGeoLine());
                                lineObjects.Add(line);
                            }
                        }
                    }

                    if (geoLines.Count == 0)
                    {
                        ed.WriteMessage("\nKhông thu thập được dữ liệu Line hợp lệ.");
                        return;
                    }

                    ed.WriteMessage($"\nĐang thực hiện gộp {geoLines.Count} Line bằng hàm {label}...");

                    // Thực thi hàm gộp
                    Stopwatch watch = Stopwatch.StartNew();
                    GeoPolyline2[] joinedPolylines = join(geoLines, Tolerance.Global);
                    watch.Stop();

                    ed.WriteMessage($"\nKết quả sau khi gộp: {joinedPolylines.Length} polyline, mất {watch.ElapsedMilliseconds} ms.");

                    // Đảm bảo có layer cho đối tượng đã gộp
                    ObjectId joinedLayerId = SplitTestRunner.EnsureLayer(db, tr, layerName);

                    // Thêm các Polyline mới tạo vào ModelSpace
                    int index = 1;
                    double totalLength = 0.0;
                    foreach (GeoPolyline2 geoPolyline in joinedPolylines)
                    {
                        Polyline polyline = geoPolyline.ToAcadPolyline();
                        polyline.LayerId = joinedLayerId;
                        polyline.ColorIndex = colorIndex;
                        polyline.ConstantWidth = 5.0; // Cho nét dày hơn để dễ quan sát

                        modelSpace.AppendEntity(polyline);
                        tr.AddNewlyCreatedDBObject(polyline, true);

                        totalLength += geoPolyline.Length;
                        ed.WriteMessage($"\n  - Polyline {index++}: {geoPolyline.VertexCount} đỉnh, Chiều dài: {geoPolyline.Length:F2}");
                    }

                    // Tổng chiều dài phải bằng tổng chiều dài các Line đầu vào: gộp không tạo thêm và
                    // không làm mất hình học. Lệch ở đây là dấu hiệu sai, nên báo ra ngay.
                    double inputLength = 0.0;
                    foreach (GeoLine2 geoLine in geoLines)
                    {
                        inputLength += geoLine.Length;
                    }

                    ed.WriteMessage($"\nTổng chiều dài: vào {inputLength:F4}, ra {totalLength:F4}"
                        + (Math.Abs(inputLength - totalLength) <= 1E-6 ? " (khớp)." : " *** LỆCH ***."));

                    // Xóa các Line ban đầu đã gộp thành công
                    foreach (Line line in lineObjects)
                    {
                        line.Erase();
                    }

                    tr.Commit();
                    ed.WriteMessage($"\n[Test] Lệnh {label} hoàn thành thành công và đã xóa các Line gốc.");
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\nLỗi trong quá trình chạy test {label}: {ex.Message}");
                    tr.Abort();
                }
            }
        }
    }
}
