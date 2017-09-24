using System;

namespace FileDatabase
{
	public class PopulatorEventArgs : EventArgs
	{
		private string path;
		public string Path
		{
			get
			{
				return path;
			}
			set
			{
				path = value;
			}
		}

		private string database;
		public string Database
		{
			get
			{
				return database;
			}
			set
			{
				database = value;
			}
		}

		private int percent;
		public int Percent
		{
			get
			{
				return percent;
			}
			set
			{
				percent = value;
			}
		}

		private int count;
		public int Count
		{
			get
			{
				return count;
			}
			set
			{
				count = value;
			}
		}

		public PopulatorEventArgs(string path, string database)
		{
			this.path = path;
			this.database = database;
			this.percent = 0;
		}

		public PopulatorEventArgs(string path, string database, int percent)
		{
			this.path = path;
			this.database = database;
			this.percent = percent;
		}

		public PopulatorEventArgs(string path, string database, int percent, int count)
		{
			this.path = path;
			this.database = database;
			this.percent = percent;
			this.count = count;
		}
	}
}
