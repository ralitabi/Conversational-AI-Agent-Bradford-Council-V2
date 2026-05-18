namespace Bradford.Core.Models;

public class ConversationTurn
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;   // "user" | "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ContactSession
{
    public string  Id         { get; set; } = string.Empty;  // BC-XXXXXX
    public string  Name       { get; set; } = string.Empty;
    public string? Email      { get; set; }
    public string? Phone      { get; set; }
    public string  Subject    { get; set; } = string.Empty;
    public string  Status     { get; set; } = "waiting";     // waiting | active | closed
    public string? AssignedTo { get; set; }                  // username of assigned admin
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AdminUser
{
    public int      Id              { get; set; }
    public string   Username        { get; set; } = string.Empty;
    public string   PasswordHash    { get; set; } = string.Empty;
    public string   Name            { get; set; } = string.Empty;
    public string   Role            { get; set; } = "admin";   // admin | superadmin
    public string?  Specializations { get; set; }              // comma-separated query categories
    public bool     IsActive        { get; set; } = true;
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt    { get; set; }
}

public class ContactMessage
{
    public int     Id         { get; set; }
    public string  SessionId  { get; set; } = string.Empty;
    public string  Sender     { get; set; } = string.Empty;  // "citizen" | "admin"
    public string  SenderName { get; set; } = string.Empty;
    public string  Content    { get; set; } = string.Empty;
    public bool    IsRead     { get; set; } = false;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class AdminProfile
{
    public int     Id            { get; set; }
    public string  Username      { get; set; } = string.Empty;
    public string  DisplayName   { get; set; } = string.Empty;
    public string  Bio           { get; set; } = string.Empty;
    public string? AvatarBase64  { get; set; }   // base64-encoded image data
    public string? AvatarMime    { get; set; }   // image/jpeg | image/png | image/webp
    public DateTime UpdatedAt    { get; set; } = DateTime.UtcNow;
}

public class AdminActivity
{
    public int Id { get; set; }
    public string Username  { get; set; } = string.Empty;
    public string Name      { get; set; } = string.Empty;
    public string Action    { get; set; } = string.Empty; // login | logout | view | delete | delete_all | analytics
    public string? Detail   { get; set; }
    public string? IpAddress{ get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class LlmMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
