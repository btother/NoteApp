using PdfSharp.Drawing;
using PdfSharp.Pdf;
using NoteApp.Models;
using System.IO;
using System.Windows.Ink;
using System.Windows.Media;

namespace NoteApp.Services {
    public static class PdfService {

   private const double PageWidth  = 595;
        private const double PageHeight = 842;
  private const double MarginLeft  = 60;
        private const double MarginTop   = 40;
        private const double MarginRight = 40;
        private const double LineSpacing = 24;

   // ISF

        public static string GetIsfPath(NotePage page) {
            if (!string.IsNullOrWhiteSpace(page.IsfPath))
        return page.IsfPath;
if (!string.IsNullOrWhiteSpace(page.PdfPath))
  return Path.ChangeExtension(page.PdfPath, ".isf");
       string dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "NoteApp", "pages");
     Directory.CreateDirectory(dir);
            return Path.Combine(dir, Guid.NewGuid() + ".isf");
        }

        public static void SaveStrokes(NotePage page, StrokeCollection strokes) {
            string isfPath = GetIsfPath(page);
   page.IsfPath = isfPath;
 Directory.CreateDirectory(Path.GetDirectoryName(isfPath)!);
            using var fs = new FileStream(isfPath, FileMode.Create, FileAccess.Write);
            strokes.Save(fs);
   }

        public static StrokeCollection LoadStrokes(NotePage page) {
string isfPath = GetIsfPath(page);
     page.IsfPath = isfPath;
            if (!File.Exists(isfPath))
    return new StrokeCollection();
            try {
             using var fs = new FileStream(isfPath, FileMode.Open, FileAccess.Read);
return new StrokeCollection(fs);
      }
  catch {
      return new StrokeCollection();
 }
        }

   public static void DeleteStrokes(NotePage page) {
       string isfPath = GetIsfPath(page);
         if (File.Exists(isfPath))
       File.Delete(isfPath);
   }

     // PDF-Erstellung

        public static string CreateLinedPage(string savePath) {
      using var doc = new PdfDocument();
    var page = doc.AddPage();
    page.Width  = XUnit.FromPoint(PageWidth);
         page.Height = XUnit.FromPoint(PageHeight);
          using var gfx = XGraphics.FromPdfPage(page);
    DrawLinedBackground(gfx);
            doc.Save(savePath);
return savePath;
        }

        private static void DrawLinedBackground(XGraphics gfx) {
            gfx.DrawRectangle(XBrushes.White, 0, 0, PageWidth, PageHeight);
            var redPen = new XPen(XColor.FromArgb(255, 220, 80, 80), 1.2);
    gfx.DrawLine(redPen, MarginLeft, MarginTop, MarginLeft, PageHeight - MarginTop);
            var linePen = new XPen(XColor.FromArgb(180, 180, 210, 240), 0.7);
     double y = MarginTop + LineSpacing;
  while (y < PageHeight - MarginTop) {
     gfx.DrawLine(linePen, MarginLeft, y, PageWidth - MarginRight, y);
                y += LineSpacing;
            }
     }

        // Export

        /// <summary>
 /// Exportiert ein gesamtes Dokument (alle Seiten einer Unterkategorie) als mehrseitige PDF.
        /// </summary>
   public static void ExportDocumentWithStrokes(Category document, string exportPath) {
     using var doc = new PdfDocument();
            foreach (var page in document.Pages) {
    var pdfPage = doc.AddPage();
     pdfPage.Width  = XUnit.FromPoint(PageWidth);
        pdfPage.Height = XUnit.FromPoint(PageHeight);
    using var gfx = XGraphics.FromPdfPage(pdfPage);
           DrawLinedBackground(gfx);
     var strokes = LoadStrokes(page);
   RenderStrokesToPdf(gfx, strokes);
  }
     if (doc.PageCount > 0)
        doc.Save(exportPath);
        }

        /// <summary>
        /// Exportiert eine einzelne Seite als PDF.
      /// </summary>
        public static void ExportPageWithStrokes(NotePage page, string exportPath) {
            using var doc = new PdfDocument();
      var pdfPage = doc.AddPage();
     pdfPage.Width  = XUnit.FromPoint(PageWidth);
pdfPage.Height = XUnit.FromPoint(PageHeight);
            using var gfx = XGraphics.FromPdfPage(pdfPage);
      DrawLinedBackground(gfx);
  var strokes = LoadStrokes(page);
            RenderStrokesToPdf(gfx, strokes);
            doc.Save(exportPath);
        }

    private static void RenderStrokesToPdf(XGraphics gfx, StrokeCollection strokes) {
            foreach (var stroke in strokes) {
   var pts = stroke.StylusPoints;
         if (pts.Count < 2) continue;
 var color     = stroke.DrawingAttributes.Color;
     double thickness = stroke.DrawingAttributes.Width;
    var pen = new XPen(
      XColor.FromArgb(color.A, color.R, color.G, color.B),
   thickness);
          pen.LineCap = XLineCap.Round;
                for (int i = 0; i < pts.Count - 1; i++) {
        gfx.DrawLine(pen, pts[i].X, pts[i].Y, pts[i+1].X, pts[i+1].Y);
                }
            }
    }

 // AutoSave

        public static void AutoSaveNotebook(Notebook notebook) {
          if (string.IsNullOrWhiteSpace(notebook.SavePath)) return;
 string notebookDir = Path.Combine(
         notebook.SavePath,
         SanitizeName(notebook.Name));
    Directory.CreateDirectory(notebookDir);
       foreach (var category in notebook.Categories)
       SaveCategoryPages(category, notebookDir);
    }

    private static void SaveCategoryPages(Category category, string parentDir) {
string categoryDir = Path.Combine(parentDir, SanitizeName(category.Name));
          Directory.CreateDirectory(categoryDir);
    // Unterkategorien = Dokumente -> als mehrseitige PDF exportieren
 foreach (var doc in category.SubCategories) {
   if (doc.Pages.Count == 0) continue;
       string fileName = $"{SanitizeName(doc.Name)}.pdf";
                string filePath = Path.Combine(categoryDir, fileName);
          ExportDocumentWithStrokes(doc, filePath);
       }
    // Rekursiv fuer tiefere Ebenen
   foreach (var sub in category.SubCategories)
 if (sub.SubCategories.Count > 0)
      SaveCategoryPages(sub, categoryDir);
        }

        private static string SanitizeName(string name) {
            foreach (char c in Path.GetInvalidFileNameChars())
  name = name.Replace(c, '_');
        return name.Trim();
        }
    }
}
