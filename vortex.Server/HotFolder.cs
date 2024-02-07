using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using Serilog;

using Newtonsoft.Json;

namespace vortex.Server
{
	class HotFolder : vortex.State.HotFolder
	{
		[JsonIgnore] public APIConnectionPool ConnectionPool = null;
		[JsonIgnore] List<Submitter> Submitters = new List<Submitter>();

		private List<string> FileTypesList = new List<string>();
		[JsonIgnore] private List<File> Queue = new List<File>();

		[JsonIgnore] FileSystemWatcher fsw = null;

		public class File {
			public string Path;
			public DateTime DateCreated;
			public bool Pending = true;
			public bool Completed = false;
			public bool IsFramework = false;
			public string FrameworkName = "";
		}

		public void Patch(vortex.State.HotFolderPatch p) {
			Log.Debug("Patching hotfolder {0} with {1}", this.Path, Newtonsoft.Json.JsonConvert.SerializeObject(p));
			this.settings = p.settings;
			this.ExportReady = p.ExportReady;
			this.ValidateAddress = p.ValidateAddress;
		}
		
		public void SetUser(vortex.State.User u) {
			this.SubmittingUser = u;
		}

		public HotFolder(vortex.State.HotFolder hf) {
			this.Path = hf.Path;
			this.settings = hf.settings;
			this.Watch = hf.Watch;
			this.Poll = hf.Poll;
			this.ExportReady = hf.ExportReady;
			this.SubmittingUser = hf.SubmittingUser;
			this.ValidateAddress = hf.ValidateAddress;
			this.FileTypes = hf.FileTypes;
			this.AbortOnValidationFailure = hf.AbortOnValidationFailure;
			this.OnSuccess = hf.OnSuccess;
			this.OnRejected = hf.OnRejected;

			FileTypesList.AddRange(this.FileTypes.Split(';', StringSplitOptions.RemoveEmptyEntries));
		}

		public void Start() {
			if(!System.IO.Directory.Exists(this.Path)) {
				Log.Error("Hot folder {0} does not exist - aborting hotfolder", this.Path);
				return;
			}

			if(Watch) {
				Log.Debug("Watching for {0} in directory {1}", this.FileTypes, this.Path);

				fsw = new FileSystemWatcher(this.Path);

				fsw.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
				fsw.Filter = "*.*";

				fsw.Created += Enqueue;
				fsw.Changed += Enqueue;

				fsw.EnableRaisingEvents = true;
			}

			double Delay = 1000;

			DateTime dtLastScan = DateTime.MinValue;

			Log.Verbose("Starting hotfolder task");

			Task.Run(async () => {
				while(Running) {
					// Scan the folder
					if((DateTime.UtcNow - dtLastScan).TotalMilliseconds > Poll.TotalMilliseconds) {
						Scan();

						dtLastScan = DateTime.UtcNow;
					}

					UpdateSnapshot();

					for(int i=0;i<Queue.Count;i++) {
						if(Queue[i].Pending) {
							if(this.settings==null) {
								Log.Information("Hotfolder {0} has no print settings, unable to submit currently", this.Path);
								Delay += Delay;
								if(Delay > 60000) Delay = 60000;
							} else {
								Log.Verbose("Attempting submission of "+Queue[i].Path);
								Queue[i].Pending = false;

								Submitter S = new Submitter();

								try {
									S.HotFolder = this;
									S.HotFolderFile = Queue[i];
									S.PDFPath = Queue[i].Path;
									S.Options = this.settings;
									S.Options.Name = System.IO.Path.GetFileName(S.PDFPath);
									S.Options.ClientId = "vortex-hotfolder";
									S.Options.LanguageCode = "en";
									S.SubmittingUser = this.SubmittingUser;
									S.ExportReady = this.ExportReady;
									S.ValidateAddress = this.ValidateAddress;
									S.AbortOnValidationFailure = this.AbortOnValidationFailure;
									S.Connection = await ConnectionPool.GetFreeConnectionAsync();

									await S.SubmitAsync().ContinueWith((SR) => {
										S.HotFolderFile.Pending=false;
										switch(SR.Result) {
											case Submitter.SubmitResult.OK:
												Log.Information("Submit {0} -> OK", System.IO.Path.GetFileName(S.HotFolderFile.Path));
												Printed(S.HotFolderFile.Path, S.JobGUID);

												Delay = 1000;
												Submitted++;
												Stats.Submitted++;
												break;
											case Submitter.SubmitResult.FailedRetry:
											case Submitter.SubmitResult.FailedUnknownError:
											case Submitter.SubmitResult.NotSubmitted:
												Delay += Delay;
												if(Delay > 60000) Delay = 60000;

												S.HotFolderFile.Pending=true;	// Mark as pending again, and to stop it being marked as completed below

												Log.Information("Submit {0} -> {1}, interval is now {2}ms", System.IO.Path.GetFileName(S.HotFolderFile.Path), SR.Result, Delay);
												break;
											case Submitter.SubmitResult.FileDoesNotExist:
												Log.Information("Submit {0} -> No longer exists in hot folder", System.IO.Path.GetFileName(S.HotFolderFile.Path));

												break;
											case Submitter.SubmitResult.Rejected:
												Log.Information("Submit {0} -> Rejected", System.IO.Path.GetFileName(S.HotFolderFile.Path));
												Rejected(S.HotFolderFile.Path);
												break;
										}

										if(!S.HotFolderFile.Pending) {
											Stats.Pending = Queue.Count(q => q.Pending==true);
											S.HotFolderFile.Completed=true;
										}

										S.Connection?.Release();
									});
								} catch (Exception e) {
									Log.Error(e, "Unhandled error attempting submission");
									Delay += Delay;
									if(Delay > 60000) Delay = 60000;
								} finally {
								}
							}
						}
					}

					UpdateSnapshot();

					ClearExpiredQueueItems();

					await Task.Delay((int)Delay);
				}
			});
		}

