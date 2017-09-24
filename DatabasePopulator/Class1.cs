#undef DEBUG

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace FileDatabase
{
	public delegate void PopulatorEventHandler(object sender, PopulatorEventArgs e);

	public class DatabasePopulator
	{
		#region Private Members
		private readonly string CreateFileTableCommand = @"CREATE TABLE Files ( FileName text, FullPath text NOT NULL, DateAdded text, Extension text, ContentType text, FileSize integer, CheckSum text, CreationTime text, LastAccessTime text, LastWriteTime text, Reviewed integer )";
		private readonly string CreateFolderTableCommand = @"CREATE TABLE Folders (FolderName text, FullPath text unique not null, FileSize integer, DateAdded text)";
		private BlockingCollection<string> bc;
		private BackgroundWorker FillQueueWorker;
		private BackgroundWorker ProcessQueueWorker;
		private BackgroundWorker ProcessQueueWorker2;
		private BackgroundWorker ProcessQueueWorker3;
		private string databasefile;
		#endregion

		#region Public Members
		public event PopulatorEventHandler ProcessingStarted;
		public event PopulatorEventHandler ProcessingProgress;
		public event PopulatorEventHandler ProcessingDone;
		public event PopulatorEventHandler ProcessingCancelled;
		public event PopulatorEventHandler QueuingStarted;
		public event PopulatorEventHandler QueuingProgress;
		public event PopulatorEventHandler QueuingDone;
		public event PopulatorEventHandler QueuingCancelled;

		public int CurrentQueueCount
		{
			get
			{
				return bc.Count;
			}
		}

		public string Root
		{
			get;
			set;
		}
		public string DatabaseFile
		{
			get
			{
				return databasefile;
			}
			set
			{
				databasefile = value;
				//				dbconnectionstring = string.Format("Data Source={0};Version=3;FailIfMissing=True", value);
			}
		}
		#endregion

		#region Constructors
		public DatabasePopulator(string path)
		{
			Root = path;
			DatabaseFile = string.Format("{0}\\usage.db", path);
			init();
		}

		public DatabasePopulator(string path, string dbase)
		{
			Root = path;
			DatabaseFile = dbase;
			init();
		}
		#endregion

		#region FillQueueWorker
		private void FillQueueWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			PopulatorEventArgs pa = (PopulatorEventArgs)e.Argument;
			e.Result = pa;
			var di = new DirectoryInfo(pa.Path);
			try
			{
				bc = new BlockingCollection<string>();
			}
			catch (Exception ex)
			{
#if DEBUG
				Debug.WriteLine(ex.StackTrace);
#endif
				//throw new Exception(ex.Message, ex);
			}
			ProcessFolder(di.GetFileSystemInfos(), pa);
		}

		private void FillQueueWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			OnQueuingProgress(sender, (PopulatorEventArgs)e.UserState);
		}

		private void FillQueueWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			bc.CompleteAdding();
			PopulatorEventArgs pa = null;
			// First, handle the case where an exception was thrown.
			if (e.Error != null)
			{
				throw new Exception(e.Error.Message, e.Error);
			}
			else if (e.Cancelled)
			{
				//Console.WriteLine("Canceled");
			}
			else
			{
				pa = (PopulatorEventArgs)e.Result;
				//Console.WriteLine(pa.);
			}
			OnQueuingDone(this, pa);
		}
		#endregion

		private void SendSQLNonQuery(string sql)
		{
#if DEBUG
			Debug.WriteLine(sql);
#endif
			try
			{
				using (SQLiteConnection mycon = new SQLiteConnection(GetConnectionString(DatabaseFile)))
				{
					mycon.Open();
					using (SQLiteTransaction mytrans = mycon.BeginTransaction())
					{
						using (SQLiteCommand mycom = new SQLiteCommand(mycon))
						{
							mycom.CommandText = sql;
							mycom.ExecuteNonQuery();
						}
						mytrans.Commit();
					}
				}
			}
			catch (SQLiteException ex)
			{
				Debug.WriteLine(ex.StackTrace);
			}
		}

		private object SendSQLQuery(string sql)
		{
#if DEBUG
			Debug.WriteLine(sql);
#endif
			object results = null;
			try
			{
				using (SQLiteConnection mycon = new SQLiteConnection(GetConnectionString(DatabaseFile)))
				{
					mycon.Open();
					using (SQLiteTransaction mytrans = mycon.BeginTransaction())
					{
						using (SQLiteCommand mycom = new SQLiteCommand(mycon))
						{
							mycom.CommandText = sql;
							results = mycom.ExecuteScalar();
						}
						mytrans.Commit();
					}
				}
			}
			catch (SQLiteException ex)
			{
				Debug.WriteLine(ex.StackTrace);
			}
			return results;
		}

