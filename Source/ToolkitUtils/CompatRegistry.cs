// ToolkitUtils
// Copyright (C) 2021  SirRandoo
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using SirRandoo.ToolkitUtils.Interfaces;
using TwitchToolkit;
using Verse;

namespace SirRandoo.ToolkitUtils;

/// <summary>
///     A registry for housing the various compatibility providers within
///     the mod.
/// </summary>
public static class CompatRegistry
{
    private static readonly List<ICompatibilityProvider> CompatibilityProviders = new List<ICompatibilityProvider>();
    private static readonly List<ISurgeryHandler> SurgeryHandlers = new List<ISurgeryHandler>();
    private static readonly List<IUsabilityHandler> UsabilityHandlers = new List<IUsabilityHandler>();
    private static readonly List<IHealHandler> HealHandlers = new List<IHealHandler>();
    private static readonly List<IPawnPowerHandler> PawnPowerHandlers = new List<IPawnPowerHandler>();

    /// <summary>
    ///     The main compatibility provider for A RimWorld of Magic.
    /// </summary>
    public static IMagicCompatibilityProvider? Magic { get; internal set; }

    /// <summary>
    ///     Whether Twitch Toolkit is compatible with the current version of
    ///     RimWorld.
    /// </summary>
    public static bool ToolkitCompatible { get; } = TwitchToolkit.Toolkit.Mod.Content.ModMetaData.VersionCompatible;

    /// <summary>
    ///     The main compatibility provider for Humanoid Alien Races.
    /// </summary>
    public static IAlienCompatibilityProvider? Alien { get; internal set; }

    /// <summary>
    ///     A collection of surgery handlers used to ensure surgeries
    ///     requested by viewers are executed properly on their pawn for
    ///     their pawn's given race.
    /// </summary>
    public static IEnumerable<ISurgeryHandler> AllSurgeryHandlers => SurgeryHandlers;

    /// <summary>
    ///     A collection of usability handlers used to ensure items used by
    ///     viewers are executed properly on their pawn.
    /// </summary>
    public static IEnumerable<IUsabilityHandler> AllUsabilityHandlers => UsabilityHandlers;

    /// <summary>
    ///     A collection of heal handlers used to ensure certain hediffs
    ///     aren't healed from viewer's pawns, like blindsight.
    /// </summary>
    public static IEnumerable<IHealHandler> AllHealHandlers => HealHandlers;

    /// <summary>
    ///     A collection of power handlers used to provide an index of the
    ///     various powers a pawn can have across mods.
    /// </summary>
    public static IEnumerable<IPawnPowerHandler> AllPawnPowerHandlers => PawnPowerHandlers;

