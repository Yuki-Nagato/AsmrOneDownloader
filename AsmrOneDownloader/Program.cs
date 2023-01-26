namespace AsmrOneDownloader {
	internal class Program {
		static async Task Main(string[] args) {
			while (true) {
				Console.Write("Input RJ code: ");
				string rjcode = Console.ReadLine() ?? throw new NullReferenceException();
				rjcode = rjcode.Trim().ToUpper();
				if (string.IsNullOrWhiteSpace(rjcode)) {
					Console.WriteLine("Exit.");
					break;
				}
				if (rjcode.StartsWith("RJ")) {
					rjcode = rjcode.Substring(2);
				}
				if (rjcode.Any(c => c < '0' || c > '9')) {
					Console.WriteLine("Wrong format RJ code. Enter xxx or RJxxx.");
					continue;
				}
				Console.WriteLine("Fetching RJ{0}...", rjcode);

				Downloader downloader = new Downloader(rjcode);
				DirectoryInfo baseDir = Directory.CreateDirectory("RJ" + rjcode);
				await downloader.UpdateToDirectory(baseDir);
			}
		}
	}
}