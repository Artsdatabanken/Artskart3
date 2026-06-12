using System.ComponentModel.DataAnnotations;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public class User : BaseEntity<Guid>
{
    [MaxLength(100)]
    public string? Name { get; set; }
    [MaxLength(100)]
    public string? Email { get; set; }
}