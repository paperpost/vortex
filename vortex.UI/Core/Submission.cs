using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Serilog;

namespace vortex.UI.Core
{
	public class Submission
	{
		public string DocumentPath;
		public vortex.API.PublicPrintOptions SelectedOptions;
		public vortex.UI.PublicPrintOptions PrintOptionsDialog;
		public vortex.State.Current State;
		private vortex.UI.ServerComms Server;
		UI.MVVM.View.PrintProgressView Progress = null;

		private System.Threading.Thread SubmissionThread = null;
		private bool ShutdownWhenComplete=false;
		public delegate void FinishedDelegate();

		// Class to display print options in State.ClientConfiguration
		// Select those in State.ClientConfiguration.Defaults
		public Submission(vortex.State.Current State, vortex.UI.ServerComms Server, string DocumentPath, string DocumentName, bool ShutdownWhenComplete) {
			this.DocumentPath = DocumentPath;
			this.Server = Server;
			this.State = State;
			this.ShutdownWhenComplete = ShutdownWhenComplete;

			this.SelectedOptions = new API.PrintOptions();

			// Initialise print options to defaults
			this.SelectedOptions.ClientId = "vortex";
			this.SelectedOptions.CopyTo = new List<API.PrintCopyTo>();
			this.SelectedOptions.DelayUntil = "";

			this.SelectedOptions.Delivery = new	API.PrintGUIDOption() { Guid = State.ClientConfig.DefaultPrintSettings.Delivery };
			this.SelectedOptions.Duplex = new API.PrintGUIDOption() { Guid = State.ClientConfig.DefaultPrintSettings.Duplex };
			this.SelectedOptions.Envelope = new API.PrintGUIDOption() { Guid = State.ClientConfig.DefaultPrintSettings.Envelope };
			this.SelectedOptions.GenerateKey = false;
			this.SelectedOptions.HoldPacks = false;
			this.SelectedOptions.IncludeAttachmentsForMainLetter = true;
			this.SelectedOptions.IncludeInsertsForMainLetter = true;
			this.SelectedOptions.Ink = new API.PrintGUIDOption() { Guid = State.ClientConfig.DefaultPrintSettings.Ink };
			this.SelectedOptions.Inserts = new List<API.PrintGUIDOption>();
			this.SelectedOptions.LanguageCode = "en";
			this.SelectedOptions.Name = DocumentName;
			this.SelectedOptions.PackMode = new API.PrintGUIDOption() { Guid = State.ClientConfig.DefaultPrintSettings.PackMode };
			this.SelectedOptions.Paper = new API.PrintGUIDOption() { Guid =	State.ClientConfig.DefaultPrintSettings.Paper };
			this.SelectedOptions.Passthrough = new List<API.PrintPassthrough>();
			this.SelectedOptions.SpecialHandling = "";

			if(State.ClientConfig.DefaultPrintSettings.Stationery != null)
				this.SelectedOptions.Stationery = new API.PrintGUIDOption() { Guid = State.ClientConfig.DefaultPrintSettings.Stationery };

		}


		// Needs to be a task otherwise we get issues with recreating the print options dialog the second time, as the assets
		//  it uses are created on the first call and reused on the second call, and can't be reused across threads
		public async Task StartAsync() {
			PrintOptionsDialog = new UI.PrintOptions(this);

			PrintOptionsDialog.OK += StartSubmission;
			PrintOptionsDialog.Cancel += SubmissionFinished;

			Log.Debug("Showing PrintOptionsDialog");

            PrintOptionsDialog.Show();
		}

		// This is started on a new STA thread so it needs its own message pump
		private async Task StartSubmission(Core.Submission Submission) {
			Log.Debug("Starting submission");
			// Tell the server to submit the job with the specified print settings
			GRPC.SubmitPDFResponse resp = await Server.SubmitDocument(Submission.SelectedOptions, Submission.DocumentPath, Submission..UserGUID);

			// Check we received a job ID back from the server
			if(resp.Jobid != Guid.Empty.ToString()) {
				// Start a SubmissionProgress thread which will communicate with the server to display progress updates

				if(State.Local.ShowProgress) {
					Progress = new MVVM.View.PrintProgressView();
					Progress.Finished += SubmissionFinished;
					Progress.Show();

					await Progress.TrackJobAsync(Server, resp.Jobid);
				} else {
					SubmissionFinished();
				}
			} else {
				SubmissionFailed(resp.Status);
			}
		}

		private void SubmissionFailed(string status) {
			Progress = new MVVM.View.PrintProgressView();
			Progress.Failed(status);
			Progress.Show();
			Progress.Closed += Progress_Closed;
		}

		private void Progress_Closed(object sender, EventArgs e) {
			SubmissionFinished();
		}

		private void SubmissionFinished() {
			Progress?.Hide();

			if(ShutdownWhenComplete)
			    System.Windows.Application.Current.Shutdown(0);
		}
	}
}
