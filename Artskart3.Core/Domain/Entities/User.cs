using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public class User : BaseEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}