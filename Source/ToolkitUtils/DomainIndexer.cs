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
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using SirRandoo.ToolkitUtils.Helpers;
using SirRandoo.ToolkitUtils.Interfaces;
using SirRandoo.ToolkitUtils.Models;
using Verse;

namespace SirRandoo.ToolkitUtils;

[StaticConstructorOnStartup]
internal static class DomainIndexer
{
    internal static readonly MutatorEntry[] Mutators;
    internal static readonly SelectorEntry[] Selectors;

    private static readonly string[] FilteredNamespaceRoots =
    {
        "System",
        "Unity",
        "Steamworks",
        "Verse",
        "RimWorld",
        "Utf8Json",
        "Mono",
        "RestSharp",
        "SimpleJSON",
        "MoonSharp",
        "TwitchLib",
        "Newtonsoft",
        "HugsLib",
        "HarmonyLib",
        "MS",
        "NAudio",
        "TMPro"
    };
    /// <summary>
    /// Initializes the <see cref="DomainIndexer"/> class by processing all assemblies in the current application
    /// domain.
    /// </summary>
    /// <remarks>This static constructor scans all assemblies in the current application domain, excluding
    /// those in the Global Assembly Cache (GAC), to populate the <see cref="Mutators"/> and <see cref="Selectors"/>
    /// arrays. If any assemblies cannot be processed, a warning is logged with details about the failure.</remarks>
    static DomainIndexer()
    {
        TkUtils.Logger.Warn("=== CHECKING FOR HAR ASSEMBLY ===");
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.FullName.Contains("ToolkitUtils.HAR"))
            {
                TkUtils.Logger.Warn($"✓ FOUND HAR ASSEMBLY: {assembly.FullName}");
            }
        }
        var builder = new StringBuilder();
        var mutators = new List<MutatorEntry>();
        var selectors = new List<SelectorEntry>();

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GlobalAssemblyCache)
            {
                continue;
            }

            try
            {
                ProcessAssembly(assembly, mutators, selectors);
            }
            catch (Exception e)
            {
                builder.Append($"  - {assembly.FullName}  |  Reason: {e.GetType().Name}({e.Message})\n");
            }
        }

        if (builder.Length > 0)
        {
            builder.Insert(0, "The following assemblies could not be processed:\n");
            TkUtils.Logger.Warn(builder.ToString());
        }

        Mutators = mutators.ToArray();
        Selectors = selectors.ToArray();
    }
    /// <summary>
    /// Processes the types within the specified assembly, identifying and adding mutators and selectors to the provided
    /// collections based on the types' characteristics.
    /// </summary>
    /// <remarks>This method iterates through all types in the given assembly, filtering out interfaces,
    /// abstract classes,  compiler-generated types, and types within namespaces that match predefined exclusion
    /// patterns. For each  remaining type, it attempts to process the type to identify mutators and selectors. If an
    /// error occurs  while processing a type, the error is logged, and processing continues with the next
    /// type.</remarks>
    /// <param name="assembly">The assembly to process. Must not be <see langword="null"/>.</param>
    /// <param name="mutators">A collection to which discovered mutator entries will be added. Must not be <see langword="null"/>.</param>
    /// <param name="selectors">A collection to which discovered selector entries will be added. Must not be <see langword="null"/>.</param>
    private static void ProcessAssembly(Assembly assembly, ICollection<MutatorEntry> mutators, ICollection<SelectorEntry> selectors)
    {
        TkUtils.Logger.Warn($"=== PROCESSING ASSEMBLY: {assembly.FullName} ===");

        foreach (Type type in assembly.GetTypes())
        {
            if (type.IsInterface || type.IsAbstract || type.GetTypeInfo().IsDefined(typeof(CompilerGeneratedAttribute), true)
                || FilteredNamespaceRoots.Any(r => type.Namespace?.StartsWith(r) == true))
            {
                continue;
            }

            // Debug: Check if this is our HAR provider
            if (type.FullName?.Contains("AlienCompatibilityProvider") == true)
            {
                TkUtils.Logger.Warn($"FOUND HAR PROVIDER: {type.FullName}");
                TkUtils.Logger.Warn($"Is ICompatibilityProvider: {typeof(ICompatibilityProvider).IsAssignableFrom(type)}");
            }

            try
            {
                ProcessType(type, mutators, selectors);
            }
            catch (Exception e)
            {
                TkUtils.Logger.Error($"Could not process type {type.Name}", e);
            }
        }
    }
    /// <summary>
    /// Processes the specified type to determine if it should be registered as a compatibility provider, selector, or
    /// mutator, and updates the provided collections accordingly.
    /// </summary>
    /// <remarks>If the type implements <see cref="ICompatibilityProvider"/>, it is processed by the
    /// compatibility registry. For generic types, the method checks if the type matches the criteria for either a
    /// selector or a mutator based on its generic type hierarchy. Matching types are processed and added to the
    /// respective collections.</remarks>
    /// <param name="type">The <see cref="Type"/> to process. This type is analyzed to determine its compatibility with specific
    /// interfaces.</param>
    /// <param name="mutators">A collection to which mutator entries will be added if the type matches the criteria for a mutator.</param>
    /// <param name="selectors">A collection to which selector entries will be added if the type matches the criteria for a selector.</param>
    private static void ProcessType(Type type, ICollection<MutatorEntry> mutators, ICollection<SelectorEntry> selectors)
    {
        bool isGeneric = type.IsGenericType || type.GetInterfaces().Any(i => i.IsGenericType);


        if (typeof(ICompatibilityProvider).IsAssignableFrom(type))
        {
            CompatRegistry.ProcessType(type);
        }
        else
        {
            switch (isGeneric)
            {
                case true when GameHelper.IsGenericTypeDeep(type, typeof(ISelectorBase<>), false, typeof(IShopItemBase)):
                    selectors.Add(ProcessSelector(type));

                    break;
                case true when GameHelper.IsGenericTypeDeep(type, typeof(IMutatorBase<>), false, typeof(IShopItemBase)):
                    mutators.Add(ProcessMutator(type));

                    break;
            }
        }
    }
    /// <summary>
    /// Processes the specified selector type and determines its associated editor target.
    /// </summary>
    /// <remarks>The method evaluates the generic type argument of the selector against predefined types such
    /// as <see cref="ThingItem"/>, <see cref="TraitItem"/>, <see cref="PawnKindItem"/>, and <see cref="EventItem"/> to
    /// determine the corresponding editor target. If no match is found, the target is set to <see
    /// cref="EditorTarget.Any"/>.</remarks>
    /// <param name="selector">The type of the selector to process. This must implement a generic interface derived from <see
    /// cref="ISelectorBase{T}"/>.</param>
    /// <returns>A <see cref="SelectorEntry"/> instance representing the processed selector, with its <see
    /// cref="SelectorEntry.Target"/> property set to the appropriate <see cref="EditorTarget"/> value based on the
    /// selector's generic type argument.</returns>
    private static SelectorEntry ProcessSelector(Type selector)
    {
        Type selectorBase = typeof(ISelectorBase<>);
        var entry = new SelectorEntry(selector);

        if (GameHelper.IsGenericTypeDeep(selector, selectorBase, false, typeof(ThingItem)))
        {
            entry.Target = EditorTarget.Item;
        }
        else if (GameHelper.IsGenericTypeDeep(selector, selectorBase, false, typeof(TraitItem)))
        {
            entry.Target = EditorTarget.Trait;
        }
        else if (GameHelper.IsGenericTypeDeep(selector, selectorBase, false, typeof(PawnKindItem)))
        {
            entry.Target = EditorTarget.Pawn;
        }
        else if (GameHelper.IsGenericTypeDeep(selector, selectorBase, false, typeof(EventItem)))
        {
            entry.Target = EditorTarget.Event;
        }
        else
        {
            entry.Target = EditorTarget.Any;
        }

        return entry;
    }
    /// <summary>
    /// Processes a mutator type and determines its associated editor target.
    /// </summary>
    /// <remarks>The method evaluates the generic type parameter of the provided mutator type to determine its
    /// target editor category. If the mutator type matches a specific item type (e.g., <see cref="ThingItem"/>, <see
    /// cref="TraitItem"/>, etc.),  the corresponding <see cref="EditorTarget"/> value is assigned. If no specific match
    /// is found, the target is set to <see cref="EditorTarget.Any"/>.</remarks>
    /// <param name="mutator">The type of the mutator to process. This must implement the <see cref="IMutatorBase{T}"/> interface.</param>
    /// <returns>A <see cref="MutatorEntry"/> instance containing the processed mutator type and its associated editor target.</returns>
    private static MutatorEntry ProcessMutator(Type mutator)
    {
        Type mutatorBase = typeof(IMutatorBase<>);
        var entry = new MutatorEntry(mutator);

        if (GameHelper.IsGenericType(mutator, mutatorBase, false, typeof(ThingItem)))
        {
            entry.Target = EditorTarget.Item;
        }
        else if (GameHelper.IsGenericType(mutator, mutatorBase, false, typeof(TraitItem)))
        {
            entry.Target = EditorTarget.Trait;
        }
        else if (GameHelper.IsGenericType(mutator, mutatorBase, false, typeof(PawnKindItem)))
        {
            entry.Target = EditorTarget.Pawn;
        }
        else if (GameHelper.IsGenericType(mutator, mutatorBase, false, typeof(EventItem)))
        {
            entry.Target = EditorTarget.Event;
        }
        else
        {
            entry.Target = EditorTarget.Any;
        }

        return entry;
    }
    /// <summary>
    /// Specifies the target type for an editor operation.
    /// </summary>
    /// <remarks>This enumeration defines the possible targets that an editor can operate on.  Use this to
    /// indicate the context or scope of an editing action.</remarks>
    internal enum EditorTarget { Any, Item, Trait, Pawn, Event }
    /// <summary>
    /// Represents an entry that associates a specific type with an editor target.
    /// </summary>
    /// <remarks>This record is used to store metadata about a type and its corresponding editor target. The
    /// <see cref="Target"/> property can be used to get or set the associated editor target.</remarks>
    /// <param name="Type"></param>
    internal record SelectorEntry(Type Type)
    {
        internal EditorTarget Target { get; set; }
    }
    /// <summary>
    /// Represents an entry that associates a type with a corresponding editor target.
    /// </summary>
    /// <remarks>This record is used to store metadata about a specific type and its related editor target.
    /// The <see cref="Target"/> property can be used to get or set the associated editor target.</remarks>
    /// <param name="Type"></param>
    internal record MutatorEntry(Type Type)
    {
        internal EditorTarget Target { get; set; }
    }
}
