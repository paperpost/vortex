using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

using Serilog;

namespace vortex.UI.Core
{
	class Preflight
	{
		private HttpClient HttpClient = new HttpClient();
		private vortex.API.Client c;

		private Guid BlobGUID;
		private vortex.State.Current State;

		public Preflight(vortex.State.Current State) {
			c = State.Local.APIClientFactory(HttpClient);

			this.State = State;
		}

		// Upload the blob if we haven't already done so
		public async Task<API.PreflightRequest> InitAsync(Core.Submission S) {
			API.PreflightRequest ret = new API.PreflightRequest();

			if(this.BlobGUID == Guid.Empty) {
				// Upload the PDF as a blob and store the guid in BlobGUID
				string Token = await State.Authentication.CurrentProvider.GetTokenAsync();

				HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);

				API.BlobBlobPutResponse R = await c.UploadBlobAsync(new API.BlobBlobPutRequest() { Filename = System.IO.Path.GetFileName(S.DocumentPath), Data = Convert.ToBase64String(System.IO.File.ReadAllBytes(S.DocumentPath)) });

				this.BlobGUID = R.BlobGuid;
			}

			ret.BlobGUID = this.BlobGUID;
			ret.PrintOptions = S.SelectedOptions;

			return ret;
		}

		// NB: Config refresh should include the maximum size of document we'll preflight
		public async Task<API.TaskPreflight> RunAsync(API.PreflightRequest Request) {
			bool Completed=false;
			API.TaskPreflight PreflightResult = null;

			Request = await c.InitPreflightRequestAsync(Request).ConfigureAwait(false);

			while(!Completed) {
				await Task.Delay(1000);

				API.TaskStatus TaskStatus = await c.GetTaskStatusAsync(Request.TaskGUID).ConfigureAwait(false);

				if(TaskStatus.Complete) {
					PreflightResult = await c.PreflightResultAsync(Request).ConfigureAwait(false);

					Completed=true;
				}
			}


			return PreflightResult;
		}
	}
}
