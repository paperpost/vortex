using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using IdentityModel.OidcClient;
using IdentityModel.OidcClient.Browser;
using System.IdentityModel.Tokens.Jwt;

using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using Serilog;

using Newtonsoft.Json;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace vortex.State
{
	public class IdentityProvider
	{
		// Settings for an OpenID provider that supports Authorisation Code with PKCE flow
		public string Authority { get; set; } = "";
		public string DiscoveryEndpoint;
		public string ClientId;
		public string ClientSecret;
		public string IssuerRegex;
		public string Name { get; set; }
		public string Scope = "openid offline_access";
		public string RedirectUri = "http://localhost/vortex";

		[JsonIgnore] public OpenIdConnectConfiguration OpenIDConfig;

		public async Task DiscoverAsync() {
			try {
				ConfigurationManager<OpenIdConnectConfiguration> configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
					this.DiscoveryEndpoint,
					new OpenIdConnectConfigurationRetriever(),
					new HttpDocumentRetriever());

				OpenIDConfig = await configurationManager.GetConfigurationAsync();
			} catch (Exception e) {
				Log.Error($"Failed to discover OIDC provider {Name} - {e.Message}");
			}
		}

		private OidcClientOptions GenerateOptions() {
			OidcClientOptions ret = new OidcClientOptions {
						Authority =this.Authority,
						ClientId = this.ClientId,
						ClientSecret = this.ClientSecret,
						Scope = this.Scope,
						RedirectUri = this.RedirectUri,
						TokenClientCredentialStyle = IdentityModel.Client.ClientCredentialStyle.PostBody,
						
					};

			ret.Policy.Discovery = new IdentityModel.Client.DiscoveryPolicy();
			ret.Policy.Discovery.ValidateEndpoints = false;
			ret.Policy.Discovery.ValidateIssuerName = false;
			ret.Policy.ValidateTokenIssuerName = false;
			ret.Policy.RequireAccessTokenHash = false;

			//ret.BackchannelHandler = new OIDCLoggingHandler(new System.Net.Http.HttpClientHandler());

			return ret;
		}

		public async Task AcquireNewTokenAsync(User U, IdentityModel.OidcClient.Browser.IBrowser Browser, int Port) {
			var opts = GenerateOptions();

			opts.RedirectUri = string.Format($"http://localhost:{Port}/vortex");
			opts.Browser = Browser;
			//opts.LoggerFactory.AddSerilog(Log.Logger);

			OidcClient _oidcClient = new OidcClient(opts);

			LoginRequest lr = new LoginRequest();

			lr.FrontChannelExtraParameters.Add("prompt", "login");	// Always prompt for re-authentication as we may not want to use an already-authenticated session

			var result = await _oidcClient.LoginAsync(lr);

			if(!result.IsError) {
				U.IdentityToken = result.IdentityToken;
				U.RefreshToken = result.RefreshToken;
				U.AccessToken = result.AccessToken;
			} else {
				Log.Error($"Failed to acquire new token {result.Error}");
			}
		}

		public async Task RefreshTokenAsync(User U) {
			if(string.IsNullOrEmpty(U.RefreshToken))
				Log.Error("User has no refresh token available");
			else {
				OidcClient _oidcClient = new OidcClient(GenerateOptions());

				Log.Debug("RefreshTokenAsync");
				var result = await _oidcClient.RefreshTokenAsync(U.RefreshToken);

				if(!result.IsError) {
					Log.Debug("Token refreshed OK");
					U.IdentityToken = result.IdentityToken;
					U.AccessToken = result.AccessToken;

					if(!string.IsNullOrEmpty(result.RefreshToken))
						U.RefreshToken = result.RefreshToken;
				} else {
					Log.Error("Failed to refresh token - "+result.Error+" - "+result.ErrorDescription);
				}
			}
		}
	}
}
