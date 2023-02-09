namespace AsmrOneDownloader {
	internal class Program {
		static async Task<int> Main(string[] args) {
			Downloader downloader = new Downloader();

			Console.Write("Input username:");
			string username = Console.ReadLine() ?? throw new ArgumentNullException(nameof(username));
			Console.Write("Input password:");
			string password = Console.ReadLine() ?? throw new ArgumentNullException(nameof(password));
			await downloader.LoginAsync(username, password);
			Console.WriteLine("Login successfully.");

			Console.Write("Input RJ code, separated by spaces: ");
			string str = Console.ReadLine() ?? throw new ArgumentNullException(nameof(str));

			string[] codes = str.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToUpper()).ToArray();
			for(int i=0; i<codes.Length; i++) {
				if (codes[i].StartsWith("RJ")) {
					codes[i] = codes[i][2..];
				}
				foreach(char ch in codes[i]) {
					if(ch < '0' || ch > '9') {
						Console.WriteLine("Wrong formatted RJ code. Enter ### or RJ###.");
						return 1;
					}
				}
			}

			foreach (string code in codes) {
				Console.WriteLine("Fetching RJ{0}...", code);

				WorkObject work = await downloader.GetWorkAsync(code);
				DirectoryInfo baseDir = new DirectoryInfo("RJ" + code + " " + Downloader.EscapeFilename(work.Title));
				await downloader.DownloadToDirectoryAsync(code, baseDir);
			}

			Console.WriteLine("All done.");
			return 0;
		}
	}
}