# L2: view

## Hệ trục bản vẽ

Mỗi bản vẽ có một hệ trục, mặc định là hệ trục riêng của đối tượng chính (với part của Tekla: x dọc theo chiều dài, y
hướng lên trong mặt cắt ngang). Mọi view đặt trong hệ trục đó, nên một dầm nằm dọc theo trục nào của model cũng cho cùng
một bản vẽ.

## Các loại view

| `ViewKind` | Hướng nhìn | Ghi chú |
|---|---|---|
| Front | theo -y hoặc +y của hệ trục bản vẽ, tuỳ phép chiếu (D9) | view chính |
| Top, Bottom | theo z của hệ trục | đặt trên hoặc dưới view đứng, tuỳ phép chiếu |
| Left, Right (view đầu) | theo x của hệ trục | |
| Back | ngược với view đứng | ít khi dùng |
| Section | qua một mặt phẳng cắt, giữ một phía | ký hiệu mặt cắt đặt trong view chứa vết cắt (L3) |
| Detail | một phần của view khác, cắt theo hình tròn hoặc chữ nhật, scale lớn hơn | ký hiệu chi tiết đặt trong view gốc |
| Isometric | theo một hướng xiên cố định | mặc định không có dimension |

Bản vẽ có những view nào là do thiết lập (L6) quyết định. View không có nét nào thì không tạo.

## DrawingView

- loại view và `ViewFrame3`;
- scale, chọn từ danh sách cho phép (Q4): scale lớn nhất mà view, cộng chỗ cho dimension, vừa với phần giấy dành cho nó;
- vùng cắt: mặc định là hộp bao các đối tượng nhìn thấy cộng một lề; khoảng độ sâu trước và sau hệ trục;
- các nét chiếu từ L1, đổi sang mm trên giấy;
- chú thích từ L3;
- tiêu đề (ví dụ "SECTION A-A 1:10") và vị trí trên tờ giấy (L4).

## View của cấu kiện dài

Cấu kiện dài vẽ ở scale vừa tờ giấy có thể ra quá nhỏ để đọc. Một thiết lập cho phép rút ngắn view: cắt bỏ đoạn giữa
các chi tiết, vẽ đường ngắt, dimension vẫn giữ độ dài thật. Phần này làm sau các bước đầu của G2, khi view thường đã chạy
được.
