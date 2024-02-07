using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vortex.Manager.MVVM;

namespace vortex.Manager.ViewModel
{
	internal class IdentityProvidersViewModel : ViewModelBase
	{
		public ObservableCollection<vortex.State.IdentityProvider> IdentityProviders { get; set; }

		public IdentityProvidersViewModel(List<vortex.State.IdentityProvider> IDP) {
			this.IdentityProviders = new ObservableCollection<State.IdentityProvider>(IDP);
		}

	}
}
