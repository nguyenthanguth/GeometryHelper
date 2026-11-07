# Các giai đoạn

Mỗi giai đoạn kết thúc khi:
- bước verify đầy đủ trong CLAUDE.md xanh;
- file DXF của các cấu kiện mẫu mở được trong AutoCAD và AUDIT không có gì phải sửa;
- maintainer đã xem các file trước khi sang giai đoạn sau.

Mỗi bước ghi theo dạng `[bước] -> verify: [kiểm tra]`.

| Giai đoạn | Nội dung | Trạng thái |
|---|---|---|
| G0 | Nghiên cứu và đặc tả | chưa bắt đầu |
| G1 | Chiếu nét thấy/khuất, ghi ra DXF | chưa bắt đầu |
| G2 | View | chưa bắt đầu |
| G3 | Dimension | chưa bắt đầu |
| G4 | Mark và ký hiệu | chưa bắt đầu |
| G5 | Tờ giấy | chưa bắt đầu |
| G6 | Preset, cầu nối Tekla, so sánh với Tekla | chưa bắt đầu |
| G7 | Docs, changelog, release | chưa bắt đầu |

## G0: nghiên cứu và đặc tả

1. Nghiên cứu FreeCAD TechDraw, HLR của OpenCascade, phép chiếu của build123d, và tài liệu người dùng về drawing của
   Tekla -> verify: [30-research.md](30-research.md) ghi rõ mỗi nguồn làm gì và lấy gì từ đó.
2. Thu thập các lỗi drawing của Tekla từ maintainer -> verify: mỗi lỗi có trong [40-tekla-bugs.md](40-tekla-bugs.md),
   kèm Tekla làm sai thế nào và đúng phải là gì.
3. Dựng các cấu kiện mẫu của [20-test-cases.md](20-test-cases.md) bằng code -> verify: mỗi cấu kiện là một `GeoSolid3`
   kín và hợp lệ (theo `Validate`), và thể tích đúng bằng thể tích tính tay.
4. Chốt các quyết định đề xuất và tên các type công khai -> verify: mọi D trong [03-decisions.md](03-decisions.md) đã
   được duyệt hoặc thay thế, và các file của từng lớp đã ghi tên cuối cùng.

## G1: chiếu nét thấy và nét khuất

1. Lấy cạnh: cạnh sắc, đường bao, bỏ cạnh trơn -> verify: số cạnh của từng cấu kiện mẫu đúng như tính tay.
2. Che khuất: mỗi cạnh được chia thành đoạn thấy và đoạn khuất -> verify: ba view chính của mọi cấu kiện mẫu khớp với nét
   tính tay, trong point tolerance.
3. Cạnh chung của hai đối tượng chạm nhau chỉ vẽ một lần -> verify: TC8 không có nét nào bị vẽ trùng.
4. Khớp cung cho đường tròn bị chia mặt (D6) -> verify: mọi lỗ của TC1 và TC2 ra đúng một đường tròn đúng đường kính.
5. Mặt cắt: các mặt bị cắt và phần nằm sau mặt cắt -> verify: diện tích mặt cắt khớp với `GeoSolid3.Section`.
6. File DXF đầu tiên có ba view, nét đặt trên layer theo thấy/khuất -> verify: mở được trong AutoCAD, AUDIT sạch;
   maintainer xem file.
7. Tốc độ -> verify:
   - benchmark trên TC10, ghi lại thời gian;
   - một lượt fuzz với các khối hộp và vết cắt ngẫu nhiên: không exception nào, không cạnh nào vừa thấy vừa khuất.

## G2: view

1. Hệ trục bản vẽ lấy từ nguồn (hệ trục của đối tượng chính) -> verify: TC2 đặt dọc theo trục nào cũng cho cùng các view.
2. View theo các trục do thiết lập chọn; mặt cắt và chi tiết; isometric -> verify: mỗi view có đúng các nét của G1.
3. Chọn scale từ danh sách cho phép, cắt vùng theo đối tượng, giới hạn độ sâu -> verify: mỗi view vừa chỗ của nó trên
   tờ giấy.

## G3: dimension

1. Các điểm cần đo của một view: hai đầu, góc của đường bao, tâm lỗ và bulông, work point -> verify: theo từng cấu kiện
   mẫu.
2. Quy tắc: tổng, chuỗi, cộng dồn, ordinate, lỗ theo nhóm -> verify: giá trị dimension khớp với model.
3. Đặt dimension thành tầng ngoài view, đúng khoảng cách, không dimension nào cắt dimension khác hay đè lên chữ của
   dimension khác -> verify: có test đếm chỗ chồng nhau, và kết quả bằng 0 trên mọi cấu kiện mẫu.

## G4: mark và ký hiệu

1. Mark cấu kiện, mark assembly, mark bulông đặt qua `Arranging` -> verify: không chồng nhau, mọi đường dẫn chỉ đúng
   vào đối tượng của nó.
2. Đường tâm, ký hiệu mặt cắt và chi tiết, cao độ, ký hiệu hàn -> verify: theo từng cấu kiện mẫu.

## G5: tờ giấy

1. Khổ giấy và khung, khung tên điền từ nguồn và thiết lập -> verify: giá trị các attribute trong DXF.
2. Bảng vật tư là một table DXF -> verify: các dòng và số lượng khớp với nguồn.
3. Xếp view cùng chú thích của nó qua `SheetPacker` -> verify: không gì chồng nhau, không gì ra ngoài tờ giấy.

## G6: preset, cầu nối Tekla, so sánh

1. Preset cho một cấu kiện, một assembly và GA -> verify: mỗi cấu kiện mẫu được vẽ với từng preset.
2. `DrawingSourceConvert` đọc assembly, part, bulông và mối hàn -> verify:
   - test TeklaConvert chạy bằng code;
   - chạy thử với Tekla đang mở của maintainer chỉ khi được maintainer đồng ý.
3. Đặt cạnh bản vẽ Tekla xuất ra cho cùng đối tượng -> verify: mỗi lỗi trong [40-tekla-bugs.md](40-tekla-bugs.md) đều
   đúng ở bản vẽ của ta.

## G7: docs, changelog, release

1. Hướng dẫn bằng tiếng Anh đơn giản, có ví dụ đo được, thành một nhóm trên site docs có trang mở đầu riêng -> verify:
   docfx `--warningsAsErrors` 0 cảnh báo, không mục menu nào thiếu href.
2. Mục **NEW.** trong changelog, release notes của package -> verify: release check.
3. Release theo CLAUDE.md -> verify: cả ba workflow xanh.
