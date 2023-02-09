using Newtonsoft.Json;
using Polly;
using System.Text;

namespace AsmrOneDownloader {
	internal class Program {
		static async Task<int> Main(string[] args) {
			Downloader downloader = new Downloader();

			UserObject user;
			string userFile = "asmr_one_user.json";
			if (!File.Exists(userFile)) {
				Console.Write("Input username (that will be saved in current directory {0}): ", userFile);
				string username = Console.ReadLine() ?? throw new ArgumentNullException(nameof(username));
				Console.Write("Input password (that will be saved in current directory {0}): ", userFile);
				string password = Console.ReadLine() ?? throw new ArgumentNullException(nameof(password));
				user = new UserObject(username, password);
			}
			else {
				string jsonStr = File.ReadAllText(userFile);
				user = JsonConvert.DeserializeObject<UserObject>(jsonStr) ?? throw new ArgumentNullException(nameof(user));
			}
			await downloader.LoginAsync(user.Username, user.Password);
			Console.WriteLine("Login successfully.");
			if (!File.Exists(userFile)) {
				File.WriteAllText(userFile, JsonConvert.SerializeObject(user, Formatting.Indented), Encoding.UTF8);
			}

			Console.Write("Input RJ code, separated by spaces: ");
			string str = Console.ReadLine() ?? throw new ArgumentNullException(nameof(str));

			string[] codes = str.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToUpper()).ToArray();
			for (int i = 0; i < codes.Length; i++) {
				if (codes[i].StartsWith("RJ")) {
					codes[i] = codes[i][2..];
				}
				foreach (char ch in codes[i]) {
					if (ch < '0' || ch > '9') {
						Console.WriteLine("Wrong formatted RJ code. Enter ### or RJ###.");
						return 1;
					}
				}
			}

			foreach (string code in codes) {
				Console.WriteLine("Fetching RJ{0}...", code);
				WorkObject work = await Downloader.Wait10SecondsAndRetryAsyncPolicy.ExecuteAsync(() => downloader.GetWorkAsync(code));
				DirectoryInfo baseDir = new DirectoryInfo("RJ" + code + " " + Downloader.EscapeFilename(work.Title));
				await downloader.DownloadToDirectoryWithRetryAsync(code, baseDir);
			}

			await downloader.LogoutAsync();
			Console.WriteLine("Logout successfully.");

			Console.WriteLine("All done.");
			return 0;
		}
	}
}