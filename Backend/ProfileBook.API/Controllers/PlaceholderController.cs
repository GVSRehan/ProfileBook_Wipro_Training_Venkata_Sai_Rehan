using Microsoft.AspNetCore.Mvc;

namespace ProfileBook.API.Controllers
{
    public class PlaceholderController : Controller
    {
        [HttpGet("/placeholders")]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("/placeholders/login")]
        public IActionResult Login()
        {
            return View();
        }

        [HttpGet("/placeholders/register")]
        public IActionResult Register()
        {
            return View();
        }

        [HttpGet("/placeholders/posts")]
        public IActionResult Posts()
        {
            return View();
        }

        [HttpGet("/placeholders/messages")]
        public IActionResult Messages()
        {
            return View();
        }
    }
}
