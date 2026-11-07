# L5: file DXF

`DrawingDxfWriter` chuyển một `Drawing` thành `DxfDatabase` rồi lưu file. Ngoài nó ra, không phần nào của module ghi DXF.

## Bố trí (D1)

Tờ giấy tỉ lệ 1:1 trong model space, đơn vị mm. Mỗi view vẽ theo scale của nó, tại chỗ L4 đã xếp. Dimension dùng
dimension style có `DIMLFAC` bằng scale của view (mỗi scale được dùng có một style), nên vẫn ghi độ dài thật của model.

## Vẽ gì, ở đâu

| Vẽ | Entity | Layer (tên là thiết lập) |
|---|---|---|
| nét thấy | LINE, ARC, CIRCLE | Visible |
| nét khuất | LINE, ARC, CIRCLE với linetype HIDDEN | Hidden |
| đường tâm | LINE với linetype CENTER | Centre |
| mặt cắt | HATCH | Hatch |
| dimension | DIMENSION (rotated, aligned, radial, diametric, ordinate) | Dimension |
| mark | MLEADER | Mark |
| khung tên | INSERT một block có attribute | Sheet |
| bảng vật tư, revision | TABLE | Sheet |
| khung, tiêu đề view, chữ | LWPOLYLINE, MTEXT | Sheet |

Linetype lấy từ các định nghĩa chuẩn nạp vào bản vẽ (nạp linetype từ file của `DxfDatabase`, hoặc tạo bằng code). Text
style và dimension style do writer tạo theo thiết lập.

## Kiểm tra

- Mở trong AutoCAD 2021 qua accoreconsole: không lỗi, AUDIT không có gì phải sửa, số entity trên mỗi layer đúng như dự
  kiến.
- Đọc lại bằng `DxfReader`: nét của mọi view đúng như trong `Drawing`, trong point tolerance.
- Lưu cùng một bản vẽ hai lần ra cùng các byte (handle và thứ tự cố định).
- Version AC1027 (D8), trừ khi G0 thấy lý do dùng version khác.
