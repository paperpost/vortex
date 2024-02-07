using System;
using System.CodeDom;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using vortex.Manager.ViewModel;

namespace vortex.Manager.MainViews
{
	public partial class HotFolder : Window
	{
		public HotFolder(vortex.State.HotFolder HF, System.Collections.Generic.List<vortex.State.IdentityProvider> IDPs)
		{
			InitializeComponent();

			this.DataContext=new HotFolderViewModel(HF, IDPs);
		}

		public vortex.State.HotFolder Folder {
			get {
				return ((HotFolderViewModel)this.DataContext).HotFolder;
			}
		}

		private void Dismiss(object o, EventArgs e) {
			this.DialogResult =	false;
			this.Close();
		}

		private void Confirm(object o, EventArgs e) {
			this.DialogResult = true;
			this.Close();
		}

	}
}