		private void UpdateSnapshot() {
			this.Snapshot = string.Format("{0} / {1} pending, {2} submitted since last report", this.Path, this.Queue.Count((q) => q.Pending==true), this.Submitted);
		}

		private void ClearExpiredQueueItems() {
			for(int i=0;i<Queue.Count;i++) {
				if(!Queue[i].Pending && Queue[i].Completed) {
					if((DateTime.Now - Queue[i].DateCreated).TotalMinutes >= this.DuplicateCheckPeriodMinutes) {
						Log.Verbose("Removing expired item {0} from queue", Queue[i].Path); 
						Queue.RemoveAt(i--);
					}
				}
			}
		}

		public void Stop() {
			if(fsw != null)
				fsw.EnableRaisingEvents=false;

			Queue.Clear();

			Running=false;
		}

		private bool ValidFileType(string Path) {
			string ext = "*"+System.IO.Path.GetExtension(Path);

			return FileTypesList.Contains(ext);
		}

		private void Enqueue(object sender, FileSystemEventArgs e) {
			AddToQueue(e.FullPath);
		}

		private bool Preprocess(HotFolder.File HotFolderFile, vortex.State.PreprocessFilter filter) {
			System.Diagnostics.Process p = new System.Diagnostics.Process();

			p.StartInfo.FileName = filter.path;
			p.StartInfo.Arguments = filter.parameterFormat.Replace("[InputPath]", HotFolderFile.Path).Replace("[OutputPath]", HotFolderFile.Path);
			p.StartInfo.RedirectStandardInput = true;
			p.StartInfo.RedirectStandardOutput = true;
			p.Start();

			string stdout = p.StandardOutput.ReadToEnd();
			string stderr = p.StandardError.ReadToEnd();

			p.WaitForExit(filter.timeout);

			if(p.HasExited) {
				if(p.ExitCode==filter.successReturnCode) {
					return true;
				} else {
					Log.Warning("Filter failed for {0}", HotFolderFile.Path);
					Log.Warning("  stdout: "+stdout);
					Log.Warning("  stderr: "+stderr);
					return false;
				}
			} else {
				Log.Warning("Filter timed out after {0}ms", filter.timeout);
				p.Kill();
				return false;
			}
		}

