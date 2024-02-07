using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using Serilog;

namespace vortex.UI.Core
{
	class Background
	{
		private vortex.State.Current State;
		private Thread tWorker = null;
		private vortex.UI.ServerComms Server = null;
		public bool Running=true;

		public bool Ready = false;

		public void Start(vortex.State.Current State, vortex.UI.ServerComms Server) {
			this.State = State;
			this.Server = Server;

			tWorker = new Thread(new ThreadStart(WorkerLoop));
			tWorker.IsBackground = true;    // This thread shouldn't keep the exe alive
			tWorker.Start();
		}

		private void WorkerLoop() {
			bool Triggered=false;
			DateTime LastAttempt = DateTime.MinValue;

			do {
				bool Trigger = false;

				if(State.ClientConfig == null) {
					if(State.LastConfigRefresh < DateTime.UtcNow.AddSeconds((double)-State.ConfigRefreshTimeoutSeconds))
						Trigger=true;
				} else {
					Trigger = (State.LastConfigRefresh < DateTime.UtcNow.AddSeconds((double)-State.ClientConfig.AccountSettings?.ConfigRefreshTimeoutSeconds));
				}

				// If we haven't even got our first config refresh, GRPC server is unavailable so limit retries to once every 60 seconds
				if(((DateTime.UtcNow - LastAttempt).TotalSeconds < 60) && (State.ClientConfig == null))
					Trigger=false;

				if(!Triggered && Trigger) {
					Triggered=true;
					Trigger=false;
					LastAttempt = DateTime.UtcNow;

					Log.Information("Triggering config refresh");

					State.ActiveUsers.ForEach((u) =>
						Task.Run(async() => await Server.TriggerRefreshConfiguration(u, "auto")).ContinueWith(t => {
							Log.Information("Config refresh attempt complete");
							Triggered=false;
							Ready=true;
							if(State.ClientConfig != null) {
								State.LastConfigRefresh = DateTime.UtcNow;
							}
						}));
				}

				System.Threading.Thread.Sleep(250);
			} while(Running);

			Log.Information("Background thread terminated");
		}
	}
}
