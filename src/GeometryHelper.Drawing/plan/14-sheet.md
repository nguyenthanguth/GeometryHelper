# L4: tờ giấy

## Tờ giấy

- khổ giấy: A0 đến A4 hoặc khổ tự chọn, lấy từ `PaperSize` của `GeometryHelper.Packing`, đặt dọc hay ngang đều được;
- khung và lề;
- khung tên ở một góc, chữ điền từ nguồn (dự án, số bản vẽ, revision) và từ thiết lập (công ty, người vẽ, ngày);
- bảng vật tư: mark, số lượng, profile, vật liệu, chiều dài, khối lượng, mỗi mark một dòng, ghi bằng table DXF;
- bảng revision.

Khung tên vẽ bằng code, hoặc lấy từ một bản vẽ maintainer cung cấp (Q2). Khung tên do maintainer cung cấp được đọc lúc
chạy từ file của người dùng, không bao giờ chép vào repo.

## Xếp view

- kích thước mỗi view gồm nét cộng chú thích, theo scale của view;
- view đứng đặt trước, các view khác đặt theo phép chiếu (D9) so với nó, mặt cắt và chi tiết đặt ở chỗ còn trống;
- xếp qua `SheetPacker.Pack`. `SheetPacker` không có vùng cấm (maintainer đã không chọn tính năng này khi làm packing),
  nên các view được xếp vào phần giấy còn lại sau khi trừ dải dành cho khung tên và các bảng. Có nên thêm vùng cấm cho
  packer hay không thì để G5 cho thấy, và maintainer quyết định;
- nếu view không vừa, giảm scale (L2) rồi xếp lại; nếu vẫn không vừa thì tăng khổ giấy khi thiết lập cho phép, còn không
  thì bản vẽ báo lại.

Không gì được chồng lên nhau và không gì được ra ngoài tờ giấy: có test kiểm cả hai điều trên mọi cấu kiện mẫu.
