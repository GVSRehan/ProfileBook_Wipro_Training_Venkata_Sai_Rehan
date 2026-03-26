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
        public DbSet<DeletedUser> DeletedUsers { get; set; }
        public DbSet<FriendRequest> FriendRequests { get; set; }
        public DbSet<AlertMessage> AlertMessages { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }
        public DbSet<PostLike> PostLikes { get; set; }
        public DbSet<PostComment> PostComments { get; set; }
        public DbSet<PostShare> PostShares { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PostLike>()
                .HasIndex(postLike => new { postLike.PostId, postLike.UserId })
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(user => user.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(user => user.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(user => user.MobileNumber)
                .IsUnique();

            modelBuilder.Entity<PasswordResetToken>()
                .HasIndex(token => new { token.UserId, token.Token })
                .IsUnique();
        }
    }
}
