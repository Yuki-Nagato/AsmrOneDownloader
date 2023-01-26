using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AsmrOneDownloader {

	// Sorted by level
	internal class PathComparer : IComparer<string> {
		public int Compare(string? x, string? y) {
			if (x == null && y == null) {
				return 0;
			}
			if (x == null) {
				return -1;
			}
			if (y == null) {
				return 1;
			}
			string[] xs = x.Split('/'), ys = y.Split('/');
			if (xs.Length != ys.Length) {
				return xs.Length.CompareTo(ys.Length);
			}
			for (int i = 0; i < xs.Length && i < ys.Length; i++) {
				if (xs[i] != ys[i]) {
					return xs[i].CompareTo(ys[i]);
				}
			}
			return 0;
		}
	}
	internal class Downloader {
		string code;
		HttpClient client;

		public Downloader(string code) {
			this.code = code ?? throw new ArgumentNullException(nameof(code));
			client = new HttpClient(new SocketsHttpHandler() { ConnectTimeout = TimeSpan.FromSeconds(30) }) { Timeout = Timeout.InfiniteTimeSpan };
		}

		string EscapeFilename(string filename) {
			return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
		}

		void DfsTracks(List<TrackObject> tracks, string dir, SortedDictionary<string, string> result) {
			foreach (TrackObject track in tracks) {
				if (track.Type == "folder") {
					DfsTracks(track.Children ?? throw new NullReferenceException(), dir + EscapeFilename(track.Title) + "/", result);
				}
				else {
					result[dir + EscapeFilename(track.Title)] = track.MediaDownloadUrl ?? throw new NullReferenceException();
				}
			}
		}

		async Task<SortedDictionary<string, string>> GetTracks() {
			var resp = await client.GetAsync("https://api.asmr.one/api/tracks/" + code);
			string respStr = await resp.Content.ReadAsStringAsync();
			List<TrackObject> tracks = JsonConvert.DeserializeObject<List<TrackObject>>(respStr) ?? throw new NullReferenceException();
			SortedDictionary<string, string> result = new SortedDictionary<string, string>(new PathComparer());
			DfsTracks(tracks, "", result);
			return result;
		}

		async Task<bool> CheckFileIntegrityAsync(FileInfo file, string url) {
			byte[] fileBytes = File.ReadAllBytes(file.FullName);
			var resp = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
			long respLength = resp.Content.Headers.ContentLength ?? throw new NullReferenceException();
			if (fileBytes.LongLength != respLength) {
				return false;
			}
			if (resp.Headers.ETag != null) {
				string etag = resp.Headers.ETag.Tag[(resp.Headers.ETag.Tag.IndexOf('"') + 1)..resp.Headers.ETag.Tag.LastIndexOf('"')];
				byte[] etagBytes = Convert.FromHexString(etag);
				using MD5 md5Obj = MD5.Create();
				byte[] md5Hash = md5Obj.ComputeHash(fileBytes);
				if (!etagBytes.SequenceEqual(md5Hash)) {
					return false;
				}
			}
			return true;
		}

		bool CheckFileIntegrity(byte[] fileBytes, HttpResponseMessage resp) {
			long respLength = resp.Content.Headers.ContentLength ?? throw new NullReferenceException();
			if (fileBytes.LongLength != respLength) {
				return false;
			}
			if (resp.Headers.ETag != null) {
				string etag = resp.Headers.ETag.Tag[(resp.Headers.ETag.Tag.IndexOf('"') + 1)..resp.Headers.ETag.Tag.LastIndexOf('"')];
				byte[] etagBytes = Convert.FromHexString(etag);
				using MD5 md5Obj = MD5.Create();
				byte[] md5Hash = md5Obj.ComputeHash(fileBytes);
				if (!etagBytes.SequenceEqual(md5Hash)) {
					return false;
				}
			}
			return true;
		}


		public async Task UpdateToDirectory(DirectoryInfo baseDir) {
			SortedDictionary<string, string> tracks = await GetTracks();
			Console.WriteLine("There are {0} files to download.", tracks.Count);
			int i = 0;
			foreach (var (path, url) in tracks) {
				++i;
				while (true) {
					try {
						Console.WriteLine("[{0}/{1}] {2}", i, tracks.Count, path);
						FileInfo file = new FileInfo(Path.Combine(baseDir.FullName, path));
						if (file.Exists) {
							if (await CheckFileIntegrityAsync(file, url)) {
								Console.WriteLine("Already downloaded and checked.");
								break;
							}
						}
						var resp = await client.GetAsync(url);
						byte[] content = await resp.Content.ReadAsByteArrayAsync();
						if (!CheckFileIntegrity(content, resp)) {
							throw new Exception("Downloaded file is corrupted.");
						}
						if (file.Directory != null) {
							file.Directory.Create();
						}
						File.WriteAllBytes(file.FullName, content);
						break;
					}
					catch (Exception ex) {
						Console.WriteLine(ex);
						Console.WriteLine("Retry after 10 seconds.");
						Thread.Sleep(10 * 1000);
					}
				}
			}
		}
	}
}
