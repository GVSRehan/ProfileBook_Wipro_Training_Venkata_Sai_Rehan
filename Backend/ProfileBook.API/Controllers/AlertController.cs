using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ProfileBook.API.Data;
using ProfileBook.API.DTOs;
using ProfileBook.API.Helpers;
using ProfileBook.API.Hubs;
using ProfileBook.API.Models;

namespace ProfileBook.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AlertController : ControllerBase
    {
        private readonly ProfileBookDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;

        public AlertController(ProfileBookDbContext context, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpGet]
        public IActionResult GetAlerts()
        {
            var alerts = _context.AlertMessages
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.AlertMessageId,
                    a.AdminUserId,
                    adminUsername = _context.Users.Where(u => u.UserId == a.AdminUserId).Select(u => u.Username).FirstOrDefault(),
                    a.Content,
                    a.CreatedAt
                })
                .ToList();

            return Ok(alerts);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("broadcast")]
        public async Task<IActionResult> BroadcastAlert(BroadcastAlertDto dto)
        {
            var adminUserId = GetCurrentUserId();
            if (adminUserId == null)
                return Unauthorized("Invalid user identity");

            var admin = _context.Users.FirstOrDefault(u => u.UserId == adminUserId.Value && u.Role == "Admin");
            if (admin == null)
                return StatusCode(StatusCodes.Status403Forbidden, "Only admins can send alerts");

            var alert = new AlertMessage
            {
                AdminUserId = adminUserId.Value,
                Content = dto.Content,
                CreatedAt = DateTime.UtcNow
            };

            _context.AlertMessages.Add(alert);
            await _context.SaveChangesAsync();

            var createdAt = DateTime.UtcNow;
            var recipientIds = _context.Users
                .Where(user => user.IsActive && user.Role == "User")
                .Select(user => user.UserId)
                .ToList();

            if (recipientIds.Count > 0)
            {
                var notifications = recipientIds.Select(userId => new UserNotification
                {
                    UserId = userId,
                    Type = "alert",
                    Title = "Emergency alert",
                    Message = dto.Content.Trim(),
                    IsRead = false,
                    CreatedAt = createdAt
                }).ToList();

                _context.UserNotifications.AddRange(notifications);
                await _context.SaveChangesAsync();

                foreach (var notification in notifications)
                {
                    await _hubContext.Clients.Group($"User_{notification.UserId}").SendAsync("ReceiveAppNotification", new
                    {
                        notification.UserNotificationId,
                        notification.UserId,
                        notification.Type,
                        notification.Title,
                        notification.Message,
                        notification.IsRead,
                        notification.CreatedAt,
                        notification.RelatedPostId,
                        notification.RelatedReportId
                    });
                }
            }

            return Ok(new { success = true, alertMessageId = alert.AlertMessageId, message = "Emergency alert broadcast successfully" });
        }

        private int? GetCurrentUserId()
        {
            return AuthHelper.GetCurrentUserId(User, _context, Request.Headers.Authorization);
        }
    }
}
