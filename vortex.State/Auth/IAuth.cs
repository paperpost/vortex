using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;


namespace vortex.State.Auth
{
	public abstract class IAuth
	{
		public enum AuthenticationType {
			OpenID,
			JWT
		}

		public abstract Task<string> GetTokenAsync();
	}
}
