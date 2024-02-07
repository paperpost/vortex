using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Serilog;
using Serilog.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

using System.CommandLine;
using System.CommandLine.NamingConventionBinder;

using vortex.Common;

namespace vortex.Service
{
	public class Program
	{
		public static async Task Main(string[] args)
		{
			InitLogging();

            if(args.Length > 0) {
                var cmdAddHotFolder = new Command("install", "Install Service");
                cmdAddHotFolder.Add(new Argument<string>("instance", "The instance name"));
                cmdAddHotFolder.Handler = CommandHandler.Create<string>(InstallService);

                var cmdRemoveHotFolder = new Command("uninstall", "Uninstall Service");
                cmdRemoveHotFolder.Add(new Argument<string>("instance", "The name of the instance to remove"));
                cmdRemoveHotFolder.Handler = CommandHandler.Create<string>(UninstallService);

                var RootCommand = new Command("vortex.service");

                RootCommand.Add(cmdAddHotFolder);
                RootCommand.Add(cmdRemoveHotFolder);

                RootCommand.Invoke(args);
            } else {
                Log.Debug("Starting service");
    			await CreateHostBuilder(args).Build().RunAsync();
                Log.Debug("Service shutting down");
            }
		}

        private static void InstallService(string Instance) {
            string path = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

            Log.Information("Installing service instance {0} / {1}", Instance, path);

            ServiceInstaller.InstallAndStart(Instance, "Vortex Server - "+Instance, path);
        }

        private static void UninstallService(string Instance) {
            Log.Information("Uninstalling service instance {0}", Instance);

            ServiceInstaller.Uninstall(Instance);
        }

        private static void InitLogging() {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .Enrich.With<vortex.Service.CallerEnricher>()
                .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({Caller}) {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(System.IO.Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Paperpost", "H-POD", "Logs", "vortex-service-.txt"), rollingInterval: RollingInterval.Day, shared: true, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({Caller}) {Message:lj}{NewLine}{Exception}")
				.WriteTo.LogSink("server")
                .CreateLogger();
        }
		
		public static IHostBuilder CreateHostBuilder(string[] args) {
			var ret = Host.CreateDefaultBuilder(args);

            ret = ret.ConfigureServices((hostContext, services) => {
					services.AddHostedService<Worker>();
				});

		    ret = ret.UseWindowsService();

            Log.Information("Created hostbuilder");

            return ret;
		}
	}
}
