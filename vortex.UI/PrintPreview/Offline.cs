using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

using SautinSoft;

using Serilog;

namespace vortex.UI.PrintPreview
{
	class Offline
	{
		const int DPI = 150;
		const int MaxPages = 10;

		public List<BitmapImage> Generate(string PDFPath) {

			List<BitmapImage> ret = new List<BitmapImage>();

			DateTime dtStart = DateTime.UtcNow;

			try {
				// PDF Focus
	            PdfFocus f = new PdfFocus();
				f.Serial = "10250574386";
	            f.OpenPdf(PDFPath);
                f.ImageOptions.ImageFormat = System.Drawing.Imaging.ImageFormat.Bmp;
                f.ImageOptions.Dpi = DPI;

				for(int i=0;i<f.PageCount && i < MaxPages;i++) {
					byte[] bPage = f.ToImage(i+1);

					BitmapImage bi = new BitmapImage();

					bi.BeginInit();
					bi.StreamSource = new MemoryStream(bPage);
					bi.EndInit();

					ret.Add(bi);
				}

				f.ClosePdf();

				Log.Information("Generated {0} offline thumbnails in {1:F2}s", ret.Count, (DateTime.UtcNow - dtStart).TotalSeconds);
			} catch (Exception ex) {
				Log.Error(ex, "Failed to generate thumbnails");
			}

			return ret;
		}
	}
}
