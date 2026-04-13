namespace DataIngestionService.Application.DTOs;

public class BatchIngestResponse
{
    public int Accepted { get; set; }
    public int Rejected { get; set; }
    public IReadOnlyList<ValidationError> Errors { get; set; } = [];
}
