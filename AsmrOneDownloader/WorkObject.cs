using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsmrOneDownloader {
	public record WorkObject(long Id, string Title, bool Nsfw, long DlCount);
}
