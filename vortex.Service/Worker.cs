using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Serilog;
using System.Net;

namespace vortex.Service {
	public class Worker : BackgroundService {
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			string CurrentDir = AppDomain.CurrentDomain.BaseDirectory;

			System.Diagnostics.EventLog.WriteEntry("vortex", $"CurrentDir: {CurrentDir}");

			string LogFolder = System.IO.Path.Combine(new System.IO.DirectoryInfo(CurrentDir).Parent.FullName, "logs");
			string ConfigPath = System.IO.Path.Combine(new System.IO.DirectoryInfo(CurrentDir).Parent.FullName, "config.json");

			vortex.State.Current State = new State.Current(ConfigPath, LogFolder);

			vortex.Server.Program.Startup(State);

			Log.Information("Server started");

			var listener = new HttpListener();
			listener.Prefixes.Add("http://*:9000/idtoken/");
			listener.Start();

			Log.Debug("http listener started");

			while (!stoppingToken.IsCancellationRequested) {
				try {
					HttpListenerContext context = await listener.GetContextAsync();
					HttpListenerRequest request = context.Request;
    
					Log.Debug("got request "+request.RawUrl);

					// do something here with request
					string resp = "";
				
					try {
						resp = await State.HotFolders[0].SubmittingUser.GetIDPIDTokenAsync();
					} catch (Exception e) {
						Log.Error(e, "failed to get id token for first hot folder");
						resp = "Not Available";
					}

					// default OK response
					context.Response.StatusCode = 200;
					context.Response.Headers.Add("Access-Control-Allow-Origin", "*");

					StreamWriter sw = new StreamWriter(context.Response.OutputStream);
					sw.Write(resp);
					sw.Flush(); 

					context.Response.Close();
				} catch (Exception e2) {
					Log.Error(e2, "Failed to respond on port 9000");
				}
			}
		}
	}
}
