using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IdentityModel.OidcClient;

using Serilog;

namespace vortex.Server
{
	class Submitter
	{
		public vortex.State.HotFolder HotFolder = null;
		public HotFolder.File HotFolderFile = null;
		public string PDFPath;
		public vortex.API.PublicPrintOptions Options;
		public APIConnection Connection;
		public double Percent;
		public DateTime FinishedAt = DateTime.MinValue;
		public vortex.State.User SubmittingUser;
		public bool Success=false;
		public string Status = "";
		public Guid JobGUID = Guid.Empty;
		public bool ExportReady = false;
		public bool ValidateAddress = false;
		public bool AbortOnValidationFailure = true;

		const int bytesPerPart = 100000;

		public delegate void FinishedDelegate(Submitter S);
		public event FinishedDelegate Finished;

		// Running a task synchronously on its own thread should be fine I think?
		public SubmitResult Submit() {
			SubmitResult sr = SubmitResult.NotSubmitted;

			sr = SubmitAsync().ConfigureAwait(false).GetAwaiter().GetResult();

			return sr;
		}

		public void SubmitThread() {
			Task.Run(async () => await SubmitAsync()).Wait();
		}

		public enum SubmitResult {
			OK,
			FailedRetry,
			FailedUnknownError,
			FileDoesNotExist,
			NotSubmitted,
			Rejected
		}

		public async Task<SubmitResult> SubmitAsync() {
			vortex.API.Client c = Connection.CreateClient();

			SubmitResult SR = SubmitResult.NotSubmitted;

			if(await SubmittingUser.EnsureAuthenticated(c, Connection.Instance)) {
				Log.Verbose("Submission of {0} on behalf of {1}", PDFPath, SubmittingUser.Email);

				Connection.HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", SubmittingUser.APIToken);

				Status = "Initialising";

				if(System.IO.File.Exists(PDFPath)) {
					if(this.HotFolder != null) {
						if(this.HotFolder.Framework)
							SR = await SubmitFrameworkAsync(c);
					}

					if(SR == SubmitResult.NotSubmitted)
						SR = await SubmitPrintAsync(c);

				} else {
					SR = SubmitResult.FileDoesNotExist;
				}

				this.FinishedAt=DateTime.UtcNow;

				if(SR == SubmitResult.OK)
					if(Finished != null) Finished(this);
			} else {
				Log.Debug("Failed to acquire valid API token");
				SR = SubmitResult.FailedRetry;
			}

			return SR;
		}

		public async Task<SubmitResult> SubmitFrameworkAsync(vortex.API.Client c) {
			SubmitResult SR = SubmitResult.NotSubmitted;



			return SR;
		}

		public async Task<SubmitResult> SubmitPrintAsync(vortex.API.Client c) {
			SubmitResult SR = SubmitResult.NotSubmitted;

			try {
				API.PublicPrintInitResponse R = await c.InitialiseSubmissionAsync(Options);

				System.IO.FileInfo FI = new System.IO.FileInfo(PDFPath);

				Percent = 10;

				int Sent = 0;
				int PartIndex=0;

				this.JobGUID = R.JobGuid;

				// 10%-90% will be the file transfers
				double ProgressPerPart = (double)80 / (double)Math.Ceiling((decimal)FI.Length / (decimal)bytesPerPart);

				System.IO.FileStream fs = new System.IO.FileStream(PDFPath, System.IO.FileMode.Open);
				System.IO.StreamReader sr = new System.IO.StreamReader(fs);

				Status = string.Format("Sending {0}", Options.Name);

				while(Sent < FI.Length) {
					byte[] b;

					if(Sent + bytesPerPart > FI.Length)
						b = new byte[FI.Length - Sent];
					else
						b = new byte[bytesPerPart];

					fs.Read(b, 0, b.Length);

					string b64 = Convert.ToBase64String(b);

					await c.AddDocumentPartAsync(new API.PublicDocumentPart() {
							JobGUID = R.JobGuid,
							FileIndex = 0,
							FileType = "pdf",
							PartIndex = PartIndex++,
							Filename = System.IO.Path.GetFileName(PDFPath),
							StartByteIndex=Sent, Data = b64 }).ConfigureAwait(false);

					Percent += ProgressPerPart;
					Sent += b.Length;
				}

				sr.Close();
				fs.Close();

				Status = "Committing";

				API.PublicPrintCommitResponse cr = await c.CommitSubmissionAsync(new API.PublicPrintCommitRequest() { JobGUID = R.JobGuid, ExportReady = this.ExportReady, ExtractAddress = this.ValidateAddress, AbortOnValidationFailure = this.AbortOnValidationFailure });

				Percent = 100;

				if(this.ExportReady) {
					switch(cr.AddressValidationResult) {
						case API.PublicPrintAddressValidationResult.Valid:
							// All OK
							break;
						default:
							// If we want the packs to be export ready then address validation must occur
							// If this didn't happen, the job should be rejected
							SR = SubmitResult.Rejected;
							break;
					}
				}

				Log.Verbose("Submission complete");

				if(SR == SubmitResult.NotSubmitted)
					SR = SubmitResult.OK;
			} catch (vortex.API.ApiException e) {
				Log.Error(e, "Submission failed");
				SR = SubmitResult.FailedUnknownError;
			} catch (Exception e) {
				Log.Error(e, "Unknown error");
			}

			return SR;
		}
	}
}
