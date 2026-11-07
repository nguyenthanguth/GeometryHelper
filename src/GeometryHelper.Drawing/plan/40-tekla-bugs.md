# Các lỗi drawing của Tekla

Các lỗi mà module này phải làm đúng. Chúng quyết định thứ tự làm trong G3 đến G5 (Q1). Mỗi lỗi có một cấu kiện dựng
bằng code cho thấy lỗi đó, không bao giờ dùng model của maintainer.

| # | Lỗi (Tekla làm gì) | Đúng phải là | Test | Trạng thái |
|---|---|---|---|---|
| T1 | (maintainer điền vào) | | | |

Một số dạng lỗi thường gặp, để gợi ý khi điền:

- dimension chồng lên nhau, chồng lên cấu kiện, hoặc bị ghi lặp;
- mark che mark khác, che dimension hoặc che nét;
- nét khuất sai: hiện ở chỗ không nên hiện, hoặc bị thiếu;
- view xếp xấu, chồng nhau, hoặc ra ngoài tờ giấy;
- giá trị sai hoặc làm tròn sai;
- bản vẽ thay đổi khi không nên thay đổi sau khi model đổi, hoặc không thay đổi khi đáng lẽ phải đổi;
- chậm với assembly lớn.
