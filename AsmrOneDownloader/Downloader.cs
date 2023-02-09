using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ShellProgressBar;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
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
	public class Downloader {
		HttpClient client;
		private string? username, password, token;

		public Downloader() {
			client = new HttpClient(new SocketsHttpHandler() { ConnectTimeout = TimeSpan.FromSeconds(30) }) { Timeout = Timeout.InfiniteTimeSpan };
		}

		public async Task LoginAsync(string username, string password) {
			var resp = await client.PostAsync("https://api.asmr.one/api/auth/me", new StringContent(JsonConvert.SerializeObject(new { name = username, password = password }, Formatting.None), Encoding.UTF8, "application/json"));
			resp.EnsureSuccessStatusCode();
			string respStr = await resp.Content.ReadAsStringAsync();
			JObject respObj = JObject.Parse(respStr);
			string token = respObj["token"]?.Value<string>() ?? throw new ArgumentNullException(nameof(token));
			this.username = username;
			this.password = password;
			this.token = token;
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		}

		public async Task LogoutAsync() {
			var resp = await client.GetAsync("https://api.asmr.one/api/auth/reg", HttpCompletionOption.ResponseContentRead);
			resp.EnsureSuccessStatusCode();
			client.DefaultRequestHeaders.Authorization = null;
			this.username = null;
			this.password = null;
			this.token = null;
		}

		public static string EscapeFilename(string filename) {
			return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
		}

		void DfsTracks(List<TrackObject> tracks, string dir, SortedDictionary<string, string> result) {
			foreach (TrackObject track in tracks) {
				if (track.Type == "folder") {
					DfsTracks(track.Children ?? throw new ArgumentNullException(nameof(track.Children)), dir + EscapeFilename(track.Title) + "/", result);
				}
				else {
					result[dir + EscapeFilename(track.Title)] = track.MediaDownloadUrl ?? throw new ArgumentNullException(nameof(track.MediaDownloadUrl));
				}
			}
		}

		public async Task<WorkObject> GetWorkAsync(string code) {
			var resp = await client.GetAsync("https://api.asmr.one/api/work/" + code);
			string respStr = await resp.Content.ReadAsStringAsync();
			WorkObject work = JsonConvert.DeserializeObject<WorkObject>(respStr) ?? throw new ArgumentNullException(nameof(work));
			return work;
		}

		async Task<SortedDictionary<string, string>> GetTracksAsync(string code) {
			var resp = await client.GetAsync("https://api.asmr.one/api/tracks/" + code);
			string respStr = await resp.Content.ReadAsStringAsync();
			List<TrackObject> tracks = JsonConvert.DeserializeObject<List<TrackObject>>(respStr) ?? throw new ArgumentNullException(nameof(tracks));
			SortedDictionary<string, string> result = new SortedDictionary<string, string>(new PathComparer());
			DfsTracks(tracks, "", result);
			return result;
		}

		async Task<Tuple<long, string, HttpResponseMessage>> FetchContentLengthAndEtagAsync(string url, bool enforceGet) {
			long? contentLength = null;
			string? etag = null;

			if (enforceGet) {
				// GET and HEAD
				var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
				if (resp.Content.Headers.ContentLength != null) {
					contentLength = resp.Content.Headers.ContentLength;
				}
				if (resp.Headers.ETag != null) {
					etag = resp.Headers.ETag.Tag;
					etag = etag.Split('"')[1];
				}
				if (contentLength != null && etag != null) {
					return Tuple.Create((long)contentLength, etag, resp);
				}
				var headResp = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url), HttpCompletionOption.ResponseHeadersRead);
				if (headResp.Content.Headers.ContentLength != null) {
					contentLength = headResp.Content.Headers.ContentLength;
				}
				if (headResp.Headers.ETag != null) {
					etag = headResp.Headers.ETag.Tag;
					etag = etag.Split('"')[1];
				}
				return Tuple.Create(contentLength ?? throw new ArgumentNullException(nameof(contentLength)), etag ?? throw new ArgumentNullException(nameof(etag)), resp);
			}
			else {
				// HEAD and GET
				var headResp = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url), HttpCompletionOption.ResponseHeadersRead);
				if (headResp.Content.Headers.ContentLength != null) {
					contentLength = headResp.Content.Headers.ContentLength;
				}
				if (headResp.Headers.ETag != null) {
					etag = headResp.Headers.ETag.Tag;
					etag = etag.Split('"')[1];
				}
				if (contentLength != null && etag != null) {
					return Tuple.Create((long)contentLength, etag, headResp);
				}
				var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
				if (resp.Content.Headers.ContentLength != null) {
					contentLength = resp.Content.Headers.ContentLength;
				}
				if (resp.Headers.ETag != null) {
					etag = resp.Headers.ETag.Tag;
					etag = etag.Split('"')[1];
				}
				return Tuple.Create(contentLength ?? throw new ArgumentNullException(nameof(contentLength)), etag ?? throw new ArgumentNullException(nameof(etag)), resp);
			}
		}

		async Task<bool> CheckFileIntegrityAsync(FileInfo file, string url) {
			byte[] fileBytes = File.ReadAllBytes(file.FullName);
			var (respLen, respEtag, resp) = await FetchContentLengthAndEtagAsync(url, false);
			return CheckFileIntegrity(fileBytes, respLen, respEtag);
		}

		static bool CheckFileIntegrity(byte[] fileBytes, long respLen, string respEtag) {
			if (fileBytes.LongLength != respLen) {
				return false;
			}
			byte[] etagBytes = Convert.FromHexString(respEtag);
			using MD5 md5Obj = MD5.Create();
			byte[] md5Hash = md5Obj.ComputeHash(fileBytes);
			return etagBytes.SequenceEqual(md5Hash);
		}

		static string NumberToUnit(double num) {
			string[] prefixes = { "", "K", "M", "G", "T" };
			int i;
			for (i = 0; i < prefixes.Length - 1; i++) {
				if (num < 1024) {
					break;
				}
				num /= 1024;
			}
			string numStr = num.ToString("F2");
			while (numStr.EndsWith('.') || numStr.Contains('.') && numStr.Length > 4) {
				numStr = numStr[..^1];
			}
			return numStr + " " + prefixes[i];
		}

		public async Task DownloadToDirectoryAsync(string code, DirectoryInfo baseDir) {
			SortedDictionary<string, string> tracks;
			while (true) {
				try {
					tracks = await GetTracksAsync(code);
					break;
				}
				catch (Exception ex) {
					Console.WriteLine(ex);
					Console.WriteLine("Retry after 10 seconds.");
					await Task.Delay(10 * 1000);
				}
			}
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
						var (respLen, respEtag, resp) = await FetchContentLengthAndEtagAsync(url, true);
						byte[] content = new byte[respLen];
						//Console.WriteLine();
						using (ProgressBar pbar = new ProgressBar(content.Length, $"Downloading", new ProgressBarOptions() { ForegroundColorDone = ConsoleColor.Gray, ShowEstimatedDuration = true })) {
							Stream stream = resp.Content.ReadAsStream();
							int len, p = 0;
							Stopwatch stopwatch = new Stopwatch();
							stopwatch.Start();
							while ((len = stream.Read(content, p, content.Length - p)) > 0) {
								p += len;
								long tick = stopwatch.ElapsedTicks;
								double bpersec = (double)(p * Stopwatch.Frequency) / tick;
								pbar.Tick(p, TimeSpan.FromSeconds((content.Length - p) / bpersec), $"{NumberToUnit(bpersec)}B/s");
							}
						}
						if (!CheckFileIntegrity(content, respLen, respEtag)) {
							throw new Exception("Downloaded file is corrupted.");
						}
						file.Directory?.Create();
						File.WriteAllBytes(file.FullName, content);
						break;
					}
					catch (Exception ex) {
						Console.WriteLine(ex);
						Console.WriteLine("Retry after 10 seconds.");
						await Task.Delay(10 * 1000);
					}
				}
			}
		}
	}
}
