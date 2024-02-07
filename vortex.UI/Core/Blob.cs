using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using System.Net.Http;

namespace vortex.UI.Core
{
	class Blob
	{
		private HttpClient http = new HttpClient();

		private vortex.State.Current State = null;

		public Blob(vortex.State.Current State) {
			http.BaseAddress = new Uri(State.APIEndpoint);

			this.State = State;
		}

		public async Task<byte[]> Download(Guid BlobGUID) {
			http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await State.Authentication.CurrentProvider.GetTokenAsync());
			HttpResponseMessage msg = await http.GetAsync(string.Format("blob/{0}", BlobGUID));

			msg.EnsureSuccessStatusCode();

			return await msg.Content.ReadAsByteArrayAsync();
		}
	}
}
