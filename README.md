# HP MCP Revit (tiếng Việt)

Bộ cài kết nối **Claude** (Claude Desktop, Claude Code) với **Revit** qua MCP.
Sau khi cài, anh/chị nói với Claude bằng tiếng Việt, ví dụ *"liệt kê các Level"*, *"đếm số tường, cửa trong mô hình"*,
*"chụp ảnh view đang mở"*, *"tạo tường dài 5 m trên TẦNG 1"* — Claude sẽ điều khiển Revit đang mở.

Bản này do **Nguyễn Hữu Phúc** viết hoàn toàn mới. Giấy phép MIT (xem [LICENSE](LICENSE)). Không dùng mã của bên thứ ba
(chỉ dùng thư viện Revit API chính thức của Autodesk khi biên dịch).

## Bộ này gồm những gì

| Thành phần | Nằm ở đâu | Dùng để làm gì |
|---|---|---|
| Add-in Revit "HP Revit MCP" | `addin/` (mã C#), `dist/addin/<năm>/HPRevitMcp.dll` (đã biên dịch sẵn) | Chạy trong Revit, mở cổng nội bộ `127.0.0.1:9877` (chỉ máy mình truy cập được) |
| MCP server | `server/server.py` | "Người phiên dịch" giữa Claude và add-in |
| Bộ cài tự động | `CaiDat.bat`, `installer/install.ps1` | Tìm các bản Revit đã cài, chép add-in, cài server, đăng ký với Claude |
| Gỡ cài đặt | `GoCai.bat` | Gỡ add-in và bỏ đăng ký |

MCP đăng ký tên **`hp-revit`** (khác tên `revit` của bộ khác nên có thể cài song song).

Các công cụ Claude có:

| Công cụ | Việc làm | Ghi? |
|---|---|---|
| `revit_ping` | Phiên bản Revit, tên dự án, view đang mở | Không |
| `revit_doc_info` | Thông tin dự án, số Level, số view | Không |
| `revit_list_levels` | Danh sách Level + cao độ (mm) | Không |
| `revit_count_by_category` | Đếm phần tử theo nhóm (tường, cửa, sàn...) | Không |
| `revit_get_elements` | Liệt kê phần tử của 1 nhóm (OST_Walls, OST_Doors...) | Không |
| `revit_get_element_params` | Đọc toàn bộ tham số của 1 phần tử | Không |
| `revit_screenshot` | Chụp ảnh view đang mở | Không |
| `revit_create_level` | Tạo Level | Có |
| `revit_create_wall` | Tạo 1 đoạn tường theo 2 điểm (mm) | Có |
| `revit_set_parameter` | Sửa 1 tham số | Có |

Đơn vị vào/ra đều là **mm**. Mỗi lệnh ghi nằm trong 1 Transaction nên **Ctrl+Z hoàn tác được**.
**Không có lệnh xóa** phần tử — cố ý để an toàn.

## Phiên bản Revit hỗ trợ

Mỗi phiên bản Revit cần một file `HPRevitMcp.dll` riêng (đã biên dịch sẵn trong `dist/addin/`).

| Revit | .NET | Tình trạng |
|---|---|---|
| 2024 | .NET Framework 4.8 | **Đã chạy thử** trên Windows 11 |
| 2017, 2018, 2019, 2020, 2021, 2022, 2023 | .NET Framework 4.8 | Đã biên dịch được, **cần kiểm tra** khi chạy thật |
| 2025, 2026 | .NET 8 | Đã biên dịch được, **cần kiểm tra** khi chạy thật |
| 2016 trở về trước | — | Không hỗ trợ |

"Biên dịch được" nghĩa là mã khớp với Revit API của năm đó, chưa nghĩa là đã chạy thử trong Revit. Ai chạy được trên bản "cần kiểm tra",
xin báo qua mục Issues để cập nhật bảng. Revit LT không có add-in nên không dùng được.

## Yêu cầu

- Windows 10/11.
- Revit 2017 trở lên (bản đầy đủ).
- **Python 3.10 trở lên** (python.org, nhớ tích *Add python.exe to PATH*) hoặc [uv](https://docs.astral.sh/uv/).
- Claude Desktop hoặc Claude Code.

## Cài đặt từng bước

### Bước 1. Tải bộ cài về
Trên trang GitHub bấm **Code → Download ZIP**, giải nén ra `C:\HP-MCP-Revit-vn` (đường dẫn không dấu).
Hoặc:

```powershell
git clone https://github.com/HuuPhucGL/HP-MCP-Revit-vn.git C:\HP-MCP-Revit-vn
```

### Bước 2. Đóng Revit, chạy bộ cài
**Bấm đúp `CaiDat.bat`.** Bộ cài sẽ:

1. Tìm các bản Revit đã cài thật (có file `Revit.exe`).
2. Chép add-in vào `%APPDATA%\Autodesk\Revit\Addins\<năm>\` của **từng bản** (file `HPRevitMcp.addin` + thư mục `HPRevitMcp`). Bản cũ (nếu có) được sao lưu vào `%LOCALAPPDATA%\HP-MCP-Revit\_backup\`.
3. Cài MCP server vào `%LOCALAPPDATA%\HP-MCP-Revit` (môi trường Python riêng, thư viện `mcp`).
4. Đăng ký MCP `hp-revit` với Claude Code và Claude Desktop (file cấu hình có sao lưu `.bak-...` trước khi sửa).

Tuỳ chọn (PowerShell):

```powershell
powershell -ExecutionPolicy Bypass -File installer\install.ps1 -Phienban 2025
```

| Tuỳ chọn | Ý nghĩa |
|---|---|
| `-Phienban 2025,2026` | Chỉ định tay năm Revit mà bộ cài không tự tìm thấy |
| `-ChiAddin` | Chỉ chép add-in |
| `-BoQuaClaude` | Không đăng ký Claude |
| `-GoCai` | Gỡ (giống `GoCai.bat`) |

### Bước 3. Mở Revit
1. Mở Revit. Lần đầu Revit hỏi về add-in **không có chữ ký số** (vì đây là mã tự biên dịch): chọn **Always Load**.
2. Mở một dự án `.rvt` (add-in chỉ trả lời khi có dự án đang mở, trừ lệnh `revit_ping`).

### Bước 4. Khởi động lại Claude và thử
Tắt hẳn Claude Desktop (cả biểu tượng khay đồng hồ) rồi mở lại, hoặc mở phiên Claude Code mới. Gõ:

> Kiểm tra kết nối Revit

Claude trả về phiên bản Revit và tên dự án là thành công.

## Tự biên dịch (cho người muốn sửa mã)

Cần [.NET SDK](https://dotnet.microsoft.com/download) 8 trở lên. Biên dịch cho một năm:

```powershell
dotnet build addin\HPRevitMcp.csproj -c Release -p:RevitYear=2024
```

File ra ở `dist\addin\2024\HPRevitMcp.dll`. Năm hợp lệ: 2017 đến 2026. Thư viện Revit API được tải tự động từ NuGet (gói `Nice3point.Revit.Api.*`), không cần cài Revit để biên dịch.

## Lỗi thường gặp

| Hiện tượng | Cách xử lý |
|---|---|
| Claude báo "Khong ket noi duoc Revit tai 127.0.0.1:9877" | Revit chưa mở, hoặc add-in chưa nạp (lần đầu phải chọn *Always Load*; kiểm tra thẻ Add-Ins) |
| Báo "Revit khong tra loi trong 120 giay" | Revit đang mở hộp thoại hoặc đang chạy lệnh khác — đóng hộp thoại rồi thử lại |
| "Chua mo du an nao" | Mở file `.rvt` trước |
| Cổng 9877 bị chiếm | Chỉ một cửa sổ Revit giữ được cổng; Revit mở sau sẽ báo lỗi cổng lúc khởi động |
| Không thấy công cụ `hp-revit` trong Claude | Khởi động lại Claude sau khi cài; kiểm tra bằng `claude mcp list` |
| Bộ cài báo thiếu Python | Cài Python 3.10+ rồi chạy lại `CaiDat.bat` |
| Tên nhóm đối tượng | Dùng tên `OST_...` (OST_Walls, OST_Doors, OST_Windows, OST_Floors, OST_Rooms...), không phụ thuộc ngôn ngữ Revit |

## An toàn

- Add-in chỉ nghe kết nối từ **chính máy này** (`127.0.0.1`).
- Không có lệnh xóa. Lệnh ghi hoàn tác được bằng Ctrl+Z. Vẫn nên **lưu/sao lưu file `.rvt` trước khi nhờ Claude sửa**.
- Không cần tài khoản, mật khẩu hay khóa API.

## Giấy phép

MIT — xem [LICENSE](LICENSE). Lịch sử thay đổi: [CHANGELOG.md](CHANGELOG.md).
