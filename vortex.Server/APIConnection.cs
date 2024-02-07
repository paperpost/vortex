using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;

using Serilog;

namespace vortex.Server
{
	class APIConnection
	{
		public string Instance;
		public string APIEndpoint;
		public HttpClient HttpClient = new HttpClient();
		public string TransactionId = Guid.NewGuid().ToString().ToLower();
		private System.Threading.SemaphoreSlim sem = new System.Threading.SemaphoreSlim(1, 1);

		public int Index;

		public APIConnection(int Index, string Instance, string APIEndpoint) {
			this.Index = Index;
			this.Instance = Instance;
			this.APIEndpoint = APIEndpoint;
		}

		public async Task<HttpClient> GetClient() {
			if(await sem.WaitAsync(0))
				return HttpClient;
			else
				return null;
		}

		public void Release() {
			sem.Release();
		}

		public API.Client CreateClient() {
			Log.Verbose("Creating connection to API on "+this.APIEndpoint);

			API.Client c = new API.Client(this.HttpClient);
			c.BaseUrl = this.APIEndpoint;

			return c;
		}
	}
}
