using System;
using System.Collections.Generic;

namespace Nanny_BackEnd.Models;

public partial class BlogCategory
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<Blog> Blogs { get; set; } = new List<Blog>();
}
