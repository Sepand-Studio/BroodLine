using System;
using UnityEngine;

namespace Broodline.Creatures
{
    /// Base and underside per species. THE BASE HEXES ARE MIRRORED FROM
    /// `PaletteContrast.Species` (Broodline.UI) because this assembly
    /// references nothing; `CreatureColourTests` in Broodline.Game.Tests
    /// asserts the mirror. The underside is the value structure bible 10.4
    /// measured Pale as needing - darker on every species, and cooler on
    /// Pale, whose base is the palest colour in the game.
    public static class SpeciesColours
    {
        static readonly (string Name, string Base, string Under)[] Table =
        {
            ("vetch",   "#6ba7c0", "#3f6f86"),
            ("ember",   "#e5867a", "#a8574d"),
            ("skitter", "#e8b34a", "#a67a22"),
            ("hollow",  "#7a6ac0", "#4d4088"),
            ("loam",    "#7cc492", "#4c8a60"),
            ("pale",    "#c6cede", "#8f9bb5"),
        };

        public static string BaseHex(string species)
        {
            foreach (var row in Table)
                if (string.Equals(row.Name, species, StringComparison.OrdinalIgnoreCase)) return row.Base;
            return null;
        }

        public static (Color Base, Color Under) For(string species)
        {
            foreach (var row in Table)
                if (string.Equals(row.Name, species, StringComparison.OrdinalIgnoreCase))
                    return (Parse(row.Base), Parse(row.Under));
            return (Color.magenta, Color.black);   // loud, never silent
        }

        public static readonly Color RaiderBase = Parse("#3a3547");
        public static readonly Color RaiderUnder = Parse("#221f2c");
        public static readonly Color RaiderAccent = Parse("#ff6b5c");

        public static Color Parse(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var c)) throw new ArgumentException("not a colour: " + hex);
            return c;
        }
    }
}
