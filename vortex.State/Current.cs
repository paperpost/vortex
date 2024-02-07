using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;
using Serilog;

namespace vortex.State
{
	public class Current
	{
		[JsonIgnore] public vortex.API.ClientConfiguration ClientConfig = null;
		public Local Local = new Local();
		public List<User> ActiveUsers = new List<User>();

		private string OverriddenConfigPath = "";

		public string Instance = "";
		public string APIEndpoint;

		public int ConfigRefreshTimeoutSeconds = 3600;		// Default to 60 mins, might be overridden by ClientConfig.AccountSettings.ConfigRefreshTimeoutSeconds

		public DateTime LastConfigRefresh;
		private string DefaultInstance = "vortex";

		public List<IdentityProvider> IdentityProviders = new List<IdentityProvider>();
		public List<HotFolder> HotFolders = new List<HotFolder>();

		public Current(string ConfigPath = "", string LogPath = "", string Instance = "") {
			if(string.IsNullOrEmpty(Instance)) Instance = DefaultInstance;

			if(!string.IsNullOrEmpty(ConfigPath))
				Local.ConfigPath = ConfigPath;

			if(!string.IsNullOrWhiteSpace(LogPath)) {
				if(!System.IO.Directory.Exists(LogPath))
					System.IO.Directory.CreateDirectory(LogPath);

				Local.LogPath = LogPath;
			}

			string json = Load();

			if(!string.IsNullOrEmpty(json)) {
				Newtonsoft.Json.JsonConvert.PopulateObject(json, this);

				// Convert IdentityProvider references in submitting user and hotfolder user to IdentityProvider objects
				this.ActiveUsers.ForEach((u) => u.SetIdentityProvider(this.IdentityProviders));

				foreach(var hf in this.HotFolders)
					hf.SubmittingUser.SetIdentityProvider(this.IdentityProviders);

				this.IdentityProviders.ForEach(async (i) => await i.DiscoverAsync());
			}
		}

		public string Load() {
			string Settings = "";

            if(System.IO.File.Exists(Local.ConfigPath)) {
				Settings = System.IO.File.ReadAllText(Local.ConfigPath);
			}

            return Settings;
		}

		public void PatchHotFolder(string Path, vortex.State.HotFolderPatch p) {
			foreach(var hf in HotFolders)
				if(hf.Path.ToLower() == Path.ToLower()) {
					hf.settings = p.settings;
					hf.ExportReady = p.ExportReady;
					hf.ValidateAddress = p.ValidateAddress;
					hf.AbortOnValidationFailure = p.AbortOnValidationFailure;
				}
		}

		public void SetHotFolderUser(string Path, vortex.State.User u) {
			foreach(var hf in HotFolders)
				if(hf.Path.ToLower() == Path.ToLower()) {
					hf.SubmittingUser = u;
					hf.SubmittingUser.SetIdentityProvider(this.IdentityProviders);
				}
		}

		public void Save() {
			string s = Newtonsoft.Json.JsonConvert.SerializeObject(this);

			System.IO.File.WriteAllText(Local.ConfigPath, s);
		}

		public bool RemoveHotFolder(string Path) {
			bool ret = false;

			foreach(HotFolder hf in HotFolders) {
				if(hf.Path == Path) {
					HotFolders.Remove(hf);
					ret=true;
					break;
				}
			}

			return ret;
		}
	}
}
