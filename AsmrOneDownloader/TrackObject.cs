using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsmrOneDownloader {
	internal record TrackObject(string Type, string Title, string? MediaDownloadUrl, List<TrackObject>? Children);
}
