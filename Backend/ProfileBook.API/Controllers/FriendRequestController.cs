using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ProfileBook.API.Data;
using ProfileBook.API.DTOs;
using ProfileBook.API.Helpers;
using ProfileBook.API.Models;

namespace ProfileBook.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FriendRequestController : ControllerBase
    {
        private readonly ProfileBookDbContext _context;

        public FriendRequestController(ProfileBookDbContext context)
        {
            _context = context;
        }

        [HttpPost("send")]
        public IActionResult SendRequest(SendFriendRequestDto dto)
        {
            var senderId = GetCurrentUserId();
            if (senderId == null)
                return Unauthorized("Invalid user identity");

            if (dto.ReceiverId == senderId.Value)
                return BadRequest("You cannot send a friend request to yourself");

            if (_context.Users.All(u => u.UserId != dto.ReceiverId || !u.IsActive))
                return NotFound("Receiver not found");

            var existingRequest = _context.FriendRequests.FirstOrDefault(fr =>
                (fr.SenderId == senderId.Value && fr.ReceiverId == dto.ReceiverId) ||
                (fr.SenderId == dto.ReceiverId && fr.ReceiverId == senderId.Value));

            if (existingRequest != null)
            {
                if (existingRequest.Status == "Accepted")
                    return BadRequest("You are already friends");

                if (existingRequest.Status == "Pending")
                    return BadRequest("A friend request already exists");
            }

            var request = new FriendRequest
            {
                SenderId = senderId.Value,
                ReceiverId = dto.ReceiverId,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.FriendRequests.Add(request);
            _context.SaveChanges();

            return Ok(new { success = true, friendRequestId = request.FriendRequestId, message = "Friend request sent" });
        }

        [HttpPost("respond/{requestId}")]
        public IActionResult RespondToRequest(int requestId, RespondFriendRequestDto dto)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized("Invalid user identity");

            var request = _context.FriendRequests.FirstOrDefault(fr => fr.FriendRequestId == requestId);
            if (request == null)
                return NotFound("Friend request not found");

            if (request.ReceiverId != currentUserId.Value)
                return StatusCode(StatusCodes.Status403Forbidden, "Only the receiver can respond to this request");

            if (request.Status != "Pending")
                return BadRequest("This friend request has already been handled");

            var action = dto.Action.Trim().ToLowerInvariant();
            if (action != "accept" && action != "reject")
                return BadRequest("Action must be accept or reject");

            request.Status = action == "accept" ? "Accepted" : "Rejected";
            request.RespondedAt = DateTime.UtcNow;
            _context.SaveChanges();

            return Ok(new { success = true, message = $"Friend request {request.Status.ToLowerInvariant()}" });
        }

        [HttpGet("mine")]
        public IActionResult GetMyRequests()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized("Invalid user identity");

            var incoming = _context.FriendRequests
                .Where(fr => fr.ReceiverId == currentUserId.Value)
                .Select(fr => new
                {
                    fr.FriendRequestId,
                    fr.SenderId,
                    senderUsername = _context.Users.Where(u => u.UserId == fr.SenderId).Select(u => u.Username).FirstOrDefault(),
                    senderProfileImage = _context.Users.Where(u => u.UserId == fr.SenderId).Select(u => u.ProfileImage).FirstOrDefault(),
                    fr.Status,
                    fr.CreatedAt,
                    fr.RespondedAt
                })
                .OrderByDescending(fr => fr.CreatedAt)
                .ToList();

            var outgoing = _context.FriendRequests
                .Where(fr => fr.SenderId == currentUserId.Value)
                .Select(fr => new
                {
                    fr.FriendRequestId,
                    fr.ReceiverId,
                    receiverUsername = _context.Users.Where(u => u.UserId == fr.ReceiverId).Select(u => u.Username).FirstOrDefault(),
                    receiverProfileImage = _context.Users.Where(u => u.UserId == fr.ReceiverId).Select(u => u.ProfileImage).FirstOrDefault(),
                    fr.Status,
                    fr.CreatedAt,
                    fr.RespondedAt
                })
                .OrderByDescending(fr => fr.CreatedAt)
                .ToList();

            return Ok(new { incoming, outgoing });
        }

        [HttpGet("friends")]
        public IActionResult GetFriends()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized("Invalid user identity");

            var acceptedRequests = _context.FriendRequests
                .Where(fr => fr.Status == "Accepted" &&
                             (fr.SenderId == currentUserId.Value || fr.ReceiverId == currentUserId.Value))
                .ToList();

            var friendIds = acceptedRequests
                .Select(fr => fr.SenderId == currentUserId.Value ? fr.ReceiverId : fr.SenderId)
                .Distinct()
                .ToList();

            var friends = _context.Users
                .Where(u => friendIds.Contains(u.UserId))
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.Email,
                    u.Role,
                    u.ProfileImage
                })
                .ToList();

            return Ok(friends);
        }

        private int? GetCurrentUserId()
        {
            return AuthHelper.GetCurrentUserId(User, _context, Request.Headers.Authorization);
        }
    }
}
