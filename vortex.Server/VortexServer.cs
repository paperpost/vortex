using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using vortex.GRPC;

using Serilog;
using vortex.State;

namespace vortex.Server
{
	class VortexServer : Vortex.VortexBase
	{
		vortex.State.Current State = null;

		int PoolSize = 10;
		APIConnectionPool ConnectionPool = null;
		List<Submitter> Submitters = new List<Submitter>();
		List<HotFolder> HotFolders = new List<HotFolder>();

		public VortexServer(vortex.State.Current State, int ConnectionPoolSize) {
			this.State = State;

			Log.Debug("Starting API connection pool for "+State.APIEndpoint);
			ConnectionPool = new APIConnectionPool(ConnectionPoolSize, State.Instance, State.APIEndpoint);

			// Create running instances of each configured hotfolder
			this.State.HotFolders.ForEach((hf) => this.AddHotFolderFromState(hf));
			this.HotFolders.ForEach((hf) => hf.Start());
		}

		private vortex.Server.HotFolder AddHotFolderFromState(vortex.State.HotFolder hf) {
			vortex.Server.HotFolder shf = new HotFolder(hf);
			shf.ConnectionPool = this.ConnectionPool;

			this.HotFolders.Add(shf);

			return shf;
		}

		public async override Task<DisableLogsResponse> DisableLogs(DisableLogsRequest request, ServerCallContext context)
		{
			this.State.Local.LogRelay = false;
			Program.InitLogging(this.State);
			this.State.Save();

			return new DisableLogsResponse();
		}

		public async override Task<EnableLogsResponse> EnableLogs(EnableLogsRequest request, ServerCallContext context)
		{
			this.State.Local.LogRelay = true;
			Program.InitLogging(this.State);
			this.State.Save();

			return new EnableLogsResponse();
		}

		public async override Task<SubmitPDFResponse> SubmitPDF(SubmitPDFRequest request, ServerCallContext context) {
			Log.Information("Received request to submit {PDF}", request.Path);

			try {
				if(System.IO.File.Exists(request.Path)) {
					Submitter S = new Submitter();

					S.Finished += this.Finished;
					S.PDFPath = request.Path;
					S.Options = Newtonsoft.Json.JsonConvert.DeserializeObject<vortex.API.PublicPrintOptions>(request.Settings);
					S.SubmittingUser = this.State.ActiveUsers.First(u => u.UserGUID == Guid.Parse(request.Userguid));
					S.Connection = await ConnectionPool.GetFreeConnectionAsync();

					Submitters.Add(S);

					Thread tSubmit = new Thread(new ThreadStart(S.SubmitThread));
					tSubmit.IsBackground = true;
					tSubmit.Start();

					Log.Information("Returning JobId to caller: "+S.Connection.TransactionId);

					return new SubmitPDFResponse { Jobid = S.Connection.TransactionId, Status = "" };
				} else {
					Log.Error("PDF does not exist, aborting submission");
					return new SubmitPDFResponse { Jobid = Guid.Empty.ToString(), Status = "PDF not found" };
				}
			} catch (Exception e) {
				Log.Error(e, "Error submitting PDF");
				return new SubmitPDFResponse { Jobid = Guid.Empty.ToString(), Status = "Unknown error" };
			}
		}

		public void Finished(Submitter S) {
			CleanUpSubmitters();
		}

		// Clients have up to a minute to request the status of a job after it has finished submitting, or no status will be available
		private void CleanUpSubmitters() {
			for(int i=0;i<Submitters.Count;i++)
				if(Submitters[i].FinishedAt > DateTime.MinValue) {
					if((DateTime.UtcNow - Submitters[i].FinishedAt).TotalSeconds > 60) {
						Log.Debug("Removing submitter for {0}", Submitters[i].PDFPath);
						Submitters[i].Connection.Release();
						Submitters.RemoveAt(i--);
					}
				}
		}

