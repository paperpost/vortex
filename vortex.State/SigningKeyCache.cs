using System;
using System.Collections.Generic;
using System.Text;

namespace vortex.State
{
	public class SigningKeyCache
	{
		public DateTime LastCached = DateTime.MinValue;

		public API.AuthenticateJWKS JWKS = null;

		public bool Expired() {
			return ((DateTime.UtcNow - LastCached).TotalHours >= 2);
		}

		public void Cache(API.AuthenticateJWKS JWKS) {
			this.JWKS = JWKS;
			this.LastCached = DateTime.UtcNow;
		}
	}
}
