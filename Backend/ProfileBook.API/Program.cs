using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using ProfileBook.API.Data;
using ProfileBook.API.Services.Implementations;
using ProfileBook.API.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json"),
    optional: true,
    reloadOnChange: true);

var resolvedConnectionString = ResolveSetting(
    builder.Configuration,
    "ConnectionStrings:DefaultConnection",
    candidateConfiguration => candidateConfiguration.GetConnectionString("DefaultConnection"));

var resolvedJwtKey = ResolveSetting(builder.Configuration, "Jwt:Key");
var resolvedJwtIssuer = ResolveSetting(builder.Configuration, "Jwt:Issuer");
var resolvedJwtAudience = ResolveSetting(builder.Configuration, "Jwt:Audience");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocal4200", policy =>
    {
        policy.WithOrigins(
                  "http://localhost:4200",
                  "http://127.0.0.1:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSignalR();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = resolvedJwtIssuer,
        ValidAudience = resolvedJwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(resolvedJwtKey))
    };
});

builder.Services.AddDbContext<ProfileBookDbContext>(options =>
    options.UseSqlServer(resolvedConnectionString));

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAdminService, AdminService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ProfileBookDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");
    EnsureDatabaseSchema(dbContext, logger);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowLocal4200");

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Placeholder}/{action=Index}/{id?}");

app.MapControllers();
app.MapHub<ProfileBook.API.Hubs.ChatHub>("/chatHub");

app.Run();

