using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{

    public class TestController : BaseApiController
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok("Test controller is working!");
        }
    }
}