#region ProcessQueueWorker
		private void ProcessQueueWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			BackgroundWorker me = sender as BackgroundWorker;
			PopulatorEventArgs pa = (PopulatorEventArgs)e.Argument;
			e.Result = pa;
			foreach (var file in bc.GetConsumingEnumerable())
			{
				if (me.CancellationPending)
				{
					OnProcessingCancelled(sender, pa);
					return;
				}
				var item = new FileInfo(file);
				var CommandText = string.Format("INSERT INTO Files (FileName, FullPath, DateAdded, Extension, ContentType, FileSize, Checksum, CreationTime, LastAccessTime, LastWriteTime) VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', {5}, '{6}', '{7}', '{8}', '{9}')", item.Name.Replace("'","''"), item.FullName.Replace("'","''"), DateTime.Now, item.Extension, Helpers.MimeMapper.GetMimeType(item.Extension), item.Length, ' ', item.CreationTime, item.LastAccessTime, item.LastWriteTime);

				SendSQLNonQuery(CommandText);
				var result = SendSQLQuery(string.Format("SELECT ROWID FROM Folders WHERE FullPath='{0}'", item.DirectoryName.Replace("'","''")));

				if (result != null)
				{
					SendSQLNonQuery(string.Format("UPDATE Folders SET FileSize=FileSize + {0} where ROWID={1}", item.Length, result.ToString()));
				}
				else
				{
					SendSQLNonQuery(string.Format("INSERT into Folders (FolderName, FullPath, FileSize, DateAdded) values ('{0}', '{1}', {2}, '{3}')", item.Directory.Name.Replace("'","''"), item.DirectoryName.Replace("'","''"), item.Length, DateTime.Now));
				}
				try
				{
					pa.Count = bc.Count;
				}
				catch (ObjectDisposedException)
				{

				}
				try
				{
					ProcessQueueWorker.ReportProgress(0, pa);
				}
				catch (InvalidOperationException)
				{
					
				}
			}
		}

		private void ProcessQueueWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			//PopulatorEventArgs pea = new PopulatorEventArgs()
			OnProcessingProgress(sender, (PopulatorEventArgs)e.UserState);
		}

		private void ProcessQueueWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			PopulatorEventArgs pa = null;
			if (!ProcessQueueWorker.IsBusy && !ProcessQueueWorker2.IsBusy)
			{
				bc.Dispose();
			}
			// First, handle the case where an exception was thrown.
			if (e.Error != null)
			{
				//				Console.WriteLine(e.Error.Message);
				throw new Exception(e.Error.Message, e.Error);
			}
			else if (e.Cancelled)
			{
				//Console.WriteLine("Canceled");
			}
			else
			{
				pa = (PopulatorEventArgs)e.Result;
			}
			OnProcessingDone(this, pa);
		}
#endregion

