using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using ProfileBook.API.DTOs;
using ProfileBook.API.Services.Interfaces;

namespace ProfileBook.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _service;

        public AdminController(IAdminService service)
        {
            _service = service;
        }

        [HttpGet("dashboard")]
        public IActionResult GetDashboardStats()
        {
            return Ok(_service.GetDashboardStats());
        }

        [HttpGet("users")]
        public IActionResult GetUsers()
        {
            var mainAdminCheck = EnsureMainAdmin();
            if (mainAdminCheck != null)
                return mainAdminCheck;

            return Ok(_service.GetUsers());
        }

        [HttpGet("profile/{userId}")]
        public IActionResult GetAdminProfile(int userId)
        {
            try
            {
                return Ok(_service.GetAdminProfile(userId));
            }
            catch (Exception ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("users/{id}")]
        public IActionResult DeleteUser(int id)
        {
            var mainAdminCheck = EnsureMainAdmin();
            if (mainAdminCheck != null)
                return mainAdminCheck;

            try
            {
                var result = _service.DeleteUser(id);
                return Ok(new { success = true, message = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("users/{id}")]
        public IActionResult UpdateUser(int id, UpdateUserDto dto)
        {
            var mainAdminCheck = EnsureMainAdmin();
            if (mainAdminCheck != null)
                return mainAdminCheck;

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { success = false, message = string.Join("; ", errors) });
            }

            try
            {
                return Ok(_service.UpdateUser(id, dto));
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("create-admin")]
        public IActionResult CreateAdmin(CreateAdminDto dto)
        {
            var mainAdminCheck = EnsureMainAdmin();
            if (mainAdminCheck != null)
                return mainAdminCheck;

            // Check for model validation errors
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { success = false, message = string.Join("; ", errors) });
            }

            try
            {
                return Ok(_service.CreateAdmin(dto));
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("create-user")]
        public IActionResult CreateUser(CreateAdminDto dto)
        {
            var mainAdminCheck = EnsureMainAdmin();
            if (mainAdminCheck != null)
                return mainAdminCheck;

            // Check for model validation errors
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { success = false, message = string.Join("; ", errors) });
            }

            try
            {
                return Ok(_service.CreateUser(dto));
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("extend-credentials/{userId}")]
        public IActionResult ExtendCredentials(int userId, [FromBody] int additionalMinutes)
        {
            var mainAdminCheck = EnsureMainAdmin();
            if (mainAdminCheck != null)
                return mainAdminCheck;

            try
            {
                return Ok(_service.ExtendCredentials(userId, additionalMinutes));
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("expiring-credentials")]
        public IActionResult GetExpiringCredentials([FromQuery] int daysAhead = 7)
        {
            var mainAdminCheck = EnsureMainAdmin();
            if (mainAdminCheck != null)
                return mainAdminCheck;

            return Ok(_service.GetExpiringCredentials(daysAhead));
        }

        [HttpGet("expired-credentials")]
        public IActionResult GetExpiredCredentials()
        {
            var mainAdminCheck = EnsureMainAdmin();
            if (mainAdminCheck != null)
                return mainAdminCheck;

            return Ok(_service.GetExpiredCredentials());
        }

        [HttpPost("deactivate-expired")]
        public IActionResult DeactivateExpiredCredentials()
        {
            var mainAdminCheck = EnsureMainAdmin();
            if (mainAdminCheck != null)
                return mainAdminCheck;

            try
            {
                return Ok(_service.DeactivateExpiredCredentials());
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("set-main-admin/{userId}")]
        public IActionResult SetMainAdmin(int userId)
        {
            var mainAdminCheck = EnsureMainAdmin();
            if (mainAdminCheck != null)
                return mainAdminCheck;

            try
            {
                return Ok(_service.SetMainAdmin(userId));
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("duration-options")]
        public IActionResult GetDurationOptions()
        {
            var durations = new[]
            {
                new { value = 1440, label = "1 Day" },
                new { value = 4320, label = "3 Days" },
                new { value = 10080, label = "1 Week" },
                new { value = 20160, label = "2 Weeks" },
                new { value = 43200, label = "1 Month" },
                new { value = 129600, label = "3 Months" },
                new { value = 259200, label = "6 Months" },
                new { value = 525600, label = "1 Year" }
            };
            return Ok(durations);
        }

        private IActionResult? EnsureMainAdmin()
        {
            var mainAdminClaim = User.FindFirstValue("is_main_admin");
            if (string.Equals(mainAdminClaim, "true", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
            if (int.TryParse(userIdClaim, out var userId))
            {
                if (_service.IsMainAdmin(userId))
                {
                    return null;
                }
            }

            var emailClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue("sub");

            if (!string.IsNullOrWhiteSpace(emailClaim) && _service.IsMainAdmin(emailClaim))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(userIdClaim) && string.IsNullOrWhiteSpace(emailClaim))
            {
                return Unauthorized(new { success = false, message = "Invalid user identity." });
            }

            if (!string.IsNullOrWhiteSpace(emailClaim) || !string.IsNullOrWhiteSpace(userIdClaim))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Only the main admin can access this resource." });
            }

            return Unauthorized(new { success = false, message = "Invalid user identity." });
        }
    }
}
