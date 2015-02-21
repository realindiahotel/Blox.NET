using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Lego.NET.Bipolar
{
	public class LegoManagementController : ApiController
	{
		// GET api/legomanagement
		public IEnumerable<string> Get()
		{
			return new string[] { "value1", "value2" };
		}

		// GET api/legomanagement/5 
		public string Get(int id)
		{
			return "value";
		}

		// POST api/legomanagement 
		public void Post([FromBody]string value)
		{
		}

		// PUT api/legomanagement/5 
		public void Put(int id, [FromBody]string value)
		{
		}

		// DELETE api/legomanagement/5 
		public void Delete(int id)
		{
		}
	}

}
