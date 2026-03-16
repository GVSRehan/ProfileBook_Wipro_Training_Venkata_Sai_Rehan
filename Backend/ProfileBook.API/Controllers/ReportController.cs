using Microsoft.AspNetCore.Mvc;
using ProfileBook.API.Data;
using ProfileBook.API.Models;

namespace ProfileBook.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportController : ControllerBase
    {
        private readonly ProfileBookDbContext _context;

        public ReportController(ProfileBookDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public IActionResult ReportUser(Report report)
        {
            _context.Reports.Add(report);
            _context.SaveChanges();

            return Ok(report);
        }

        [HttpGet]
        public IActionResult GetReports()
        {
            return Ok(_context.Reports.ToList());
        }
    }
}