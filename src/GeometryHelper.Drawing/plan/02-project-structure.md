# Cấu trúc dự án

## Project

`src/GeometryHelper.Drawing/GeometryHelper.Drawing.csproj`, tạo ở G1 khi bắt đầu có code:

- target netstandard2.0 như các thư viện khác;
- chỉ tham chiếu `GeometryHelper`, không bao giờ tham chiếu Tekla;
- version lấy từ `GeometryHelperVersion` trong `Directory.Build.props`, đóng gói thành `GeometryHelper.Drawing` và phát
  hành cùng lúc với các package khác;
- mọi thành viên public có XML documentation (thiếu thì build `-warnaserror` báo lỗi CS1591);
- thêm vào `GeometryHelper.slnx`.

## Thư mục

```
src/GeometryHelper.Drawing/
  README.md                       giới thiệu module và mục lục kế hoạch
  plan/                           kế hoạch này (*.md)
  GeometryHelper.Drawing.csproj
  Drawing.cs                      kết quả: các view và tờ giấy, theo mm trên giấy
  DrawingBuilder.cs               nguồn + thiết lập -> Drawing
  Source/
    DrawingObject.cs              một đối tượng: solid, loại, tên, mark, profile, vật liệu, cha, thuộc tính
    DrawingObjectKind.cs          Part, Bolt, Hole, Weld, Rebar, ...
    DrawingSource.cs              các đối tượng của một bản vẽ, và hệ trục của bản vẽ
  Projection/
    ViewProjector.cs              điểm vào của L1
    ProjectedView2.cs             các nét và mặt cắt của một view
    ProjectedEdge2.cs             một đoạn thẳng hoặc cung 2D, thấy/khuất, loại nét, đối tượng
    EdgeVisibility.cs             Visible, Hidden
    EdgeKind.cs                   Sharp, Silhouette, SectionOutline, ...
    Internal/                     lấy cạnh, tính che khuất, chỉ mục không gian
  Views/
    DrawingView.cs                một view: hệ trục, scale, vùng cắt, độ sâu, nét chiếu, chú thích
    ViewKind.cs                   Front, Top, Bottom, Left, Right, Back, Section, Detail, Isometric
    ViewFrame3.cs                 hệ trục của view và khoảng độ sâu
    ViewBuilder.cs                các view mà thiết lập yêu cầu
  Annotation/
    Dimensions/                   điểm cần đo, quy tắc dimension, chuỗi, các tầng
    Marks/                        mark cấu kiện, assembly, bulông, đường dẫn
    Symbols/                      đường tâm, ký hiệu hàn, cao độ, ký hiệu mặt cắt và chi tiết
  Sheets/
    DrawingSheet.cs               tờ giấy: khổ, khung, khung tên, các bảng
    TitleBlock.cs
    PartList.cs
    SheetLayout.cs                xếp view qua SheetPacker
  Output/
    DrawingDxfWriter.cs           Drawing -> DxfDatabase -> file
  Settings/
    DrawingSettings.cs            và các phần theo từng lớp, và các preset

src/GeometryHelper.TeklaConvert/
  DrawingSourceConvert.cs         assembly và part của Tekla -> DrawingSource

tests/GeometryHelper.Drawing.UnitTest/       xUnit net48, như các bộ test khác
  TestBodies.cs                   các cấu kiện của 20-test-cases.md, dựng bằng code
  Projection/ Views/ Annotation/ Sheets/ Output/ Builder/

benchmarks/GeometryHelper.Benchmarks/Scenarios/
  Drawing*.cs                     đo thời gian chiếu nét và cả bản vẽ, có chữ ký kết quả

src/GeometryHelper/docs/drawing/  (hoặc thư mục docs của project này, quyết định ở G7)
  index.md và mỗi lớp một trang, thành một nhóm trong menu docs có trang mở đầu riêng
```

Để test ở project riêng thì bộ test drawing không làm chậm bộ test lõi, và khớp với cách mỗi thư viện một bộ test. Giữ
riêng hay gộp vào `GeometryHelper.UnitTest` là quyết định D7 trong [03-decisions.md](03-decisions.md).

## Verify

Khi đã có project, bước verify trong CLAUDE.md thêm một dòng:

```bash
dotnet test tests/GeometryHelper.Drawing.UnitTest/GeometryHelper.Drawing.UnitTest.csproj -c Release --no-build
```

và lệnh build `GeometryHelper.slnx` sẽ build luôn các project mới. Thay đổi nào chạm tới đường code mà benchmark đo thì
chạy cả benchmark.

## Xem kết quả

- AutoCAD 2021 qua accoreconsole, với plugin của leader (mở file, AUDIT, đếm entity), như các lần làm DXF trước.
- Viewer có sẵn trong repo (`tools/GeometryHelper.Viewer`), đọc được DXF.
- Ghi SVG của một view qua `SvgWriter` để xem nhanh trên trình duyệt khi đang làm L1.
