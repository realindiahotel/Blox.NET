using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;
using System.Net.Http;

namespace Blox.NET.Bipolar
{
	partial class HttpInterfaceService : ServiceBase
	{
		public HttpInterfaceService()
		{
			InitializeComponent();
		}

		protected override void OnStart(string[] args)
		{
			StartMe();
		}

		public void StartMe()
		{
			// TODO: Add code here to start your service.
			string baseAddress = "http://localhost:8334/";

			// Start OWIN host 
			using (WebApp.Start<Startup>(url: baseAddress))
			{
				// Create HttpCient and make a request to api/Bloxmanagement 
				HttpClient client = new HttpClient();

				var response = client.GetAsync(baseAddress + "api/Bloxmanagement").Result;

				Console.WriteLine(response);
				Console.WriteLine(response.Content.ReadAsStringAsync().Result);
			}

		}

		protected override void OnStop()
		{
			// TODO: Add code here to perform any tear-down necessary to stop your service.
		}
	}
}
