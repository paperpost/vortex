using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

using Newtonsoft.Json;

using Serilog;

namespace vortex.State
{
	public class User
	{
		// This class contains user details as well as per-user client settings
		public Guid UserGUID = Guid.Empty;
		public string APIToken = "";

		[JsonIgnore] public IdentityProvider IdentityProvider { get; set; }

		public string AccountLogin { get; set; }
		public string IdentityProviderName = "";
		public string IdentityToken = "";
		public string AccessToken = "";
		public string RefreshToken = "";
		public SigningKeyCache SigningKeyCache = new SigningKeyCache();

		public void SetIdentityProvider(List<IdentityProvider> Providers) {
			foreach(IdentityProvider P in Providers) {
				if(P.Name == IdentityProviderName) {
					this.IdentityProvider = P;
					break;
				}
			}
		}

		public void ClearAPIToken() {
			this.APIToken = null;
			this.UserGUID = Guid.Empty;
		}

		public User Clone() {
			User ret = new User();

			ret.AccountLogin = this.AccountLogin;
			ret.IdentityProviderName = this.IdentityProviderName;
			ret.IdentityToken = this.IdentityToken;
			ret.AccessToken = this.AccessToken;
			ret.RefreshToken = this.RefreshToken;

			return ret;
		}

		public string Email {
			get {
				try {
					if(!string.IsNullOrEmpty(this.APIToken)) {
						System.IdentityModel.Tokens.Jwt.JwtSecurityToken JWT = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(this.APIToken);

						return JWT.Subject;	// APIv3 JWTs always have the email address as the subject
					} else
						return "";
				} catch (Exception e) {
					Log.Error(e, "Failed to decode JWT in Email getter");
					return "";
				}
			}
		}

		private System.Threading.SemaphoreSlim semTokenRefresh = new System.Threading.SemaphoreSlim(1, 1);

		public async Task EnsureIDPIDToken() {
			if(this.IdentityProvider == null)
				throw new InvalidOperationException("IdentityProvider not set");

			if(this.IdentityProvider.OpenIDConfig == null)
				throw new InvalidOperationException("OpenIdConfig not set on IdentityProvider");

			bool ValidIDToken=false;

			if(!string.IsNullOrEmpty(this.IdentityToken)) {
				System.IdentityModel.Tokens.Jwt.JwtSecurityToken IDToken = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(this.IdentityToken);

				System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler sth = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();

				Microsoft.IdentityModel.Tokens.SecurityToken st = null;

				try {
					sth.ValidateToken(this.IdentityToken, new Microsoft.IdentityModel.Tokens.TokenValidationParameters() {
						ValidateLifetime = true,
						ValidateAudience = false,
						ValidateIssuer = false,
						IssuerSigningKeys = this.IdentityProvider.OpenIDConfig.SigningKeys }, out st);
				} catch (Microsoft.IdentityModel.Tokens.SecurityTokenExpiredException eExpired) {
					Log.Verbose("ID token has expired");
				} catch (Exception e) {
					Log.Error(e, "ID token validation failure");
				}

				if(st != null)
					ValidIDToken=true;
			} else {
				Log.Information("Hotfolder has no ID token registered");
			}

			if(ValidIDToken) {
				Log.Verbose("ID token is valid");
			} else {
				Log.Verbose("Attempting OpenID token refresh");
				// Refresh the token
				await this.IdentityProvider.RefreshTokenAsync(this);
			}
		}

		public async Task<bool> EnsureAuthenticated(API.Client c, string Instance) {
			bool ValidToken = false;

			await semTokenRefresh.WaitAsync(-1);

			try {
				System.IdentityModel.Tokens.Jwt.JwtSecurityToken JWT = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(this.APIToken);

				System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler sth = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();

				Microsoft.IdentityModel.Tokens.SecurityToken st = null;

				if(SigningKeyCache.Expired()) {
					API.AuthenticateJWKS jwks = await c.KeysAsync();
					SigningKeyCache.Cache(jwks);

					Log.Verbose("Refreshed API token signing keys");
				}

				try {
					var keys = new List<JsonWebKey>();

					foreach (var webKey in SigningKeyCache.JWKS.Keys) {
						keys.Add(new JsonWebKey(Newtonsoft.Json.JsonConvert.SerializeObject(webKey)));
					}

					sth.ValidateToken(this.APIToken, new Microsoft.IdentityModel.Tokens.TokenValidationParameters() {
							ValidateLifetime = true,
							ValidateAudience = false,
							ValidateIssuer = true,
							ValidIssuer = "h-pod.co.uk",
							IssuerSigningKeys = keys
						}, out st);
				} catch (Exception e) {
					Log.Verbose(e, "API token validation failure");
					Log.Verbose( this.APIToken);
				}

				if(st != null) {
					ValidToken=true;
					Log.Verbose("Existing API token is valid");
				}
			} catch (Exception e) {
				Log.Verbose(e, "API token not valid, attempting to acquire a new one");

			}

			if(!ValidToken) {
				try {
					await EnsureIDPIDToken();

					if(!string.IsNullOrEmpty(this.IdentityToken)) {
						API.AuthenticateResponse AR = null;

						try {
							API.AuthenticateRequest req = new API.AuthenticateRequest() { Instance = Instance, AccountLogin = this.AccountLogin, Token = this.IdentityToken };

							Log.Verbose("Attempting authentication with "+Newtonsoft.Json.JsonConvert.SerializeObject(req));

							AR = await c.AuthenticateAsync(req);
						} catch (API.ApiException ae) {
							Log.Error(ae, "Error authenticating with current ID token");
						}

						if(AR != null) {
							if(!string.IsNullOrEmpty(AR.Token)) {
								Log.Information("Authenticated OK");

								this.APIToken = AR.Token;
								ValidToken = true;

								System.IdentityModel.Tokens.Jwt.JwtSecurityToken JWT = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(this.APIToken);
							} else {
								Log.Error("AuthenticateAsync did not return a valid API token");
							}

							ValidToken = true;
						} else {
							Log.Information("No AuthResult returned by AuthenticateAsync");
						}
					} else {
						Log.Information("Failed to identify user");
					}
				} catch (Exception e) {
					Log.Error(e, "Failed to acquire a new API token");
				}
			}

			semTokenRefresh.Release();

			return ValidToken;
		}

		public async Task<string> GetIDPIDTokenAsync() {
			// Use the identity provider to get the latest token for this user
			await EnsureIDPIDToken();

			return IdentityToken;
		}
	}
}
