namespace CommonData;

public class User
{
    public Guid Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string HashedPassword { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public List<Document> Documents { get; set; } = new();
}