using Grpc.Core;
using Serilog;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using vortex.API;
using vortex.GRPC;
using vortex.Manager.MVVM;
using vortex.Manager.View;

namespace vortex.Manager.ViewModel
{
	internal class MainWindowViewModel : ViewModelBase
	{
		public RelayCommand HotFoldersViewCommand { get; set; }
		public RelayCommand IdentityProvidersViewCommand { get; set; }

		public HotFoldersViewModel HotFoldersVM { get; set; }
		public IdentityProvidersViewModel IdentityProvidersVM { get; set; }

		private object _currentView;

		public object CurrentView {
			get { return _currentView; }
			set {
				_currentView=value;
				OnPropertyChanged();
			}
		}

		private static Channel channel;
        public static Vortex.VortexClient client = null;
        public static vortex.State.Current State;

        private static void ConnectToServer() {
            Log.Verbose("Connecting to vortex server on port "+State.Local.GRPCPort.ToString());

            channel = new Channel(string.Format("127.0.0.1:{0}", State.Local.GRPCPort), ChannelCredentials.Insecure);
			client = new Vortex.VortexClient(channel);
        }
		
		public MainWindowViewModel() {
			string InstallDir = (new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory)).Parent.FullName;

			if(System.Diagnostics.Debugger.IsAttached)
				InstallDir = @"C:\Program Files\Vortex\";

			string LogFolder = System.IO.Path.Combine(InstallDir, "logs");
			string ConfigPath = System.IO.Path.Combine(InstallDir, "config.json");

			// Initialise the unpopulated state
			State = new State.Current(ConfigPath, InstallDir);

			List<vortex.State.HotFolder> HotFolders = new List<State.HotFolder>();
			List<vortex.State.IdentityProvider> IDPs = new List<State.IdentityProvider>();

			try {
				ConnectToServer();
	            GetHotFoldersResponse r = client.GetHotFolders(new GetHotFoldersRequest());

				if(r.Hotfolders != null ) {
					HotFolders = Newtonsoft.Json.JsonConvert.DeserializeObject<List<vortex.State.HotFolder>>(r.Hotfolders);

					if(HotFolders==null) HotFolders = new List<State.HotFolder>();

					HotFolders.ForEach(hf => hf.SubmittingUser.SetIdentityProvider(State.IdentityProviders));
				}

				GetIdentityProvidersResponse idpr = client.GetIdentityProviders(new GetIdentityProvidersRequest());

				if(idpr.Identityproviders != null) {
					IDPs = Newtonsoft.Json.JsonConvert.DeserializeObject<List<vortex.State.IdentityProvider>>(idpr.Identityproviders);

					if(IDPs == null) IDPs = new List<State.IdentityProvider>();
				}
            } catch (Grpc.Core.RpcException e) {
				MessageBox.Show("Connection to Vortex Server failed - "+e.Message);
				Application.Current.Shutdown(1);
				return;
            }


			HotFoldersVM = new HotFoldersViewModel(HotFolders);

			IdentityProvidersVM = new IdentityProvidersViewModel(IDPs);

			CurrentView = HotFoldersVM;

			HotFoldersViewCommand = new RelayCommand(o => {
				CurrentView = HotFoldersVM;
			});

			IdentityProvidersViewCommand = new RelayCommand(o => {
				CurrentView = IdentityProvidersVM;
			});

			// Periodically update hot folder stats
			DispatcherTimer tmrFolderWatcher = new DispatcherTimer();  
			tmrFolderWatcher.Tick += new EventHandler(UpdateFolderStats);  
			tmrFolderWatcher.Interval = new TimeSpan(0, 0, 5);  
			tmrFolderWatcher.Start();  
		}

		private void UpdateFolderStats(object? o, EventArgs e) {
	        GetHotFoldersResponse r = client.GetHotFolders(new GetHotFoldersRequest());

			if(r.Hotfolders != null ) {
				List<vortex.State.HotFolder> HotFolders = Newtonsoft.Json.JsonConvert.DeserializeObject<List<vortex.State.HotFolder>>(r.Hotfolders);

				if(HotFolders != null)
					HotFoldersVM.UpdateStats(HotFolders);
			}
		}
	}
}
