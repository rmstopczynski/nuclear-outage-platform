using System.Collections.Generic;

namespace MVC_EF_Start_8.Models
{
    /// <summary>
    /// Facility name -> grid region, used to group the "outage by region"
    /// chart. Keyed on the actual short facilityName strings the EIA API
    /// returns (e.g. "Cooper", "Fermi", "Watts Bar Nuclear Plant") rather
    /// than long official NRC names -- the previous version of this map
    /// used only long-form names ("Cooper Nuclear Station"), which meant
    /// the old Contains-based fallback match in HomeController almost
    /// never succeeded (a short string can't "contain" a longer one),
    /// and most facilities silently fell through to "Unknown". Fixed here
    /// by keying on what EIA actually sends; the matching logic itself
    /// was also fixed (see HomeController.GetChartData).
    /// </summary>
    public static class FacilityRegionMap
    {
        public static readonly Dictionary<string, string> Regions = new Dictionary<string, string>
        {
            // Southeast
            ["Vogtle"] = "Southeast",
            ["McGuire"] = "Southeast",
            ["Turkey Point"] = "Southeast",
            ["Browns Ferry"] = "Southeast",
            ["Catawba"] = "Southeast",
            ["St Lucie"] = "Southeast",
            ["St. Lucie"] = "Southeast",
            ["V C Summer"] = "Southeast",
            ["Virgil C Summer"] = "Southeast",
            ["Watts Bar Nuclear Plant"] = "Southeast",
            ["Joseph M Farley"] = "Southeast",
            ["Oconee"] = "Southeast",
            ["North Anna"] = "Southeast",
            ["Surry"] = "Southeast",
            ["Sequoyah"] = "Southeast",
            ["Crystal River"] = "Southeast",
            ["Harris"] = "Southeast",
            ["Shearon Harris"] = "Southeast",
            ["Edwin I Hatch"] = "Southeast",
            ["Hatch"] = "Southeast",
            ["H B Robinson"] = "Southeast",
            ["Robinson"] = "Southeast",
            ["Brunswick"] = "Southeast",

            // Midwest
            ["Braidwood Generation Station"] = "Midwest",
            ["Byron Generating Station"] = "Midwest",
            ["Quad Cities Generating Station"] = "Midwest",
            ["Prairie Island"] = "Midwest",
            ["Point Beach"] = "Midwest",
            ["Cooper"] = "Midwest",
            ["Davis Besse"] = "Midwest",
            ["Duane Arnold"] = "Midwest",
            ["LaSalle Generating Station"] = "Midwest",
            ["Clinton Power Station"] = "Midwest",
            ["Monticello"] = "Midwest",
            ["Callaway"] = "Midwest",
            ["Palisades"] = "Midwest",
            ["Fermi"] = "Midwest",
            ["Donald C Cook"] = "Midwest",
            ["Perry"] = "Midwest",
            ["Dresden Generating Station"] = "Midwest",
            ["Fort Calhoun"] = "Midwest",
            ["Kewaunee"] = "Midwest",

            // Northeast
            ["Limerick"] = "Northeast",
            ["Seabrook"] = "Northeast",
            ["PSEG Hope Creek Generating Station"] = "Northeast",
            ["Hope Creek"] = "Northeast",
            ["Indian Point"] = "Northeast",
            ["Millstone"] = "Northeast",
            ["Nine Mile Point Nuclear Station"] = "Northeast",
            ["James A Fitzpatrick"] = "Northeast",
            ["Peach Bottom"] = "Northeast",
            ["Calvert Cliffs Nuclear Power Plant"] = "Northeast",
            ["PPL Susquehanna"] = "Northeast",
            ["Susquehanna"] = "Northeast",
            ["Oyster Creek"] = "Northeast",
            ["Pilgrim"] = "Northeast",
            ["Vermont Yankee"] = "Northeast",
            ["Three Mile Island"] = "Northeast",
            ["Beaver Valley"] = "Northeast",
            ["R. E. Ginna Nuclear Power Plant"] = "Northeast",
            ["R E Ginna"] = "Northeast",
            ["Ginna"] = "Northeast",
            ["PSEG Salem Generating Station"] = "Northeast",
            ["Salem"] = "Northeast",

            // South Central
            ["Comanche Peak"] = "South Central",
            ["Arkansas Nuclear One"] = "South Central",
            ["South Texas Project"] = "South Central",
            ["Grand Gulf"] = "South Central",
            ["River Bend Station"] = "South Central",
            ["Waterford"] = "South Central",
            ["Wolf Creek Generating Station"] = "South Central",

            // West
            ["Palo Verde"] = "West",
            ["Columbia Generating Station"] = "West",
            ["San Onofre"] = "West",
            ["Diablo Canyon"] = "West",
        };
    }
}
