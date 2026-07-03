using PdfSharp.Drawing;
using PdfSharp.Pdf;
using NoteApp.Models;
using System.IO;
using System.Windows.Ink;
using System.Windows.Media;

namespace NoteApp.Services
{
public static class PdfService
{
    // A4 in Punkten (72 dpi): 595 x 842
    private const double PageWidth = 595;
    private const double PageHeight = 842;
    private const double MarginLeft = 60;
    private const double MarginTop = 40;
    private const double MarginRight = 40;
    private const double LineSpacing = 24;

              
        /// <summary>
        /// Erstellt eine neue leere A4-linierte PDF-Seite und gibt den Pfad zurück.
        /// </summary>
        public static string CreateLinedPage(string savePath)
        {
            using var doc = new PdfDocument();
            var page = doc.AddPage();
            page.Width = XUnit.FromPoint(PageWidth);
            page.Height = XUnit.FromPoint(PageHeight);

            using var gfx = XGraphics.FromPdfPage(page);
            DrawLinedBackground(gfx);

            doc.Save(savePath);
            return savePath;
        }

        /// <summary>
        /// Zeichnet den linierten Hintergrund mit Rand auf eine PDF-Seite.
        /// </summary>
        private static void DrawLinedBackground(XGraphics gfx)
        {
            // Weißer Hintergrund
            gfx.DrawRectangle(XBrushes.White, 0, 0, PageWidth, PageHeight);

            // Roter Rand (linke Linie)
            var redPen = new XPen(XColor.FromArgb(255, 220, 80, 80), 1.2);
            gfx.DrawLine(redPen, MarginLeft, MarginTop, MarginLeft, PageHeight - MarginTop);

            // Blaue Linien
            var linePen = new XPen(XColor.FromArgb(180, 180, 210, 240), 0.7);
            double y = MarginTop + LineSpacing;
            while (y < PageHeight - MarginTop)
            {
                gfx.DrawLine(linePen, MarginLeft, y, PageWidth - MarginRight, y);
                y += LineSpacing;
            }
        }

        /// <summary>
        /// Exportiert eine Seite mit Strokes als PDF.
        /// </summary>
        public static void ExportPageWithStrokes(NotePage page, string exportPath)
        {
            using var doc = new PdfDocument();
            var pdfPage = doc.AddPage();
            pdfPage.Width = XUnit.FromPoint(PageWidth);
            pdfPage.Height = XUnit.FromPoint(PageHeight);

            using var gfx = XGraphics.FromPdfPage(pdfPage);
            DrawLinedBackground(gfx);

            // Strokes zeichnen
            foreach (var stroke in page.Strokes)
            {
                if (stroke.Points.Count < 2) continue;
                var color = (System.Windows.Media.Color)ColorConverter.ConvertFromString(stroke.Color);
                var pen = new XPen(XColor.FromArgb(color.A, color.R, color.G, color.B), stroke.Thickness);
                pen.LineCap = XLineCap.Round;

                for (int i = 0; i < stroke.Points.Count - 1; i++)
                {
                    gfx.DrawLine(pen,
                        stroke.Points[i].X, stroke.Points[i].Y,
                        stroke.Points[i + 1].X, stroke.Points[i + 1].Y);
                }
            }

            doc.Save(exportPath);
        }
    }

        

}