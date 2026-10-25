namespace CommonData;

public class ProcessedDocumentData
{
    public int Id { get; set; }
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public long FileSize { get; set; }
    public string FileName { get; set; } = string.Empty;
}