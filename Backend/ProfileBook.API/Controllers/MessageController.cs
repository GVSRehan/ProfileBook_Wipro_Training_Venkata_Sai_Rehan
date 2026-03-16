using Microsoft.AspNetCore.Mvc;
using ProfileBook.API.Data;
using ProfileBook.API.Models;
using ProfileBook.API.Patterns.Factory;
using ProfileBook.API.Patterns.Observer;

namespace ProfileBook.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MessageController : ControllerBase
    {
        private readonly ProfileBookDbContext _context;
        private readonly NotificationSubject _subject;

        public MessageController(ProfileBookDbContext context)
        {
            _context = context;

            _subject = new NotificationSubject();
            _subject.Attach(new NotificationService());
        }

        [HttpPost]
        public IActionResult SendMessage(Message message)
        {
            _context.Messages.Add(message);
            _context.SaveChanges();

            var notification = NotificationFactory.CreateNotification("message");
            _subject.Notify(notification);

            return Ok(message);
        }

        [HttpGet]
        public IActionResult GetMessages()
        {
            var messages = _context.Messages.ToList();
            return Ok(messages);
        }
    }
}