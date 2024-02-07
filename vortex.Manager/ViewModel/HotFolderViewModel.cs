using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Ribbon;
using System.Windows.Input;
using vortex.Manager.MVVM;
using vortex.Manager.View;

namespace vortex.Manager.ViewModel
{
	internal class HotFolderViewModel : ViewModelBase
	{
		private vortex.State.HotFolder? _hotFolder;
		private List<vortex.State.IdentityProvider> _IDPs;

		public RelayCommand AuthenticateNewUserCommand { get; set; }

		public vortex.State.HotFolder HotFolder {
			get {
				return _hotFolder;
			}
			set {
				_hotFolder = value;
				OnPropertyChanged();
			}
		}

		public List<vortex.State.IdentityProvider> IDPs {
			get {
				return _IDPs;
			}
			set {
				_IDPs = value;
				OnPropertyChanged();
			}
		}

		public string AuthenticatedUser {
			get {
				return HotFolder.SubmittingUser.Email;
			}
		}

		public string IdentityProvider {
			get {
				return HotFolder.SubmittingUser.IdentityProviderName;
			}
		}

		public HotFolderViewModel(vortex.State.HotFolder HotFolder, List<vortex.State.IdentityProvider> IDPs) {
			this.HotFolder = HotFolder;
			this.IDPs = IDPs;

			AuthenticateNewUserCommand = new RelayCommand(o => AuthenticateNewUser());
		}

		private async void AuthenticateNewUser() {
		    SystemBrowser B = new SystemBrowser();

            await HotFolder.SubmittingUser.IdentityProvider.AcquireNewTokenAsync(HotFolder.SubmittingUser, B, B.Port);

			vortex.State.User UserToValidate = HotFolder.SubmittingUser.Clone();

			UserToValidate.APIToken = "";

			GRPC.ValidateUserResponse resp = await MainWindowViewModel.client.ValidateUserAsync(new GRPC.ValidateUserRequest() { User = Newtonsoft.Json.JsonConvert.SerializeObject(UserToValidate) });

			if(resp.Success) {
				HotFolder.SubmittingUser = Newtonsoft.Json.JsonConvert.DeserializeObject<vortex.State.User>(resp.User);
				HotFolder.SubmittingUser.SetIdentityProvider(IDPs);

				OnPropertyChanged("AuthenticatedUser");
			}
		}
	}
}