#region Private Methods
		private void init()
		{
#if DEBUG
			Debug.WriteLine("Initializing...");
#endif
			bc = new BlockingCollection<string>();
			FillQueueWorker = new BackgroundWorker();
			FillQueueWorker.WorkerReportsProgress = true;
			FillQueueWorker.WorkerSupportsCancellation = true;
			FillQueueWorker.DoWork += FillQueueWorker_DoWork;
			FillQueueWorker.ProgressChanged += FillQueueWorker_ProgressChanged;
			FillQueueWorker.RunWorkerCompleted += FillQueueWorker_RunWorkerCompleted;
			ProcessQueueWorker = new BackgroundWorker();
			ProcessQueueWorker.WorkerReportsProgress = true;
			ProcessQueueWorker.WorkerSupportsCancellation = true;
			ProcessQueueWorker.DoWork += new DoWorkEventHandler(ProcessQueueWorker_DoWork);
			ProcessQueueWorker.ProgressChanged += ProcessQueueWorker_ProgressChanged;
			ProcessQueueWorker.RunWorkerCompleted += ProcessQueueWorker_RunWorkerCompleted;
			ProcessQueueWorker2 = new BackgroundWorker();
			ProcessQueueWorker2.WorkerReportsProgress = false;
			ProcessQueueWorker2.WorkerSupportsCancellation = true;
			ProcessQueueWorker2.DoWork += new DoWorkEventHandler(ProcessQueueWorker_DoWork);
//			ProcessQueueWorker2.ProgressChanged += ProcessQueueWorker_ProgressChanged;
//			ProcessQueueWorker2.RunWorkerCompleted += ProcessQueueWorker_RunWorkerCompleted;
			ProcessQueueWorker3 = new BackgroundWorker();
			ProcessQueueWorker3.WorkerReportsProgress = false;
			ProcessQueueWorker3.WorkerSupportsCancellation = true;
			ProcessQueueWorker3.DoWork += new DoWorkEventHandler(ProcessQueueWorker_DoWork);
//			ProcessQueueWorker3.ProgressChanged += ProcessQueueWorker_ProgressChanged;
//			ProcessQueueWorker3.RunWorkerCompleted += ProcessQueueWorker_RunWorkerCompleted;
			if (!File.Exists(this.DatabaseFile))
			{
				SQLiteConnection.CreateFile(DatabaseFile);
				SendSQLNonQuery(CreateFolderTableCommand);
				SendSQLNonQuery(CreateFileTableCommand);
			}
			else
			{
				if (SendSQLQuery("select ROWID from sqlite_master where name='Files'") == null)
				{
					SendSQLNonQuery(CreateFileTableCommand);
				}
				else
				{
					SendSQLNonQuery("delete from Files");
				}
				if (SendSQLQuery("select ROWID from sqlite_master where name='Folders'") == null)
				{
					SendSQLNonQuery(CreateFolderTableCommand);
				}
				else
				{
					SendSQLNonQuery("delete from Folders");
				}
			}
		}

		private void ProcessFolder(IEnumerable<FileSystemInfo> fsi, PopulatorEventArgs pa)
		{
			foreach (FileSystemInfo info in fsi)
			{
				if (this.ProcessQueueWorker.CancellationPending)
				{
					OnQueuingCancelled(new PopulatorEventArgs(info.FullName, ""), null);
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
						Debug.WriteLine(string.Format("Descending into {0}", info.FullName));
						ProcessFolder(dirInfo.GetFileSystemInfos(), pa);
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
						bc.Add(file.FullName);
					}
				}
				FillQueueWorker.ReportProgress(0, pa);
			}
		}

		private string GetChecksum(string filename)
		{
			//try
			//{
			//	MD5 foo = MD5.Create();
			//	byte[] checksum = foo.ComputeHash(new FileStream(filename, FileMode.Open));
			//	return Helpers.StringUtilities.PrintByteArray(checksum);
			//}
			//catch (Exception ex)
			//{
			return "";
			//}
		}
#endregion

