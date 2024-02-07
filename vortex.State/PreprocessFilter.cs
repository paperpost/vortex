using System;
using System.Collections.Generic;
using System.Text;

namespace vortex.State
{
	public class PreprocessFilter
	{
		public string path;
		public string parameterFormat;
		public int successReturnCode;
		public int timeout = 30000;
	}
}
