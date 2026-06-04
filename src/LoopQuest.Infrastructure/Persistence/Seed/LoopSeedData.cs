using LoopQuest.Domain.Entities;
using LoopQuest.Domain.Enums;

namespace LoopQuest.Infrastructure.Persistence.Seed;

/// <summary>
/// The starter loop library (spec section 12). Values are approximate and deliberately span flat,
/// balanced and steep weeks so the calibration logic has a good spread to choose from.
///
/// NOTE: verify these numbers against real route data before relying on them. Fantasy entries are loose.
/// All values here are in meters (km figures from the spec multiplied by 1000).
/// </summary>
public static class LoopSeedData
{
    public static IReadOnlyList<Loop> Loops { get; } =
    [
        Loop.Create(
            name: "Five Loops of Central Park",
            description: "Five flat-ish laps of NYC's Central Park loop. Open to everyone — finish it on distance alone.",
            category: LoopCategory.Urban,
            tier: LoopTier.Easy,
            targetDistanceMeters: 49_000,
            targetElevationMeters: 300,
            realWorldReference: "Central Park, NYC"),

        Loop.Create(
            name: "Giza Pyramid Circuit x3",
            description: "Three laps around the Giza plateau. Short and flat.",
            category: LoopCategory.Urban,
            tier: LoopTier.Easy,
            targetDistanceMeters: 21_000,
            targetElevationMeters: 60,
            realWorldReference: "Giza Plateau, Egypt"),

        Loop.Create(
            name: "Thames Path Loop",
            description: "A riverside half-and-a-bit along the Thames. Gentle rolling terrain.",
            category: LoopCategory.Urban,
            tier: LoopTier.Moderate,
            targetDistanceMeters: 30_000,
            targetElevationMeters: 150,
            realWorldReference: "London, UK"),

        Loop.Create(
            name: "Zermatt Marathon",
            description: "St. Niklaus to Gornergrat. A genuine alpine marathon with serious climbing.",
            category: LoopCategory.Classic,
            tier: LoopTier.Moderate,
            targetDistanceMeters: 42_000,
            targetElevationMeters: 1_850,
            realWorldReference: "St. Niklaus to Gornergrat, Switzerland"),

        Loop.Create(
            name: "Western States 100",
            description: "The legendary Sierra Nevada 100-miler. A true ultra.",
            category: LoopCategory.Classic,
            tier: LoopTier.Epic,
            targetDistanceMeters: 161_000,
            targetElevationMeters: 5_500,
            realWorldReference: "Sierra Nevada, USA"),

        Loop.Create(
            name: "Hardrock 100",
            description: "San Juan Mountains 100-miler with brutal vertical. A boss level.",
            category: LoopCategory.Classic,
            tier: LoopTier.Epic,
            targetDistanceMeters: 160_000,
            targetElevationMeters: 10_000,
            realWorldReference: "San Juan Mountains, USA"),

        Loop.Create(
            name: "UTMB (Boss Level)",
            description: "The Mont Blanc massif circuit. Hard-locked behind real mountains.",
            category: LoopCategory.Classic,
            tier: LoopTier.Epic,
            targetDistanceMeters: 171_000,
            targetElevationMeters: 10_000,
            realWorldReference: "Mont Blanc massif"),

        Loop.Create(
            name: "Mount Doom Ascent",
            description: "A short, savage climb up Mt Ngauruhoe. Steep and unrelenting.",
            category: LoopCategory.Fantasy,
            tier: LoopTier.Moderate,
            targetDistanceMeters: 16_000,
            targetElevationMeters: 1_100,
            realWorldReference: "Mt Ngauruhoe, NZ"),

        Loop.Create(
            name: "The Long Climb to Mordor",
            description: "A fantasy ultra: sixty kilometres and three thousand metres of climbing into Mordor.",
            category: LoopCategory.Fantasy,
            tier: LoopTier.Hard,
            targetDistanceMeters: 60_000,
            targetElevationMeters: 3_000,
            realWorldReference: "Middle-earth"),
    ];
}
