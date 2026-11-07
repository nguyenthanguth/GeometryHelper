# L1: chiếu nét và nét khuất

Lõi của mọi bản vẽ, và là phần thư viện chưa có. Với các solid và một hệ trục view cho trước, lớp này trả về các nét 2D
của view: nét nào thấy, nét nào khuất, và các mặt bị mặt cắt cắt qua.

## Đầu vào và đầu ra

Đầu vào:

- các solid, mỗi solid kèm id của đối tượng;
- hệ trục view (`ViewFrame3`): x sang phải tờ giấy, y lên trên, z hướng về người nhìn; độ sâu gần và xa; với mặt cắt thì
  thêm mặt phẳng cắt và phía được giữ lại;
- một `Tolerance`.

Đầu ra, `ProjectedView2`:

- các `ProjectedEdge2`, mỗi cái gồm:
  - hình: một `GeoLine2`, hoặc một `GeoArc2` sau khi khớp cung (D6);
  - `EdgeVisibility`: Visible hoặc Hidden;
  - `EdgeKind`: Sharp, Silhouette hoặc SectionOutline;
  - id của đối tượng;
- các mặt cắt: mỗi đối tượng bị cắt một `GeoFace2`, để vẽ hatch;
- tất cả theo mm của view ở tỉ lệ 1:1; scale của view do L2 áp sau.

## Phương pháp (đề xuất, D2)

Solid là đa diện: mọi mặt là đa giác phẳng. Nét khuất được loại chính xác trên đó.

1. **Chuyển hệ trục.** Đưa mọi solid về hệ trục của view.
2. **Cạnh ứng viên.** Với mỗi cạnh của solid và hai mặt kề nó:
   - cạnh sắc (hai mặt gặp nhau ở góc lớn hơn góc trơn, là một thiết lập) là ứng viên;
   - cạnh trơn (giữa hai mặt ghép của cùng một bề mặt cong, như ống hay góc lượn) chỉ là ứng viên ở chỗ nó là đường bao:
     một mặt quay về phía người nhìn, mặt kia quay đi;
   - cạnh nhìn thẳng từ đầu (hình chiếu ngắn hơn point tolerance) bị bỏ.
3. **Mặt che.** Các mặt quay về phía người nhìn, chiếu xuống 2D, mỗi mặt kèm mặt phẳng của nó để tính độ sâu. Mặt nhìn
   thấy dưới dạng một đường (edge-on) thì không che.
4. **Che khuất.** Với mỗi cạnh ứng viên, tìm các mặt che có hộp 2D giao với hộp của cạnh, qua một chỉ mục không gian:
   - cắt đoạn 2D của cạnh theo đa giác của mặt che (kể cả lỗ), được các khoảng tham số;
   - trong mỗi khoảng, hiệu độ sâu giữa cạnh và mặt thay đổi tuyến tính dọc theo cạnh, nên đổi dấu nhiều nhất một lần:
     chia cạnh tại đó, phần nằm sau mặt là phần khuất;
   - các mặt chứa cạnh không bao giờ che cạnh đó; mặt mà cạnh nằm trên nó (trong tolerance) cũng không che.
   Hợp các khoảng khuất là phần khuất; phần còn lại là phần thấy.
5. **Dọn dẹp.**
   - Bỏ các đoạn ngắn hơn point tolerance.
   - Nối các đoạn thẳng hàng chạm nhau, cùng trạng thái thấy/khuất và cùng đối tượng.
   - Hai đối tượng chạm nhau mà cạnh trùng lên nhau thì chỉ vẽ một nét: thấy được ưu tiên hơn khuất, và id nhỏ hơn giữ
     nét, để mọi lần chạy cho cùng một kết quả.
6. **Cung (D6).** Chuỗi đoạn ngắn nằm trên cùng một đường tròn trong tolerance được thay bằng một cung, hoặc một đường tròn
   nếu khép kín.
7. **Thứ tự.** Sắp các cạnh theo id đối tượng, loại nét, trạng thái thấy/khuất và toạ độ, có tie-break đầy đủ.

## Mặt cắt

- Các mặt bị cắt lấy từ `GeoSolid3.Section(plane, tolerance)`.
- Phần nằm ở phía được giữ lại được vẽ bằng cách chiếu phần đó của mỗi solid. Có hai cách, G0 sẽ cho thấy cách nào chắc
  chắn hơn:
  - tách solid bằng `GeoSolid3.TrySplitBy`;
  - hoặc cắt các cạnh ứng viên và mặt che theo nửa không gian.
- Các mặt bị cắt che những gì nằm sau chúng.

## Tốc độ

- Chỉ mục không gian quét theo trục có ít cặp nhất, không theo một trục cố định (bẫy tốc độ trong CLAUDE.md).
- Mục tiêu, chốt ở G0: ba view của một assembly 50 cấu kiện xong trong chưa tới 1 giây.
- Benchmark ký kết quả đến từng bit, để mọi lần tăng tốc chứng minh được là không đổi kết quả.

## Các trường hợp khó cần test

- Cạnh xuyên qua một mặt: khuất ở một phía, thấy ở phía kia.
- Các mặt đồng phẳng của hai cấu kiện chạm nhau.
- Mặt lệch phẳng một chút, như mặt cắt của Tekla (lịch sử tolerance 1E-2 của thư viện).
- Ống nhìn dọc trục (chỉ thấy đường tròn), nhìn ngang (hai đường bao) và nhìn xiên.
- Vật dài và mảnh dọc theo trục quét (bẫy bậc hai).
- Lỗ ô van, lỗ xuyên qua cánh, bulông nằm trong lỗ.
