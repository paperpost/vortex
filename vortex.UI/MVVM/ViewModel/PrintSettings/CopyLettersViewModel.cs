using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vortex.UI.MVVM.ViewModel
{
	class CopyLettersViewModel
	{
		Core.Submission Submission = null;

		public void Populate(vortex.UI.Core.Submission S) {
			this.Submission = S;
		}
	}
}
