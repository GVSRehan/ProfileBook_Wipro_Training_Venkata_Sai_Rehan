using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ProfileBook.API.Data;
using ProfileBook.API.Helpers;
using ProfileBook.API.Models;

namespace ProfileBook.API.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ProfileBookDbContext _context;

        public ChatHub(ProfileBookDbContext context)
        {
            _context = context;
        }

        public async Task SendMessage(int receiverId, string message)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                throw new HubException("Invalid sender identity");
            }

            var senderId = currentUserId.Value;

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new HubException("Message cannot be empty");
            }

            if (!AreFriends(senderId, receiverId))
            {
                throw new HubException("Messages can only be sent to accepted friends");
            }

            var msg = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                MessageContent = message.Trim(),
                TimeStamp = DateTime.UtcNow,
                IsRead = false
            };

            _context.Messages.Add(msg);
            await _context.SaveChangesAsync();

            await Clients.Group($"User_{receiverId}").SendAsync("ReceiveMessage", msg);
            await Clients.Group($"User_{senderId}").SendAsync("ReceiveMessage", msg);
        }

        public async Task SendTyping(int receiverId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                throw new HubException("Invalid sender identity");
            }

            var senderId = currentUserId.Value;
            if (!AreFriends(senderId, receiverId))
            {
                throw new HubException("Typing indicators are only available between accepted friends");
            }

            var senderUsername = _context.Users
                .Where(u => u.UserId == senderId)
                .Select(u => u.Username)
                .FirstOrDefault() ?? $"User {senderId}";

            await Clients.Group($"User_{receiverId}").SendAsync("ReceiveTyping", new
            {
                senderId,
                receiverId,
                senderUsername
            });
        }

        public async Task SendGroupMessage(int groupId, string message)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                throw new HubException("Invalid sender identity");
            }

            var senderId = currentUserId.Value;
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new HubException("Message cannot be empty");
            }

            var memberIds = GetGroupMemberIds(groupId);
            if (!memberIds.Contains(senderId))
            {
                throw new HubException("You can only message groups you belong to");
            }

            var msg = new Message
            {
                SenderId = senderId,
                ReceiverId = null,
                GroupId = groupId,
                MessageContent = message.Trim(),
                TimeStamp = DateTime.UtcNow,
                IsRead = false
            };

            _context.Messages.Add(msg);
            await _context.SaveChangesAsync();

            foreach (var memberId in memberIds)
            {
                await Clients.Group($"User_{memberId}").SendAsync("ReceiveMessage", msg);
            }
        }

        public override async Task OnConnectedAsync()
        {
            var userId = ResolveCurrentUserIdFromConnection();
            if (userId != null)
            {
                Context.Items["ResolvedUserId"] = userId.Value;
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId.Value}");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetCurrentUserId();
            if (userId != null)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId.Value}");
            }
            await base.OnDisconnectedAsync(exception);
        }

        private bool AreFriends(int userId1, int userId2)
        {
            return _context.FriendRequests.Any(fr =>
                fr.Status == "Accepted" &&
                ((fr.SenderId == userId1 && fr.ReceiverId == userId2) ||
                 (fr.SenderId == userId2 && fr.ReceiverId == userId1)));
        }

        private int? GetCurrentUserId()
        {
            if (Context.Items.TryGetValue("ResolvedUserId", out var cachedUserId) && cachedUserId is int resolvedUserId)
            {
                return resolvedUserId;
            }

            return ResolveCurrentUserIdFromConnection();
        }

        private int? ResolveCurrentUserIdFromConnection()
        {
            var claimCandidates = new[]
            {
                Context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value,
                Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                Context.UserIdentifier
            };

            foreach (var candidate in claimCandidates)
            {
                if (int.TryParse(candidate, out var userId))
                {
                    return userId;
                }
            }

            var accessToken = Context.GetHttpContext()?.Request.Query["access_token"].ToString();
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                var token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
                foreach (var claim in token.Claims)
                {
                    if ((claim.Type == ClaimTypes.NameIdentifier ||
                         claim.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" ||
                         claim.Type == "nameid") &&
                        int.TryParse(claim.Value, out var tokenUserId))
                    {
                        return tokenUserId;
                    }
                }

                var email = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == "sub")?.Value;
                if (!string.IsNullOrWhiteSpace(email))
                {
                    var normalizedEmail = IdentityNormalizer.NormalizeEmail(email);
                    return _context.Users
                        .Where(u => u.Email.Trim().ToLower() == normalizedEmail)
                        .Select(u => (int?)u.UserId)
                        .FirstOrDefault();
                }
            }

            return AuthHelper.GetCurrentUserId(Context.User, _context, accessToken);
        }

        private List<int> GetGroupMemberIds(int groupId)
        {
            var serialized = _context.Groups
                .Where(group => group.GroupId == groupId)
                .Select(group => group.GroupMembers)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(serialized))
            {
                return new List<int>();
            }

            return serialized
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var userId) ? (int?)userId : null)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToList();
        }
    }
}
