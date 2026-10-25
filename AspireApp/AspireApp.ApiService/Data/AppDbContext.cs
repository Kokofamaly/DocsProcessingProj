using DocsProcessingProj.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DocsProcessingProj.Api.Data;

public class AppDbContext : DbContext {
    public DbSet<Document> Documents { get; set; } = null!;
    public AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base (options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Document>().HasAlternateKey(d => d.BlobName);
    }
    
}
