using System;

namespace FilesystemUtilities
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

		public PopulatorEventArgs(string path)
		{
			this.path = path;
			this.percent = 0;
		}

		public PopulatorEventArgs(string path, int percent)
		{
			this.path = path;
			this.percent = percent;
		}

		public PopulatorEventArgs(string path, int percent, int count)
		{
			this.path = path;
			this.percent = percent;
			this.count = count;
		}
	}
}
