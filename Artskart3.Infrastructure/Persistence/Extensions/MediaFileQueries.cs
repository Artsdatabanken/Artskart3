using System.Linq.Expressions;
using Artskart3.Core.Domain.Entities;

namespace Artskart3.Infrastructure.Persistence.Extensions;

public static class MediaFileQueries
{
    private static readonly string[] ImageTypes = ["image/jpeg", "image/png", "image/gif", "image/webp", "image/avif"];

    public static readonly Expression<Func<MediaFile, bool>> IsViewableImage = m =>
        !m.IsDeleted && !m.MediaFileType.IsDeleted && ImageTypes.Contains(m.MediaFileType.MimeType!);
}
