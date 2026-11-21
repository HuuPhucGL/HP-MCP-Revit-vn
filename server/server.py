"""HP Revit MCP server.

Cong dung : Cau noi giua Claude (MCP, stdio) va add-in HP Revit MCP chay trong Revit
            (TCP 127.0.0.1:9877, JSON 1 dong). Don vi dau vao/dau ra: mm.
Ngay viet : 2026-11-21
"""
import itertools
import json
import os
import socket

from mcp.server.fastmcp import FastMCP, Image

HOST = "127.0.0.1"
PORT = int(os.environ.get("HP_REVIT_PORT", "9877"))
_ids = itertools.count(1)

mcp = FastMCP("hp-revit")


def _call(method: str, params: dict | None = None, timeout: float = 130.0):
    """Gui 1 lenh toi Revit va cho ket qua."""
    req = {"id": next(_ids), "method": method, "params": params or {}}
    try:
        s = socket.create_connection((HOST, PORT), timeout=5)
    except OSError as e:
        raise RuntimeError(
            "Khong ket noi duoc Revit tai 127.0.0.1:%d (%s). Kiem tra: Revit da mo chua, "
            "add-in 'HP Revit MCP' da nap chua (khi Revit hoi, chon 'Always Load')." % (PORT, e)
        )
    with s:
        s.settimeout(timeout)
        s.sendall((json.dumps(req) + "\n").encode("utf-8"))
        buf = b""
        while b"\n" not in buf:
            chunk = s.recv(1 << 16)
            if not chunk:
                break
            buf += chunk
    if not buf:
        raise RuntimeError("Revit dong ket noi ma khong tra ket qua.")
    resp = json.loads(buf.split(b"\n", 1)[0].decode("utf-8"))
    if not resp.get("ok"):
        raise RuntimeError("Loi trong Revit: %s" % resp.get("error"))
    return resp.get("result")


@mcp.tool()
def revit_ping() -> dict:
    """Kiem tra ket noi Revit: phien ban Revit, add-in, ten du an dang mo, view dang mo."""
    return _call("ping", timeout=15)


@mcp.tool()
def revit_doc_info() -> dict:
    """Thong tin du an dang mo: ten, duong dan, so Level, so view, thong tin du an."""
    return _call("doc_info", timeout=30)


@mcp.tool()
def revit_list_levels() -> list:
    """Liet ke Level (ten, id, cao do mm), sap theo cao do."""
    return _call("list_levels", timeout=30)


@mcp.tool()
def revit_count_by_category() -> list:
    """Dem so phan tu theo nhom doi tuong (Walls, Doors, Floors...), nhieu nhat truoc."""
    return _call("count_by_category", timeout=60)


@mcp.tool()
def revit_get_elements(category: str, limit: int = 50) -> dict:
    """Liet ke phan tu cua mot nhom. category dung ten OST_ (OST_Walls, OST_Doors, OST_Windows,
    OST_Floors, OST_StructuralColumns, OST_StructuralFraming, OST_Rooms...). Toi da 500 phan tu."""
    return _call("get_elements", {"category": category, "limit": limit}, timeout=60)


@mcp.tool()
def revit_get_element_params(element_id: int) -> dict:
    """Doc toan bo tham so (ten, gia tri, kieu, chi doc) cua mot phan tu theo id."""
    return _call("get_element_params", {"element_id": element_id}, timeout=30)


@mcp.tool()
def revit_create_level(elevation_mm: float, name: str | None = None) -> dict:
    """Tao Level moi tai cao do (mm). Hoan tac duoc bang Ctrl+Z trong Revit."""
    return _call("create_level", {"elevation_mm": elevation_mm, "name": name})


@mcp.tool()
def revit_create_wall(level: str, x1_mm: float, y1_mm: float, x2_mm: float, y2_mm: float,
                      height_mm: float = 3000, wall_type: str | None = None) -> dict:
    """Tao 1 doan tuong tu diem (x1,y1) den (x2,y2) (mm) tren Level cho truoc.
    wall_type: ten loai tuong (bo trong = loai mac dinh)."""
    return _call("create_wall", {"level": level, "x1_mm": x1_mm, "y1_mm": y1_mm,
                                 "x2_mm": x2_mm, "y2_mm": y2_mm,
                                 "height_mm": height_mm, "wall_type": wall_type})


@mcp.tool()
def revit_set_parameter(element_id: int, name: str, value: str, unit: str = "") -> dict:
    """Sua 1 tham so cua phan tu. Tham so so thuc: unit='mm' (do dai) hoac 'raw' (don vi trong).
    Khong ho tro xoa phan tu."""
    return _call("set_parameter", {"element_id": element_id, "name": name, "value": value, "unit": unit})


@mcp.tool()
def revit_list_views() -> list:
    """Liet ke view (khong gom sheet va template): id, ten, loai, ty le (vd 50 = 1/50)."""
    return _call("list_views", timeout=30)


@mcp.tool()
def revit_list_sheets() -> list:
    """Liet ke sheet: so hieu, ten, so view da dat."""
    return _call("list_sheets", timeout=30)


@mcp.tool()
def revit_create_sheet(number: str, name: str, titleblock: str | None = None) -> dict:
    """Tao sheet (vd number='KT-01', name='MAT BANG TANG 1'). titleblock: ten khung ten
    ('Ho ten family : Ten type' hoac chi ten type); bo trong = sheet khong khung ten."""
    return _call("create_sheet", {"number": number, "name": name, "titleblock": titleblock})


@mcp.tool()
def revit_place_view(sheet_number: str, view_name: str, x_mm: float = 210, y_mm: float = 150) -> dict:
    """Dat view len sheet tai toa do (mm) tinh tu goc duoi-trai sheet. Mac dinh gan giua A3 ngang."""
    return _call("place_view", {"sheet_number": sheet_number, "view_name": view_name, "x_mm": x_mm, "y_mm": y_mm})


@mcp.tool()
def revit_set_view_scale(view_name: str, scale: int = 50) -> dict:
    """Dat ty le view (50 = 1/50, 100 = 1/100)."""
    return _call("set_view_scale", {"view_name": view_name, "scale": scale})


@mcp.tool()
def revit_export_pdf(folder: str, file_name: str, sheet_numbers: list[str] | None = None,
                     combine: bool = True) -> dict:
    """Xuat sheet ra PDF kho A3 (Revit 2022 tro len). sheet_numbers bo trong = tat ca sheet.
    combine=True gop 1 file. Khong ghi de file co san. folder: thu muc dich (tao neu chua co)."""
    return _call("export_pdf", {"folder": folder, "file_name": file_name,
                                "sheet_numbers": sheet_numbers or [], "combine": combine}, timeout=300)


@mcp.tool()
def revit_screenshot(width: int = 1600) -> Image:
    """Chup anh view dang mo trong Revit (PNG) de kiem tra."""
    res = _call("screenshot", {"width": width}, timeout=90)
    path = res["path"]
    with open(path, "rb") as f:
        data = f.read()
    try:
        os.remove(path)
    except OSError:
        pass
    return Image(data=data, format="png")


if __name__ == "__main__":
    mcp.run()
