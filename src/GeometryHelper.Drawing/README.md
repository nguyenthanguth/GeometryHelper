# GeometryHelper.Drawing

Module tạo bản vẽ chế tạo và bản vẽ bố trí chung từ model 3D, ghi ra DXF. Bản vẽ gồm các view của đối tượng với nét
thấy, nét khuất và mặt cắt; dimension; mark; và tờ giấy có khung tên, bảng vật tư. Mục tiêu là làm được những gì drawing
của Tekla Structures làm, nhưng không có các lỗi của nó, và không phụ thuộc Tekla ngoài bước đọc model.

**Trạng thái: đang lập kế hoạch.** Chưa có code. Các file kế hoạch dưới đây là căn cứ để phát triển: đọc trước khi bắt
đầu mỗi giai đoạn, và cập nhật mỗi khi có quyết định mới.

Các file kế hoạch viết bằng tiếng Việt theo yêu cầu của maintainer, để dễ review. Code, comment, docs của thư viện và
commit message vẫn viết bằng tiếng Anh như quy định trong CLAUDE.md.

## Kế hoạch

| File | Nội dung |
|---|---|
| [plan/00-overview.md](plan/00-overview.md) | mục tiêu, một loại bản vẽ duy nhất, phạm vi, những gì không làm, cách tham khảo Tekla |
| [plan/01-architecture.md](plan/01-architecture.md) | các lớp L0 đến L6, luồng dữ liệu, phác thảo API, quy tắc chung của mọi lớp |
| [plan/02-project-structure.md](plan/02-project-structure.md) | thư mục và file, project, test, benchmark, docs, cách đưa vào bước verify |
| [plan/03-decisions.md](plan/03-decisions.md) | các quyết định đánh số, đã duyệt hoặc đề xuất, và các câu hỏi còn mở |
| [plan/04-phases.md](plan/04-phases.md) | các giai đoạn G0 đến G7, mỗi bước kèm cách kiểm tra, và trạng thái |
| [plan/10-source.md](plan/10-source.md) | L0: các đối tượng dùng để tạo bản vẽ |
| [plan/11-projection.md](plan/11-projection.md) | L1: chiếu nét thấy, nét khuất và mặt cắt |
| [plan/12-views.md](plan/12-views.md) | L2: view, hệ trục, scale và vùng cắt |
| [plan/13-annotation.md](plan/13-annotation.md) | L3: dimension, mark, đường tâm và ký hiệu |
| [plan/14-sheet.md](plan/14-sheet.md) | L4: tờ giấy, khung tên, bảng vật tư, xếp view |
| [plan/15-output-dxf.md](plan/15-output-dxf.md) | L5: ghi gì ra DXF và ghi thế nào |
| [plan/16-settings.md](plan/16-settings.md) | L6: thiết lập và các preset |
| [plan/17-tekla-bridge.md](plan/17-tekla-bridge.md) | đọc model Tekla thành nguồn của bản vẽ |
| [plan/20-test-cases.md](plan/20-test-cases.md) | các cấu kiện mẫu dựng bằng code để kiểm tra mọi giai đoạn |
| [plan/30-research.md](plan/30-research.md) | mã nguồn mở và tài liệu cần nghiên cứu, và đã học được gì từ mỗi nguồn |
| [plan/40-tekla-bugs.md](plan/40-tekla-bugs.md) | các lỗi drawing của Tekla mà module này phải làm đúng |
