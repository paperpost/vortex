using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using vortex.Manager.MVVM;

namespace vortex.Manager.ViewModel
{
	internal class HotFoldersViewModel : ViewModelBase
	{
		public ObservableCollection<vortex.State.HotFolder> HotFolders { get; set; }

		public HotFoldersViewModel(List<vortex.State.HotFolder> HotFolders) {
			this.HotFolders = new ObservableCollection<State.HotFolder>(HotFolders);
		}

		public void UpdateStats(List<vortex.State.HotFolder> HotFolders) {
			bool Changed = false;

			foreach(vortex.State.HotFolder hf in this.HotFolders) {
				foreach(vortex.State.HotFolder hfLatest in HotFolders) {
					if(hf.Path==hfLatest.Path) {
						if(hf.Stats.Pending != hfLatest.Stats.Pending) {
							Changed = true;
							hf.Stats.Pending = hfLatest.Stats.Pending;
						}

						if(hf.Stats.Submitted != hfLatest.Stats.Submitted) {
							Changed=true;
							hf.Stats.Pending = hfLatest.Stats.Pending;
						}

						break;
					}
				}
			}

			if(Changed)
				OnPropertyChanged();
		}
	}
}
