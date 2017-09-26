#undef DEBUG

using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace FilesystemUtilities
{
	/// <summary>
	/// This class provides access to process a folder and its subfolders.
	/// </summary>
	public class FolderProcessor
	{
		#region Private Members
		/// <summary>
		/// A <see cref="Stopwatch"/> to time the folder processing.
		/// </summary>
		private Stopwatch sw;

		/// <summary>
		/// A private property to hold the value for cancelling the processing.
		/// </summary>
		private bool CancelRequested;
		#endregion

		#region Public Members
		/// <summary>
		/// A <see href="ConcurrentDictionary"/> to hold folder names as a <see cref="string"/> and aggregated file sizes as a <see cref="long"/>.
		/// </summary>
		public ConcurrentDictionary<string, long> Folders;

		/// <summary>
		/// A public method to get the current count of items in the Folders <see cref="ConcurrentDictionary{TKey, TValue}"/>.
		/// </summary>
		public int CurrentQueueCount
		{
			get
			{
				return Folders.Count;
			}
		}

		/// <summary>
		/// Gets or sets the starting root of the <see cref="FolderProcessor"/>.
		/// </summary>
		public string Root
		{
			get;
			set;
		}

		/// <summary>
		/// A custom event action to handle the start of the processing.
		/// </summary>
		public event Action ScanningStarted;
		/// <summary>
		/// A custom event action to handle the end of the processing.
		/// </summary>
		public event Action ScanningDone;
		#endregion

		#region Constructors
		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="path">The root of the <see cref="FolderProcessor"/></param>
		public FolderProcessor(string path)
		{
			Root = path;
			init();
		}
		#endregion

		#region Private Methods
		/// <summary>
		/// A private method to initialize the <see cref="FolderProcessor"/>.
		/// </summary>
		private void init()
		{
#if DEBUG
			Debug.WriteLine("Initializing...");
#endif
			this.CancelRequested = false;
			Folders = new ConcurrentDictionary<string, long>();
			sw = new Stopwatch();
		}

		/// <summary>
		/// A recursive method to walk down the folder tree, processing each subfolder as it is encountered.
		/// </summary>
		/// <param name="fsi">An <see cref="IEnumerable{FileSystemInfo}"/> object containing the starting point of the folder processing for this iteration.</param>
		private void ProcessFolder(IEnumerable<FileSystemInfo> fsi)
		{
			foreach (FileSystemInfo info in fsi)
			{
				if (CancelRequested)
				{
					return;
				}
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
						try
						{
							var test = dirInfo.GetFileSystemInfos();
							Task tsk = new Task(() => ProcessFolder(test));
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
			}
		}
#endregion

		#region Public Methods
		/// <summary>
		/// A public method to start the scan.
		/// </summary>
		public void StartScanning()
		{
			var di = new DirectoryInfo(this.Root);
			Folders.Clear();
			OnScanningStarted();
			ProcessFolder(di.GetFileSystemInfos());
			OnScanningDone();
		}

		/// <summary>
		/// A public method to stop the scan.
		/// </summary>
		public void StopScanning()
		{
			CancelRequested = true;
		}

		/// <summary>
		/// Creates a formatted report of the top folders and their file sizes.
		/// </summary>
		/// <returns>A <see cref="string"/> containing the formatted report.</returns>
		/// <param name="count">An <see cref="int"/> containing the number of requested folders for the report.</param>
		public string GetReport(int count)
		{
			StringBuilder sb = new StringBuilder();
			var folders = Folders.ToArray();
			var sorted = folders.OrderBy(item => item.Value).ToArray();
			for (var i = sorted.Length; i > (sorted.Length - count > 0 ? sorted.Length - count: 0); i--)
			{
				sb.AppendLine(string.Format("{0}: {1}", sorted[i-1].Key, Helpers.StringUtilities.GetBytesReadable(sorted[i-1].Value)));
			}
			sb.AppendLine(string.Format("{0} folders scanned in {1}", Folders.Count, sw.Elapsed.ToString(@"hh\:mm\:ss")));
			return sb.ToString();
		}
		#endregion

		#region Events
		/// <summary>
		/// Fired when the scan starts.
		/// </summary>
		private void OnScanningStarted()
		{
			sw.Restart();
			ScanningStarted?.Invoke();
		}

		/// <summary>
		/// Fired when the scan ends.
		/// </summary>
		private void OnScanningDone()
		{
			sw.Stop();
			CancelRequested = false;
			ScanningDone?.Invoke();
		}
		#endregion
	}
}