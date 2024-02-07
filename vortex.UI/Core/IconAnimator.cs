using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vortex.UI.Core
{
	class IconAnimator
	{
		List<System.Drawing.Bitmap> Icons = new List<System.Drawing.Bitmap>();

		private int WorkingState=1;

		private bool IsDarkTheme() {
			return ((int)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 1) == 0);
		}

		public IconAnimator(System.Windows.Forms.NotifyIcon notifyIcon) {
			string ResourceURLFormat = "pack://application:,,,/vortex.UI;component/Icons/vortex-{0}-light.png";

			if(!IsDarkTheme())
				ResourceURLFormat = "pack://application:,,,/vortex.UI;component/Icons/vortex-{0}.png";

			for(int i=0;i<7;i++)
				Icons.Add(new System.Drawing.Bitmap(System.Windows.Application.GetResourceStream( new Uri(string.Format(ResourceURLFormat, i))).Stream));
		}

		public System.Drawing.Icon Idle() {
			return System.Drawing.Icon.FromHandle(Icons[0].GetHicon());
		}

		public System.Drawing.Icon Working() {
			if(WorkingState == 7) WorkingState = 1;
			return System.Drawing.Icon.FromHandle(Icons[WorkingState++].GetHicon());
		}
	}
}
