﻿namespace AsmrOneDownloader {
	internal class Program {
		static async Task Main(string[] args) {
			while (true) {
				Console.Write("Input RJ code: ");
				string? rjcode = Console.ReadLine();
				if (string.IsNullOrWhiteSpace(rjcode)) {
					Console.WriteLine("Exit.");
					break;
				}
				rjcode = rjcode.Trim().ToUpper();
				if (rjcode.StartsWith("RJ")) {
					rjcode = rjcode.Substring(2);
				}
				if (rjcode.Any(c => c < '0' || c > '9')) {
					Console.WriteLine("Wrong formatted RJ code. Enter ### or RJ###.");
					continue;
				}
				Console.WriteLine("Fetching RJ{0}...", rjcode);

				Downloader downloader = new Downloader(rjcode);
				DirectoryInfo baseDir = new DirectoryInfo("RJ" + rjcode);
				await downloader.UpdateToDirectory(baseDir);
			}
		}
	}
}