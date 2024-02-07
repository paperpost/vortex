using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Serilog;

namespace vortex.State.Auth
{
	public class HPOD : IAuth
	{
		private static readonly System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();

		public string AccountLogin = "HPOD";
		public string EmailAddress = "jeff.belcher@paperpost.co.uk";
		public string Instance = "dev";
		public string Password = "Hand5fr33";

		public int OverrideTokenLifetimeSeconds=60;
		public string CurrentToken = "";

		public override async Task<string> GetTokenAsync()
		{
			vortex.API.Client C = new API.Client(client);

			bool TokenValid=false;

			if(!string.IsNullOrEmpty(CurrentToken)) {
				TokenValid=true;
			}

			if(!TokenValid) {
				Log.Debug("Acquiring API token for testing from {APIURL}", C.BaseUrl);

				API.AuthenticateResponse AR = await C.AuthenticateAsync(new API.AuthenticateRequest() { AccountLogin=AccountLogin, EmailAddress=EmailAddress, Instance=Instance, Password=Password }).ConfigureAwait(false);

				if(!string.IsNullOrEmpty(AR.Token)) {
					CurrentToken = AR.Token;
					Log.Debug("OK");
				} else
					Log.Debug("Failed");
			}

			return CurrentToken;
		}
	}
}
