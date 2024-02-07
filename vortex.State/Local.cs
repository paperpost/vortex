using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;

namespace vortex.State
{
	public class Local
	{
		// This class contains local configuration data set by the installer and not influenced by server settings
		public int GRPCPort = 30051;
		public int ConfigRefreshInterval = 300;
		public string InstallPath;
		[JsonIgnore] public string LogPath;
		public string ConfigPath;
		public bool LogRelay = false;
		public bool ShowProgress=true;
		public bool ShowPrintSettings=true;

		public Local() {
			this.LogPath = System.IO.Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Paperpost", "H-POD", "Logs", "vortex-.txt");
		}
	}
}
