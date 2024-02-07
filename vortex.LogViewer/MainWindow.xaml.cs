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

using Grpc.Core;
using vortex.GRPC;

namespace vortex.LogViewer
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		LogServer LS = new LogServer();

		public MainWindow()
		{
			InitializeComponent();

			Grpc.Core.Server server = new Grpc.Core.Server {
				Services = { Vortex.BindService(LS) },
				Ports = { new ServerPort("localhost", 30303, ServerCredentials.Insecure) }
			};

			server.Start();

			var dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
			dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
			dispatcherTimer.Interval = new TimeSpan(0,0,2);
			dispatcherTimer.Start();
		}

		private void dispatcherTimer_Tick(object sender, EventArgs e) {
			List<string> Sources = LS.Sources();

			foreach(string s in Sources) {
				TextBox tb = null;

				foreach(TabItem ti in tc.Items) {
					if(ti.Header.ToString() == s) {
						tb = (TextBox)ti.Content;
					}
				}

				if(tb == null) {
					TabItem ti = new TabItem() { Header = s };
					tb = new TextBox() {
							Name = "txtLog",
							Background = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
							Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 170, 0)),
							FontFamily = new FontFamily("Courier New"),
							FontSize = 18,
							FontWeight = FontWeights.Bold
						};
					ti.Content = tb;
					tc.Items.Add(ti);

					if(tc.Items.Count==1)
						ti.IsSelected=true;
				}

				tb.Text = LS.FromSource(s);
			}

			CommandManager.InvalidateRequerySuggested();
		}
	}
}
