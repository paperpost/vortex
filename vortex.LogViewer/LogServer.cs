using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using vortex.GRPC;
using Grpc.Core;

namespace vortex.LogViewer
{
	class LogServer : Vortex.VortexBase
	{
		List<LogSession> Sessions = new List<LogSession>();

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

			return new LogItemResponse() { Success = true };
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
	}

	class LogSession {
		public string source;
		public List<string> messages = new List<string>();
	}
}
