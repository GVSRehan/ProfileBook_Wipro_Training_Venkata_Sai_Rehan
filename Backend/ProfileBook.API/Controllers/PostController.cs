using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ProfileBook.API.Data;
using ProfileBook.API.DTOs;
using ProfileBook.API.Helpers;
using ProfileBook.API.Models;
using ProfileBook.API.Patterns.Factory;
using ProfileBook.API.Patterns.Observer;
using Microsoft.AspNetCore.SignalR;
using ProfileBook.API.Hubs;

namespace ProfileBook.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PostController : ControllerBase
    {
        private readonly ProfileBookDbContext _context;
        private readonly NotificationSubject _subject;
        private readonly IHubContext<ChatHub> _hubContext;

        public PostController(ProfileBookDbContext context, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;

            // Observer Pattern
            _subject = new NotificationSubject();
            _subject.Attach(new NotificationService());
        }

        // =====================================
        // CREATE POST (USER)
        // =====================================
        [HttpPost]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> CreatePost([FromForm] CreatePostDto dto)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized("Invalid user identity");

            if (dto == null)
                return BadRequest("Invalid post");

            var postImagePath = dto.PostImage;
            if (dto.PostImageFile is { Length: > 0 })
            {
                var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "posts");
                Directory.CreateDirectory(uploadsRoot);

                var safeFileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.PostImageFile.FileName)}";
                var filePath = Path.Combine(uploadsRoot, safeFileName);

                await using var stream = new FileStream(filePath, FileMode.Create);
                await dto.PostImageFile.CopyToAsync(stream);

                postImagePath = $"/uploads/posts/{safeFileName}";
            }

            var post = new Post
            {
                UserId = currentUserId.Value,
                Content = dto.Content,
                PostImage = postImagePath,
                Status = "Pending"
            };

            post.Status = "Pending";

            await _context.Posts.AddAsync(post);
            await _context.SaveChangesAsync();

            // Factory Pattern Notification
            var notification = NotificationFactory.CreateNotification("post");
            _subject.Notify(notification);

            await NotifyAdminsAboutNewPendingPostAsync(post);

            return Ok(post);
        }

        // =====================================
        // GET APPROVED POSTS (USER FEED)
        // =====================================
        [HttpGet]
        public async Task<IActionResult> GetPosts([FromQuery] string? search = null)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized("Invalid user identity");

            var posts = await BuildApprovedPostFeedAsync(currentUserId.Value, search);

            return Ok(posts);
        }

        // =====================================
        // USER - TOGGLE LIKE
        // =====================================
        [HttpPost("{id}/like")]
        public async Task<IActionResult> ToggleLike(int id)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized("Invalid user identity");

            var post = await GetApprovedPostAsync(id, currentUserId.Value);
            if (post == null)
                return NotFound(new { success = false, message = "Post not found or not visible to you" });

            var existingLike = await _context.PostLikes
                .FirstOrDefaultAsync(postLike => postLike.PostId == id && postLike.UserId == currentUserId.Value);

            var isLiked = existingLike == null;
            if (existingLike == null)
            {
                _context.PostLikes.Add(new PostLike
                {
                    PostId = id,
                    UserId = currentUserId.Value
                });
            }
            else
            {
                _context.PostLikes.Remove(existingLike);
            }

            await _context.SaveChangesAsync();

            if (isLiked && post.UserId != currentUserId.Value)
            {
                var likerUsername = await GetUsernameAsync(currentUserId.Value);
                await CreatePostActivityNotificationAsync(
                    post.UserId,
                    post.PostId,
                    "PostLiked",
                    "New like",
                    $"{likerUsername} liked your post.");
            }

            var likeCount = await _context.PostLikes.CountAsync(postLike => postLike.PostId == id);

            return Ok(new
            {
                success = true,
                postId = id,
                liked = isLiked,
                likeCount
            });
        }

        // =====================================
        // USER - ADD COMMENT
        // =====================================
        [HttpPost("{id}/comments")]
        public async Task<IActionResult> AddComment(int id, [FromBody] CreatePostCommentDto dto)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized("Invalid user identity");

            var post = await GetApprovedPostAsync(id, currentUserId.Value);
            if (post == null)
                return NotFound(new { success = false, message = "Post not found or not visible to you" });

            var commentText = dto?.CommentText?.Trim();
            if (string.IsNullOrWhiteSpace(commentText))
                return BadRequest(new { success = false, message = "Comment cannot be empty" });

            var comment = new PostComment
            {
                PostId = id,
                UserId = currentUserId.Value,
                CommentText = commentText
            };

            _context.PostComments.Add(comment);
            await _context.SaveChangesAsync();

            var commenterUsername = await GetUsernameAsync(currentUserId.Value);
            if (post.UserId != currentUserId.Value)
            {
                await CreatePostActivityNotificationAsync(
                    post.UserId,
                    post.PostId,
                    "PostCommented",
                    "New comment",
                    $"{commenterUsername} commented on your post.");
            }

            var commentCount = await _context.PostComments.CountAsync(postComment => postComment.PostId == id);

            return Ok(new
            {
                success = true,
                comment.PostCommentId,
                comment.PostId,
                comment.UserId,
                username = commenterUsername,
                comment.CommentText,
                comment.CreatedAt,
                commentCount
            });
        }

        // =====================================
        // USER - SHARE POST TO FRIEND
        // =====================================
        [HttpPost("{id}/share")]
        public async Task<IActionResult> SharePost(int id, [FromBody] SharePostDto dto)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized("Invalid user identity");

            var post = await GetApprovedPostAsync(id, currentUserId.Value);
            if (post == null)
                return NotFound(new { success = false, message = "Post not found or not visible to you" });

            if (dto == null || dto.RecipientUserId <= 0)
                return BadRequest(new { success = false, message = "Recipient is required" });

            if (dto.RecipientUserId == currentUserId.Value)
                return BadRequest(new { success = false, message = "You cannot share a post with yourself" });

            var recipientExists = await _context.Users.AnyAsync(user => user.UserId == dto.RecipientUserId && user.IsActive);
            if (!recipientExists)
                return NotFound(new { success = false, message = "Recipient not found" });

            if (!AreFriends(currentUserId.Value, dto.RecipientUserId))
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Posts can only be shared with accepted friends" });

            var postAuthorUsername = await GetUsernameAsync(post.UserId);
            var senderUsername = await GetUsernameAsync(currentUserId.Value);

            var share = new PostShare
            {
                PostId = id,
                SenderUserId = currentUserId.Value,
                RecipientUserId = dto.RecipientUserId
            };

            var message = new Message
            {
                SenderId = currentUserId.Value,
                ReceiverId = dto.RecipientUserId,
                MessageContent = BuildSharedPostMessage(post, postAuthorUsername),
                TimeStamp = DateTime.UtcNow
            };

            _context.PostShares.Add(share);
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            await _hubContext.Clients.Group($"User_{dto.RecipientUserId}").SendAsync("ReceiveMessage", message);
            await _hubContext.Clients.Group($"User_{currentUserId.Value}").SendAsync("ReceiveMessage", message);

            if (post.UserId != currentUserId.Value)
            {
                await CreatePostActivityNotificationAsync(
                    post.UserId,
                    post.PostId,
                    "PostShared",
                    "Post shared",
                    $"{senderUsername} shared your post.");
            }

            var shareCount = await _context.PostShares.CountAsync(postShare => postShare.PostId == id);

            return Ok(new
            {
                success = true,
                message = "Post shared successfully",
                postId = id,
                shareCount
            });
        }

        // =====================================
        // ADMIN - GET ALL POSTS
        // =====================================
        [Authorize(Roles = "Admin")]
        [HttpGet("all")]
        public async Task<IActionResult> GetAllPosts([FromQuery] string? search = null)
        {
            var normalizedSearch = search?.Trim().ToLowerInvariant();
            var posts = await _context.Posts
                .Where(p => string.IsNullOrWhiteSpace(normalizedSearch) ||
                            p.Content.ToLower().Contains(normalizedSearch) ||
                            _context.Users.Where(u => u.UserId == p.UserId)
                                .Select(u => (u.Username ?? string.Empty).ToLower())
                                .FirstOrDefault()!.Contains(normalizedSearch))
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            return Ok(posts);
        }

        // =====================================
        // ADMIN - GET PENDING POSTS
        // =====================================
        [Authorize(Roles = "Admin")]
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingPosts()
        {
            var posts = await _context.Posts
                .Where(p => p.Status != null && p.Status.ToLower() == "pending")
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return Ok(posts);
        }

        // =====================================
        // ADMIN - APPROVE POST
        // =====================================
        [Authorize(Roles = "Admin")]
        [HttpPut("approve/{id}")]
        public async Task<IActionResult> ApprovePost(int id)
        {
            var reviewedAt = DateTime.UtcNow;
            var affectedRows = await _context.Posts
                .Where(post => post.PostId == id && post.Status == "Pending")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(post => post.Status, "Approved")
                    .SetProperty(post => post.ReviewedAt, reviewedAt)
                    .SetProperty(post => post.ReviewedBy, "Admin")
                );

            if (affectedRows == 0)
            {
                var existingPost = await _context.Posts.FindAsync(id);
                if (existingPost == null)
                    return NotFound(new { success = false, message = "Post not found" });

                return Conflict(new
                {
                    success = false,
                    message = $"This post was already reviewed and is currently '{existingPost.Status}'."
                });
            }

            var post = await _context.Posts.FindAsync(id);
            if (post == null)
                return NotFound(new { success = false, message = "Post not found" });

            await CreatePostDecisionNotificationAsync(
                post.UserId,
                post.PostId,
                "PostApproved",
                "Post approved",
                "Your post was approved by the admin.");

            var notification = NotificationFactory.CreateNotification("approve");
            _subject.Notify(notification);

            return Ok(new { success = true, message = "Post approved successfully", post });
        }

        // =====================================
        // ADMIN - REJECT POST
        // =====================================
        [Authorize(Roles = "Admin")]
        [HttpPut("reject/{id}")]
        public async Task<IActionResult> RejectPost(int id, [FromBody] RejectPostRequest request)
        {
            var rejectionReason = request?.Reason ?? "Post does not meet community guidelines";
            var reviewedAt = DateTime.UtcNow;
            var affectedRows = await _context.Posts
                .Where(post => post.PostId == id && post.Status == "Pending")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(post => post.Status, "Rejected")
                    .SetProperty(post => post.ReviewedAt, reviewedAt)
                    .SetProperty(post => post.ReviewedBy, "Admin")
                    .SetProperty(post => post.RejectionReason, rejectionReason)
                );

            if (affectedRows == 0)
            {
                var existingPost = await _context.Posts.FindAsync(id);
                if (existingPost == null)
                    return NotFound(new { success = false, message = "Post not found" });

                return Conflict(new
                {
                    success = false,
                    message = $"This post was already reviewed and is currently '{existingPost.Status}'."
                });
            }

            var post = await _context.Posts.FindAsync(id);
            if (post == null)
                return NotFound(new { success = false, message = "Post not found" });

            await CreatePostDecisionNotificationAsync(
                post.UserId,
                post.PostId,
                "PostRejected",
                "Post rejected",
                $"Your post was rejected. Reason: {post.RejectionReason}");

            var notification = NotificationFactory.CreateNotification("reject");
            _subject.Notify(notification);

            return Ok(new { success = true, message = "Post rejected successfully", post });
        }

        // =====================================
        // ADMIN - DELETE POST
        // =====================================
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePost(int id)
        {
            var post = await _context.Posts.FindAsync(id);

            if (post == null)
                return NotFound(new { success = false, message = "Post not found" });

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Post deleted successfully", postId = id });
        }

        private int? GetCurrentUserId()
        {
            return AuthHelper.GetCurrentUserId(User, _context, Request.Headers.Authorization);
        }

        private async Task<List<object>> BuildApprovedPostFeedAsync(int currentUserId, string? search = null)
        {
            var visibleAuthorIds = await GetVisiblePostAuthorIdsAsync(currentUserId);
            var normalizedSearch = search?.Trim().ToLowerInvariant();

            var posts = await _context.Posts
                .Where(post =>
                    post.Status != null &&
                    post.Status.ToLower() == "approved" &&
                    visibleAuthorIds.Contains(post.UserId) &&
                    (string.IsNullOrWhiteSpace(normalizedSearch) ||
                     post.Content.ToLower().Contains(normalizedSearch) ||
                     _context.Users
                        .Where(user => user.UserId == post.UserId)
                        .Select(user => (user.Username ?? string.Empty).ToLower())
                        .FirstOrDefault()!.Contains(normalizedSearch)))
                .OrderByDescending(post => post.CreatedAt)
                .Select(post => new
                {
                    post.PostId,
                    post.UserId,
                    Username = _context.Users
                        .Where(user => user.UserId == post.UserId)
                        .Select(user => user.Username)
                        .FirstOrDefault() ?? "Unknown user",
                    ProfileImage = _context.Users
                        .Where(user => user.UserId == post.UserId)
                        .Select(user => user.ProfileImage)
                        .FirstOrDefault(),
                    post.Content,
                    post.PostImage,
                    post.Status,
                    post.CreatedAt,
                    post.ReviewedAt
                })
                .ToListAsync();

            if (posts.Count == 0)
                return new List<object>();

            var postIds = posts.Select(post => post.PostId).ToList();
            if (!await PostEngagementTablesExistAsync())
            {
                return BuildApprovedPostFeedResponse(posts);
            }

            var likeCounts = new Dictionary<int, int>();
            var likedPostIds = new HashSet<int>();
            var shareCounts = new Dictionary<int, int>();
            var comments = new List<object>();

            try
            {
                likeCounts = await _context.PostLikes
                    .Where(postLike => postIds.Contains(postLike.PostId))
                    .GroupBy(postLike => postLike.PostId)
                    .Select(group => new { PostId = group.Key, Count = group.Count() })
                    .ToDictionaryAsync(item => item.PostId, item => item.Count);

                likedPostIds = (await _context.PostLikes
                    .Where(postLike => postIds.Contains(postLike.PostId) && postLike.UserId == currentUserId)
                    .Select(postLike => postLike.PostId)
                    .ToListAsync())
                    .ToHashSet();

                shareCounts = await _context.PostShares
                    .Where(postShare => postIds.Contains(postShare.PostId))
                    .GroupBy(postShare => postShare.PostId)
                    .Select(group => new { PostId = group.Key, Count = group.Count() })
                    .ToDictionaryAsync(item => item.PostId, item => item.Count);

                comments = await _context.PostComments
                    .Where(postComment => postIds.Contains(postComment.PostId))
                    .OrderBy(postComment => postComment.CreatedAt)
                    .Select(postComment => new
                    {
                        postComment.PostCommentId,
                        postComment.PostId,
                        postComment.UserId,
                        Username = _context.Users
                            .Where(user => user.UserId == postComment.UserId)
                            .Select(user => user.Username)
                            .FirstOrDefault() ?? "Unknown user",
                        postComment.CommentText,
                        postComment.CreatedAt
                    })
                    .Cast<object>()
                    .ToListAsync();
            }
            catch
            {
                return BuildApprovedPostFeedResponse(posts);
            }

            var commentsByPostId = comments
                .Cast<dynamic>()
                .GroupBy(comment => (int)comment.PostId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(comment => (object)new
                        {
                            comment.PostCommentId,
                            comment.PostId,
                            comment.UserId,
                            comment.Username,
                            comment.CommentText,
                            comment.CreatedAt
                        })
                        .ToList());

            return BuildApprovedPostFeedResponse(posts, likeCounts, likedPostIds, shareCounts, commentsByPostId);
        }

        private async Task<Post?> GetApprovedPostAsync(int postId, int currentUserId)
        {
            var post = await _context.Posts.FirstOrDefaultAsync(post =>
                post.PostId == postId &&
                post.Status != null &&
                post.Status.ToLower() == "approved");

            if (post == null || !IsPostVisibleToUser(post.UserId, currentUserId))
            {
                return null;
            }

            return post;
        }

        private bool AreFriends(int userId1, int userId2)
        {
            return _context.FriendRequests.Any(friendRequest =>
                friendRequest.Status == "Accepted" &&
                ((friendRequest.SenderId == userId1 && friendRequest.ReceiverId == userId2) ||
                 (friendRequest.SenderId == userId2 && friendRequest.ReceiverId == userId1)));
        }

        private bool IsPostVisibleToUser(int postOwnerUserId, int currentUserId)
        {
            return postOwnerUserId == currentUserId || AreFriends(postOwnerUserId, currentUserId);
        }

        private async Task<List<int>> GetVisiblePostAuthorIdsAsync(int currentUserId)
        {
            var friendIds = await _context.FriendRequests
                .Where(friendRequest =>
                    friendRequest.Status == "Accepted" &&
                    (friendRequest.SenderId == currentUserId || friendRequest.ReceiverId == currentUserId))
                .Select(friendRequest => friendRequest.SenderId == currentUserId
                    ? friendRequest.ReceiverId
                    : friendRequest.SenderId)
                .Distinct()
                .ToListAsync();

            if (!friendIds.Contains(currentUserId))
            {
                friendIds.Add(currentUserId);
            }

            return friendIds;
        }

        private async Task<string> GetUsernameAsync(int userId)
        {
            return await _context.Users
                .Where(user => user.UserId == userId)
                .Select(user => user.Username)
                .FirstOrDefaultAsync() ?? $"User {userId}";
        }

        private async Task<bool> PostEngagementTablesExistAsync()
        {
            var requiredTables = new[] { "PostLikes", "PostComments", "PostShares" };

            var existingTables = await _context.Database.SqlQueryRaw<string>(
                """
                SELECT TABLE_NAME AS [Value]
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_NAME IN ('PostLikes', 'PostComments', 'PostShares')
                """
            ).ToListAsync();

            return requiredTables.All(existingTables.Contains);
        }

        private static List<object> BuildApprovedPostFeedResponse(
            IEnumerable<dynamic> posts,
            IReadOnlyDictionary<int, int>? likeCounts = null,
            IReadOnlySet<int>? likedPostIds = null,
            IReadOnlyDictionary<int, int>? shareCounts = null,
            IReadOnlyDictionary<int, List<object>>? commentsByPostId = null)
        {
            likeCounts ??= new Dictionary<int, int>();
            likedPostIds ??= new HashSet<int>();
            shareCounts ??= new Dictionary<int, int>();
            commentsByPostId ??= new Dictionary<int, List<object>>();

            return posts
                .Select(post =>
                {
                    commentsByPostId.TryGetValue((int)post.PostId, out var postComments);
                    return (object)new
                    {
                        post.PostId,
                        post.UserId,
                        post.Username,
                        profileImage = post.ProfileImage,
                        post.Content,
                        post.PostImage,
                        post.Status,
                        post.CreatedAt,
                        post.ReviewedAt,
                        likeCount = likeCounts.TryGetValue((int)post.PostId, out var likeCount) ? likeCount : 0,
                        commentCount = postComments?.Count ?? 0,
                        shareCount = shareCounts.TryGetValue((int)post.PostId, out var shareCount) ? shareCount : 0,
                        likedByCurrentUser = likedPostIds.Contains((int)post.PostId),
                        comments = postComments ?? new List<object>()
                    };
                })
                .ToList();
        }

        private static string BuildSharedPostMessage(Post post, string postAuthorUsername)
        {
            var parts = new List<string> { $"Shared a post from {postAuthorUsername}." };

            if (!string.IsNullOrWhiteSpace(post.Content))
            {
                parts.Add(post.Content.Trim());
            }

            if (!string.IsNullOrWhiteSpace(post.PostImage))
            {
                parts.Add($"Image: http://localhost:5072{post.PostImage}");
            }

            var message = string.Join(" ", parts);
            return message.Length > 500 ? $"{message[..497]}..." : message;
        }

        private async Task CreatePostDecisionNotificationAsync(int userId, int postId, string type, string title, string message)
        {
            await CreateUserNotificationAsync(userId, type, title, message, postId, null);
        }

        private async Task CreatePostActivityNotificationAsync(int userId, int postId, string type, string title, string message)
        {
            await CreateUserNotificationAsync(userId, type, title, message, postId, null);
        }

        private async Task CreateUserNotificationAsync(int userId, string type, string title, string message, int? relatedPostId, int? relatedReportId)
        {
            var userNotification = new UserNotification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                RelatedPostId = relatedPostId,
                RelatedReportId = relatedReportId,
                CreatedAt = DateTime.UtcNow
            };

            _context.UserNotifications.Add(userNotification);
            await _context.SaveChangesAsync();

            await _hubContext.Clients.Group($"User_{userId}").SendAsync("ReceiveAppNotification", new
            {
                userNotification.UserNotificationId,
                userNotification.UserId,
                userNotification.Type,
                userNotification.Title,
                userNotification.Message,
                userNotification.IsRead,
                userNotification.CreatedAt,
                userNotification.RelatedPostId,
                userNotification.RelatedReportId
            });
        }

        private async Task NotifyAdminsAboutNewPendingPostAsync(Post post)
        {
            var adminRecipients = _context.Users
                .Where(user => user.Role == "Admin" && user.IsActive)
                .Select(user => user.UserId)
                .ToList();

            foreach (var adminId in adminRecipients)
            {
                await _hubContext.Clients.Group($"User_{adminId}").SendAsync("ReceiveAppNotification", new
                {
                    userNotificationId = 0,
                    userId = adminId,
                    type = "PostSubmitted",
                    title = "New pending post",
                    message = $"Post #{post.PostId} is waiting for review.",
                    isRead = false,
                    createdAt = DateTime.UtcNow,
                    relatedPostId = post.PostId,
                    relatedReportId = (int?)null
                });
            }
        }
    }

    // Request model for post rejection with reason
    public class RejectPostRequest
    {
        public string? Reason { get; set; }
    }
}
