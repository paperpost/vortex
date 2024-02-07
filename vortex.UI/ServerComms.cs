using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grpc.Core;
using vortex.GRPC;
using Grpc.Core.Interceptors;

using Serilog;
using Serilog.Context;

namespace vortex.UI
{
	public class ServerComms
	{
		static Channel channel;

		private vortex.State.Current State;

		public delegate void ServerNotAvailableDelegate();

		public event ServerNotAvailableDelegate ServerNotAvailable;

		private void OnServerNotAvailable() {
			if(ServerNotAvailable != null)
				ServerNotAvailable();
		}

		public ServerComms(vortex.State.Current State) {
			this.State = State;

			Log.Debug("Initialising GRPC to port {port}", State.Local.GRPCPort);
            channel = new Channel(string.Format("127.0.0.1:{0}", State.Local.GRPCPort), ChannelCredentials.Insecure);
			channel.Intercept(new vortex.Common.gRPCLogger());
		}

		public async Task Shutdown() {
			Log.Debug("Shutting down GRPC client");
			await channel.ShutdownAsync();
			Log.Debug("GRPC client terminated");
		}

		public async Task TriggerRefreshConfiguration(vortex.State.User User, string reason) {
			Log.Information("Refresh config triggered [{reason}]", reason);
			var client = new Vortex.VortexClient(channel);

			RefreshConfigurationRequest req = new RefreshConfigurationRequest();

			req.Trigger = reason;
			req.Token = User.GetIDPIDToken();

			try {
				var resp = await client.RefreshConfigurationAsync(req, deadline: DateTime.UtcNow.AddSeconds(30));

				if(resp.Success) {
					State.ClientConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<vortex.API.ClientConfiguration>(resp.Config);
				} else
					Log.Information("Configuration refresh result - "+resp.Status);
			} catch (RpcException re) {
				Log.Error(re, "GRPC Server not available");
				OnServerNotAvailable();
			}
		}

		public async Task<SubmitPDFResponse> SubmitDocument(vortex.API.PrintOptions Options, string PDFPath, string UserGUID) {
			Log.Information("Submitting {PDF}", PDFPath);

			var client = new Vortex.VortexClient(channel);

			try {
				var User = State.ActiveUsers.First(u => u.UserGUID == Guid.Parse(UserGUID));

				if(User != null) {
					string Token = User.GetIDPIDToken();

					Log.Debug("Got token, making gRPC call to server to submit file");

					SubmitPDFResponse resp = client.SubmitPDF(new SubmitPDFRequest() { Path=PDFPath, Settings=Newtonsoft.Json.JsonConvert.SerializeObject(Options), Userguid = UserGUID });

					Log.Debug("gRPC call returned");

					return resp;
				} else {
					return new SubmitPDFResponse() { Status = "failed, user not found" };
				}
			} catch (RpcException re) { 
				Log.Error(re, "GRPC Server not available");
				OnServerNotAvailable();
			} catch (Exception e) {
				Log.Error(e, "Unhandled error");
			}

			return null;
		}

		public async Task<vortex.GRPC.SubmitStatusResponse> SubmitStatus(string JobId) {
			try {
				var client = new Vortex.VortexClient(channel);

				Log.Debug("Requesting status of job "+JobId);

				SubmitStatusRequest req = new SubmitStatusRequest() { Jobid = JobId };

				var resp = await client.SubmitStatusAsync(req);

				Log.Debug("Got status of job "+JobId);

				return resp;
			} catch (Exception e) {
				Log.Error(e, "Failed to acquire submission status for job "+JobId);
				return null;
			}
		}
	}
}
