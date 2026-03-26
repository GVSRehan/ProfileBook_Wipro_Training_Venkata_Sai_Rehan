using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProfileBook.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPostEngagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionTaken",
                table: "Reports",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                table: "Reports",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "Reports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedBy",
                table: "Reports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Reports",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "ReceiverId",
                table: "Messages",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "Messages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                table: "Messages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Groups",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Groups",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AlertMessages",
                columns: table => new
                {
                    AlertMessageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminUserId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertMessages", x => x.AlertMessageId);
                });

            migrationBuilder.CreateTable(
                name: "FriendRequests",
                columns: table => new
                {
                    FriendRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SenderId = table.Column<int>(type: "int", nullable: false),
                    ReceiverId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FriendRequests", x => x.FriendRequestId);
                });

            migrationBuilder.CreateTable(
                name: "PostComments",
                columns: table => new
                {
                    PostCommentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CommentText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostComments", x => x.PostCommentId);
                });

            migrationBuilder.CreateTable(
                name: "PostLikes",
                columns: table => new
                {
                    PostLikeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostLikes", x => x.PostLikeId);
                });

            migrationBuilder.CreateTable(
                name: "PostShares",
                columns: table => new
                {
                    PostShareId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    SenderUserId = table.Column<int>(type: "int", nullable: false),
                    RecipientUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostShares", x => x.PostShareId);
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    UserNotificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RelatedPostId = table.Column<int>(type: "int", nullable: true),
                    RelatedReportId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.UserNotificationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PostLikes_PostId_UserId",
                table: "PostLikes",
                columns: new[] { "PostId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertMessages");

            migrationBuilder.DropTable(
                name: "FriendRequests");

            migrationBuilder.DropTable(
                name: "PostComments");

            migrationBuilder.DropTable(
                name: "PostLikes");

            migrationBuilder.DropTable(
                name: "PostShares");

            migrationBuilder.DropTable(
                name: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "ActionTaken",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "AdminNotes",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ResolvedBy",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Groups");

            migrationBuilder.AlterColumn<int>(
                name: "ReceiverId",
                table: "Messages",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