		public async override Task<GetAccountConfigurationResponse> GetAccountConfiguration(GetAccountConfigurationRequest request, ServerCallContext context)
		{
			GetAccountConfigurationResponse resp = new GetAccountConfigurationResponse();

			try {
				vortex.State.User U = Newtonsoft.Json.JsonConvert.DeserializeObject<vortex.State.User>(request.User);

				U.SetIdentityProvider(this.State.IdentityProviders);

				APIConnection API = await ConnectionPool.GetFreeConnectionAsync();

				vortex.API.Client Client = API.CreateClient();

				// Get default settings from H-POD
				Log.Information("Getting account configuration");
				if(await U.EnsureAuthenticated(Client, API.Instance)) {
					API.HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", U.APIToken);

					vortex.API.ClientConfiguration Config = await Client.GetPrintConfigurationAsync();

					resp.Config = Newtonsoft.Json.JsonConvert.SerializeObject(Config);
					resp.Success = true;
				} else {
					Log.Information("Failed to authenticate against account");
				}
			} catch (Exception e) {
				Log.Error(e, "Unhandled error getting account configuration");
			}

			return resp;
		}

		public async override Task<RefreshConfigurationResponse> RefreshConfiguration(RefreshConfigurationRequest request, ServerCallContext context) {
			System.IdentityModel.Tokens.Jwt.JwtSecurityToken JWT = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(request.Token);

			Log.Information("Received config refresh request from {0}", JWT.Subject);

			APIConnection C = await ConnectionPool.GetFreeConnectionAsync();
			vortex.GRPC.RefreshConfigurationResponse resp = new RefreshConfigurationResponse();

			try {
				vortex.API.Client c = new API.Client(C.HttpClient);
				vortex.API.ClientConfiguration cc = null;

				C.HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.Token);

				try {
					cc = await c.GetPrintConfigurationAsync();
					resp.Success = true;
					resp.Status = "OK";
					resp.Config = Newtonsoft.Json.JsonConvert.SerializeObject(cc);
				} catch (vortex.API.ApiException e) {
					Log.Error(e, "Refresh configuration request failed");
					Log.Error(c.BaseUrl);
					Log.Error(e.Response);
					resp.Status = "Failed - "+e.Message;
					resp.Success = false;
				}
			} finally {
				C.Release();
			}

			// TODO: Cache this config for the requesting user so that we can throttle badly behaved client requests
			Log.Information("Returning configuration to caller");

			return resp;
		}

		public async override Task<SubmitStatusResponse> SubmitStatus(SubmitStatusRequest request, ServerCallContext context) {
			Log.Debug("Received request for status of job {JobId}", request.Jobid);
			SubmitStatusResponse ssr = new SubmitStatusResponse { Percent = -1, Status = "Not Found" };

			foreach(Submitter S in Submitters) {
				if(S.Connection.TransactionId == request.Jobid)
					Log.Debug("Returning: {0}", S.Percent);
					ssr = new SubmitStatusResponse { Percent = S.Percent, Status = S.Status };
			}

			CleanUpSubmitters();

			return ssr;
		}

		public async override Task<GetHotFoldersResponse> GetHotFolders(GetHotFoldersRequest req, ServerCallContext context) {
			GetHotFoldersResponse resp = new GetHotFoldersResponse();
			
			resp.Hotfolders = Newtonsoft.Json.JsonConvert.SerializeObject(this.HotFolders);

			this.HotFolders.ForEach((hf) => hf.Submitted = 0);

			return resp;
		}

		public async override Task<SetHotFolderUserResponse> SetHotFolderUser(SetHotFolderUserRequest req, ServerCallContext context) {
			SetHotFolderUserResponse resp = new SetHotFolderUserResponse() { Success=false };

			bool Found = false;

			foreach(HotFolder hf in HotFolders) {
				if(hf.Path.ToLower() == req.Path.ToLower()) {
					Found = true;

					vortex.State.User u = Newtonsoft.Json.JsonConvert.DeserializeObject<vortex.State.User>(req.User);

					hf.SetUser(u);

					this.State.SetHotFolderUser(req.Path, u);
					this.State.Save();

					resp.Success = true;
				}
			}

			if(!Found)
				resp.Error = "Hot folder not found";

			return resp;
		}

		public async override Task<ValidateUserResponse> ValidateUser(ValidateUserRequest req, ServerCallContext context) {
			ValidateUserResponse resp = new ValidateUserResponse() { Success=false };

			vortex.State.User u = Newtonsoft.Json.JsonConvert.DeserializeObject<vortex.State.User>(req.User);

			// User will have at least an ID token so attempt to authenticate the user and set the API token in the response without associating
			APIConnection API = null;

			try {
				API = await ConnectionPool.GetFreeConnectionAsync();

				vortex.API.Client Client = API.CreateClient();

				u.SetIdentityProvider(State.IdentityProviders);

				if(await u.EnsureAuthenticated(Client, API.Instance)) {
					resp.User = Newtonsoft.Json.JsonConvert.SerializeObject(u);
					resp.Success = true;
				}
			} finally {
				API?.Release();
			}

			return resp;
		}

