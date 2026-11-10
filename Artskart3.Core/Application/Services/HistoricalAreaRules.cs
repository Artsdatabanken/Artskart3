using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Application.Services;

/// <summary>
/// Året et historisk fylke eller en historisk kommune sluttet å gjelde, vist som «før {år}».
///
/// Area har ingen sluttdato, og TimeStamp er importdatoen. Importene følger likevel
/// inndelingene: alt stemplet før 2020 er inndelingen fra før regionreformen, som gjaldt
/// til 1.1.2020, og alt stemplet senere er inndelingen som gjaldt til 1.1.2024. Unntakene
/// er Sør- og Nord-Trøndelag, som ble Trøndelag 1.1.2018, og Møre og Romsdals grense fra
/// 2017, som endret seg da Rindal ble overført til Trøndelag 1.1.2019.
///
/// Kontrollert 11.11.2026 mot alle 443 historiske områder i Area, med
/// no.wikipedia.org/wiki/Norske_kommunenummer og regjeringen.no sine oversikter over
/// kommunenummer og grensejusteringer. Regelen gjelder bare dagens data: blir dagens
/// områder historiske ved en ny reform, eller kommer områdene fra fellesartserveren med
/// egne datoer, må den byttes ut.
/// </summary>
public static class HistoricalAreaRules
{
    public static int EndYear(int areaTypeId, string fid, DateTime timeStamp)
    {
        if (areaTypeId == (int)AreaType.County)
        {
            if (fid is "16" or "17") return 2018;
            if (fid == "15_2017") return 2019;
        }

        return timeStamp.Year < 2020 ? 2020 : 2024;
    }
}
