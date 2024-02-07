using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

using CommandLine;
using CommandLine.Text;

namespace vortex.Submit
{
	class Program
	{
		static void Main(string[] args) {
			var result = Parser.Default.ParseArguments<Options>(args).MapResult(opts => SubmitDocument(opts).ConfigureAwait(false).GetAwaiter().GetResult(), _ => -1);
		}

		private static async Task<int> SubmitDocument(Options O) {
			bool Submitted = false;

			while(!Submitted) {
				using (HttpClient client = new HttpClient()) {
					vortex.API.Client c = new API.Client(client);

					c.BaseUrl = "https://api-uk.h-pod.co.uk/v3";

					try {
						Console.Write("auth...");
						API.AuthenticateResponse auth = await c.AuthenticateAsync(new API.AuthenticateRequest() {
										AccountLogin = O.accountLogin,
										EmailAddress = O.email,
										Password = O.password,
										Instance = O.instance,
									});


						client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Token);

						API.PublicPrintOptions PO = Newtonsoft.Json.JsonConvert.DeserializeObject<API.PublicPrintOptions>(System.IO.File.ReadAllText(O.optionsFile));

						PO.Name = System.IO.Path.GetFileName(O.file);

						Console.Write("init...");
						API.PublicPrintInitResponse job = await c.InitialiseSubmissionAsync(PO);

						byte[] bFile = System.IO.File.ReadAllBytes(O.file);

						// 400KB max chunk

						for(int part=0,i=0;i<bFile.Length;i+=400000,part++) {
							int size = 400000;
							if(i+size > bFile.Length)
								size = bFile.Length-i;

							byte[] bPart = new byte[size];

							Array.Copy(bFile, i, bPart, 0, size);

							Console.Write($"doc data [{i}-{i+size}]...");
							await c.AddDocumentPartAsync(new API.PublicDocumentPart() {
									JobGUID = job.JobGuid,
									FileIndex = 0,
									Filename = O.file,
									FileType = "pdf",
									PartIndex = part,
									StartByteIndex = i,
									Data = Convert.ToBase64String(bPart)
								});
						}


						API.PublicPrintCommitRequest CommitRequest = new API.PublicPrintCommitRequest() {
									JobGUID = job.JobGuid,
									ExtractAddress = O.validateAddress,
									AbortOnValidationFailure = O.abortOnValidationFailure,
									ExportReady = O.exportReady,
							};

						Console.WriteLine("submit...");
						API.PublicPrintCommitResponse result = await c.CommitSubmissionAsync(CommitRequest);

						Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(result));

						Submitted=true;
					} catch (Exception e) {
						Console.WriteLine("error - "+e.Message+" - retrying");
					}
				}
			}

			return 0;
		}
	}
}