		public async override Task<AddHotFolderResponse> AddHotFolder(AddHotFolderRequest req, ServerCallContext context)
		{
			AddHotFolderResponse resp = new AddHotFolderResponse() { Success = false };
			APIConnection API = null;

			try {
				vortex.State.HotFolder hf = Newtonsoft.Json.JsonConvert.DeserializeObject<vortex.State.HotFolder>(req.Hotfolder);

				hf.SubmittingUser.SetIdentityProvider(this.State.IdentityProviders);

				API = await ConnectionPool.GetFreeConnectionAsync();

				vortex.API.Client Client = API.CreateClient();

				// Get default settings from H-POD
				Log.Information("Getting account configuration for hot folder setup");
				if(await hf.SubmittingUser.EnsureAuthenticated(Client, API.Instance)) {
					API.HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hf.SubmittingUser.APIToken);

					vortex.API.ClientConfiguration Config = await Client.GetPrintConfigurationAsync();

					if(Config != null) {
						if(hf.settings == null) {
							if(Config.DefaultPrintSettings != null) {
								Log.Information("No print settings provided, applying defaults");
								hf.settings = new API.PublicPrintOptions() {
										Delivery = new API.PublicPrintGUIDOption() { Guid = Config.DefaultPrintSettings.Delivery },
										Duplex = new API.PublicPrintGUIDOption() { Guid = Config.DefaultPrintSettings.Duplex },
										Ink = new API.PublicPrintGUIDOption() { Guid = Config.DefaultPrintSettings.Ink },
										PackMode = new API.PublicPrintGUIDOption() { Guid = Config.DefaultPrintSettings.PackMode },
										Paper = new API.PublicPrintGUIDOption() { Guid = Config.DefaultPrintSettings.Paper },
										Envelope = new API.PublicPrintGUIDOption() { Guid = Config.DefaultPrintSettings.Envelope }
									};

								if(Config.DefaultPrintSettings.Inserts != null) {
									hf.settings.Inserts = new List<API.PublicPrintGUIDOption>();

									foreach(var I in Config.DefaultPrintSettings.Inserts) {
										hf.settings.Inserts.Add(new API.PublicPrintGUIDOption() { Guid = I.VersionGuid });
									}
								}
							} else {
								Log.Information("No default print settings available and no print settings provided");
							}
						}

						hf.FileTypes = Config.AccountSettings.HotFolderFileTypes;
					} else {
						Log.Information("Failed to acquire account configuration");
					}
				} else {
					Log.Information("Failed to authenticate user to acquire default print options");
					resp.Success = false;
				}

				if(hf.Validate()) {
					bool AlreadyMonitoring=false;

					foreach(vortex.Server.HotFolder f in this.HotFolders)
						if(f.Path == hf.Path)
							AlreadyMonitoring=true;

					if(!AlreadyMonitoring) {
						this.State.HotFolders.Add(hf);
						vortex.Server.HotFolder shf = this.AddHotFolderFromState(hf);

						Log.Information("Setting identity provider for submitting user");
						shf.SubmittingUser.SetIdentityProvider(this.State.IdentityProviders);
						shf.Start();

						this.State.Save();

						Log.Information("Added hot folder");

						resp.Success = true;
					} else {
						Log.Error("Already monitoring hotfolder");
						resp.Success = false;
					}
				} else {
					Log.Error("Hot folder validation failed");
					resp.Success = false;
				}
			} catch (Exception e) {
				Log.Error(e, "Failed to AddHotFolder");
				resp.Success = false;
			} finally {
				API?.Release();
			}

