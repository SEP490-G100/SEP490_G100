using System;
using System.Collections.Generic;

namespace Nanny_BackEnd;

public partial class Blog
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Summary { get; set; }

    public string Content { get; set; } = null!;

    public string? ThumbnailUrl { get; set; }

    public Guid AuthorUserId { get; set; }

    public Guid? CategoryId { get; set; }

    public int Status { get; set; }

    public int ViewCount { get; set; }

    public DateTime? PublishedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual User AuthorUser { get; set; } = null!;

    public virtual BlogCategory? Category { get; set; }
}
