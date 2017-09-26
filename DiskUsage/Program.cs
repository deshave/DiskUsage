using System;
using System.Diagnostics;

namespace DiskUsage
{
	class Program
	{
		static private FilesystemUtilities.DirectoryProcessor dbp;

		static void Main(string[] args)
		{
			string path = "";
			if (args.Length == 1)
			{
				path = args[0];
			}
			else
			{
				Console.WriteLine(string.Format("Usage: {0} \"<path>\"", Environment.CommandLine));
				return;
			}
			try
			{
				dbp = new FilesystemUtilities.DirectoryProcessor(path);
				dbp.ScanningStarted += Dbp_ScanningStarted;
				dbp.ScanningDone += Dbp_ScanningDone;
				dbp.StartScanning();
				Console.WriteLine(dbp.GetReport(20));
				Console.ReadKey();
			}
			catch (Exception)
			{
				Console.WriteLine("An unexpected error occurred.");
				return;
			}
		}

		private static void Dbp_ScanningDone()
		{
			Console.WriteLine("Scanning done.");
		}

		private static void Dbp_ScanningStarted()
		{
			Console.WriteLine("Scanning started...");
		}
	}
}
