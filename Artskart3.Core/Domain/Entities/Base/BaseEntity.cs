using System;

namespace Artskart3.Core.Domain.Entities.Base
{
    public abstract class BaseEntity<TKey>
    {
        public TKey Id { get; init; } = default!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }

    public abstract class BaseEntity : BaseEntity<int> { }
    
}
