# Tổng quan

## Mục tiêu

Maintainer viết công cụ cho Tekla Structures, và drawing của Tekla có quá nhiều lỗi để tin dùng. Module này tự làm bản
vẽ: đọc các đối tượng của model, vẽ các view với nét thấy, nét khuất và mặt cắt, ghi dimension và mark, xếp view lên tờ
giấy có khung tên và bảng vật tư, rồi ghi ra file DXF mở được bằng AutoCAD, DWG TrueView hoặc viewer có sẵn trong repo.

## Một loại bản vẽ duy nhất

Tekla có bản vẽ single part, assembly, cast unit và GA, mỗi loại có code và thiết lập riêng. Ở đây chỉ có một kiểu
`Drawing`. Bản vẽ thể hiện gì chỉ do hai thứ quyết định:

- các đối tượng đưa vào: một bản mã, một dầm kèm các chi tiết, hay cả một khung nhà;
- thiết lập dùng để tạo: những view nào, scale nào, dimension và mark nào.

Bản vẽ một bản mã và bản vẽ một khung nhà chạy qua cùng một đoạn code. "Single part" hay "GA" chỉ là một preset thiết
lập, không phải một kiểu riêng.

## Phạm vi

Làm:

- cấu kiện và assembly thép: bản mã, thép hình, ống, cắt, vát, lỗ, bulông, mối hàn;
- view theo các trục của bản vẽ, mặt cắt, chi tiết, view isometric;
- dimension tự động, mark, đường tâm, ký hiệu bulông và hàn, không chồng lên nhau;
- tờ giấy khổ chuẩn hoặc tự chọn, khung tên, bảng vật tư, bảng revision;
- ghi DXF qua `DxfDatabase`, với entity DIMENSION thật, layer và linetype;
- đọc đối tượng từ Tekla qua Open API.

Làm sau, khi phần trên đã chạy tốt:

- cấu kiện bê tông có cốt thép và bảng thống kê thép;
- cập nhật bản vẽ khi model thay đổi, giữ lại những gì đã sửa tay;
- xuất PDF;
- đọc đối tượng từ IFC.

Không làm:

- chỉnh sửa bản vẽ tương tác; bản vẽ được tạo ra rồi mở bằng CAD (đang xem xét lại, xem phần giao diện);
- ghi ngược vào drawing của Tekla, vì như vậy sẽ mang lại đúng các lỗi của Tekla.

## Tham khảo Tekla

Tekla là thước đo, và chỉ được dùng theo các cách sau:

- Open API, để đọc model và thiết lập của các bản vẽ có sẵn;
- tài liệu hướng dẫn người dùng, để nắm khái niệm và các tuỳ chọn của bản vẽ;
- kết quả xuất ra: bản vẽ Tekla tạo cho một đối tượng được xuất DXF hoặc DWG, rồi đặt cạnh bản vẽ của ta cho cùng đối
  tượng để so sánh.

Không bao giờ dịch ngược hay sao chép từ file nhị phân của Tekla. Giấy phép của Tekla cấm việc này, và package phát hành
theo MIT trên NuGet, nơi không thể rút lại bản đã đẩy lên. Code viết lại từ code của Tekla cũng sẽ mang theo lỗi của nó.

## Thế nào là thành công

- Mọi view của các cấu kiện mẫu trong [20-test-cases.md](20-test-cases.md) có đúng những nét một người vẽ sẽ vẽ, kiểm
  bằng số trong test và bằng mắt trong AutoCAD.
- Mỗi lỗi ghi trong [40-tekla-bugs.md](40-tekla-bugs.md) có một test mà bản vẽ của ta vượt qua.
- Bản vẽ một assembly 50 cấu kiện tạo xong dưới 1 giây, một khung 1 000 cấu kiện dưới 1 phút (mục tiêu đặt tạm ở G0,
  đo và chốt ở G1).
- File DXF mở được trong AutoCAD 2021 và AUDIT không có gì phải sửa.
