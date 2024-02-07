using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grpc.Core;
using vortex.GRPC;

namespace vortex.PDF
{
	class Program
	{
		// This is just a small executable for NovaPDF to call.  It will RPC to vortex.Server to submit the file
		static int Main(string[] args) {
            // Pull GRPC port out of registry

            Channel channel = new Channel("127.0.0.1:30051", ChannelCredentials.Insecure);

            var client = new Vortex.VortexClient(channel);
            var reply = client.SubmitPDF(new SubmitPDFRequest { Path = args[0] });
            Console.WriteLine("Status: " + reply.Status);

            channel.ShutdownAsync().ConfigureAwait(false).GetAwaiter().GetResult();

            return 0;
        }
	}
}
