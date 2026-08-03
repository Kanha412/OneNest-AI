using System.ComponentModel.DataAnnotations;
using OneNest.Domain.Enums;

namespace OneNest.Application.DTOs.Documents;

public class CreateDocumentRequest
{
    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    public DocumentCategory Category { get; set; } = DocumentCategory.Other;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;
}
