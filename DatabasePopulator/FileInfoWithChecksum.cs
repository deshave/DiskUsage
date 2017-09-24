using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace FileDatabase
{
	class FileInfoWithChecksum
	{
		public FileInfo info
		{
			get; set;
		}

		public string checksum
		{
			get; set;
		}

		public FileInfoWithChecksum(FileInfo fi)
		{
			info = fi;
		}
	}
}
