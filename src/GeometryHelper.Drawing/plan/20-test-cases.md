# Các cấu kiện mẫu

Các cấu kiện dựng bằng code, nằm trong `TestBodies.cs` của bộ test drawing. Mọi giai đoạn đều kiểm tra trên chúng.
Không cấu kiện nào lấy từ model của maintainer: model thật nào làm lộ ra lỗi thì dựng ở đây một cấu kiện cho thấy đúng
điều đó.

Nét và giá trị mong đợi của từng cấu kiện được tính tay ở G0 và ghi vào test.

| # | Cấu kiện | Kiểm tra gì |
|---|---|---|
| TC1 | bản mã 300 x 200 x 20 có bốn lỗ 22 cách mép 50, đường tròn chia mặt như Tekla chia | lỗ ra đường tròn (D6), thành lỗ khuất trong view cạnh, dimension lỗ |
| TC2 | thép chữ I cao 300, cánh 150 x 12, bụng 8, dài 3 000, có bản mã đầu dày 20 với sáu lỗ 22 | cạnh sắc và cạnh khuất của thép hình, dimension chi tiết gắn thêm tính từ cấu kiện chính |
| TC3 | ống 168.3 x 6, dài 1 000, chia mặt | bỏ cạnh trơn, giữ đường bao; nhìn dọc, nhìn ngang và nhìn xiên 30 độ |
| TC4 | thép góc 100 x 100 x 10, một đầu cắt vát và một góc khoét | cạnh vết cắt, cạnh xuyên qua mặt |
| TC5 | cột chữ I 250 x 250 trên bản đế 400 x 400 x 25, bốn sườn và bốn bulông | assembly: nhiều đối tượng, bulông trong lỗ, mark các chi tiết |
| TC6 | dầm có một đầu cắt xiên 30 độ và đầu kia khoét cánh | mặt cắt nghiêng, đường bao vết cắt |
| TC7 | dầm bê tông 300 x 600 với bốn thanh 20 và đai 10 (giai đoạn sau) | vẽ cốt thép, khuất trong bê tông |
| TC8 | hai bản mã chạm nhau theo mặt và theo cạnh | cạnh chung chỉ vẽ một lần, thấy ưu tiên hơn khuất |
| TC9 | bản mã của TC1 xoay sao cho có mặt nhìn đúng thành một đường, và một bản lệch đi một chút | mặt nhìn thành đường, mặt lệch phẳng một chút |
| TC10 | một khung 200 assembly như TC5 trên một lưới | tốc độ; bản vẽ GA |

Trước hết mỗi cấu kiện được kiểm tra chính nó: kín và hợp lệ theo `GeoSolid3.Validate`, và thể tích đúng bằng thể tích
tính từ đường bao.
