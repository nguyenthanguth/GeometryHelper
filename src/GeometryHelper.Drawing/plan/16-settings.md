# L6: thiết lập

`DrawingSettings` chứa mọi thứ một bản vẽ có thể thay đổi, dưới dạng object C# (D5). Mỗi lớp một phần:

| Phần | Chứa |
|---|---|
| `Views` | những view nào, phép chiếu (D9), các mặt cắt và chi tiết cần có, lề của vùng cắt, góc trơn của L1 |
| `Scales` | các scale cho phép, lớn nhất trước (Q4) |
| `Dimensions` | các quy tắc được bật, khoảng cách giữa các tầng, chiều cao chữ và cách làm tròn, có gộp dimension bằng nhau hay không |
| `Marks` | mỗi loại mark hiển thị gì (Q5), chiều cao chữ, khung |
| `Sheet` | các khổ giấy cho phép, khung tên (Q2), các cột của bảng vật tư, bảng revision |
| `Layers` | tên, màu và linetype của các layer ở L5 |
| `Text` | font và các chiều cao chữ |

## Preset

Một loại bản vẽ duy nhất (A1) lấy cách thể hiện từ một preset:

| Preset | Dùng cho | Khác ở |
|---|---|---|
| `DrawingSettings.Part` | một cấu kiện | ba view và mặt cắt của cấu kiện, ghi dimension mọi lỗ, chỉ có mark cấu kiện |
| `DrawingSettings.Assembly` | một assembly | hệ trục của cấu kiện chính, dimension chi tiết gắn thêm tính từ cấu kiện chính, mark các chi tiết, bảng vật tư |
| `DrawingSettings.General` | nhiều assembly | mặt bằng và mặt đứng, mark assembly, lưới trục và cao độ, không ghi dimension từng cấu kiện |

Preset chỉ là một object thiết lập. Một bản vẽ tạo bằng preset `Part` từ một assembly vẫn hợp lệ và vẽ mọi đối tượng được
đưa vào.

Thiết lập được sao chép khi tạo bản vẽ, nên sửa thiết lập sau đó không làm thay đổi bản vẽ đã tạo.
