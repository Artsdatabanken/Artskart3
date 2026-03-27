using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class RejectedRecord : BaseEntity
{
    public string Gid { get; set; } = null!;

    public int SourceDataId { get; set; }

    public string RecordId { get; set; } = null!;

    public DateTime DatetimeRecordProsessed { get; set; }

    public string TentativId { get; set; } = null!;

    public string InstitutionCode { get; set; } = null!;

    public string CollectionCode { get; set; } = null!;

    public virtual ICollection<ProcessRecordResult> ProcessRecordResults { get; set; } = new List<ProcessRecordResult>();
}
