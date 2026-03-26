using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProfileBook.API.Data;
using ProfileBook.API.Helpers;

namespace ProfileBook.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserNotificationController : ControllerBase
    {
        private readonly ProfileBookDbContext _context;

        public UserNotificationController(ProfileBookDbContext context)
        {
            _context = context;
        }

        [HttpGet("mine")]
        public IActionResult GetMine()
        {
            var currentUserId = AuthHelper.GetCurrentUserId(User, _context, Request.Headers.Authorization);
            if (currentUserId == null)
                return Unauthorized(new { success = false, message = "Invalid user identity" });

            var notifications = _context.UserNotifications
                .Where(n => n.UserId == currentUserId.Value)
                .OrderByDescending(n => n.CreatedAt)
                .Take(25)
                .ToList();

            return Ok(notifications);
        }

        [HttpPut("read-all")]
        public IActionResult MarkAllAsRead()
        {
            var currentUserId = AuthHelper.GetCurrentUserId(User, _context, Request.Headers.Authorization);
            if (currentUserId == null)
                return Unauthorized(new { success = false, message = "Invalid user identity" });

            var notifications = _context.UserNotifications
                .Where(n => n.UserId == currentUserId.Value && !n.IsRead)
                .ToList();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            _context.SaveChanges();

            return Ok(new { success = true, markedCount = notifications.Count });
        }
    }
}
