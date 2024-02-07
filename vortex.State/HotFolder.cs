using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace vortex.State
{
	public class HotFolder
	{
		public string Path { get; set; }
		public vortex.API.PublicPrintOptions settings;
		public bool Watch = true;							// FileSystemWatcher enabled
		public TimeSpan Poll = new TimeSpan(0, 5, 0);		// How frequently to re-scan
		public User SubmittingUser { get; set; }			// User to submit as
		public bool ExportReady = false;					// Submit as export-ready packs
		public bool ValidateAddress = false;				// Validate address on submission
		public bool AbortOnValidationFailure = true;		// If address validation fails, job will not be submitted
		public bool Framework = false;
		public bool Running = true;
		public string FileTypes = "*.pdf";
		public int DuplicateCheckPeriodMinutes = 0;
		public OnCompleteAction OnSuccess;
		public OnCompleteAction OnRejected;
		public int Submitted = 0;
		public List<PreprocessFilter> Filters = new List<PreprocessFilter>();
		public Statistics Stats { get; set; }

		public HotFolder() {
			Stats = new Statistics();
		}

		public class Statistics {
			public int Pending { get; set; }
			public int Submitted { get; set; }
		}

		public class OnCompleteAction {
			public string MoveTo;
			public bool Delete;
		}

		public string Snapshot = "";

		public bool Validate() {
			bool Valid = false;

			Valid = System.IO.Directory.Exists(Path);

			if(Valid) {
				if(this.SubmittingUser.IdentityProvider == null)
					Valid=false;

				if(Valid) {
					if(this.settings == null) {
						// Authenticate and get default print settings from server
						Valid=false;
					} else {
						// Validate the print settings
						Valid=true;
					}
				}
			}

			return Valid;
		}
	}
}
