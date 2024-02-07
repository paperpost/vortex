using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vortex.UI.Core;
using System.Windows.Threading;

namespace vortex.UI.MVVM.ViewModel
{
	class PrintOptionsViewModel : ObservableObject
	{
		public RelayCommand DocumentViewCommand { get; set; }
		public RelayCommand DeliveryViewCommand { get; set; }
		public RelayCommand InsertsAttachmentsViewCommand { get; set; }
		public RelayCommand CopyLettersViewCommand { get; set; }

		public DocumentViewModel DocumentVM  { get; set; }
		public DeliveryViewModel DeliveryVM { get; set; }
		public InsertsAttachmentsViewModel InsertsAttachmentsVM { get; set; }
		public CopyLettersViewModel CopyLettersVM { get; set; }

		private object _currentView;

		public Core.Submission Submission = null;

		public object CurrentView {
			get {
				return _currentView;
			}
			set {
				_currentView=value;

				OnPropertyChanged();
			}
		}

		public PrintOptionsViewModel() {
			DocumentVM = new DocumentViewModel();
			DeliveryVM = new DeliveryViewModel();
			InsertsAttachmentsVM = new InsertsAttachmentsViewModel();
			CopyLettersVM = new CopyLettersViewModel();

			CurrentView=DocumentVM;

			DocumentViewCommand = new RelayCommand(o => {
				CurrentView  = DocumentVM;
			});

			DeliveryViewCommand = new RelayCommand(o => {
				CurrentView = DeliveryVM;
			});

			InsertsAttachmentsViewCommand = new RelayCommand(o => {
				CurrentView = InsertsAttachmentsVM;
			});

			CopyLettersViewCommand = new RelayCommand(o => {
				CurrentView = CopyLettersVM;
			});
		}

		public void Scrape() {
			// Update this.Submission with the latest
		}

		public void Populate(Submission S) {
			this.Submission = S;

			DocumentVM.Populate(S);
			DeliveryVM.Populate(S);
			InsertsAttachmentsVM.Populate(S);
			CopyLettersVM.Populate(S);
		}
	}
}
