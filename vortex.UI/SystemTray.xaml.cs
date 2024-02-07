using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

using Serilog;

using vortex.Common;

namespace vortex
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class SystemTray : Window
	{
		private System.Windows.Forms.NotifyIcon notifyIcon = null;
        private UI.ServerComms Server;
        private vortex.State.Current State = null;
        private UI.Core.IconAnimator IconAnimator = null;

		public SystemTray()
		{
			InitializeComponent();
		}

        private void InitState() {
            State = vortex.UI.Core.StateStorage.Load();

            string[] args = System.Environment.GetCommandLineArgs();

            for(int i=1;i<args.Length;i++) {
                switch(args[i].ToLower()) {
                    case "/control":
                        State.MainInstance = true;
                        break;
                    case "/logserver":
                        State.LogServer = true;
                        break;
                }
            }
        }

        private void InitLogging() {
            if(State.LogServer) {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .Enrich.FromLogContext()
                    .Enrich.With<vortex.UI.Core.CallerEnricher>()
                    .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({Caller}) {Message:lj}{NewLine}{Exception}")
                    .WriteTo.File(State.Local.LogPath, rollingInterval: RollingInterval.Hour, shared: true, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({Caller}) {Message:lj}{NewLine}{Exception}")
                    .WriteTo.LogSink("client")
                    .CreateLogger();
            } else {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .Enrich.FromLogContext()
                    .Enrich.With<vortex.UI.Core.CallerEnricher>()
                    .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({Caller}) {Message:lj}{NewLine}{Exception}")
                    .WriteTo.File(State.Local.LogPath, rollingInterval: RollingInterval.Hour, shared: true, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({Caller}) {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();
            }
        }

        private void InitSystemTray() {
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.DoubleClick += notifyIcon_DoubleClick;

            IconAnimator = new UI.Core.IconAnimator(notifyIcon);

            notifyIcon.Icon = IconAnimator.Idle();
            notifyIcon.Visible=true;
            notifyIcon.BalloonTipText = "H-POD";
            notifyIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            notifyIcon.ContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                new System.Windows.Forms.ToolStripMenuItem {
                    Name = "mnuDefaultPrintOptions",
                    Text = "&Default Print Options...",
                },
                new System.Windows.Forms.ToolStripMenuItem {
                    Name = "mnuNotificationSettings",
                    Text = "&Notification Settings...",
                },
                new System.Windows.Forms.ToolStripMenuItem {
                    Name = "mnuDirectSubmission",
                    Text = "&Direct Submission...",
                },
                new System.Windows.Forms.ToolStripSeparator(),
                new System.Windows.Forms.ToolStripMenuItem {
                    Name = "mnuAdvanced",
                    Text = "&Advanced"
                },
                new System.Windows.Forms.ToolStripSeparator(),
                new System.Windows.Forms.ToolStripMenuItem {
                    Name = "mnuQuit",
                    Text = "E&xit..."
                }
            });

            System.Windows.Forms.ToolStripMenuItem mnuAdvanced = (System.Windows.Forms.ToolStripMenuItem)notifyIcon.ContextMenuStrip.Items["mnuAdvanced"];

            mnuAdvanced.DropDownItems.AddRange(new System.Windows.Forms.ToolStripMenuItem[] {
                new System.Windows.Forms.ToolStripMenuItem() {
                    Name = "mnuCredentials",
                    Text = "&Credentials..."
                },
                new System.Windows.Forms.ToolStripMenuItem() {
                    Name = "mnuRefreshConfiguration",
                    Text = "&Refresh Configuration..."
                },
                new System.Windows.Forms.ToolStripMenuItem() {
                    Name = "mnuClearCache",
                    Text = "&Clear Cache"
                },
                new System.Windows.Forms.ToolStripMenuItem() {
                    Name = "mnuOfflineQueue",
                    Text = "&Offline Queue..."
                }
            });

            notifyIcon.ContextMenuStrip.Items["mnuDirectSubmission"].Click += new System.EventHandler(DirectSubmission);
            notifyIcon.ContextMenuStrip.Items["mnuDefaultPrintOptions"].Click += new System.EventHandler(ShowDefaultPrintOptions);
            notifyIcon.ContextMenuStrip.Items["mnuQuit"].Click += new System.EventHandler(Quit);
            mnuAdvanced.DropDownItems["mnuRefreshConfiguration"].Click += new System.EventHandler(RefreshConfiguration);
        }

        protected override void OnInitialized(EventArgs e) {
            InitState();
            InitLogging();

            Log.Information("v"+System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());

            if(State.MainInstance) {
                InitSystemTray();
                Task.Run(async() => await StartWorkingAsync());
            }

            Loaded += OnLoaded;

            // Connect to the GRPC server
            Server = new UI.ServerComms(State);
            Server.ServerNotAvailable += GRPCServerNotAvailable;

            // Start the background worker which will handle config refreshes, log housekeeping etc
            vortex.UI.Core.Background B = new UI.Core.Background();
            B.Start(State, Server);

            // Wait for Background to be ready - ie: have attempted at least one config refresh
            while(!B.Ready)
                System.Threading.Thread.Sleep(100);

            // At this point we should have either a cached config or a latest config, as long as vortex.Server is up and running - if not, do nothing more, GRPCServerNotAvailable will shut things down
            if(State.ClientConfig != null) {
                string[] args = System.Environment.GetCommandLineArgs();

                for(int i=1;i<args.Length;i++) {
                    //Log.Information("arg[{0}] {1}", i, args[i]);

                    switch(args[i].ToLower()) {
                        case "/pdf":
                            string PDFPath = args[++i];
			                string DocumentName = System.IO.Path.GetFileName(PDFPath);

                            Log.Information("Received command-line instruction to print {0}", PDFPath);

			                if(DocumentName.Contains("_HPOD_"))
				                DocumentName = DocumentName.Substring(0, DocumentName.IndexOf("_HPOD_"))+".pdf";

                            // Fire off a message to submit the file somehow...
                            vortex.UI.Core.Submission S = new UI.Core.Submission(State, Server, PDFPath, DocumentName, true);
                            break;
                    }
                }
            }

            StopWorking();

            base.OnInitialized(e);
        }

        private delegate void CloseApplicationDelegate();

        private void CloseApplicationHandler() {
            Log.Information("Shutting down");
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new CloseApplicationDelegate(CloseApplication));
        }

        private void CloseApplication() {
            Application.Current.Shutdown(0);
        }

        private void GRPCServerNotAvailable() {
            Log.Information("Vortex Server unavailable via gRPC");

            MessageBox.Show("Vortex Server is not available.  Please ensure the Vortex Server service is running");

            Environment.Exit(-1);
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e) {
            // NOP
        }

        private async void DirectSubmission(object sender, EventArgs e) {
            var dialog = new Microsoft.Win32.OpenFileDialog();

            dialog.Filter = "PDF (.pdf)|*.pdf"; // Filter files by extension

            Log.Information("User requested direct submission");

            bool? result = dialog.ShowDialog();

            if(result==true) {
                Log.Information("File {0} selected for direct submission", dialog.FileName);
                string PDFPath = dialog.FileName;
			    string DocumentName = System.IO.Path.GetFileName(PDFPath);

			    if(DocumentName.Contains("_HPOD_"))
				    DocumentName = DocumentName.Substring(0, DocumentName.IndexOf("_HPOD_"))+".pdf";

                vortex.UI.Core.Submission S = new UI.Core.Submission(State, Server, PDFPath, DocumentName, false);
                await S.StartAsync();
            }
        }

        private void ShowDefaultPrintOptions(object sender, EventArgs e) {
            Log.Information("User opened default print options using system tray");

            //vortex.UI.DefaultPrintOptions DPO = new UI.DefaultPrintOptions(State.ClientConfig);
            //DPO.Show();
        }

        private void Quit(object o, EventArgs e) {
            Log.Information("User requested exit using system tray");
            notifyIcon.Visible=false;

            Application.Current.Shutdown(0);
        }

        private bool StillWorking = false;

        private void StopWorking() {
            StillWorking=false;
        }

        private async Task StartWorkingAsync() {
            StillWorking=true;

            while(StillWorking) {
                Log.Verbose("Working...");
                notifyIcon.Icon = IconAnimator.Working();

                await Task.Delay(100);
            }

            Log.Verbose("Working finished, setting idle icon");

            notifyIcon.Icon = IconAnimator.Idle();
        }

        private async void RefreshConfiguration(object o, EventArgs e) {
            await StartWorkingAsync();

            Log.Debug("Starting refresh configuration task");
            await Server.TriggerRefreshConfiguration("manual");
            
            StopWorking();
            Log.Information("Refresh config complete");
        }
    }
}
