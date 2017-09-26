#undef DEBUG

using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;

/// <summary>
/// Utilities for quickly analyzing large lists of directories and files.
/// </summary>
namespace FilesystemUtilities
{
	/// <summary>
	/// This class provides access to process a directory and its child directories and formats a report of the directories and their aggregated sizes.
	/// </summary>
	public class DirectoryProcessor
	{
		#region Private Members
		/// <summary>
		/// A <see cref="Stopwatch"/> to time the directory processing.
		/// </summary>
		private Stopwatch sw;

		/// <summary>
		/// A <see cref="bool"/> flag for cancelling the processing.
		/// </summary>
		private bool CancelRequested;
		#endregion

		#region Public Members
		/// <summary>
		/// A <see cref="ConcurrentDictionary{TKey, TValue}"/> to hold directory names as a <see cref="string"/> and aggregated file sizes as a <see cref="long"/>.
		/// </summary>
		public ConcurrentDictionary<string, long> Directories;

		/// <summary>
		/// Current count of items in the Directories <see cref="ConcurrentDictionary{TKey, TValue}"/>.
		/// </summary>
		public int CurrentQueueCount
		{
			get
			{
				return Directories.Count;
			}
		}

		/// <summary>
		/// Gets or sets the starting root of the <see cref="DirectoryProcessor"/>.
		/// </summary>
		public string Root
		{
			get;
			set;
		}

		/// <summary>
		/// Occurs when the processing begins.
		/// </summary>
		public event Action ScanningStarted;
		/// <summary>
		/// Occurs when the processing ends.
		/// </summary>
		public event Action ScanningDone;
		#endregion

		#region Constructors
		/// <summary>
		/// Initializes a new instance of the <see cref="DirectoryProcessor"/> class, and sets the <see cref="Root"/> property to the specified value.
		/// </summary>
		/// <param name="path">The path location to start the processing. Must be a valid filesystem path.</param>
		/// <exception cref="ArgumentException">The value of the <c>path</c> parameter is null</exception>
		/// <exception cref="DirectoryNotFoundException">The value of the <c>path</c> parameter could not be found.</exception>
		public DirectoryProcessor(string path)
		{
			if (path == null)
			{
				throw new ArgumentException("Method was passed a null value.", "path");
			}
			if (!Directory.Exists(path))
			{
				throw new DirectoryNotFoundException(string.Format("The specified path, \"{0}\", could not be accessed.", path));
			}
			Root = path;
			init();
		}
		#endregion

		#region Private Methods
		/// <summary>
		/// Initializes the <see cref="DirectoryProcessor"/> object.
		/// </summary>
		private void init()
		{
#if DEBUG
			Debug.WriteLine("Initializing...");
#endif
			this.CancelRequested = false;
			Directories = new ConcurrentDictionary<string, long>();
			sw = new Stopwatch();
		}

		/// <summary>
		/// Walks down the directory tree, processing each child directory as it is encountered.
		/// </summary>
		/// <param name="fsi">An <see cref="IEnumerable{FileSystemInfo}"/> object containing the starting point of the directory processing for this iteration.</param>
		private void ProcessDirectory(IEnumerable<FileSystemInfo> fsi)
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

						Directories.AddOrUpdate(info.FullName, 0, (key, existingval) =>
						{
							existingval = 0;
							return existingval;
						});
						try
						{
							var test = dirInfo.GetFileSystemInfos();
							Task tsk = new Task(() => ProcessDirectory(test));
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
						Directories.AddOrUpdate(file.DirectoryName, 0, (key, existingval) =>
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
		/// Starts the scan.
		/// </summary>
		public void StartScanning()
		{
			var di = new DirectoryInfo(this.Root);
			Directories.Clear();
			OnScanningStarted();
			ProcessDirectory(di.GetFileSystemInfos());
			OnScanningDone();
		}

		/// <summary>
		/// Stop the scans.
		/// </summary>
		public void StopScanning()
		{
			CancelRequested = true;
		}

		/// <summary>
		/// Creates a formatted report of the top directories and their file sizes.
		/// </summary>
		/// <returns>A <see cref="string"/> containing the formatted report.</returns>
		/// <param name="count">An <see cref="int"/> containing the number of requested directories for the report.</param>
		public string GetReport(int count)
		{
			StringBuilder sb = new StringBuilder();
			var directories = Directories.ToArray();
			var sorted = directories.OrderBy(item => item.Value).ToArray();
			for (var i = sorted.Length; i > (sorted.Length - count > 0 ? sorted.Length - count: 0); i--)
			{
				sb.AppendLine(string.Format("{0}: {1}", sorted[i-1].Key, Helpers.StringUtilities.GetBytesReadable(sorted[i-1].Value)));
			}
			sb.AppendLine(string.Format("{0} directories scanned in {1}", Directories.Count, sw.Elapsed.ToString(@"hh\:mm\:ss")));
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