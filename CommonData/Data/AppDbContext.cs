using Microsoft.EntityFrameworkCore;

namespace CommonData;

public class AppDbContext : DbContext {
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<ProcessedDocumentData> ProcessedDocumentDatas { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;


    public AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base (options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Document>().HasAlternateKey(d => d.BlobName);
        modelBuilder.Entity<Document>().HasIndex(d => new { d.UserId, d.FileHash }).IsUnique();

        modelBuilder.Entity<User>().HasMany(u => u.Documents).WithOne(d => d.User).HasForeignKey(d => d.UserId);
        modelBuilder.Entity<User>().HasIndex(u => u.Login).IsUnique();

        modelBuilder.Entity<ProcessedDocumentData>().HasIndex(p => p.DocumentId).IsUnique();

        modelBuilder.Entity<OutboxMessage>().HasIndex(o => new { o.PublishedAt, o.CreatedAt, o.NextAttemptAt, o.DeadLetteredAt});

        modelBuilder.Entity<OutboxMessage>().Property(o => o.Payload).IsRequired();

    }
    
}