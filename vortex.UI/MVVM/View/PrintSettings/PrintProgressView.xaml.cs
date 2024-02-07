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
using System.Windows.Shapes;

using Serilog;

namespace vortex.UI.MVVM.View
{
	/// <summary>
	/// Interaction logic for PrintProgressView.xaml
	/// </summary>
	public partial class PrintProgressView : Window
	{
		public delegate void FinishedDelegate();
		public event FinishedDelegate Finished;

		public PrintProgressView()
		{
			InitializeComponent();
		}

		public void Failed(string status) {
			txtProgress.Foreground = new SolidColorBrush(Colors.Red);
			txtProgress.Text = status;
		}

		public async Task TrackJobAsync(vortex.UI.ServerComms Server, string JobId) {
			double Progress=0;

			try {
				while((Progress < 100) && (Progress >= 0)) {
					Log.Debug("Acquiring submission status for "+JobId);
					var ret = await Server.SubmitStatus(JobId);

					txtProgress.Text = string.Format("{0}%", ret.Percent);
					txtStatus.Text = ret.Status;
					pbProgress.Value = ret.Percent;

					Progress = ret.Percent;

					Log.Debug("Progress: {0}%", ret.Percent);

					await Task.Delay(250);
				}
			} catch (Exception e) {
				Log.Error(e, "Error tracking job "+JobId);
			}

			this.Finished();
		}
	}
}
