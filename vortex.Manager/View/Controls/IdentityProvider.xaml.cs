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
	/// Interaction logic for IdentityProvider.xaml
	/// </summary>
	public partial class IdentityProvider : UserControl
	{
		private vortex.State.IdentityProvider _idp;

		public vortex.State.IdentityProvider IDP{
			get => _idp;
			set {
				_idp=value;
			}
		}

		public IdentityProvider()
		{
			InitializeComponent();
		}
	}
}
