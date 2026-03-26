using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ProfileBook.API.Data;
using ProfileBook.API.DTOs;
using ProfileBook.API.Helpers;
using ProfileBook.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ProfileBook.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportController : ControllerBase
    {
        private readonly ProfileBookDbContext _context;

        public ReportController(ProfileBookDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public IActionResult ReportUser(CreateReportDto dto)
        {
            var reportingUserId = GetCurrentUserId();
            if (reportingUserId == null)
                return Unauthorized("Invalid user identity");

            if (!_context.Users.Any(u => u.UserId == dto.ReportedUserId && u.IsActive))
                return NotFound("Reported user not found");

            if (dto.ReportedUserId == reportingUserId.Value)
                return BadRequest("You cannot report yourself");

            var hasOpenReport = _context.Reports.Any(report =>
                report.ReportingUserId == reportingUserId.Value &&
                report.ReportedUserId == dto.ReportedUserId &&
                report.Status == "Open");

            if (hasOpenReport)
                return BadRequest(new { success = false, message = "You already have an open report for this user" });

            var report = new Report
            {
                ReportedUserId = dto.ReportedUserId,
                ReportingUserId = reportingUserId.Value,
                Reason = dto.Reason,
                TimeStamp = DateTime.UtcNow
            };

            _context.Reports.Add(report);
            _context.SaveChanges();

            return Ok(new
            {
                success = true,
                message = "User reported successfully",
                report
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetReports()
        {
            var reports = await (
                from report in _context.Reports.AsNoTracking()
                join reportedUser in _context.Users.AsNoTracking()
                    on report.ReportedUserId equals reportedUser.UserId into reportedUsers
                from reportedUser in reportedUsers.DefaultIfEmpty()
                join reportingUser in _context.Users.AsNoTracking()
                    on report.ReportingUserId equals reportingUser.UserId into reportingUsers
                from reportingUser in reportingUsers.DefaultIfEmpty()
                orderby report.TimeStamp descending
                select new
                {
                    report.ReportId,
                    report.ReportedUserId,
                    reportedUsername = reportedUser != null ? reportedUser.Username : null,
                    report.ReportingUserId,
                    reportingUsername = reportingUser != null ? reportingUser.Username : null,
                    report.Reason,
                    report.TimeStamp,
                    report.Status,
                    report.ActionTaken,
                    report.AdminNotes,
                    report.ResolvedAt,
                    report.ResolvedBy
                })
                .ToListAsync();

            return Ok(reports);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{reportId}/action")]
        public IActionResult TakeAction(int reportId, TakeReportActionDto dto)
        {
            var currentAdminId = GetCurrentUserId();
            if (currentAdminId == null)
                return Unauthorized(new { success = false, message = "Invalid admin identity" });

            var report = _context.Reports.FirstOrDefault(r => r.ReportId == reportId);
            if (report == null)
                return NotFound(new { success = false, message = "Report not found" });

            if (!string.Equals(report.Status, "Open", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "This report has already been resolved" });

            var reportedUser = _context.Users.FirstOrDefault(u => u.UserId == report.ReportedUserId);
            if (reportedUser == null)
                return NotFound(new { success = false, message = "Reported user not found" });

            var action = dto.Action.Trim().ToLowerInvariant();
            report.Status = "Resolved";
            report.ActionTaken = action;
            report.AdminNotes = dto.AdminNotes?.Trim();
            report.ResolvedAt = DateTime.UtcNow;
            report.ResolvedBy = _context.Users.Where(u => u.UserId == currentAdminId.Value).Select(u => u.Username).FirstOrDefault() ?? "Admin";

            if (action == "deactivate")
            {
                reportedUser.IsActive = false;
            }

            _context.UserNotifications.Add(new UserNotification
            {
                UserId = report.ReportingUserId,
                Type = "ReportAction",
                Title = "Report reviewed",
                Message = action == "dismiss"
                    ? $"Your report for user #{report.ReportedUserId} was reviewed and dismissed."
                    : $"Your report for user #{report.ReportedUserId} was reviewed. Action taken: {action}.",
                RelatedReportId = report.ReportId,
                CreatedAt = DateTime.UtcNow
            });

            _context.SaveChanges();

            return Ok(new
            {
                success = true,
                message = $"Report marked as resolved with action '{action}'"
            });
        }

        private int? GetCurrentUserId()
        {
            return AuthHelper.GetCurrentUserId(User, _context, Request.Headers.Authorization);
        }
    }
}
