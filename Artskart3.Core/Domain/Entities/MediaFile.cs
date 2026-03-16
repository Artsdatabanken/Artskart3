using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class MediaFile : BaseEntity
{
    public string Origin { get; set; } = null!;

    public int Size { get; set; }

    public bool Downloaded { get; set; }

    public bool InDownloadqueue { get; set; }

    public int DownloadRetryCount { get; set; }

    public byte[]? Image { get; set; }

    public int MediaFileTypeId { get; set; }

    public string? License { get; set; }

    public string? RightsHolder { get; set; }

    public string? Description { get; set; }

    public int? ObservationId { get; set; }

    public virtual MediaFileType MediaFileType { get; set; } = null!;

    public virtual Observation? Observation { get; set; }
}
