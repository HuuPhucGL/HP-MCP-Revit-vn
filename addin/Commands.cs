// HP Revit MCP - cac lenh
// Cong dung : Thuc hien lenh tu Claude tren luong chinh cua Revit.
// Don vi    : Dau vao/dau ra tinh bang mm (Revit trong bo la feet, 1 ft = 304.8 mm).
// An toan   : Khong co lenh xoa. Lenh ghi chay trong Transaction rieng (Ctrl+Z hoan tac duoc).
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
        private const double FtToMm = 304.8;

        // ---------- Tien ich ----------
        private static long IdOf(ElementId id)
        {
#if REVIT2024_OR_GREATER
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }

        private static ElementId MakeId(long v)
        {
#if REVIT2024_OR_GREATER
            return new ElementId(v);
#else
            return new ElementId((int)v);
#endif
        }

        private static double Mm(double feet) { return Math.Round(feet * FtToMm, 1); }

        private static string Str(Dictionary<string, object> p, string k, string def = null)
        {
            object v;
            return p.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : def;
        }

        private static double Num(Dictionary<string, object> p, string k, double? def = null)
        {
            object v;
            if (p.TryGetValue(k, out v) && v != null) return Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture);
            if (def.HasValue) return def.Value;
            throw new ArgumentException("Thieu tham so '" + k + "'");
        }

        private static Document Doc(UIApplication app)
        {
            var d = app.ActiveUIDocument;
            if (d == null) throw new InvalidOperationException("Chua mo du an (file .rvt) nao trong Revit.");
            return d.Document;
        }

        private static Dictionary<string, object> Obj(params object[] kv)
        {
            var d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }

        private static Category FindCategory(Document doc, string name)
        {
            BuiltInCategory bic;
            if (Enum.TryParse(name, true, out bic))
            {
                var c = Category.GetCategory(doc, bic);
                if (c != null) return c;
            }
            foreach (Category c in doc.Settings.Categories)
                if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) return c;
            throw new ArgumentException("Khong tim thay nhom doi tuong '" + name + "'. Dung ten OST_..., vd OST_Walls, OST_Doors, OST_Floors.");
        }

        private static Level FindLevel(Document doc, string name)
        {
            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().ToList();
            var lv = levels.FirstOrDefault(l => l.Name == name);
            if (lv == null) throw new ArgumentException("Khong co Level '" + name + "'. Hien co: " + string.Join(", ", levels.Select(l => l.Name)));
            return lv;
        }

        // ---------- Dieu phoi ----------
        public static object Run(UIApplication app, string method, Dictionary<string, object> p)
        {
            switch (method)
            {
                case "ping": return Ping(app);
                case "doc_info": return DocInfo(app);
                case "list_levels": return ListLevels(app);
                case "count_by_category": return CountByCategory(app);
                case "get_elements": return GetElements(app, p);
                case "get_element_params": return GetElementParams(app, p);
                case "create_level": return CreateLevel(app, p);
                case "create_wall": return CreateWall(app, p);
                case "set_parameter": return SetParameter(app, p);
                case "screenshot": return Screenshot(app, p);
                case "list_views": return ListViews(app);
                case "list_sheets": return ListSheets(app);
                case "create_sheet": return CreateSheet(app, p);
                case "place_view": return PlaceView(app, p);
                case "set_view_scale": return SetViewScale(app, p);
                case "export_pdf": return ExportPdf(app, p);
                default: throw new ArgumentException("Lenh khong ho tro: " + method);
            }
        }

        // ---------- Lenh doc ----------
        private static object Ping(UIApplication app)
        {
            var d = app.ActiveUIDocument != null ? app.ActiveUIDocument.Document : null;
            return Obj("plugin", App.Version,
                       "revit_version", app.Application.VersionNumber,
                       "revit_build", app.Application.VersionBuild,
                       "document", d != null ? d.Title : null,
                       "path", d != null ? d.PathName : null,
                       "active_view", d != null ? d.ActiveView.Name : null);
        }

        private static object DocInfo(UIApplication app)
        {
            var doc = Doc(app);
            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).GetElementCount();
            var views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Count(v => !v.IsTemplate);
            return Obj("title", doc.Title, "path", doc.PathName, "is_modified", doc.IsModified,
                       "is_workshared", doc.IsWorkshared, "active_view", doc.ActiveView.Name,
                       "level_count", levels, "view_count", views,
                       "project_name", doc.ProjectInformation != null ? doc.ProjectInformation.Name : null,
                       "project_number", doc.ProjectInformation != null ? doc.ProjectInformation.Number : null);
        }

        private static object ListLevels(UIApplication app)
        {
            var doc = Doc(app);
            return new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                .OrderBy(l => l.Elevation)
                .Select(l => Obj("id", IdOf(l.Id), "name", l.Name, "elevation_mm", Mm(l.Elevation))).ToList();
        }

        private static object CountByCategory(UIApplication app)
        {
            var doc = Doc(app);
            var g = new FilteredElementCollector(doc).WhereElementIsNotElementType()
                .Where(e => e.Category != null)
                .GroupBy(e => e.Category.Name)
                .OrderByDescending(x => x.Count())
                .Select(x => Obj("category", x.Key, "count", x.Count())).ToList();
            return g;
        }

        private static object GetElements(UIApplication app, Dictionary<string, object> p)
        {
            var doc = Doc(app);
            var cat = FindCategory(doc, Str(p, "category", ""));
            int limit = (int)Math.Max(1, Math.Min(500, Num(p, "limit", 50)));
            var all = new FilteredElementCollector(doc).OfCategoryId(cat.Id).WhereElementIsNotElementType().ToList();
            var items = all.Take(limit).Select(e =>
            {
                var t = doc.GetElement(e.GetTypeId());
                Level lv = e.LevelId != ElementId.InvalidElementId ? doc.GetElement(e.LevelId) as Level : null;
                return Obj("id", IdOf(e.Id), "name", e.Name, "type", t != null ? t.Name : null,
                           "level", lv != null ? lv.Name : null);
            }).ToList();
            return Obj("category", cat.Name, "total", all.Count, "returned", items.Count, "elements", items);
        }

        private static object GetElementParams(UIApplication app, Dictionary<string, object> p)
        {
            var doc = Doc(app);
            var e = doc.GetElement(MakeId((long)Num(p, "element_id")));
            if (e == null) throw new ArgumentException("Khong co phan tu id " + Str(p, "element_id"));
            var list = new List<object>();
            foreach (Parameter prm in e.Parameters)
            {
                string val;
                try { val = prm.AsValueString() ?? prm.AsString(); } catch { val = null; }
                if (val == null && prm.StorageType == StorageType.Integer) val = prm.AsInteger().ToString();
                if (val == null && prm.StorageType == StorageType.Double) val = prm.AsDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                if (val == null && prm.StorageType == StorageType.ElementId) val = IdOf(prm.AsElementId()).ToString();
                list.Add(Obj("name", prm.Definition.Name, "value", val,
                             "type", prm.StorageType.ToString(), "read_only", prm.IsReadOnly));
            }
            return Obj("id", IdOf(e.Id), "name", e.Name, "category", e.Category != null ? e.Category.Name : null, "parameters", list);
        }

        // ---------- Lenh ghi (Transaction, khong xoa) ----------
        private static object CreateLevel(UIApplication app, Dictionary<string, object> p)
        {
            var doc = Doc(app);
            string name = Str(p, "name");
            double elev = Num(p, "elevation_mm") / FtToMm;
            if (!string.IsNullOrEmpty(name) &&
                new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().Any(l => l.Name == name))
                throw new ArgumentException("Level '" + name + "' da ton tai.");
            using (var t = new Transaction(doc, "HP MCP: tao Level"))
            {
                t.Start();
                Level lv = Level.Create(doc, elev);
                if (!string.IsNullOrEmpty(name)) lv.Name = name;
                t.Commit();
                return Obj("id", IdOf(lv.Id), "name", lv.Name, "elevation_mm", Mm(lv.Elevation));
            }
        }

        private static object CreateWall(UIApplication app, Dictionary<string, object> p)
        {
            var doc = Doc(app);
            var lv = FindLevel(doc, Str(p, "level"));
            var a = new XYZ(Num(p, "x1_mm") / FtToMm, Num(p, "y1_mm") / FtToMm, lv.Elevation);
            var b = new XYZ(Num(p, "x2_mm") / FtToMm, Num(p, "y2_mm") / FtToMm, lv.Elevation);
            double h = Num(p, "height_mm", 3000) / FtToMm;
            string typeName = Str(p, "wall_type");
            using (var t = new Transaction(doc, "HP MCP: tao tuong"))
            {
                t.Start();
                Wall w;
                var line = Line.CreateBound(a, b);
                if (typeName != null)
                {
                    var wt = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().FirstOrDefault(x => x.Name == typeName);
                    if (wt == null) throw new ArgumentException("Khong co loai tuong '" + typeName + "'.");
                    w = Wall.Create(doc, line, wt.Id, lv.Id, h, 0, false, false);
                }
                else
                {
                    w = Wall.Create(doc, line, lv.Id, false);
                    var hp = w.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
                    if (hp != null && !hp.IsReadOnly) hp.Set(h);
                }
                t.Commit();
                return Obj("id", IdOf(w.Id), "type", w.WallType.Name, "level", lv.Name,
                           "length_mm", Mm(line.Length), "height_mm", Mm(h));
            }
        }

        private static object SetParameter(UIApplication app, Dictionary<string, object> p)
        {
            var doc = Doc(app);
            var e = doc.GetElement(MakeId((long)Num(p, "element_id")));
            if (e == null) throw new ArgumentException("Khong co phan tu id " + Str(p, "element_id"));
            string name = Str(p, "name");
            var prm = e.LookupParameter(name);
            if (prm == null) throw new ArgumentException("Khong co tham so '" + name + "'.");
            if (prm.IsReadOnly) throw new InvalidOperationException("Tham so '" + name + "' chi doc.");
            string val = Str(p, "value", "");
            string unit = Str(p, "unit", "");
            using (var t = new Transaction(doc, "HP MCP: sua tham so"))
            {
                t.Start();
                switch (prm.StorageType)
                {
                    case StorageType.String: prm.Set(val); break;
                    case StorageType.Integer: prm.Set(int.Parse(val)); break;
                    case StorageType.Double:
                        double d = double.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                        if (unit == "mm") d /= FtToMm;
                        else if (unit != "raw") throw new ArgumentException("Tham so so thuc: truyen unit='mm' (do dai) hoac unit='raw' (don vi trong cua Revit).");
                        prm.Set(d);
                        break;
                    default: throw new NotSupportedException("Kieu tham so " + prm.StorageType + " chua ho tro.");
                }
                t.Commit();
            }
            return Obj("id", IdOf(e.Id), "name", name, "value_now", prm.AsValueString() ?? prm.AsString());
        }

        // ---------- Chup anh ----------
        private static object Screenshot(UIApplication app, Dictionary<string, object> p)
        {
            var doc = Doc(app);
            int width = (int)Num(p, "width", 1600);
            string dir = Path.GetTempPath();
            string prefix = Path.Combine(dir, "hp_revit_" + DateTime.Now.ToString("HHmmssfff"));
            var opt = new ImageExportOptions
            {
                ExportRange = ExportRange.SetOfViews,
                FilePath = prefix,
                HLRandWFViewsFileType = ImageFileType.PNG,
                ImageResolution = ImageResolution.DPI_150,
                ZoomType = ZoomFitType.FitToPage,
                PixelSize = width
            };
            opt.SetViewsAndSheets(new List<ElementId> { doc.ActiveView.Id });
            doc.ExportImage(opt);
            var file = Directory.GetFiles(dir, Path.GetFileName(prefix) + "*.png").FirstOrDefault();
            if (file == null) throw new InvalidOperationException("Revit khong xuat duoc anh (khung nhin hien tai co the khong xuat anh duoc).");
            return Obj("path", file, "view", doc.ActiveView.Name);
        }
    }
}
