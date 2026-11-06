using Artskart3.Core.Application.Services;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Persistence.Services;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class FilterMaskQueryTests
{
    private sealed record Rad(byte Mask) : IHarFilterMaske;

    /// <summary>
    /// Intervallet for skjulte funn må gi nøyaktig det bitoperasjonen gir. Et avvik her
    /// er raske og gale tall, så alle masker prøves mot alle utvalg.
    /// </summary>
    [Fact]
    public void Gir_samme_rader_som_bitoperasjonen_for_alle_masker_og_utvalg()
    {
        var alleMasker = Enumerable.Range(0, FilterMask.Count).Select(m => new Rad((byte)m)).ToArray();

        for (var care = 1; care < FilterMask.Count; care++)
        {
            for (var want = 0; want < FilterMask.Count; want++)
            {
                if ((want & ~care) != 0) continue;

                var (c, w) = (care, want);
                var utvalg = new FilterMask.Selection((byte)care, (byte)want);

                alleMasker.AsQueryable().WhereMask(utvalg)
                    .Should().BeEquivalentTo(alleMasker.Where(r => (r.Mask & c) == w), $"care {care}, want {want}");
            }
        }
    }

    [Fact]
    public void Inaktivt_utvalg_slipper_alt_gjennom()
    {
        var rader = new[] { new Rad(0), new Rad(32) };

        rader.AsQueryable().WhereMask(new FilterMask.Selection(0, 0)).Should().Equal(rader);
    }
}
