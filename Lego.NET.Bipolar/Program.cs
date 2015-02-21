using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace Lego.NET.Bipolar
{
	static class Program
	{
		/// <summary>
		/// Am I bipolar? Probably......bleh
		/// NodeHostService is a Windows Service to be used to run Lego.NET as a full bitcoin node via Windows Service
		/// HttpInterfaceService is what it is, an OWIN Self Hosted Web API again as a Windows Service, we will use the HttpInterfaceService
		/// as command and control through the RESTful API and it will control NodeHostService, get stats, etc.
		/// </summary>
		static void Main()
		{
			//this little snippet lets us run the services as not services for debugging thanks to this chap http://www.codeproject.com/Articles/10153/Debugging-Windows-Services-under-Visual-Studio-NET
#if (!DEBUG)
			ServiceBase[] ServicesToRun;
			ServicesToRun = new ServiceBase[]
			{
				new NodeHostService(),
				new HttpInterfaceService()
			};
			ServiceBase.Run(ServicesToRun);
#else
			HttpInterfaceService httpService = new HttpInterfaceService();
			httpService.StartMe();
			NodeHostService nodeService = new NodeHostService();
			nodeService.StarteMe();

			//will sit here for ever till I stop debuging, this is ok because my P2PListener is on a thread of it's own so it persists listening for connections and spawing threads for connections :)
			System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);

#endif
		}
	}
}
