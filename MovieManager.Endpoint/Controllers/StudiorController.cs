using Microsoft.AspNetCore.Mvc;
using MovieManager.BusinessLogic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MovieManager.Endpoint.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StudioController : Controller
    {
        private string notFoundMessage = "No studios found!";
        private string badRequestMessage = "Value cannot be null!";
        private StudioService _studioService;

        public StudioController(StudioService studioService)
        {
            _studioService = studioService;
        }

        [HttpGet]
        [Route("/studio/names")]
        public ActionResult GetAllNames()
        {
            var directors = _studioService.GetUniqueStudios();
            if (directors.Count > 0)
            {
                return Ok(directors);
            }
            return NotFound(notFoundMessage);
        }
    }
}
