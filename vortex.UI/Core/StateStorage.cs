using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Serilog;

namespace vortex.UI.Core
{
	class StateStorage
	{
        private static string GetSettings(string ServiceInstance) {
            string Settings = "";

            string JsonPath = System.IO.Path.Combine(@"C:\HPOD\src\vortex\vortex.Server", "sample-config.json");

            if(System.IO.File.Exists(JsonPath))
                Settings = System.IO.File.ReadAllText(JsonPath);

            return Settings;
        }

        private static IDictionary<string,string> GetOverrides() {
            Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Paperpost\H-POD\Overrides");
            Dictionary<string,string> Overrides = new Dictionary<string, string>();

            if(rk == null)
                rk = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Paperpost\H-POD\Overrides");

            if(rk != null) {
                string[] Names = rk.GetValueNames();

                foreach(string Name in Names)
                    Overrides.Add(Name, (string)rk.GetValue(Name, ""));

                rk.Close();
            }

            return Overrides;
        }

		public static vortex.State.Current Load() {
            vortex.State.Current State = null;

            // Load the current state from the registry, Sqlite DB, JSON file, wherever
            string JSONSettings = GetSettings("vortex");

            // Read in any overrides from the registry to replace values in the stored state
            IDictionary<string,string> Overrides = GetOverrides();

            if(JSONSettings.Length > 0) {
                State = new vortex.State.Current(JSONSettings);
            } else
                State = new vortex.State.Current();

            foreach(string key in Overrides.Keys) {
                try {
                    string[] parts = key.Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries);

                    System.Reflection.FieldInfo field = null;
                    object parent = State;

                    field = parent.GetType().GetField(parts[0]);

                    for(int i=1;i<parts.Length;i++) {
                        parent = field.GetValue(parent);
                        field = field.FieldType.GetField(parts[i]);
                    }

                    switch(field.FieldType.Name) {
                        case "Boolean":
                            field.SetValue(parent, bool.Parse(Overrides[key]));
                            break;
                        default:
                            field.SetValue(parent, Overrides[key]);
                            break;
                    }
                } catch (Exception e) {
                    Log.Error(e, "Failed to override setting {Name}", key);
                }
            }

            return State;
		}

		public static void Save(vortex.State.Current State) {
            Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Paperpost\H-POD");

            if(rk != null) {
                rk.SetValue("vortex", Newtonsoft.Json.JsonConvert.SerializeObject(State));
                rk.Close();
            }
		}
	}
}
