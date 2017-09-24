using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileDatabase
{
	public struct FileItem
	{
		public int RowID
		{
			get;
			set;
		}

		public int Count
		{
			get;
			set;
		}

		public string FileName
		{
			get;
			set;
		}

		public string FullPath
		{
			get;
			set;
		}

		public long FileSize
		{
			get;
			set;
		}

		public string ContentType
		{
			get;
			set;
		}

		public string CreationTime
		{
			get;
			set;
		}

		public string LastModifiedTime
		{
			get;set;
		}

		public string LastAccessedTime
		{
			get;set;
		}

		public Bitmap Thumb
		{
			get;
			set;
		}

		public Icon Icon
		{
			get;
			set;
		}

		public string Checksum
		{
			get;
			set;
		}
	}
}
