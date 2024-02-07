using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

using Serilog;

namespace vortex.UI.MVVM.ViewModel
{
	class DocumentViewModel : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private Core.Submission Submission = null;

		public List<BitmapImage> Thumbnails = new List<BitmapImage>();
		private BitmapImage CurrentPageImage = null;
		private int CurrentPageIndex = 0;
		private Core.Preflight Preflight = null;

		public DocumentViewModel() {
		}

		public void Populate(vortex.UI.Core.Submission S) {
			this.Submission = S;

			Preflight = new Core.Preflight(S.State);

			GenerateThumbnails();
		}

		public void GenerateThumbnails() {
			Thumbnails = (new PrintPreview.Offline()).Generate(this.Submission.DocumentPath);
			CurrentPageImage = Thumbnails[0];
			bool PreflightSuccess = false;
			Task.Run(async () => {
				//API.PreflightRequest req = await Preflight.InitAsync(Submission).ConfigureAwait(false);
				//API.TaskPreflight tpf = await Preflight.RunAsync(req).ConfigureAwait(false);

				//if(tpf.Thumbnails.Count > 0) {
					
					// Download each thumbnail image and load into this.Thumbnails
					//this.Thumbnails = tpf.Thumbnails;

				//	PreflightSuccess = true;
				//}
			}).ContinueWith((t) => {
				if(t.IsFaulted) {
					Log.Error(t.Exception, "Preflight error");
					PreflightFailed=true;
				} else {
					if(PreflightSuccess) {
						// Update the visible image with the latest thumbnail;
						CurrentPage = this.Thumbnails[CurrentPageIndex];
					} else {
						PreflightFailed=true;
					}
				}
			});
		}

		private bool _PreflightFailed = false;

		public bool PreflightFailed {
			get {
				return _PreflightFailed;
			}

			set {
				_PreflightFailed = value;
				OnPropertyChanged();
			}
		}

		public BitmapImage CurrentPage {
			get {
				return CurrentPageImage;
			}
			set {
				CurrentPageImage = value;
				OnPropertyChanged();
			}
		}

		public System.Collections.Generic.ICollection<vortex.API.ClientConfigurationStationeryType> StationeryOptions {
			get {
				API.ClientConfigurationStationeryType NoStationery = new API.ClientConfigurationStationeryType() { Guid = Guid.Empty.ToString(), Name = "None" };
				List<API.ClientConfigurationStationeryType> Sorted = new List<API.ClientConfigurationStationeryType>();

				Sorted.Add(NoStationery);
				Sorted.AddRange(this.Submission.State.ClientConfig.Stationery);

				return Sorted;
			}
		}

		public System.Collections.Generic.ICollection<vortex.API.ClientConfigurationSignature> SignatureOptions {
			get {
				API.ClientConfigurationSignature NoSignature = new API.ClientConfigurationSignature() { BlobGuid = Guid.Empty.ToString(), Guid = Guid.Empty.ToString(), Name = "None" };
				API.ClientConfigurationSignature MySignature = new API.ClientConfigurationSignature() { BlobGuid = Guid.Empty.ToString(), Guid = Guid.Empty.ToString(), Name = "My Signature" };

				List<API.ClientConfigurationSignature> Sorted = new List<API.ClientConfigurationSignature>();
				Sorted.Add(NoSignature);
				Sorted.Add(MySignature);
				Sorted.AddRange(this.Submission.State.ClientConfig.Signatures);

				return Sorted;
			}
		}

		public string Stationery {
			get {
				if(this.Submission.SelectedOptions.Stationery != null)
					return this.Submission.SelectedOptions.Stationery.Guid;
				else
					return Guid.Empty.ToString();
			}
			set {
				API.ClientConfigurationStationeryType Selected = this.Submission.State.ClientConfig.Stationery.First((s) => s.Guid == value);

				if(Selected != null) {
					Log.Debug("Stationery {0} / {1} selected", Selected.Guid, Selected.Name);
					this.Submission.SelectedOptions.Stationery = new API.PrintGUIDOption() { Guid = Selected.Guid, Name = Selected.Name };
				} else
					Log.Error("Selected stationery {0} not found in client config", value);
			}
		}

		public string Signature {
			get {
				if(string.IsNullOrEmpty(this.Submission.SelectedOptions.Signature))
					return "None";
				else
					return this.Submission.SelectedOptions.Signature;
			}

			set {
				Log.Debug("Signature set to "+value);
				this.Submission.SelectedOptions.Signature = value;
			}
		}

		protected void OnPropertyChanged([CallerMemberName] string name = null) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
	}
}
