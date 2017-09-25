#undef DEBUG

using System;
using FileDatabase;
using System.Diagnostics;


namespace DiskUsage
{
	class Program
	{
		static private DatabasePopulator dbp;

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
				dbp = new DatabasePopulator(path);
				dbp.ScanningStarted += Dbp_ScanningStarted;
				dbp.ScanningDone += Dbp_ScanningDone;
				dbp.StartScanning();
				Console.WriteLine(string.Format("Queue Count: {0}", dbp.CurrentQueueCount));
				Console.WriteLine("Done scanning");
				Console.WriteLine(dbp.GetReport());
				Console.ReadKey();
			}
			catch (Exception ex)
			{
				Debug.Write(ex.StackTrace);
				return;
				//throw new Exception(ex.Message, ex);
			}
		}

		private static void Dbp_ScanningDone(object sender, PopulatorEventArgs e)
		{
			Console.WriteLine("Scanning done.");
		}

		private static void Dbp_ScanningStarted(object sender, PopulatorEventArgs e)
		{
			Console.WriteLine("Scanning started...");
		}
	}
}
