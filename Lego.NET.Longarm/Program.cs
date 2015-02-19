using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace Lego.NET.Longarm
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		static void Main()
		{
#if (!DEBUG)
			ServiceBase[] ServicesToRun;
			ServicesToRun = new ServiceBase[]
			{
				new LegoService(),
				new LegoMonitorService()
			};
			ServiceBase.Run(ServicesToRun);
#else
			LegoService service = new LegoService();
			service.Start();

			System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
#endif


		}
	}
}
