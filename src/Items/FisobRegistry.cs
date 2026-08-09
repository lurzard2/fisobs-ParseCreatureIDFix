using Fisobs.Core;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using ObjectType = AbstractPhysicalObject.AbstractObjectType;

namespace Fisobs.Items;

/// <summary>
/// A registry that stores <see cref="Fisob"/> instances and the hooks relevant to them.
/// </summary>
public sealed class FisobRegistry : Registry
{
    bool init;

    /// <summary>
    /// The singleton instance of this class.
    /// </summary>
    public static FisobRegistry Instance { get; } = new FisobRegistry();

    readonly Dictionary<ObjectType, Fisob> fisobs = new();

    private FisobRegistry() { }

    /// <inheritdoc/>
    protected override void Process(IContent entry)
    {
        if (entry is Fisob fisob) {
            fisobs[fisob.Type] = fisob;
        }
    }

    /// <inheritdoc/>
    protected override void Initialize()
    {
        On.RainWorld.OnModsInit += RainWorld_OnModsInit;
        On.ItemSymbol.SymbolDataFromItem += ItemSymbol_SymbolDataFromItem;
        On.ItemSymbol.ColorForItem += ItemSymbol_ColorForItem;
        On.ItemSymbol.SpriteNameForItem += ItemSymbol_SpriteNameForItem;
        On.SaveState.AbstractPhysicalObjectFromString += SaveState_AbstractPhysicalObjectFromString;
    }

    private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);

        if (!init) {
            init = true;
            foreach (var common in fisobs.Values) {
                common.LoadResources(self);
            }
        }
    }

    private IconSymbol.IconSymbolData? ItemSymbol_SymbolDataFromItem(On.ItemSymbol.orig_SymbolDataFromItem orig, AbstractPhysicalObject item)
    {
        if (fisobs.TryGetValue(item.type, out var fisob)) {
            return new IconSymbol.IconSymbolData(CreatureTemplate.Type.StandardGroundCreature, item.type, fisob.Icon.Data(item));
        }
        return orig(item);
    }

    private Color ItemSymbol_ColorForItem(On.ItemSymbol.orig_ColorForItem orig, ObjectType itemType, int intData)
    {
        if (fisobs.TryGetValue(itemType, out var fisob)) {
            return fisob.Icon.SpriteColor(intData);
        }
        return orig(itemType, intData);
    }

    private string ItemSymbol_SpriteNameForItem(On.ItemSymbol.orig_SpriteNameForItem orig, ObjectType itemType, int intData)
    {
        if (fisobs.TryGetValue(itemType, out var fisob)) {
            return fisob.Icon.SpriteName(intData);
        }
        return orig(itemType, intData);
    }

    private AbstractPhysicalObject? SaveState_AbstractPhysicalObjectFromString(On.SaveState.orig_AbstractPhysicalObjectFromString orig, World world, string objString)
    {
        
        var data = objString.Split(new[] { "<oA>" }, StringSplitOptions.None);
        
        /*
         * ID.-1.9646<oB>0    //ID (+ rippleLayer)
         * <oA>ENUM           // Type
         * <oA>XX_A01.77.25.2 // pos in room
         * <oA>67             // WorldCoordinate
         * <oA>0              // placedObjectIndex
         */

        var obj = orig(world, objString);
        
        string id;
        if (data[0].Contains("<oB>")) {
            // An APO's Ripple Layer when written is injected between ID and Type - "<oA>ID<oB>rippleLayer<oA>Type(...)".
            var s = Regex.Split(data[0], "<oB>");
            id = s[0];
            obj.rippleLayer =  int.Parse(s[1]);
        } else {
            id = data[0];
        }
        var type = new ObjectType(data[1]);
        string worldCoord = data.Length > 2 ? data[2] : "";
        string customData = data.Length > 3 ? data[3] : "";
        
        if (fisobs.TryGetValue(type, out Fisob f)) {
            var newData = new EntitySaveData(f.Type, EntityID.FromString(id), WorldCoordinate.FromString(worldCoord), customData, SaveUtils.PopulateUnrecognizedStringAttrs(data, 4));
            obj = f.Parse(world, newData, null);
        }

        return obj;
    }
}
