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

namespace vortex.UI
{
	/// <summary>
	/// Interaction logic for PrintOptions.xaml
	/// </summary>
	public partial class PrintOptions
	{
		public delegate Task OKDelegate(Core.Submission S);
		public delegate void CancelDelegate();

		public event OKDelegate OK;
		public event CancelDelegate Cancel;

		public PrintOptions(vortex.UI.Core.Submission S)
		{
			InitializeComponent();

			((MVVM.ViewModel.PrintOptionsViewModel)this.DataContext).Populate(S);
		}

		private void MouseDownHandler(object sender, MouseButtonEventArgs e) {
			if (e.ChangedButton == MouseButton.Left)
				this.DragMove();
		}

		private void CloseDialog(object o, EventArgs e) {
			this.Close();
			Cancel();
		}

		private void Submit(object o, EventArgs e) {
			MVVM.ViewModel.PrintOptionsViewModel VM = (MVVM.ViewModel.PrintOptionsViewModel)this.DataContext;

			VM.Scrape();

			Core.Submission S = VM.Submission;

			this.Close();
			OK(S);
		}

		private void Scrape() {
			// Scrape into this.Current from the form controls
		}
	}
}
