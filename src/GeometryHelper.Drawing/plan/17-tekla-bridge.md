# Cầu nối Tekla

`DrawingSourceConvert` trong `GeometryHelper.TeklaConvert` chuyển đối tượng model Tekla thành `DrawingSource`. Đây là
phần code duy nhất của module này chạm vào Tekla, và nó chỉ đọc.

## Đọc gì

| Từ Tekla | Thành |
|---|---|
| một `Assembly`: cấu kiện chính và phụ, các sub-assembly | các `DrawingObject` loại Part, `IsMain` cho cấu kiện chính, `ParentId` là assembly |
| solid của part (`GetSolid`), qua `SolidConvert.TryToGeoSolid3` | `Solid` |
| hệ trục của part | `Frame` |
| position, profile, vật liệu, tên, và các report property mà thiết lập yêu cầu | `Mark`, `Profile`, `Material`, `Name`, `Attributes` |
| bolt group: vị trí, đường kính, kích thước lỗ, các cấu kiện được bắt bulông | đối tượng loại Bolt và Hole |
| mối hàn: loại, kích thước, các cấu kiện được hàn, đường hàn | đối tượng loại Weld |
| cốt thép (sau này), qua `ReinforcementConvert` | đối tượng loại Rebar |

Các lời gọi API cụ thể sẽ chốt ở G0, theo Open API của Tekla 2020, 2025 và 2026, vì cầu nối được build cho từng version.

## Quy tắc

- Chỉ đọc: không bao giờ thay đổi model.
- Build cho từng version Tekla đều có test chạy bằng code, như các thành viên khác của TeklaConvert.
- Chạy thử với Tekla đang mở của maintainer phải được maintainer đồng ý trước, mỗi lần chạy.
- Log theo quy ước của các cầu nối: Warn cho exception bắt được, một dòng Debug gộp cho các đối tượng bị bỏ qua, một dòng
  Info tóm tắt cho mỗi lần gọi hàm công khai.
- Dữ liệu model từ dự án của maintainer không bao giờ đưa vào repo; test dựng đối tượng bằng code.
