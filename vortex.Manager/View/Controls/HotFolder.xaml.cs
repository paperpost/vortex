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

namespace vortex.Manager.View.Controls
{
	/// <summary>
	/// Interaction logic for HotFolder.xaml
	/// </summary>
	public partial class HotFolder : UserControl
	{
		public HotFolder()
		{
			this.MouseDoubleClick += EditHF;

			InitializeComponent();
		}

		private void EditHF(object o, EventArgs e) {
			vortex.Manager.MainViews.HotFolder HF = new Manager.MainViews.HotFolder((vortex.State.HotFolder)this.DataContext, ViewModel.MainWindowViewModel.State.IdentityProviders);

			HF.Owner = Window.GetWindow(this);

			HF.ShowDialog();
		}
	}
}
