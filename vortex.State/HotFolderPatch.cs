using System;
using System.Collections.Generic;
using System.Text;

namespace vortex.State
{
	public class HotFolderPatch
	{
		public vortex.API.PublicPrintOptions settings;
		public bool ExportReady = false;
		public bool ValidateAddress = false;
		public bool AbortOnValidationFailure = true;

		public HotFolderPatch() {
		}

		public HotFolderPatch(HotFolder hf) {
			this.settings = hf.settings;
			this.ExportReady = hf.ExportReady;
			this.ValidateAddress = hf.ValidateAddress;
			this.AbortOnValidationFailure = hf.AbortOnValidationFailure;
		}
	}
}
