using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Bitcoin.Lego.Data
{
	public static class DatabaseConnection
	{
		private static String _superUserLegoDbPwd;
		private static String _connectionString = @"Server=tcp:cglpto7ask.database.windows.net,1433;Database=Lego.NET.DB;User ID=SuperUserLego@cglpto7ask;Password="+_superUserLegoDbPwd+@"Trusted_Connection=False;Encrypt=True;Connection Timeout=30;";

		static DatabaseConnection()
		{
			try
			{
				TextReader tr = new StreamReader(@"\Data\DB_PWD.txt");
				_superUserLegoDbPwd = tr.ReadLine();
				tr.Close();
			}
			catch
			{
				_superUserLegoDbPwd = "";
			}
		}
	}
}