		private void AddToQueue(string Path) {
			if(ValidFileType(Path)) {
				if(System.IO.File.Exists(Path)) {
					System.IO.FileInfo FI = new FileInfo(Path);

					HotFolder.File hff = new HotFolder.File() { Path = Path, DateCreated = FI.CreationTime };

					bool FiltersPassed = true;

					// Run the file through any configured filters
					foreach(vortex.State.PreprocessFilter f in this.Filters) {
						if(!this.Preprocess(hff, f))
							FiltersPassed = false;
					}

					if(FiltersPassed) {
						// If the detected file has the same name and same timestamp as a pending queue item, ignore it
						if(!Queue.Any(F => (F.Path == Path) && (F.DateCreated == FI.CreationTime))) {
							Log.Debug("Adding file "+Path);
							Queue.Add(hff);
						}
					} else {
						Rejected(Path);
					}
				}
			}
		}

		private void Printed(string Path, Guid JobGUID) {
			if(this.OnSuccess?.MoveTo != null) {
				string TargetFolder = "";

				if(System.IO.Path.IsPathFullyQualified(this.OnSuccess.MoveTo))
					TargetFolder = this.OnSuccess.MoveTo;
				else
					TargetFolder = System.IO.Path.Combine(this.Path, this.OnSuccess.MoveTo);

				if(!System.IO.Directory.Exists(TargetFolder))
					System.IO.Directory.CreateDirectory(TargetFolder);


				string TargetFile = System.IO.Path.Combine(TargetFolder, string.Format("{0}-{1}.{2}", System.IO.Path.GetFileName(Path), JobGUID, System.IO.Path.GetExtension(Path)));

				Log.Information("Moving file to "+TargetFile);

				System.IO.File.Move(Path, TargetFile);
			} else {
				Log.Information("Deleting "+Path);
				System.IO.File.Delete(Path);
			}
		}

		private void Rejected(string Path) {
			if(this.OnRejected?.MoveTo != null) {
				if(!System.IO.Directory.Exists(System.IO.Path.Combine(this.Path, this.OnRejected.MoveTo)))
					System.IO.Directory.CreateDirectory(System.IO.Path.Combine(this.Path, this.OnRejected.MoveTo));

				string RejectedPath = System.IO.Path.Combine(this.Path, this.OnRejected.MoveTo, System.IO.Path.GetFileName(Path));
				int i=1;

				if(System.IO.File.Exists(RejectedPath)) {
					do {
						RejectedPath = System.IO.Path.Combine(this.Path, this.OnRejected.MoveTo, string.Format("{0}-{1}.{2}", System.IO.Path.GetFileName(Path), i++, System.IO.Path.GetExtension(Path)));
					} while (System.IO.File.Exists(RejectedPath));
				}

				System.IO.File.Move(Path, System.IO.Path.Combine(this.Path, this.OnRejected.MoveTo, System.IO.Path.GetFileName(RejectedPath)));
			} else {
				Log.Information("Deleting "+Path);
				System.IO.File.Delete(Path);
			}
		}

		private void Scan() {
			// Scan for any files we've not learned about via FileSystemWatcher and add them to Queue
			DateTime dtStart = DateTime.UtcNow;

			// GetFiles can only accept a single wildcard

			string[] Types = this.FileTypes.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);

			string[] Files = System.IO.Directory.GetFiles(this.Path);

			foreach(string File in Files) {
				AddToQueue(File);
			}

			DateTime dtEnd = DateTime.UtcNow;

			Log.Debug("Scanned {0} in {1}ms", this.Path, (dtEnd - dtStart).TotalMilliseconds);
		}
	}
}
