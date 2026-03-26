using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProfileBook.API.Data;
using ProfileBook.API.DTOs;
using ProfileBook.API.Helpers;
using ProfileBook.API.Models;

namespace ProfileBook.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GroupController : ControllerBase
    {
        private readonly ProfileBookDbContext _context;

        public GroupController(ProfileBookDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetGroups()
        {
            var mainAdminCheck = EnsureMainAdmin();
            if (mainAdminCheck != null)
                return mainAdminCheck;

            var groups = _context.Groups
                .OrderByDescending(g => g.CreatedAt)
                .ToList()
                .Select(g => new
                {
                    g.GroupId,
                    g.GroupName,
                    memberUserIds = ParseMemberIds(g.GroupMembers),
                    g.CreatedAt,
                    g.CreatedBy
                });

            return Ok(groups);
        }

        [HttpGet("mine")]
        public IActionResult GetMyGroups()
        {
            var currentUserId = AuthHelper.GetCurrentUserId(User, _context, Request.Headers.Authorization);
            if (currentUserId == null)
                return Unauthorized(new { success = false, message = "Invalid user identity" });

            var groups = _context.Groups
                .OrderByDescending(g => g.CreatedAt)
                .ToList()
                .Where(g => ParseMemberIds(g.GroupMembers).Contains(currentUserId.Value))
                .Select(g => new
                {
                    g.GroupId,
                    g.GroupName,
                    memberUserIds = ParseMemberIds(g.GroupMembers),
                    g.CreatedAt,
                    g.CreatedBy
                });

            return Ok(groups);
        }

        [HttpPost]
        public IActionResult CreateGroup(CreateGroupDto dto)
        {
            var mainAdminCheck = EnsureMainAdmin();
            if (mainAdminCheck != null)
                return mainAdminCheck;

            if (_context.Groups.Any(g => g.GroupName == dto.GroupName.Trim()))
                return BadRequest(new { success = false, message = "Group name already exists" });

            var validMemberIds = _context.Users
                .Where(u => u.Role == "User" && dto.MemberUserIds.Contains(u.UserId))
                .Select(u => u.UserId)
                .Distinct()
                .ToList();

            var group = new Group
            {
                GroupName = dto.GroupName.Trim(),
                GroupMembers = string.Join(",", validMemberIds),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "Admin"
            };

            _context.Groups.Add(group);
            _context.SaveChanges();

            return Ok(new { success = true, message = "Group created successfully", groupId = group.GroupId });
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteGroup(int id)
        {
            var mainAdminCheck = EnsureMainAdmin();
            if (mainAdminCheck != null)
                return mainAdminCheck;

            var group = _context.Groups.Find(id);
            if (group == null)
                return NotFound(new { success = false, message = "Group not found" });

            _context.Groups.Remove(group);
            _context.SaveChanges();

            return Ok(new { success = true, message = "Group deleted successfully" });
        }

        private IActionResult? EnsureMainAdmin()
        {
            var currentUserId = AuthHelper.GetCurrentUserId(User, _context, Request.Headers.Authorization);
            if (currentUserId == null)
                return Unauthorized(new { success = false, message = "Invalid user identity" });

            var user = _context.Users.FirstOrDefault(u => u.UserId == currentUserId.Value);
            if (user?.IsMainAdmin == true)
                return null;

            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Only the main admin can manage groups." });
        }

        private static List<int> ParseMemberIds(string? serialized)
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
    }
}
