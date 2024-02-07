	using CommandLine;

namespace vortex.Submit {
	class Options {
		[Option("instance", Required=true, HelpText="The DNS name for your H-POD instance")]
		public string instance { get; set; }

		[Option("path", Required=true, HelpText="Path to the file to submit")]
		public string file { get; set; }

		[Option("printoptions", Required=true, HelpText ="Path to the .json file containing the print options")]
		public string optionsFile { get; set; }

		[Option("account", Required =true, HelpText="Account login")]
		public string accountLogin { get; set; }

		[Option("email", Required=true, HelpText="User login")]
		public string email { get; set; }

		[Option("password", Required=true, HelpText ="Password")]
		public string password { get; set; }

		[Option("validate", Required=false, Default = false, HelpText = "Run address validation on submission")]
		public bool validateAddress { get; set; }

		[Option("invalidAbort", Required = false, Default = false, HelpText = "Abort submission if address validation failed")]
		public bool abortOnValidationFailure { get; set; }

		[Option("exportReady", Required = false, Default = false, HelpText = "Pack is export-ready and requires no further processing")]
		public bool exportReady { get; set; }
	}
}