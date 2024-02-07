using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace vortex.State.Auth
{
	class OpenID : IAuth
	{
		public string DiscoveryURL;

		public override async Task<string> GetTokenAsync() {
			return "";
		}
	}
}
