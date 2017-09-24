#undef DEBUG

using System;
using FileDatabase;
using System.Diagnostics;


namespace DiskUsage
{
	class Program
	{
		static private DatabasePopulator dbp;
		static public int spincounter;
		static public int spincounter2;

		static void Main(string[] args)
		{
			string path = "";
			spincounter = 0;
			spincounter2 = 0;
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
				//				dbp = new DatabasePopulator(path, System.Environment.CurrentDirectory + @"\default.db");
				dbp = new DatabasePopulator(path, string.Format("{0}\\usage.db", path));
				dbp.QueuingStarted += Dbp_QueuingStarted;
				dbp.QueuingProgress += Dbp_QueuingProgress;
				dbp.QueuingDone += Dbp_QueuingDone;
				dbp.QueuingCancelled += Dbp_QueuingCancelled;
				dbp.ProcessingStarted += Dbp_ProcessingStarted;
				dbp.ProcessingProgress += Dbp_ProcessingProgress;
				dbp.ProcessingDone += Dbp_ProcessingDone;
				dbp.ProcessingCancelled += Dbp_ProcessingCancelled;
				dbp.StartScanning();
				dbp.StartProcessing();
				Console.ReadKey();
			}
			catch (Exception ex)
			{
				Debug.Write(ex.StackTrace);
				return;
				//throw new Exception(ex.Message, ex);
			}
		}

		private static void Dbp_ProcessingCancelled(object sender, PopulatorEventArgs e)
		{
#if DEBUG
			Console.WriteLine("Processing Cancelled event triggered.");
#endif
			//throw new NotImplementedException();
		}

		private static void Dbp_QueuingCancelled(object sender, PopulatorEventArgs e)
		{
#if DEBUG
			Console.WriteLine("Queuing Cancelled event triggered.");
#endif
			//throw new NotImplementedException();
		}

		private static void Dbp_QueuingStarted(object sender, PopulatorEventArgs e)
		{
#if DEBUG
			Console.WriteLine("Queuing Started event triggered.");
#endif
			Console.WriteLine(string.Format("Descending into {0}", e.Path));
		}

		private static void Dbp_QueuingProgress(object sender, PopulatorEventArgs e)
		{
#if DEBUG
			Console.WriteLine("Queuing Progress event triggered.");
#endif
			spincounter++;
			if (spincounter % 4 == 0)
			{
				Console.Write("\b|");
			}
			if (spincounter % 4 == 1)
			{
				Console.Write("\b/");
			}
			if (spincounter % 4 == 2)
			{
				Console.Write("\b-");
			}
			if (spincounter % 4 == 3)
			{
				Console.Write("\b\\");
			}
		}

		private static void Dbp_QueuingDone(object sender, PopulatorEventArgs e)
		{
#if DEBUG
			Console.WriteLine("Queuing Done event triggered.");
#endif
			PopulatorEventArgs pa = (PopulatorEventArgs)e;
			//dbp.StartProcessing();
			//Debug.WriteLine(string.Format("Queue Count: {0}", dbp.CurrentQueueCount));
			Console.WriteLine("\bDone scanning");
		}

		private static void Dbp_ProcessingStarted(object sender, PopulatorEventArgs e)
		{
#if DEBUG
			Console.WriteLine("Processing Started event triggered.");
#endif
			Console.WriteLine("Processing files & folders.");
			//throw new NotImplementedException();
		}

		private static void Dbp_ProcessingProgress(object sender, PopulatorEventArgs e)
		{
#if DEBUG
			Console.WriteLine("Processing Progress event triggered.");
#endif
			Console.Write(string.Format("\b\b\b\b\b{0}", e.Count));
			//spincounter2++;
			//if (spincounter2 % 4 == 0)
			//{
			//	Console.Write("\b|");
			//}
			//if (spincounter2 % 4 == 1)
			//{
			//	Console.Write("\b/");
			//}
			//if (spincounter2 % 4 == 2)
			//{
			//	Console.Write("\b-");
			//}
			//if (spincounter2 % 4 == 3)
			//{
			//	Console.Write("\b\\");
			//}
		}

		private static void Dbp_ProcessingDone(object sender, PopulatorEventArgs e)
		{
#if DEBUG
			Console.WriteLine("Processing Done event triggered.");
#endif
			Console.WriteLine("\bPrinting report...");
			Console.Write(dbp.GetReport());
			Console.WriteLine("Press any key to exit...");
		}
	}
}
