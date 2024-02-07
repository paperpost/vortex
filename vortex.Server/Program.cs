using System;
using vortex.GRPC;
using Grpc.Core;
using Grpc.Core.Interceptors;

using System.Threading;
using System.Threading.Tasks;

using Serilog;

using vortex.Common;
using System.Runtime.CompilerServices;
using System.Net;
using System.IO;

namespace vortex.Server
{
	public class Program
	{
		private static vortex.State.Current State = null;

		static async Task Main(string[] args)
		{
			try {
				Startup(State);

				Log.Information("Starting listener for idtoken on port 9000");
				var listener = new HttpListener();
				listener.Prefixes.Add("http://*:9000/idtoken");
				listener.Start();

				while (true) {
					HttpListenerContext context = await listener.GetContextAsync();
					HttpListenerRequest request = context.Request;
    
					// do something here with request
					string resp = await State.HotFolders[0].SubmittingUser.GetIDPIDTokenAsync();

					StreamWriter sw = new StreamWriter(context.Response.OutputStream);
					sw.Write(resp);
					sw.Flush(); 

					// default OK response
					context.Response.StatusCode = 200;
					context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
					context.Response.Close();
				}

				//await Task.Delay(Timeout.Infinite);
			} catch (Exception e) {
				Log.Error(e, "Failed to start server, Vortex configuration may be corrupt");
			}

			Log.Information("Terminating");
		}

		public static void InitLogging(vortex.State.Current State) {
			if(!System.Diagnostics.Debugger.IsAttached) {
				Log.Logger = new LoggerConfiguration()
					.MinimumLevel.Debug()
					.Enrich.FromLogContext()
					.Enrich.With<vortex.Server.CallerEnricher>()
					.WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({Caller}) {Message:lj}{NewLine}{Exception}")
					.WriteTo.File(System.IO.Path.Combine(State.Local.LogPath, "vortex.server-.log"), rollingInterval: RollingInterval.Day, shared: true, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({Caller}) {Message:lj}{NewLine}{Exception}")
					.WriteTo.LogSink("server", LogRelay: State.Local.LogRelay)
					.CreateLogger();
			} else {
				Log.Logger = new LoggerConfiguration()
					.MinimumLevel.Verbose()
					.Enrich.FromLogContext()
					.Enrich.With<vortex.Server.CallerEnricher>()
					.WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({Caller}) {Message:lj}{NewLine}{Exception}")
					.WriteTo.LogSink("server", LogRelay: State.Local.LogRelay)
					.CreateLogger();
			}
		}

		public static void Startup(vortex.State.Current State) {
			try {
				Program.State = State;

				InitLogging(State);

				Log.Information("Starting gRPC server on port "+State.Local.GRPCPort.ToString());
				Grpc.Core.Server server = new Grpc.Core.Server {
					Services = { Vortex.BindService(new VortexServer(State, 10)).Intercept(new vortex.Common.gRPCLogger()) },
					Ports = { new ServerPort("localhost", State.Local.GRPCPort, ServerCredentials.Insecure) }
				};

				try {
					server.Start();
				} catch (Exception e) {
					Log.Error(e, "Failed to start server");
				}

			} catch (Exception eStartup) {
				Log.Error(eStartup, "Failed to start server, Vortex configuration may be corrupt");
			}
		}
	}
}
