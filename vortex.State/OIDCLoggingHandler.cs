using Serilog;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;

namespace vortex.State
{
	internal class OIDCLoggingHandler : DelegatingHandler {
		public OIDCLoggingHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (request.Content != null)
			{
//				Log.Verbose(await request.Content.ReadAsStringAsync().ConfigureAwait(false));
			}

			HttpResponseMessage response = null;

			try {
				response = await base.SendAsync(request, cancellationToken);

				Log.Verbose("Response:");
				Log.Verbose(response.ToString());
				if (response.Content != null) {
					Log.Verbose(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
				}
				Log.Verbose("---");
			} catch (Exception e) {
				Log.Error(e, "Request Failed");
				throw;
			}

			return response;
		}
	}
}
