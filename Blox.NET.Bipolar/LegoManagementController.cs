using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Blox.NET.Bipolar
{
	public class BloxManagementController : ApiController
	{
		// GET api/Bloxmanagement
		public IEnumerable<string> Get()
		{
			return new string[] { "value1", "value2" };
		}

		// GET api/Bloxmanagement/5 
		public string Get(int id)
		{
			return "value";
		}

		// POST api/Bloxmanagement 
		public void Post([FromBody]string value)
		{
		}

		// PUT api/Bloxmanagement/5 
		public void Put(int id, [FromBody]string value)
		{
		}

		// DELETE api/Bloxmanagement/5 
		public void Delete(int id)
		{
		}
	}

}
