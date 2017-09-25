#undef DEBUG

using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace FileDatabase
{
	public delegate void PopulatorEventHandler(object sender, PopulatorEventArgs e);

	public class DatabasePopulator
	{
		#region Private Members
		private Stopwatch sw;
		#endregion

		#region Public Members
		public ConcurrentDictionary<string, long> Folders;

		public int CurrentQueueCount
		{
			get
			{
				return Folders.Count;
			}
		}

		public string Root
		{
			get;
			set;
		}

		public event PopulatorEventHandler ScanningStarted;
		public event PopulatorEventHandler ScanningDone;
		#endregion

		#region Constructors
		public DatabasePopulator(string path)
		{
			Root = path;
			init();
		}
		#endregion

#region Private Methods
		private void init()
		{
#if DEBUG
			Debug.WriteLine("Initializing...");
#endif
			Folders = new ConcurrentDictionary<string, long>();
			sw = new Stopwatch();
		}

		private void ProcessFolder(IEnumerable<FileSystemInfo> fsi, PopulatorEventArgs pa)
		{
			foreach (FileSystemInfo info in fsi)
			{
				// We skip reparse points 
				if ((info.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
				{
					Debug.WriteLine(string.Format("{0} is a reparse point. Skipping.", info.FullName));
					//break;
				}

				if ((info.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
				{
					// If our FileSystemInfo object is a directory, we call this method again on the new directory.
					try
					{
						DirectoryInfo dirInfo = (DirectoryInfo)info;
#if DEBUG
						Debug.WriteLine(string.Format("Descending into {0}", info.FullName));
#endif

						Folders.AddOrUpdate(info.FullName, 0, (key, existingval) =>
						{
							existingval = 0;
							return existingval;
						});
						//ProcessFolder(dirInfo.GetFileSystemInfos(), pa);
						try
						{
							var test = dirInfo.GetFileSystemInfos();
							//							Thread myNewThread = new Thread(() => ProcessFolder(test, pa));
							//							myNewThread.Start();
							//Parallel.Invoke(() => ProcessFolder(test, pa));
							Task tsk = new Task(() => ProcessFolder(test, pa));
							tsk.Start();
							tsk.Wait();
						}
						catch (UnauthorizedAccessException)
						{
							continue;
						}
					}
					catch (UnauthorizedAccessException)
					{
						Debug.WriteLine(string.Format("{0} is inaccessible. Skipping.", info.FullName));
					}
					catch (Exception ex)
					{
						Debug.WriteLine(ex.StackTrace);
						// Skipping any errors
						// Really, we should catch each type of Exception - 
						// this will catch -any- exception that occurs, 
						// which may not be the behavior we want.
						break;
					}
				}
				else
				{
					// If our FileSystemInfo object isn't a directory, we cast it as a FileInfo object, 
					// make sure it's not null, and add it to the list.
					var file = info as FileInfo;
					if (file != null)
					{
						Folders.AddOrUpdate(file.DirectoryName, 0, (key, existingval) =>
						{
							existingval += file.Length;
							return existingval;
						});
					}
				}
			//	FillQueueWorker.ReportProgress(0, pa);
			}
		}

#endregion

#region Public Methods
		public void StartScanning()
		{
			PopulatorEventArgs pa = new PopulatorEventArgs(this.Root);
			var di = new DirectoryInfo(pa.Path);
			Folders.Clear();
			OnScanningStarted(this, pa);
			ProcessFolder(di.GetFileSystemInfos(), pa);
			OnScanningDone(this, null);
		}

		public void StopScanning()
		{
		}

		public string GetReport()
		{
			StringBuilder sb = new StringBuilder();
			var folders = Folders.ToArray();
			var sorted = folders.OrderBy(item => item.Value).ToArray();
			//var foo = folders[0].Key
			for (var i = sorted.Length; i > (sorted.Length - 20 > 0 ? sorted.Length - 20: 0); i--)
			{
				sb.AppendLine(string.Format("{0}: {1}", sorted[i-1].Key, Helpers.StringUtilities.GetBytesReadable(sorted[i-1].Value)));
			}
			sb.AppendLine(string.Format("{0} folders scanned in {1}", Folders.Count, sw.Elapsed.ToString(@"hh\:mm\:ss")));
			return sb.ToString();
		}
		#endregion

		#region Events
		private void OnScanningStarted(object sender, PopulatorEventArgs e)
		{
			sw.Restart();
			ScanningStarted?.Invoke(sender, e);
		}

		private void OnScanningDone(object sender, PopulatorEventArgs e)
		{
			sw.Stop();
			ScanningDone?.Invoke(sender, e);
		}
		#endregion
	}
}