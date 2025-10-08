using AlienRace;
using RimWorld;
using SirRandoo.ToolkitUtils.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace SirRandoo.ToolkitUtils.HAR;
/// <summary>
/// Provides compatibility support for the Humanoid Alien Races (HAR) mod, enabling integration with alien race-specific
/// features.
/// </summary>
/// <remarks>This class is designed to work with the Humanoid Alien Races (HAR) mod, identified by the mod ID
/// "erdelf.HumanoidAlienRaces". It implements the <see cref="IAlienCompatibilityProvider"/> interface to provide
/// functionality such as body type reassignment, trait validation, and xenotype restrictions for alien races. The
/// provider is automatically registered if the HAR mod is active.</remarks>
/// <param name="ModId"></param>
public record AlienCompatibilityProvider(string ModId = "erdelf.HumanoidAlienRaces") : IAlienCompatibilityProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AlienCompatibilityProvider"/> class with a default alien race
    /// identifier.
    /// </summary>
    /// <remarks>This constructor sets the default alien race identifier to "erdelf.HumanoidAlienRaces". Use
    /// this constructor when the default identifier is sufficient for your use case.</remarks>
    public AlienCompatibilityProvider() : this("erdelf.HumanoidAlienRaces")
    {
    }
    /// <summary>
    /// Initializes the <see cref="AlienCompatibilityProvider"/> class and registers it with the compatibility registry if
    /// the Humanoid Alien Races (HAR) mod is detected.
    /// </summary>
    /// <remarks>This static constructor checks for the presence of the HAR mod by its identifier
    /// ("erdelf.HumanoidAlienRaces"). If the mod is active, the constructor attempts to register the <see
    /// cref="AlienCompatibilityProvider"/> with the compatibility registry using an internal method, if available. Debug
    /// logs are generated to indicate the detection status and the outcome of the registration process.</remarks>
    //static AlienCompatibilityProvider()
    //{
    //    TkUtils.Logger.Debug("Checking for HAR mod...");
    //    bool harLoaded = ModLister.GetActiveModWithIdentifier("erdelf.HumanoidAlienRaces") != null;

    //    TkUtils.Logger.Debug($"HAR mod detected: {harLoaded}");
    //}
    /// <inheritdoc/>
    public bool TryReassignBody(Pawn pawn)
    {
        if (pawn.def is not ThingDef_AlienRace alienRace)
        {
            return false;
        }

        AlienPartGenerator generator = alienRace.alienRace.generalSettings.alienPartGenerator;


        if (generator.bodyTypes.NullOrEmpty() || generator.bodyTypes.Contains(pawn.story.bodyType))
        {
            return true;
        }

        List<BodyTypeDef> bodyTypes = generator.bodyTypes.ListFullCopy();

        if (bodyTypes.Count > 0)
        {
            switch (pawn.gender)
            {
                case Gender.Male:
                    bodyTypes.Remove(BodyTypeDefOf.Female);

                    break;
                case Gender.Female:
                    bodyTypes.Remove(BodyTypeDefOf.Male);

                    break;
            }
        }

        pawn.story.bodyType = bodyTypes.TryRandomElement(out BodyTypeDef newBody) ? newBody : BodyTypeDefOf.Thin;

        return true;
    }

    /// <inheritdoc/>
    public bool IsTraitForced(Pawn pawn, string? defName, int degree)
    {
        if (pawn.def is not ThingDef_AlienRace alienRace || alienRace.alienRace.generalSettings.forcedRaceTraitEntries.NullOrEmpty())
        {
            return false;
        }

        foreach (AlienChanceEntry<TraitWithDegree> entry in alienRace.alienRace.generalSettings.forcedRaceTraitEntries)
        {
            if (string.Equals(entry.entry.def.defName, defName) && entry.entry.degree == degree)
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public bool IsTraitDisallowed(Pawn pawn, string defName, int degree)
    {
        if (pawn.def is not ThingDef_AlienRace alienRace || alienRace.alienRace.generalSettings.disallowedTraits.NullOrEmpty())
        {
            return false;
        }

        foreach (AlienChanceEntry<TraitWithDegree> entry in alienRace.alienRace.generalSettings.disallowedTraits)
        {
            if (string.Equals(entry.entry.def.defName, defName) && entry.entry.degree == degree)
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public bool IsTraitAllowed(Pawn pawn, TraitDef traitDef, int degree = -10) =>
        !IsTraitDisallowed(pawn, traitDef.defName, degree) && !IsTraitForced(pawn, traitDef.defName, degree);

    /// <inheritdoc/>
    /// /// <summary>
    /// Gets the list of xenotypes allowed for the specified race
    /// </summary>
    /// <param name="raceDef">The race definition to check</param>
    /// <returns>List of allowed xenotype definition names</returns>
    public List<string> GetAllowedXenotypes(ThingDef raceDef)
    {
        TkUtils.Logger.Debug($"=== HAR PROVIDER DEBUG for {raceDef?.defName} ===");
        TkUtils.Logger.Debug($"RaceDef is null: {raceDef == null}");
        TkUtils.Logger.Debug($"RaceDef type: {raceDef?.GetType()}");
        TkUtils.Logger.Debug($"Is ThingDef_AlienRace: {raceDef is ThingDef_AlienRace}");

        if (!ModsConfig.BiotechActive || raceDef == ThingDefOf.Human)
        {
            TkUtils.Logger.Debug($"Returning empty list - Biotech inactive or human race");
            return new List<string>();
        }

        if (raceDef is ThingDef_AlienRace alienRace)
        {
            TkUtils.Logger.Debug($"Found ThingDef_AlienRace: {alienRace.defName}");
            TkUtils.Logger.Debug($"AlienRace field: {alienRace.alienRace != null}");

            var restriction = alienRace.alienRace?.raceRestriction;
            TkUtils.Logger.Debug($"Race restriction: {restriction != null}");

            if (restriction != null)
            {
                TkUtils.Logger.Debug($"onlyUseRaceRestrictedXenotypes: {restriction.onlyUseRaceRestrictedXenotypes}");
                TkUtils.Logger.Debug($"whiteXenotypeList count: {restriction.whiteXenotypeList?.Count ?? 0}");

                if (restriction.whiteXenotypeList != null)
                {
                    foreach (var xeno in restriction.whiteXenotypeList)
                    {
                        TkUtils.Logger.Debug($"Whitelisted xenotype: {xeno.defName}");
                    }
                }
            }
        }
        else
        {
            TkUtils.Logger.Debug($"Race {raceDef.defName} is NOT a ThingDef_AlienRace");
        }

        bool isHARRace = false;
        try
        {
            var alienRaceField = raceDef.GetType().GetField("alienRace");
            isHARRace = alienRaceField != null;
            TkUtils.Logger.Debug($"Is HAR race (via reflection): {isHARRace}");
        }
        catch
        {
            TkUtils.Logger.Debug($"Reflection check failed - not a HAR race");
        }

        if (!isHARRace)
        {
            TkUtils.Logger.Debug($"Not a HAR race - returning empty list");
            return new List<string>();
        }

        var result = DefDatabase<XenotypeDef>.AllDefs
            .Where(xenotype => RaceRestrictionSettings.CanUseXenotype(xenotype, raceDef))
            .Select(xenotype => xenotype.defName)
            .ToList();

        TkUtils.Logger.Debug($"Final result: {result.Count} xenotypes");
        TkUtils.Logger.Debug($"=== END HAR PROVIDER DEBUG ===");

        return result;
    }
    /// <inheritdoc/>
    public bool IsXenotypeAllowed(ThingDef raceDef, XenotypeDef xenotype)
    {
        if (raceDef == ThingDefOf.Human)
            return true;

        return RaceRestrictionSettings.CanUseXenotype(xenotype, raceDef);
    }
}

