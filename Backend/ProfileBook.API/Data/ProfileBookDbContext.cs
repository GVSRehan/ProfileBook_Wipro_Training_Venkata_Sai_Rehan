using Microsoft.EntityFrameworkCore;
using ProfileBook.API.Models;

namespace ProfileBook.API.Data
{
    public class ProfileBookDbContext : DbContext
    {
        public ProfileBookDbContext(DbContextOptions<ProfileBookDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Group> Groups { get; set; }
    }
}