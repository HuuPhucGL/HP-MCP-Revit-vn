// HP Revit MCP - lenh ho so: view, sheet, xuat PDF
// Cong dung : Tao sheet, dat view len sheet, dat ty le view, xuat PDF khong ghi de.
// Ngay viet : 2026-11-21
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HPRevitMcp
{
    internal static partial class Commands
    {
        private static ViewSheet FindSheet(Document doc, string number)
        {
            var s = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                .FirstOrDefault(x => x.SheetNumber == number);
            if (s == null) throw new ArgumentException("Khong co sheet so hieu '" + number + "'.");
            return s;
        }

        private static View FindView(Document doc, string name)
        {
            var v = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                .FirstOrDefault(x => !x.IsTemplate && !(x is ViewSheet) && x.Name == name);
            if (v == null) throw new ArgumentException("Khong co view ten '" + name + "'.");
            return v;
        }

        private static object ListViews(UIApplication app)
        {
            var doc = Doc(app);
            return new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                .Where(v => !v.IsTemplate && !(v is ViewSheet))
                .OrderBy(v => v.ViewType.ToString()).ThenBy(v => v.Name)
                .Select(v => Obj("id", IdOf(v.Id), "name", v.Name, "type", v.ViewType.ToString(), "scale", v.Scale)).ToList();
        }

        private static object ListSheets(UIApplication app)
        {
            var doc = Doc(app);
            return new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                .OrderBy(s => s.SheetNumber)
                .Select(s => Obj("id", IdOf(s.Id), "number", s.SheetNumber, "name", s.Name,
                                 "view_count", s.GetAllPlacedViews().Count)).ToList();
        }

        private static object CreateSheet(UIApplication app, Dictionary<string, object> p)
        {
            var doc = Doc(app);
            string number = Str(p, "number");
            string name = Str(p, "name", "");
            string tb = Str(p, "titleblock");
            if (string.IsNullOrEmpty(number)) throw new ArgumentException("Thieu 'number' (vd KT-01).");
            if (new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Any(x => x.SheetNumber == number))
                throw new ArgumentException("Sheet '" + number + "' da ton tai.");
            var symbols = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType().Cast<FamilySymbol>().ToList();
            ElementId tbId = ElementId.InvalidElementId;
            if (!string.IsNullOrEmpty(tb))
            {
                var fs = symbols.FirstOrDefault(x => x.Name == tb || (x.FamilyName + " : " + x.Name) == tb);
                if (fs == null)
                    throw new ArgumentException("Khong co khung ten '" + tb + "'. Hien co: " +
                        string.Join("; ", symbols.Select(x => x.FamilyName + " : " + x.Name)));
                tbId = fs.Id;
            }
            using (var t = new Transaction(doc, "HP MCP: tao sheet"))
            {
                t.Start();
                var sh = ViewSheet.Create(doc, tbId);
                sh.SheetNumber = number;
                sh.Name = name;
                t.Commit();
                return Obj("id", IdOf(sh.Id), "number", sh.SheetNumber, "name", sh.Name,
                           "has_titleblock", tbId != ElementId.InvalidElementId);
            }
        }

        private static object PlaceView(UIApplication app, Dictionary<string, object> p)
        {
            var doc = Doc(app);
            var sh = FindSheet(doc, Str(p, "sheet_number"));
            var v = FindView(doc, Str(p, "view_name"));
            if (!Viewport.CanAddViewToSheet(doc, sh.Id, v.Id))
                throw new InvalidOperationException("Khong dat duoc view '" + v.Name + "' len sheet (view da nam tren sheet khac, hoac loai view khong dat duoc).");
            double x = Num(p, "x_mm", 210) / FtToMm, y = Num(p, "y_mm", 150) / FtToMm;
            using (var t = new Transaction(doc, "HP MCP: dat view len sheet"))
            {
                t.Start();
                var vp = Viewport.Create(doc, sh.Id, v.Id, new XYZ(x, y, 0));
                t.Commit();
                return Obj("viewport_id", IdOf(vp.Id), "sheet", sh.SheetNumber, "view", v.Name, "scale", v.Scale);
            }
        }

        private static object SetViewScale(UIApplication app, Dictionary<string, object> p)
        {
            var doc = Doc(app);
            var v = FindView(doc, Str(p, "view_name"));
            int scale = (int)Num(p, "scale");
            using (var t = new Transaction(doc, "HP MCP: dat ty le view"))
            {
                t.Start();
                v.Scale = scale;
                t.Commit();
            }
            return Obj("view", v.Name, "scale", v.Scale);
        }

        // Xuat PDF: Revit 2022 tro len co PDFExportOptions; ban cu thi in tay bang may in PDF.
        private static object ExportPdf(UIApplication app, Dictionary<string, object> p)
        {
#if REVIT2022_OR_GREATER
            var doc = Doc(app);
            string folder = Str(p, "folder");
            string fileName = Str(p, "file_name");
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(fileName)) throw new ArgumentException("Can 'folder' va 'file_name'.");
            if (fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) fileName = fileName.Substring(0, fileName.Length - 4);
            bool combine = true;
            object cbv;
            if (p.TryGetValue("combine", out cbv) && cbv is bool) combine = (bool)cbv;
            List<ViewSheet> chosen = new List<ViewSheet>();
            object nums;
            if (p.TryGetValue("sheet_numbers", out nums) && nums is List<object> && ((List<object>)nums).Count > 0)
                foreach (var n in (List<object>)nums) chosen.Add(FindSheet(doc, Convert.ToString(n)));
            else
                chosen = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().ToList();
            if (chosen.Count == 0) throw new InvalidOperationException("Du an chua co sheet nao de xuat.");
            chosen = chosen.OrderBy(s => s.SheetNumber).ToList();
            Directory.CreateDirectory(folder);
            string target = Path.Combine(folder, fileName + ".pdf");
            if (combine && File.Exists(target))
                throw new InvalidOperationException("File da ton tai, khong ghi de: " + target + ". Hay doi ten file.");
            var before = new HashSet<string>(Directory.GetFiles(folder, "*.pdf"));
            var opt = new PDFExportOptions
            {
                Combine = combine,
                FileName = fileName,
                PaperFormat = ExportPaperFormat.ISO_A3,
                ColorDepth = ColorDepthType.Color,
                HideCropBoundaries = true,
                HideScopeBoxes = true,
                HideReferencePlane = true,
                HideUnreferencedViewTags = true
            };
            bool ok = doc.Export(folder, chosen.Select(s => s.Id).ToList(), opt);
            if (!ok) throw new InvalidOperationException("Revit xuat PDF khong thanh cong.");
            var files = Directory.GetFiles(folder, "*.pdf").Where(f => !before.Contains(f)).ToList();
            return Obj("folder", folder, "sheets", chosen.Select(s => s.SheetNumber + " - " + s.Name).ToList(), "files", files);
#else
            throw new NotSupportedException("Revit truoc 2022 khong co xuat PDF qua API. Hay In va chon may in PDF thu cong, hoac dung Revit 2022 tro len.");
#endif
        }
    }
}