			return resp;
		}

		public async override Task<RemoveHotFolderResponse> RemoveHotFolder(RemoveHotFolderRequest request, ServerCallContext context)
		{
			try {
				bool ret = this.RemoveHotFolder(request.Path);

				return new RemoveHotFolderResponse() { Success = ret };
			} catch (Exception e) {
				Log.Error(e, "Failed to RemoveHotFolder");
				return new RemoveHotFolderResponse() { Success = false };
			}
		}

		public bool RemoveHotFolder(string Path) {
			bool ret = false;

			foreach(HotFolder hf in HotFolders) {
				if(hf.Path.ToLower() == Path.ToLower()) {
					Log.Information("Stopping hot folder "+hf.Path);
					// Stop the hot folder and remove it from the server's set
					hf.Stop();
					HotFolders.Remove(hf);

					// Remove the hot folder from the state's set
					this.State.RemoveHotFolder(Path);
					this.State.Save();
					ret=true;
					break;
				}
			}

			return ret;
		}

		public override async Task<GetConfigResponse> GetConfig(GetConfigRequest request, ServerCallContext context)
		{	
			GetConfigResponse r = new GetConfigResponse();
			vortex.State.ServerConfigPatch p = new State.ServerConfigPatch() { APIEndpoint = this.State.APIEndpoint, Instance = this.State.Instance };

			r.Config = Newtonsoft.Json.JsonConvert.SerializeObject(p);

			return r;
		}

		public override async Task<SetConfigResponse> SetConfig(SetConfigRequest request, ServerCallContext context)
		{
			bool Success = false;

			vortex.State.ServerConfigPatch p = Newtonsoft.Json.JsonConvert.DeserializeObject<vortex.State.ServerConfigPatch>(request.Config);

			if(p != null) {
				if(!string.IsNullOrEmpty(p.Instance)) {
					if(this.State.Instance != p.Instance) {
						this.State.Instance = p.Instance;

						// Instance has changed so remove all hot folders and clear all user tokens
						//this.HotFolders[0].SubmittingUser.
						foreach(var HF in this.HotFolders)
							HF.SubmittingUser.ClearAPIToken();
					}

					Success=true;
				}

				if(!string.IsNullOrEmpty(p.APIEndpoint)) {
					Success=true;
					this.State.APIEndpoint = p.APIEndpoint;
				}

				if(Success)
					this.State.Save();
			}

			return new SetConfigResponse() { Success = Success };
		}

		public override async Task<PatchHotFolderResponse> PatchHotFolder(PatchHotFolderRequest request, ServerCallContext context)
		{
			PatchHotFolderResponse resp = new PatchHotFolderResponse() { Success=false };

			foreach(HotFolder hf in HotFolders) {
				if(hf.Path.ToLower() == request.Path.ToLower()) {
					vortex.State.HotFolderPatch hfp = Newtonsoft.Json.JsonConvert.DeserializeObject<vortex.State.HotFolderPatch>(request.Patch);
					hf.Patch(hfp);

					this.State.PatchHotFolder(request.Path, hfp);
					this.State.Save();

					resp.Success = true;
				}
			}

			return resp;
		}

		public async override Task<GetIdentityProvidersResponse> GetIdentityProviders(GetIdentityProvidersRequest request, ServerCallContext context)
		{
			try {
				return new GetIdentityProvidersResponse() { Identityproviders = Newtonsoft.Json.JsonConvert.SerializeObject(this.State.IdentityProviders) };
			} catch (Exception e) {
				Log.Error(e, "Failed to GetIdentityProviders");
				return new GetIdentityProvidersResponse();
			}
		}

		public async override Task<AddIdentityProviderResponse> AddIdentityProvider(AddIdentityProviderRequest request, ServerCallContext context)
		{
			AddIdentityProviderResponse resp = new AddIdentityProviderResponse();

			try {
				vortex.State.IdentityProvider IDP = Newtonsoft.Json.JsonConvert.DeserializeObject<vortex.State.IdentityProvider>(request.Settings);

				IDP.Name = request.Name;

				if(this.State.IdentityProviders.Find((idp) => idp.Name == request.Name) == null) {
					this.State.IdentityProviders.Add(IDP);
					this.State.Save();

					resp.Success = true;
				} else {
					Log.Error("Failed to add identity provider as another with the same name already exists");
				}
			} catch (Exception e) {
				Log.Error(e, "Failed to add identity provider "+request.Settings);
			}

			return resp;
		}

		public async override Task<RemoveIdentityProviderResponse> RemoveIdentityProvider(RemoveIdentityProviderRequest request, ServerCallContext context)
		{
			RemoveIdentityProviderResponse resp = new RemoveIdentityProviderResponse();

			try {
				this.State.IdentityProviders.RemoveAll((idp) => idp.Name == request.Name);
				this.State.Save();

				resp.Success = true;
			} catch (Exception e) {
				Log.Error(e, "Failed to remove identity provider "+request.Name);
			}

			return resp;
		}
	}
}
