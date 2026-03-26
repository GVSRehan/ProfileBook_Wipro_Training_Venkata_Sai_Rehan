using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ProfileBook.API.Data;
using ProfileBook.API.DTOs;
using ProfileBook.API.Helpers;
using ProfileBook.API.Hubs;
using ProfileBook.API.Models;
using ProfileBook.API.Patterns.Factory;
using ProfileBook.API.Patterns.Observer;

namespace ProfileBook.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MessageController : ControllerBase
    {
        private readonly ProfileBookDbContext _context;
        private readonly NotificationSubject _subject;
        private readonly IHubContext<ChatHub> _hubContext;

        public MessageController(ProfileBookDbContext context, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;

            _subject = new NotificationSubject();
            _subject.Attach(new NotificationService());
        }

        [HttpPost]
        public IActionResult SendMessage(SendMessageDto dto)
        {
            var senderId = GetCurrentUserId();
            if (senderId == null)
                return Unauthorized("Invalid user identity");

            if (dto.GroupId.HasValue)
            {
                if (!IsUserInGroup(senderId.Value, dto.GroupId.Value))
                    return StatusCode(StatusCodes.Status403Forbidden, "You can only message groups you belong to");

                var groupMessage = new Message
                {
                    SenderId = senderId.Value,
                    ReceiverId = null,
                    GroupId = dto.GroupId.Value,
                    MessageContent = dto.MessageContent,
                    TimeStamp = DateTime.UtcNow
                };

                _context.Messages.Add(groupMessage);
                _context.SaveChanges();

                var memberIds = GetGroupMemberIds(dto.GroupId.Value);
                foreach (var memberId in memberIds)
                {
                    _hubContext.Clients.Group($"User_{memberId}").SendAsync("ReceiveMessage", groupMessage);
                }

                var groupNotification = NotificationFactory.CreateNotification("message");
                _subject.Notify(groupNotification);

                return Ok(groupMessage);
            }

            if (!AreFriends(senderId.Value, dto.ReceiverId))
                return StatusCode(StatusCodes.Status403Forbidden, "Messages can only be sent to accepted friends");

            var message = new Message
            {
                SenderId = senderId.Value,
                ReceiverId = dto.ReceiverId,
                MessageContent = dto.MessageContent,
                TimeStamp = DateTime.UtcNow
            };

            _context.Messages.Add(message);
            _context.SaveChanges();

            _hubContext.Clients.Group($"User_{dto.ReceiverId}").SendAsync("ReceiveMessage", message);
            _hubContext.Clients.Group($"User_{senderId.Value}").SendAsync("ReceiveMessage", message);

            var notification = NotificationFactory.CreateNotification("message");
            _subject.Notify(notification);

            return Ok(message);
        }

        [HttpGet]
        public IActionResult GetMessages()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized("Invalid user identity");

            var accessibleGroupIds = GetAccessibleGroupIds(currentUserId.Value);
            var messages = _context.Messages
                .Where(m =>
                    m.SenderId == currentUserId.Value ||
                    m.ReceiverId == currentUserId.Value ||
                    (m.GroupId.HasValue && accessibleGroupIds.Contains(m.GroupId.Value)))
                .OrderByDescending(m => m.TimeStamp)
                .ToList();

            return Ok(messages);
        }

        [HttpPut("read/{friendId}")]
        public IActionResult MarkConversationAsRead(int friendId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized("Invalid user identity");

            if (!AreFriends(currentUserId.Value, friendId))
                return StatusCode(StatusCodes.Status403Forbidden, "Messages can only be accessed between accepted friends");

            var unreadMessages = _context.Messages
                .Where(m => m.SenderId == friendId && m.ReceiverId == currentUserId.Value && !m.IsRead)
                .ToList();

            if (unreadMessages.Count == 0)
                return Ok(new { success = true, updatedCount = 0 });

            var readAt = DateTime.UtcNow;
            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
                message.ReadAt = readAt;
            }

            _context.SaveChanges();

            var readerUsername = _context.Users
                .Where(u => u.UserId == currentUserId.Value)
                .Select(u => u.Username)
                .FirstOrDefault() ?? $"User {currentUserId.Value}";

            _hubContext.Clients.Group($"User_{friendId}").SendAsync("MessagesRead", new
            {
                readerId = currentUserId.Value,
                readerUsername,
                conversationUserId = friendId,
                readAt
            });

            return Ok(new { success = true, updatedCount = unreadMessages.Count, readAt });
        }

        private bool AreFriends(int userId1, int userId2)
        {
            return _context.FriendRequests.Any(fr =>
                fr.Status == "Accepted" &&
                ((fr.SenderId == userId1 && fr.ReceiverId == userId2) ||
                 (fr.SenderId == userId2 && fr.ReceiverId == userId1)));
        }

        private bool IsUserInGroup(int userId, int groupId)
        {
            return GetGroupMemberIds(groupId).Contains(userId);
        }

        private List<int> GetAccessibleGroupIds(int userId)
        {
            return _context.Groups
                .ToList()
                .Where(group => ParseGroupMembers(group.GroupMembers).Contains(userId))
                .Select(group => group.GroupId)
                .ToList();
        }

        private List<int> GetGroupMemberIds(int groupId)
        {
            var serialized = _context.Groups
                .Where(group => group.GroupId == groupId)
                .Select(group => group.GroupMembers)
                .FirstOrDefault();

            return ParseGroupMembers(serialized);
        }

        private static List<int> ParseGroupMembers(string? serialized)
        {
            if (string.IsNullOrWhiteSpace(serialized))
                return new List<int>();

            return serialized
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var userId) ? (int?)userId : null)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToList();
        }

        private int? GetCurrentUserId()
        {
            return AuthHelper.GetCurrentUserId(User, _context, Request.Headers.Authorization);
        }
    }
}
