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
using vortex.Manager.MVVM;

namespace vortex.Manager.View
{
	/// <summary>
	/// Interaction logic for HotFolders.xaml
	/// </summary>
	internal partial class HotFolders : UserControl
	{
		protected RelayCommand AddHotFolderCommand { get; set; }

		public HotFolders()
		{
			InitializeComponent();

			AddHotFolderCommand = new RelayCommand(o => AddHotFolder());
		}

		private void AddHotFolder() {
			vortex.State.HotFolder HF = new State.HotFolder();

			vortex.Manager.MainViews.HotFolder vHF = new Manager.MainViews.HotFolder(HF, ViewModel.MainWindowViewModel.State.IdentityProviders);

			vHF.Owner = Window.GetWindow(this);

			vHF.ShowDialog();
		}
	}
}
