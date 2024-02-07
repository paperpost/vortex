using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using Grpc.Core;

using vortex.GRPC;

namespace vortex.CLI
{
	internal class LogServer : Vortex.VortexBase
	{
		List<LogSession> Sessions = new List<LogSession>();

		DateTime FirstLogsReceived = DateTime.MinValue;
		DateTime StartTime = DateTime.UtcNow;

		string Buffer="";

		public async override Task<LogItemResponse> LogMessage(LogItem LI, ServerCallContext context) {
			LogSession LS = null;

			foreach(LogSession S in Sessions)
				if(S.source == LI.Source) {
					LS = S;
					break;
				}

			if(LS == null) {
				LS = new LogSession() { source = LI.Source };
				Sessions.Add(LS);
			}

			LS.messages.Add(string.Format("{0}: {1}", DateTime.Parse(LI.Time).ToString("HH:mm:ss.fff"), LI.Message));

			if(FirstLogsReceived==DateTime.MinValue) FirstLogsReceived = DateTime.UtcNow;

			lock(Buffer) {
				Buffer += string.Format("{0}: {1}\n", DateTime.Parse(LI.Time).ToString("HH:mm:ss.fff"), LI.Message);
			}

			return new LogItemResponse() { Success = true };
		}

		public void DumpLatest() {
			lock(Buffer) {
				if(Buffer.Length > 0) {
					Console.Write(Buffer);
					Buffer="";
				}

				if(((DateTime.UtcNow - StartTime).TotalSeconds > 60) && (FirstLogsReceived==DateTime.MinValue)) {
					Console.WriteLine("No logs received after 60 seconds - check log relay is enabled on your vortex server (vortex logging enable)");
					FirstLogsReceived = DateTime.UtcNow;
				}
			}
		}

		public List<string> Sources() {
			List<string> ret = new List<string>();

			foreach(LogSession LS in Sessions)
				ret.Add(LS.source);

			return ret;
		}

		public string FromSource(string s) {
			System.Text.StringBuilder sb = new StringBuilder();

			foreach(LogSession LS in Sessions) {
				if(LS.source == s) {
					foreach(string msg in LS.messages) {
						sb.AppendLine(msg);
					}
				}
			}

			return sb.ToString();
		}

		public void Start() {
			Grpc.Core.Server server = new Grpc.Core.Server {
				Services = { Vortex.BindService(this) },
				Ports = { new ServerPort("localhost", 30303, ServerCredentials.Insecure) }
			};

			server.Start();
		}
	}

	class LogSession {
		public string source;
		public List<string> messages = new List<string>();
	}
}
