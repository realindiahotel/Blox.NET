using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.Sql;
using System.Data.SqlClient;
using Bitcoin.BitcoinUtilities;
using Bitcoin.Lego.Protocol_Messages;
using Bitcoin.Lego;
using System.Net;

namespace Bitcoin.Lego.Data_Interface
{
	/// <summary>
	/// A static class designed for interfacing with an SQL Server DB, whether that is in the cloud or local is up to you :)
	/// DEVS PLEASE ENSURE PARAMETERS ARE USED AND NOT JUST APPENDING STRINGS TO BUILD QUERIES AS THIS WILL PROTECT FROM SQL INJECTION ATTACKS THANKS THASHIZNETS <3
	/// </summary>
	public class DatabaseConnection
	{
		private String _connectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=LegoDB;Integrated Security=True;Connect Timeout=60;Encrypt=False;TrustServerCertificate=False";
		private SqlConnection _sqlConnectionObj;

		public DatabaseConnection()
		{
			_sqlConnectionObj = new SqlConnection(_connectionString);
			OpenDBConnection();
		}

		public bool CloseDBConnection()
		{
			try
			{
				if (_sqlConnectionObj.State != System.Data.ConnectionState.Closed)
				{
					_sqlConnectionObj.Close();
				}

				return true;
			}
#if (!DEBUG)
			catch
			{

			}
#else
			catch (Exception ex)
			{
                Console.WriteLine("Exception: "+ex.Message);
				if (ex.InnerException != null)
				{
					Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
				}
			}
#endif

			return false;
		}

		public bool OpenDBConnection()
		{
			if (!IsOpen)
			{
				try
				{
					//in case the connection is in some weird fucked up state we will attempt to close it before opening it
					_sqlConnectionObj.Close();
				}
#if (!DEBUG)
			catch
			{

			}
#else
				catch (Exception ex)
				{
					Console.WriteLine("Exception: " + ex.Message);
					if (ex.InnerException != null)
					{
						Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
					}
				}
#endif

				try
				{
					_sqlConnectionObj.Open();
				}
#if (!DEBUG)
			catch
			{
				return false
			}
#else
				catch (Exception ex)
				{
					Console.WriteLine("Exception: " + ex.Message);
					if (ex.InnerException != null)
					{
						Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
					}

					return false;
				}
#endif
			}

			return true;
		}

		public bool IsOpen
		{
			get
			{
				if (_sqlConnectionObj.State != System.Data.ConnectionState.Open)
				{
					return false;
				}

				return true;
			}
		}

		public bool AddAddress(PeerAddress addressToAdd)
		{
			try
			{
				if (!IsOpen)
				{
					OpenDBConnection();
				}

				if (!IsAddressKnown(addressToAdd))
				{
					//saw some clients with weird big numbers un the service field, I don't trust them so they get treated as SPV nodes
					if (addressToAdd.Services != (ulong)Globals.Services.NODE_NETWORK && addressToAdd.Services != (ulong)Globals.Services.SPV_NODE_NETWORK)
					{
						addressToAdd.Services=0;
					}

                    SqlCommand addAddrCmd = new SqlCommand("INSERT INTO [AddressPool] VALUES (@Param1, @Param2, @Param3, @Param4);", _sqlConnectionObj);
					addAddrCmd.Parameters.Add(new SqlParameter("@Param1", addressToAdd.IPAddress.ToString()));
					addAddrCmd.Parameters.Add(new SqlParameter("@Param2", Convert.ToInt32(addressToAdd.Time)));
					addAddrCmd.Parameters.Add(new SqlParameter("@Param3", Convert.ToInt64(addressToAdd.Services)));
					addAddrCmd.Parameters.Add(new SqlParameter("@Param4", Convert.ToInt32(addressToAdd.Port)));

					if (addAddrCmd.ExecuteNonQuery() < 1)
					{
						return false;
					}
				}
			}
#if (!DEBUG)
			catch
			{
				return false;
			}
#else
			catch (Exception ex)
			{
				Console.WriteLine("Exception: " + ex.Message);
				if (ex.InnerException != null)
				{
					Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
				}

				return false;
			}
#endif

			//if address is already known to us we say true anyway
			//Pendulum - The Island Pt II (Dusk)
			return true;
		}

		public bool IsAddressKnown(PeerAddress addressToCheck)
		{
			try
			{
				if (!IsOpen)
				{
					OpenDBConnection();
				}

				uint currentTime = addressToCheck.Time;

				if (GetAddress(addressToCheck.IPAddress.ToString(), ref addressToCheck))
				{
					if (currentTime > addressToCheck.Time)
					{
						//newer time update
						UpdateAddressTime(addressToCheck, currentTime);
					}

					return true;
				}
			}
#if (!DEBUG)
			catch
			{

			}
#else
			catch (Exception ex)
			{
				Console.WriteLine("Exception: " + ex.Message);
				if (ex.InnerException != null)
				{
					Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
				}
			}
#endif

			return false;
		}

		public bool UpdateAddressTime(PeerAddress addressToUpdate, uint time)
		{
			try
			{
				if (!IsOpen)
				{
					OpenDBConnection();
				}

				SqlCommand updAddrCmd = new SqlCommand("UPDATE [AddressPool] SET [Time]=@Param1;", _sqlConnectionObj);
				updAddrCmd.CommandTimeout = 15000;
				updAddrCmd.Parameters.Add(new SqlParameter("@Param1", Convert.ToInt32(time)));
				if (updAddrCmd.ExecuteNonQuery() >= 1)
				{
					return true;
				}
			}
#if (!DEBUG)
			catch
			{

			}
#else
			catch (Exception ex)
			{
				Console.WriteLine("Exception: " + ex.Message);
				if (ex.InnerException != null)
				{
					Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
				}
			}
#endif

			return false;
		}

		private bool GetAddress(String ip, ref PeerAddress addressToGet)
		{
            try
			{
				if (!IsOpen)
				{
					OpenDBConnection();
				}

				SqlCommand getAddrCmd = new SqlCommand("SELECT * FROM [AddressPool] WHERE [IPAddress]=@Param1;", _sqlConnectionObj);
				getAddrCmd.Parameters.Add(new SqlParameter("@Param1", ip));
				SqlDataReader dataReader = getAddrCmd.ExecuteReader();

				if (dataReader.Read())
				{
					addressToGet = new PeerAddress(IPAddress.Parse(dataReader.GetString(0)), dataReader.GetInt32(3), Convert.ToUInt64(dataReader.GetInt64(2)), Convert.ToUInt32(dataReader.GetInt32(1)),Globals.ClientVersion, false);
					dataReader.Close();
					return true;
				}

				dataReader.Close();
			}
#if (!DEBUG)
			catch
			{

			}
#else
			catch (Exception ex)
			{
				Console.WriteLine("Exception: " + ex.Message);
				if (ex.InnerException != null)
				{
					Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
				}
			}
#endif

			return false;
		}

		public String ConnectionString
		{
			get
			{
				return _connectionString;
			}
			set
			{
				_connectionString = value;
			}
		}
	}
}
