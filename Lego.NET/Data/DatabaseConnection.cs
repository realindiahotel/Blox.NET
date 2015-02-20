using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bitcoin.Lego.Data
{
	public static class DatabaseConnection
	{
		private static String _superUserLegoDbPwd;
		private static String _connectionString = @"Server=tcp:cglpto7ask.database.windows.net,1433;Database=Lego.NET.DB;User ID=SuperUserLego@cglpto7ask;Password="+_superUserLegoDbPwd+@"Trusted_Connection=False;Encrypt=True;Connection Timeout=30;";
       
	}
}
