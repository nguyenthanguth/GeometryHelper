# L3: chú thích

## Các điểm cần đo

Trước khi ghi dimension, mỗi view liệt kê những điểm có thể đo tới, mỗi điểm kèm đối tượng và chi tiết sinh ra nó:

- hai đầu và các góc đường bao của mỗi cấu kiện, như nhìn thấy trong view;
- tâm lỗ và tâm bulông, kèm nhóm của chúng;
- work point và đường tham chiếu của cấu kiện chính;
- mép các vết cắt, vát và chi tiết gắn thêm.

## Dimension

Các quy tắc, bật tắt qua thiết lập, quyết định điểm nào được đo và đo thế nào:

| Quy tắc | Cho ra |
|---|---|
| Tổng | chiều dài, rộng, cao tổng của cấu kiện chính |
| Chuỗi | dimension nối tiếp giữa các điểm liên tiếp |
| Cộng dồn | tất cả tính từ một gốc, trên một đường giống ordinate |
| Lỗ theo nhóm | bước lỗ của bolt group dạng "n x p = tổng" và khoảng cách tới đầu cấu kiện |
| Chi tiết gắn thêm | vị trí mỗi chi tiết tính từ đầu hoặc work point của cấu kiện chính |

Cách đặt:

- các đường dimension xếp thành tầng ngoài view, dimension tổng nằm ngoài cùng, khoảng cách giữa các tầng là thiết lập
  theo mm trên giấy;
- không đường dimension nào cắt đường khác, không chữ nào đè lên nét hay chữ khác; có test đếm chỗ chồng nhau trên mọi
  cấu kiện mẫu và yêu cầu bằng 0;
- các dimension bằng nhau liền kề có thể gộp thành "n x p";
- trong một view không có dimension nào bị ghi lặp;
- giá trị là độ dài thật của model (D1: `DIMLFAC`), làm tròn theo thiết lập.

Dimension ghi ra là entity DIMENSION thật (L5), nên người dùng kéo được trong CAD mà giá trị vẫn đúng.

## Mark

- mark cấu kiện, mark assembly và mark bulông; mỗi loại hiển thị gì do thiết lập quyết định (Q5);
- đặt qua `Arranging` (`Arranger.Run`), lấy nét và dimension của view làm vật cản, để mark không che mark khác, không
  che dimension, và không che nét ở chỗ tránh được;
- đường dẫn từ mỗi mark tới một điểm bên trong đối tượng của nó, ghi bằng MLEADER.

## Ký hiệu

- đường tâm của lỗ, bolt group và ống;
- ký hiệu mặt cắt (đường cắt kèm chữ và mũi tên) và ký hiệu chi tiết (vòng tròn kèm chữ);
- ký hiệu hàn (Q3) trên đường dẫn tới mối hàn;
- ký hiệu cao độ cho bản vẽ GA.
