using Microsoft.AspNetCore.Mvc;
using ProfileBook.API.Data;
using ProfileBook.API.Models;
using ProfileBook.API.Patterns.Factory;
using ProfileBook.API.Patterns.Observer;

namespace ProfileBook.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PostController : ControllerBase
    {
        private readonly ProfileBookDbContext _context;
        private readonly NotificationSubject _subject;

        public PostController(ProfileBookDbContext context)
        {
            _context = context;

            _subject = new NotificationSubject();
            _subject.Attach(new NotificationService());
        }

        [HttpPost]
        public IActionResult CreatePost(Post post)
        {
            _context.Posts.Add(post);
            _context.SaveChanges();

            var notification = NotificationFactory.CreateNotification("post");
            _subject.Notify(notification);

            return Ok(post);
        }

        [HttpGet]
        public IActionResult GetPosts()
        {
            var posts = _context.Posts.ToList();
            return Ok(posts);
        }
    }
}