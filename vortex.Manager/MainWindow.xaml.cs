using System;
using System.Windows;

namespace vortex.Manager
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			this.MouseDown += delegate{DragMove();};
		}

		private void Quit(object o, EventArgs e) {
			Application.Current.Shutdown(0);
		}
	}
}