    /// <summary>
    ///     A collection of compatibility providers, including specialized
    ///     providers, currently registered within the mod.
    /// </summary>
    public static IEnumerable<ICompatibilityProvider> AllCompatibilityProviders => CompatibilityProviders;
    /// <summary>
    /// Processes the specified type to determine if it implements <see cref="ICompatibilityProvider"/>  and, if so,
    /// registers it for compatibility handling based on its associated mod identifier.
    /// </summary>
    /// <remarks>This method checks if the provided type implements the <see cref="ICompatibilityProvider"/>
    /// interface.  If the type does not implement the interface, the method exits without further action.  If the type
    /// implements the interface, the method verifies whether the mod associated with the  <see
    /// cref="ICompatibilityProvider.ModId"/> is active. If the mod is active, the provider is registered  for
    /// compatibility handling.</remarks>
    /// <param name="type">The <see cref="Type"/> to process. Must represent a class that can be instantiated.</param>
    internal static void ProcessType(Type type)
    {
        // TkUtils.Logger.Debug($"Processing type for compatibility: {type.FullName}");
        /// Check if the type implements ICompatibilityProvider
        if (Activator.CreateInstance(type) is not ICompatibilityProvider provider)
        {
            TkUtils.Logger.Warn($"Type {type.FullName} is not an ICompatibilityProvider");
            return;
        }
        // TkUtils.Logger.Debug($"Found provider: {provider.GetType().FullName} with ModId: {provider.ModId}");
        bool dependencyLoaded = provider.ModId.StartsWith("Ludeon")
            ? ModLister.GetExpansionWithIdentifier(provider.ModId)?.Status == ExpansionStatus.Active
            : ModLister.GetActiveModWithIdentifier(provider.ModId) != null;
        // TkUtils.Logger.Debug($"Dependency loaded for {provider.ModId}: {dependencyLoaded}");
        if (!dependencyLoaded)
        {
            TkUtils.Logger.Warn($"Skipping registration of provider {provider.GetType().FullName} as its dependency {provider.ModId} is not loaded.");
            return;
        }
        RegisterAndCatalogue(provider);
    }
    /// <summary>
    /// Registers a compatibility provider and categorizes it into the appropriate handler collection based on its type.
    /// </summary>
    /// <remarks>This method adds the specified <paramref name="provider"/> to the general compatibility
    /// provider collection  and, if applicable, to a specific handler collection based on its implemented interface.
    /// Supported interfaces  include <see cref="ISurgeryHandler"/>, <see cref="IUsabilityHandler"/>, <see
    /// cref="IHealHandler"/>,  <see cref="IPawnPowerHandler"/>, <see cref="IMagicCompatibilityProvider"/>, and <see
    /// cref="IAlienCompatibilityProvider"/>.  If the provider implements <see cref="IMagicCompatibilityProvider"/> or
    /// <see cref="IAlienCompatibilityProvider"/>,  it will replace the existing magic or alien compatibility provider,
    /// respectively.</remarks>
    /// <param name="provider">The compatibility provider to register. Must implement one or more of the supported compatibility interfaces.</param>
    private static void RegisterAndCatalogue(ICompatibilityProvider provider)
    {
        TkUtils.Logger.Debug($"Registering compatibility provider: {provider.GetType().FullName}");
        /// Add to the general compatibility providers list
        CompatibilityProviders.Add(provider);
        /// Categorize into specific handler collections
        switch (provider)
        {
            case ISurgeryHandler surgery:
                SurgeryHandlers.Add(surgery);
                TkUtils.Logger.Debug($"Registered surgery handler: {surgery.GetType().FullName}");
                break;
            case IUsabilityHandler usability:
                UsabilityHandlers.Add(usability);
                TkUtils.Logger.Debug($"Registered usability handler: {usability.GetType().FullName}");
                break;
            case IHealHandler heal:
                HealHandlers.Add(heal);
                TkUtils.Logger.Debug($"Registered heal handler: {heal.GetType().FullName}");
                break;
            case IPawnPowerHandler pawnPower:
                PawnPowerHandlers.Add(pawnPower);
                TkUtils.Logger.Debug($"Registered pawn power handler: {pawnPower.GetType().FullName}");
                break;
            case IMagicCompatibilityProvider magic:
                Magic = magic;
                TkUtils.Logger.Debug($"Registered magic compatibility provider: {magic.GetType().FullName}");
                break;
            case IAlienCompatibilityProvider alien:
                Alien = alien;
                TkUtils.Logger.Debug($"Registered alien compatibility provider: {alien.GetType().FullName}");
                break;
        }
    }
    /// <summary>
    ///     Returns whether a hediff should be healed.
    /// </summary>
    /// <param name="hediff">The hediff to check</param>
    /// <returns>Whether it should be healed</returns>
    public static bool IsHealable(Hediff hediff)
    {
        foreach (IHealHandler handler in HealHandlers)
        {
            if (!handler.CanHeal(hediff))
            {
                TkUtils.Logger.Info($"The handler {handler.GetType().FullDescription()} requested that the hediff {hediff.def.defName} not be healed.");
                return false;
            }
        }
        return true;
    }
    /// <summary>
    ///     Returns whether a body part record should be healed.
    /// </summary>
    /// <param name="record">The record to check</param>
    /// <returns>Whether it should be healed</returns>
    public static bool IsHealable(BodyPartRecord record)
    {
        foreach (IHealHandler handler in HealHandlers)
        {
            if (!handler.CanHeal(record))
            {
                TkUtils.Logger.Info($"The handler {handler.GetType().FullDescription()} requested that the body part {record.def.defName} not be healed.");
                return false;
            }
        }
        return true;
    }
}
