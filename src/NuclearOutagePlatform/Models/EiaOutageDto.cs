using System.Collections.Generic;

namespace MVC_EF_Start_8.Models
{
    /// <summary>
    /// Raw shape of a single record as returned by the EIA API. All fields
    /// are strings because that's what the API sends -- this DTO exists
    /// specifically to absorb that, so nothing else in the app has to deal
    /// with un-parsed strings. OutageIngestionService maps these into real
    /// OutageRecord entities (with actual DateOnly/decimal types) before
    /// anything gets persisted.
    ///
    /// This DTO/entity split is a standard pattern for insulating your
    /// domain model from an external API's shape -- if EIA changes their
    /// response format, only this file and the mapping code need to change,
    /// not the database schema or anything downstream of it.
    /// </summary>
    public class EiaOutageDto
    {
        public string? period { get; set; }
        public string? facility { get; set; }
        public string? facilityName { get; set; }
        public string? generator { get; set; }
        public string? capacity { get; set; }
        public string? outage { get; set; }
        public string? percentOutage { get; set; }
    }

    public class EiaResponse
    {
        public List<EiaOutageDto> data { get; set; } = new();
    }

    public class EiaRoot
    {
        public EiaResponse? response { get; set; }
    }
}
