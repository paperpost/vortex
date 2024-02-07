using System;

using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Configuration;

using Grpc.Core;
using vortex.GRPC;

namespace vortex.Common
{
	public class LogSink : ILogEventSink
	{
		static Channel channel;
		Vortex.VortexClient client;

		private string Source;
		int LogServerPort = 30303;
		DateTime LastConnectionAttempt = DateTime.MinValue;
		bool Connected = false;
		bool Enabled=true;

		private readonly IFormatProvider _formatProvider;

		public LogSink(IFormatProvider formatProvider, string source, bool Enabled=true) {
			_formatProvider = formatProvider;

			this.Enabled = Enabled;
			this.Source = source;

			if(Enabled)
				TryConnect();
		}

		public void Emit(LogEvent logEvent) {
			if(Enabled) {
				if(!Connected)
					if((DateTime.UtcNow - LastConnectionAttempt).TotalSeconds > 10)
						TryConnect();

				if(Connected) {
					var message = logEvent.RenderMessage(_formatProvider);

					try {
						client.LogMessage(new LogItem() { Time = DateTime.UtcNow.ToString("dd MMM yyyy HH:mm:ss.fff"), Message = message, Source = Source }, deadline: DateTime.UtcNow.AddSeconds(0.5));
					} catch {
						Connected=false;
					}
				}
			}
	    }

		private bool TryConnect() {
			try {
				if(!Connected) {
					LastConnectionAttempt = DateTime.UtcNow;

					channel = new Channel(string.Format("127.0.0.1:{0}", LogServerPort), ChannelCredentials.Insecure);
					client = new Vortex.VortexClient(channel);

					Connected=true;
				}
			} catch {
				Connected=false;
				Console.WriteLine("Could not connect to gRPC log server");
			}

			return Connected;
		}
	}

	public static class LogSinkExtensions
	{
		public static LoggerConfiguration LogSink(
				  this LoggerSinkConfiguration loggerConfiguration, string source,
				  IFormatProvider formatProvider = null, bool LogRelay = false)
		{
			return loggerConfiguration.Sink(new LogSink(formatProvider, source, LogRelay));
		}
	}
}
