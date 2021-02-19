using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BasingApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BasingApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BasingController : ControllerBase
    {
    
        /// <summary>
        /// Basing Test
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult FetchData()
        {
            return Ok(JsonConvert.SerializeObject(TickerUpdater.CurrentData));
        }
    }
}
