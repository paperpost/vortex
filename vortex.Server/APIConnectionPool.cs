using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

using Serilog;

namespace vortex.Server
{
	internal class APIConnectionPool
	{
		List<APIConnection> ConnectionPool = new List<APIConnection>();

		public APIConnectionPool(int PoolSize, string Instance, string APIEndpoint) {
			for(int i=0;i<PoolSize;i++)
				ConnectionPool.Add(new APIConnection(i, Instance, APIEndpoint));
		}

		public async Task<APIConnection> GetFreeConnectionAsync() {
			while(true) {
				foreach(APIConnection C in ConnectionPool) {
					HttpClient ret = await C.GetClient();

					if(ret != null) {
						Log.Verbose("Using API connection {connectionId}", C.Index);
						return C;
					}
				}

				Task.Delay(500).Wait();	// Wait 250ms then try again to find a free connection
			}
		}
	}
}