static void EnsureDatabaseSchema(ProfileBookDbContext dbContext, ILogger logger)
{
    try
    {
        var hasMigrationHistoryTable = dbContext.Database
            .SqlQueryRaw<int>("SELECT CAST(1 AS int) AS [Value] WHERE OBJECT_ID(N'[__EFMigrationsHistory]') IS NOT NULL")
            .Any();

        var hasExistingCoreTables = dbContext.Database.SqlQueryRaw<int>(
            """
            SELECT TOP 1 CAST(1 AS int) AS [Value]
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME IN ('Users', 'Posts', 'Groups', 'Messages', 'Reports')
            """
        ).Any();

        var hasInitialCreateRecorded = hasMigrationHistoryTable && dbContext.Database.SqlQueryRaw<int>(
            """
            SELECT TOP 1 CAST(1 AS int) AS [Value]
            FROM [__EFMigrationsHistory]
            WHERE [MigrationId] = '20260321132733_InitialCreate'
            """
        ).Any();

        if (hasExistingCoreTables && !hasInitialCreateRecorded)
        {
            logger.LogWarning("Existing database tables found without the initial EF migration recorded. Skipping automatic migrations to avoid recreating existing tables.");
            EnsureCompatibilityObjects(dbContext, logger);
            return;
        }

        dbContext.Database.Migrate();
        EnsureCompatibilityObjects(dbContext, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization failed.");
        throw;
    }
}

static void EnsureCompatibilityObjects(ProfileBookDbContext dbContext, ILogger logger)
{
    dbContext.Database.ExecuteSqlRaw(
        """
        UPDATE [Users]
        SET [Email] = LOWER(LTRIM(RTRIM([Email]))),
            [Username] = LTRIM(RTRIM([Username])),
            [MobileNumber] = LTRIM(RTRIM([MobileNumber]))
        WHERE [Email] <> LOWER(LTRIM(RTRIM([Email])))
           OR [Username] <> LTRIM(RTRIM([Username]))
           OR [MobileNumber] <> LTRIM(RTRIM([MobileNumber]));
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = 'IX_Users_Email'
              AND object_id = OBJECT_ID(N'[Users]')
        )
        AND NOT EXISTS (
            SELECT [Email]
            FROM [Users]
            GROUP BY [Email]
            HAVING COUNT(*) > 1
        )
        BEGIN
            CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
        END
        """
    );

    LogDuplicateIdentityValues(dbContext, logger);

    dbContext.Database.ExecuteSqlRaw(
        """
        IF NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = 'IX_Users_Username'
              AND object_id = OBJECT_ID(N'[Users]')
        )
        AND NOT EXISTS (
            SELECT [Username]
            FROM [Users]
            GROUP BY [Username]
            HAVING COUNT(*) > 1
        )
        BEGIN
            CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = 'IX_Users_MobileNumber'
              AND object_id = OBJECT_ID(N'[Users]')
        )
        AND NOT EXISTS (
            SELECT [MobileNumber]
            FROM [Users]
            GROUP BY [MobileNumber]
            HAVING COUNT(*) > 1
        )
        AND EXISTS (
            SELECT 1
            FROM sys.columns
            WHERE object_id = OBJECT_ID(N'[Users]')
              AND name = 'MobileNumber'
              AND max_length <> -1
        )
        BEGIN
            CREATE UNIQUE INDEX [IX_Users_MobileNumber] ON [Users] ([MobileNumber]);
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF OBJECT_ID(N'[DeletedUsers]') IS NULL
        BEGIN
            CREATE TABLE [DeletedUsers] (
                [DeletedUserId] int NOT NULL IDENTITY(1,1),
                [OriginalUserId] int NOT NULL,
                [Username] nvarchar(max) NOT NULL,
                [Email] nvarchar(max) NOT NULL,
                [MobileNumber] nvarchar(max) NOT NULL,
                [Role] nvarchar(max) NOT NULL,
                [DeletedAt] datetime2 NOT NULL,
                [DeletedBy] nvarchar(max) NULL,
                [CreatedAt] datetime2 NULL,
                CONSTRAINT [PK_DeletedUsers] PRIMARY KEY ([DeletedUserId])
            );
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF OBJECT_ID(N'[PasswordResetTokens]') IS NULL
        BEGIN
            CREATE TABLE [PasswordResetTokens] (
                [PasswordResetTokenId] int NOT NULL IDENTITY(1,1),
                [UserId] int NOT NULL,
                [Token] nvarchar(450) NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                [ExpiresAt] datetime2 NOT NULL,
                [IsUsed] bit NOT NULL,
                [UsedAt] datetime2 NULL,
                CONSTRAINT [PK_PasswordResetTokens] PRIMARY KEY ([PasswordResetTokenId]),
                CONSTRAINT [FK_PasswordResetTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX [IX_PasswordResetTokens_UserId_Token]
                ON [PasswordResetTokens] ([UserId], [Token]);
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF COL_LENGTH('Posts', 'RejectionReason') IS NULL
        BEGIN
            ALTER TABLE [Posts] ADD [RejectionReason] nvarchar(500) NULL;
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF OBJECT_ID(N'[FriendRequests]') IS NULL
        BEGIN
            CREATE TABLE [FriendRequests] (
                [FriendRequestId] int NOT NULL IDENTITY(1,1),
                [SenderId] int NOT NULL,
                [ReceiverId] int NOT NULL,
                [Status] nvarchar(20) NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                [RespondedAt] datetime2 NULL,
                CONSTRAINT [PK_FriendRequests] PRIMARY KEY ([FriendRequestId])
            );
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF OBJECT_ID(N'[AlertMessages]') IS NULL
        BEGIN
            CREATE TABLE [AlertMessages] (
                [AlertMessageId] int NOT NULL IDENTITY(1,1),
                [AdminUserId] int NOT NULL,
                [Content] nvarchar(500) NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                CONSTRAINT [PK_AlertMessages] PRIMARY KEY ([AlertMessageId])
            );
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF OBJECT_ID(N'[UserNotifications]') IS NULL
        BEGIN
            CREATE TABLE [UserNotifications] (
                [UserNotificationId] int NOT NULL IDENTITY(1,1),
                [UserId] int NOT NULL,
                [Type] nvarchar(50) NOT NULL,
                [Title] nvarchar(120) NOT NULL,
                [Message] nvarchar(500) NOT NULL,
                [IsRead] bit NOT NULL CONSTRAINT [DF_UserNotifications_IsRead] DEFAULT 0,
                [CreatedAt] datetime2 NOT NULL,
                [RelatedPostId] int NULL,
                [RelatedReportId] int NULL,
                CONSTRAINT [PK_UserNotifications] PRIMARY KEY ([UserNotificationId])
            );
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF COL_LENGTH('Messages', 'IsRead') IS NULL
        BEGIN
            ALTER TABLE [Messages] ADD [IsRead] bit NOT NULL CONSTRAINT [DF_Messages_IsRead] DEFAULT 0;
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF COL_LENGTH('Messages', 'ReadAt') IS NULL
        BEGIN
            ALTER TABLE [Messages] ADD [ReadAt] datetime2 NULL;
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF COL_LENGTH('Messages', 'GroupId') IS NULL
        BEGIN
            ALTER TABLE [Messages] ADD [GroupId] int NULL;
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF EXISTS (
            SELECT 1
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = 'Messages'
              AND COLUMN_NAME = 'ReceiverId'
              AND IS_NULLABLE = 'NO'
        )
        BEGIN
            ALTER TABLE [Messages] ALTER COLUMN [ReceiverId] int NULL;
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        UPDATE [Messages]
        SET [ReceiverId] = NULL
        WHERE [GroupId] IS NOT NULL AND [ReceiverId] = 0;
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF COL_LENGTH('Reports', 'Status') IS NULL
        BEGIN
            ALTER TABLE [Reports] ADD [Status] nvarchar(30) NOT NULL CONSTRAINT [DF_Reports_Status] DEFAULT 'Open';
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF COL_LENGTH('Reports', 'ActionTaken') IS NULL
        BEGIN
            ALTER TABLE [Reports] ADD [ActionTaken] nvarchar(30) NULL;
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF COL_LENGTH('Reports', 'AdminNotes') IS NULL
        BEGIN
            ALTER TABLE [Reports] ADD [AdminNotes] nvarchar(500) NULL;
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF COL_LENGTH('Reports', 'ResolvedAt') IS NULL
        BEGIN
            ALTER TABLE [Reports] ADD [ResolvedAt] datetime2 NULL;
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF COL_LENGTH('Reports', 'ResolvedBy') IS NULL
        BEGIN
            ALTER TABLE [Reports] ADD [ResolvedBy] nvarchar(100) NULL;
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF COL_LENGTH('Groups', 'CreatedAt') IS NULL
        BEGIN
            ALTER TABLE [Groups] ADD [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_Groups_CreatedAt] DEFAULT GETUTCDATE();
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF COL_LENGTH('Groups', 'CreatedBy') IS NULL
        BEGIN
            ALTER TABLE [Groups] ADD [CreatedBy] nvarchar(100) NULL;
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF OBJECT_ID(N'[PostLikes]') IS NULL
        BEGIN
            CREATE TABLE [PostLikes] (
                [PostLikeId] int NOT NULL IDENTITY(1,1),
                [PostId] int NOT NULL,
                [UserId] int NOT NULL,
                [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_PostLikes_CreatedAt] DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_PostLikes] PRIMARY KEY ([PostLikeId])
            );
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF OBJECT_ID(N'[PostComments]') IS NULL
        BEGIN
            CREATE TABLE [PostComments] (
                [PostCommentId] int NOT NULL IDENTITY(1,1),
                [PostId] int NOT NULL,
                [UserId] int NOT NULL,
                [CommentText] nvarchar(500) NOT NULL,
                [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_PostComments_CreatedAt] DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_PostComments] PRIMARY KEY ([PostCommentId])
            );
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF OBJECT_ID(N'[PostShares]') IS NULL
        BEGIN
            CREATE TABLE [PostShares] (
                [PostShareId] int NOT NULL IDENTITY(1,1),
                [PostId] int NOT NULL,
                [SenderUserId] int NOT NULL,
                [RecipientUserId] int NOT NULL,
                [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_PostShares_CreatedAt] DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_PostShares] PRIMARY KEY ([PostShareId])
            );
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        IF NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = 'IX_PostLikes_PostId_UserId'
              AND object_id = OBJECT_ID(N'[PostLikes]')
        )
        BEGIN
            CREATE UNIQUE INDEX [IX_PostLikes_PostId_UserId]
            ON [PostLikes] ([PostId], [UserId]);
        END
        """
    );

    dbContext.Database.ExecuteSqlRaw(
        """
        UPDATE [Users]
        SET [CredentialsExpireAt] = NULL
        WHERE [Role] = 'User' AND [CredentialsExpireAt] IS NOT NULL;
        """
    );

    logger.LogInformation("Database compatibility checks completed.");
}

static void LogDuplicateIdentityValues(ProfileBookDbContext dbContext, ILogger logger)
{
    LogDuplicateValues(
        dbContext.Users
            .AsNoTracking()
            .GroupBy(user => user.Email.Trim().ToLower())
            .Where(group => group.Key != string.Empty && group.Count() > 1)
            .Select(group => new DuplicateIdentityValue(
                "Email",
                group.Key,
                string.Join(", ", group
                    .OrderBy(user => user.UserId)
                    .Select(user => $"{user.UserId}:{user.Username}:{user.Role}"))))
            .ToList(),
        logger);

    LogDuplicateValues(
        dbContext.Users
            .AsNoTracking()
            .GroupBy(user => user.Username.Trim().ToLower())
            .Where(group => group.Key != string.Empty && group.Count() > 1)
            .Select(group => new DuplicateIdentityValue(
                "Username",
                group.Key,
                string.Join(", ", group
                    .OrderBy(user => user.UserId)
                    .Select(user => $"{user.UserId}:{user.Username}:{user.Role}"))))
            .ToList(),
        logger);

    LogDuplicateValues(
        dbContext.Users
            .AsNoTracking()
            .GroupBy(user => user.MobileNumber.Trim())
            .Where(group => group.Key != string.Empty && group.Count() > 1)
            .Select(group => new DuplicateIdentityValue(
                "MobileNumber",
                group.Key,
                string.Join(", ", group
                    .OrderBy(user => user.UserId)
                    .Select(user => $"{user.UserId}:{user.Username}:{user.Role}"))))
            .ToList(),
        logger);
}

static void LogDuplicateValues(IEnumerable<DuplicateIdentityValue> duplicates, ILogger logger)
{
    foreach (var duplicate in duplicates)
    {
        logger.LogWarning(
            "Duplicate {Field} detected for value '{Value}'. Accounts: {Accounts}",
            duplicate.Field,
            duplicate.Value,
            duplicate.Accounts);
    }
}

static string ResolveSetting(
    IConfiguration configuration,
    string key,
    Func<IConfiguration, string?>? selector = null)
{
    selector ??= candidateConfiguration => candidateConfiguration[key];

    var directValue = selector(configuration);
    if (IsConfiguredValue(directValue))
    {
        return directValue!;
    }

    foreach (var basePath in GetConfigSearchPaths())
    {
        var candidateConfiguration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .Build();

        var candidateValue = selector(candidateConfiguration);
        if (IsConfiguredValue(candidateValue))
        {
            return candidateValue!;
        }
    }

    throw new InvalidOperationException(
        $"Missing valid configuration for '{key}'. Add it to appsettings.Local.json, user secrets, or environment variables.");
}

static IEnumerable<string> GetConfigSearchPaths()
{
    var paths = new[]
    {
        Directory.GetCurrentDirectory(),
        AppContext.BaseDirectory,
        Path.Combine(Directory.GetCurrentDirectory(), "Backend", "ProfileBook.API")
    };

    return paths
        .Where(Directory.Exists)
        .Select(Path.GetFullPath)
        .Distinct(StringComparer.OrdinalIgnoreCase);
}

static bool IsConfiguredValue(string? value)
{
    return !string.IsNullOrWhiteSpace(value)
        && !value.Trim().StartsWith("__SET_", StringComparison.OrdinalIgnoreCase);
}

internal readonly record struct DuplicateIdentityValue(string Field, string Value, string Accounts);