#region Public Methods
		public void StartScanning()
		{
			PopulatorEventArgs pa = new PopulatorEventArgs(this.Root, this.DatabaseFile, 0, 0);
			FillQueueWorker.RunWorkerAsync(pa);
			OnQueuingStarted(this, pa);
		}

		public void StopScanning()
		{
			FillQueueWorker.CancelAsync();
		}

		public void StartProcessing()
		{
			PopulatorEventArgs pa = new PopulatorEventArgs(this.Root, this.DatabaseFile, 0, 0);
			ProcessQueueWorker.RunWorkerAsync(pa);
			ProcessQueueWorker2.RunWorkerAsync(pa);
			ProcessQueueWorker3.RunWorkerAsync(pa);
			OnProcessingStarted(this, pa);
		}

		public void StopProcessing()
		{
			ProcessQueueWorker.CancelAsync();
		}

		public string GetReport()
		{
			StringBuilder sb = new StringBuilder();
			using (var dbconnection = new SQLiteConnection(GetConnectionString(this.DatabaseFile)))
			{
				dbconnection.Open();
				using (SQLiteCommand cmd = dbconnection.CreateCommand())
				{
					//cmd.CommandText = @"select e1.FullPath, (select sum(FileSize) from Files e2 where e2.FullPath like e1.FullPath || '%') subquery2 from Files e1 group by e1.FullPath order by subquery2 desc limit 20";
					cmd.CommandText = @"select * from Folders order by FileSize desc limit 25";
					cmd.CommandType = System.Data.CommandType.Text;
					SQLiteDataReader r = cmd.ExecuteReader();
					if (r.HasRows)
					{
						while (r.Read())
						{
							//sb.AppendLine(string.Format("{0}: {1}", Convert.ToString(r["FullPath"]), Helpers.StringUtilities.GetBytesReadable(Convert.ToInt64(r["Subquery2"]))));
							sb.AppendLine(string.Format("{0}: {1}", Convert.ToString(r["FullPath"]), Helpers.StringUtilities.GetBytesReadable(Convert.ToInt64(r["FileSize"]))));
						}
					}
				}
			}
			return sb.ToString();
		}
#endregion

#region Events
		private void OnQueuingStarted(object sender, PopulatorEventArgs e)
		{
			QueuingStarted?.Invoke(sender, e);
		}

		private void OnQueuingProgress(object sender, PopulatorEventArgs e)
		{
			QueuingProgress?.Invoke(sender, e);
		}

		private void OnQueuingDone(object sender, PopulatorEventArgs e)
		{
			QueuingDone?.Invoke(sender, e);
		}

		private void OnQueuingCancelled(object sender, PopulatorEventArgs e)
		{
			QueuingCancelled?.Invoke(sender, e);
		}

		private void OnProcessingStarted(object sender, PopulatorEventArgs e)
		{
			ProcessingStarted?.Invoke(sender, e);
		}

		private void OnProcessingProgress(object sender, PopulatorEventArgs e)
		{
			ProcessingProgress?.Invoke(sender, e);
		}

		private void OnProcessingDone(object sender, PopulatorEventArgs e)
		{
			ProcessingDone?.Invoke(sender, e);
		}

		private void OnProcessingCancelled(object sender, PopulatorEventArgs e)
		{
			ProcessingCancelled?.Invoke(sender, e);
		}


#endregion

#region Static Methods
		private static string GetConnectionString(string filename)
		{
			return string.Format("Data Source={0};Version=3;FailIfMissing=True", filename);
		}

		private static string GetSQLStatement(SQLiteCommand sqlcmd)
		{
			var start = sqlcmd.CommandText;
			var fields = Regex.Matches(start, "@\\w+");
			for (var i = 0; i < fields.Count; i++)
			{
				var quote = sqlcmd.Parameters[fields[i].Value].DbType == System.Data.DbType.Int64 ? "" : "'";
				start = start.Replace(fields[i].Value, string.Format("{0}{1}{2}", quote, sqlcmd.Parameters[fields[i].Value].Value.ToString(), quote));
			}
			return start;
		}
#endregion
	}
}