# Các quyết định

Mỗi quyết định có một số hiệu không bao giờ đổi. Quyết định ở trạng thái **đề xuất** cho tới khi maintainer duyệt, khi
đó chuyển thành **đã duyệt**. Quyết định bị thay thế vẫn giữ trong danh sách, ghi **thay bằng Dn**, để không mất lý do.

## Đã duyệt

| # | Quyết định | Lý do |
|---|---|---|
| A1 | Chỉ một loại bản vẽ. Single part, assembly và GA là các preset thiết lập, không phải kiểu riêng hay giai đoạn riêng. | Maintainer, 2026-11-08: khác nhau ở đối tượng và quy tắc, không ở code. |
| A2 | Kết quả ghi ra DXF qua `DxfDatabase`. | Đã ghi được mọi entity bản vẽ cần và đã kiểm với AutoCAD 2021; bản vẽ mở được ở mọi CAD mà không cần Tekla. |
| A3 | Chỉ tham khảo Tekla qua Open API, tài liệu người dùng và kết quả xuất ra. Không dịch ngược file nhị phân của Tekla. | Giấy phép của Tekla, và package MIT trên NuGet; maintainer hỏi về Drawings.dll ngày 2026-11-08 và đây là câu trả lời. |
| A4 | Module nằm ở `src/GeometryHelper.Drawing`. | Maintainer tạo thư mục này, 2026-11-08. |
| A5 | Lập kế hoạch cấu trúc trước khi viết code; G1 (chiếu nét thấy/khuất ra DXF) là giai đoạn code đầu tiên. | Maintainer, 2026-11-08. |
| A6 | Các file kế hoạch viết bằng tiếng Việt; code, comment, docs thư viện và commit message vẫn bằng tiếng Anh. | Maintainer, 2026-11-08: để dễ review và cải thiện. |

## Đề xuất

| # | Quyết định | Nên chọn vì | Cách còn lại |
|---|---|---|---|
| D1 | File DXF là tờ giấy tỉ lệ 1:1 trong model space; mỗi view vẽ theo scale của nó; dimension đặt `DIMLFAC` bằng scale, nên vẫn ghi độ dài thật của model. | Mọi trình xem DXF đều hiển thị như khi in. Cách Tekla tự xuất DWG bố trí bản vẽ sẽ kiểm ở G0. | Model space tỉ lệ 1:1 theo model và mỗi view một viewport trong layout: đúng kiểu AutoCAD, nhưng nét khuất mỗi view khác nhau nên mỗi view vẫn cần một bản riêng, và nhiều trình xem chỉ hiện model space. |
| D2 | Loại nét khuất chính xác trên các mặt phẳng của solid; cạnh giữa các mặt ghép của bề mặt cong chỉ giữ ở chỗ là đường bao (silhouette). | Solid vốn đã là đa diện; tính chính xác trên đó giữ được tolerance của thư viện. | Qua lưới tam giác và depth buffer: đơn giản hơn, nhưng nét chỉ chính xác tới mức pixel. |
| D3 | Lỗ và bulông lấy từ dữ liệu của nguồn (bolt group và lỗ của Tekla) trước; nhận diện lỗ từ hình học thuần làm sau, cùng với IFC. | Tekla biết chính xác chúng; nhận diện từ hình học đã chia mặt là một dự án riêng. | Nhận diện từ hình học ngay từ đầu. |
| D4 | `GeometryHelper.Drawing` là một package riêng, phát hành cùng version với các package khác. | Module lớn, người chỉ dùng phần hình học không cần kéo theo; thư mục A4 khớp với các package khác. | Một namespace trong package chính. |
| D5 | Thiết lập là object C# có preset; đọc từ JSON làm sau nếu cần. | Ít code nhất; test dựng trực tiếp được. | Dùng JSON ngay từ đầu. |
| D6 | Chuỗi đoạn thẳng ngắn do đường tròn hay cung bị chia mặt để lại (lỗ, ống, góc lượn) được khớp lại thành cung trước khi ghi dimension và ghi file. | Lỗ ghi dimension theo tâm và vẽ bằng CIRCLE là điều người vẽ mong đợi. | Giữ nguyên các đoạn. |
| D7 | Test ở project riêng `tests/GeometryHelper.Drawing.UnitTest`. | Khớp cách mỗi thư viện một bộ test, và không làm chậm bộ test lõi. | Gộp vào `GeometryHelper.UnitTest`. |
| D8 | DXF version AC1027 (AutoCAD 2013), trừ khi G0 thấy entity nào cần version khác. | MLEADER và table ghi tốt ở version này, và AutoCAD 2021 mở được. | R2000 như `DxfWriter` đang ghi, nhưng R2000 không có MLEADER. |
| D9 | Mặc định phép chiếu góc thứ ba, phép chiếu góc thứ nhất là một tuỳ chọn. | Sẽ đối chiếu với thiết lập Tekla của maintainer ở G0. | Mặc định góc thứ nhất (ISO, châu Âu). |

## Câu hỏi còn mở

| # | Câu hỏi | Ai trả lời |
|---|---|---|
| Q1 | Lỗi nào của drawing Tekla quan trọng nhất? Câu này quyết định thứ tự làm trong G3 đến G5. Ghi vào [40-tekla-bugs.md](40-tekla-bugs.md). | maintainer |
| Q2 | Khung tên: dùng khung tên của maintainer (đọc bố cục từ một file DWG/DXF, không bao giờ commit vào repo) hay vẽ bằng code? | maintainer |
| Q3 | Ký hiệu hàn theo chuẩn nào: ISO 2553 hay AWS A2.4? | maintainer |
| Q4 | Các scale được dùng (ví dụ 1:1, 1:2, 1:5, 1:10, 1:15, 1:20, 1:25, 1:50, 1:100) và các khổ giấy được dùng. | maintainer, G0 đặt mặc định |
| Q5 | Mark: mark cấu kiện và mark assembly hiển thị gì (position, profile, chiều dài, vật liệu). | maintainer, G0 đặt mặc định |
