# Kiến trúc

## Các lớp

| # | Lớp | Nhiệm vụ | Chi tiết |
|---|---|---|---|
| L0 | Nguồn | các đối tượng cần vẽ: mỗi đối tượng một `GeoSolid3`, kèm loại, tên, mark, profile, vật liệu, cha và thuộc tính | [10-source.md](10-source.md) |
| L1 | Chiếu nét | từ một hướng nhìn ra các nét 2D, thấy hoặc khuất, và các mặt bị cắt; mỗi nét biết thuộc đối tượng nào | [11-projection.md](11-projection.md) |
| L2 | View | các view của bản vẽ: theo trục, mặt cắt, chi tiết, isometric; scale, vùng cắt và độ sâu | [12-views.md](12-views.md) |
| L3 | Chú thích | dimension theo quy tắc, mark, đường tâm, ký hiệu bulông và hàn, đặt không chồng nhau | [13-annotation.md](13-annotation.md) |
| L4 | Tờ giấy | khổ giấy, khung tên, bảng vật tư, bảng revision, vị trí các view | [14-sheet.md](14-sheet.md) |
| L5 | Xuất | file DXF: layer, linetype, dimension style, block, entity DIMENSION, hatch | [15-output-dxf.md](15-output-dxf.md) |
| L6 | Thiết lập | bản vẽ thể hiện gì và thể hiện thế nào, và các preset | [16-settings.md](16-settings.md) |

Cầu nối Tekla nằm ngoài các lớp: nó chuyển đối tượng model Tekla thành nguồn L0
([17-tekla-bridge.md](17-tekla-bridge.md)).

## Luồng dữ liệu

```
Model Tekla ──DrawingSourceConvert (TeklaConvert)──┐
đối tượng dựng bằng code ──────────────────────────┼──► DrawingSource (L0)
IFC (sau này) ─────────────────────────────────────┘          │
                                                              ▼
                        DrawingSettings (L6) ──► DrawingBuilder
                                                              │
        ┌─────────────────────────────────────────────────────┤
        ▼                                                     ▼
  ViewBuilder (L2) ── từng view ──► ViewProjector (L1) ──► nét chiếu, mặt cắt
        │                                                     │
        ▼                                                     ▼
  Annotator (L3): dimension, mark, ký hiệu ◄──────── các điểm cần đo của mỗi view
        │
        ▼
  SheetLayout (L4): kích thước view ──► SheetPacker; khung tên, bảng vật tư
        │
        ▼
  Drawing (kết quả, tất cả theo mm trên giấy) ──► DrawingDxfWriter (L5) ──► .dxf
```

`Drawing` là một kết quả thuần dữ liệu: các view với nét, hatch, dimension và mark, tất cả theo toạ độ trên giấy, cùng
tờ giấy. Test kiểm tra được trực tiếp mà không cần đọc lại DXF, và sau này ghi được ra định dạng khác (SVG để xem nhanh
qua `SvgWriter`, PDF).

## Phác thảo API công khai

```csharp
using GeometryHelper.Drawing;

DrawingSource source = DrawingSourceConvert.FromAssembly(assembly);   // Tekla, or new DrawingSource(objects) in code
DrawingSettings settings = DrawingSettings.Assembly;                  // a preset, or settings built in code
Drawing drawing = DrawingBuilder.Build(source, settings);
drawing.SaveDxf("B-101.dxf");
```

Mỗi lớp dùng riêng được, như các module khác của thư viện:

```csharp
ProjectedView2 view = ViewProjector.Project(solids, viewFrame, tolerance);   // L1 alone: lines of a view
```

Các tên trên chỉ là đề xuất, G0 sẽ chốt trong đặc tả. Tên mang số chiều ở cuối như mọi nơi trong thư viện.

## Quy tắc chung của mọi lớp

- **Không gọi Tekla dưới L0.** Chỉ `GeometryHelper.TeklaConvert` chạm vào Tekla. Mọi phần khác nhận `GeoSolid3` và dữ
  liệu thuần, nên mọi test chạy bằng code.
- **Milimét.** Model tính bằng mm; giấy tính bằng mm trên giấy. Scale của view chuyển từ cái này sang cái kia.
- **Tolerance.** Mọi phép so toạ độ đi qua một `Tolerance` truyền vào; không so bằng `==`.
- **Kết quả ổn định.** Cùng nguồn và thiết lập thì ra cùng một file DXF đến từng byte: mọi phép sắp xếp có tie-break đầy
  đủ, không kết quả nào phụ thuộc thứ tự duyệt của `Dictionary` hay `HashSet`.
- **Guard và log.** Kiểm tham số qua `Guard`; `GeometryHelperLog` ghi một dòng Info tóm tắt cho mỗi lần gọi hàm công
  khai, Warn cho exception bắt được, và một dòng Debug gộp cho các trường hợp bỏ qua có chủ đích.
- **Bẫy tốc độ.** Không sort-and-sweep theo một trục cố định (chọn trục có ít cặp nhất); không dùng key `(a << 32) | b`
  trong `Dictionary` mà không có comparer riêng.
- **Dùng lại phần có sẵn.** Đặt mark qua `Arranging`, xếp view qua `Packing`, ghi file qua `DxfDatabase`. Lỗi tìm thấy ở
  các phần đó được sửa ngay tại đó, kèm test riêng, không lách ở đây.
