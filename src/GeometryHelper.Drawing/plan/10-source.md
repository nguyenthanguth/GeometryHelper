# L0: nguồn

Những gì dùng để tạo bản vẽ. Lớp này không biết gì về Tekla: cầu nối điền dữ liệu vào
([17-tekla-bridge.md](17-tekla-bridge.md)), còn test thì dựng bằng code.

## DrawingObject

| Thành viên | Chứa gì |
|---|---|
| `Id` | khoá duy nhất trong nguồn, không đổi giữa các lần chạy (GUID của Tekla, hoặc do code đặt) |
| `Kind` | `DrawingObjectKind`: Part, Bolt, Hole, Weld, Rebar, sau này thêm loại khác |
| `Solid` | `GeoSolid3` cần vẽ, theo mm của model; lỗ có thể chỉ mang trục và đường kính |
| `Frame` | `GeoCoordinateSystem3` riêng của đối tượng (với part của Tekla là hệ trục của nó: x dọc theo chiều dài) |
| `Name`, `Mark`, `Profile`, `Material` | các chữ hiển thị trên mark và bảng vật tư |
| `ParentId` | assembly chứa đối tượng, nếu có |
| `IsMain` | là cấu kiện chính của assembly; mặc định bản vẽ lấy hệ trục của nó |
| `Attributes` | các giá trị có tên khác, dùng cho mark, khung tên và bảng vật tư (chuỗi sang chuỗi, sắp theo tên) |

Bulông và lỗ mang những gì ký hiệu và dimension của chúng cần: trục, đường kính, bolt group chứa nó và cách bố trí của
nhóm. Mối hàn mang loại, kích thước, các cấu kiện được hàn và đường hàn. Danh sách thành viên chính xác sẽ chốt ở G0,
theo những gì Open API của Tekla cung cấp.

## DrawingSource

- các đối tượng, theo một thứ tự cố định (theo `Id`, để kết quả không phụ thuộc thứ tự model trả về);
- hệ trục của bản vẽ: mặc định là hệ trục của đối tượng chính, hoặc do người gọi đặt;
- các chữ của khung tên lấy từ model (dự án, số bản vẽ, revision).

## Kiểm tra

- Mọi solid phải kín và hợp lệ (`GeoSolid3.Validate`). Đối tượng nào không đạt vẫn được vẽ như nó đang có, ghi log một
  lần kèm id, và được đếm trong báo cáo của bản vẽ; không bao giờ bị bỏ đi trong im lặng.
- Id phải duy nhất; id trùng bị từ chối qua `Guard`.
