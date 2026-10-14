using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext {
    public AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base (options) { }
    public DbSet<Document> Documents { get; set; }
    public DbSet<Notification> Notifications { get; set; }
}
