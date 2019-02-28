# ONI-MaterialProbeMod

A Mod for Oxygen Not Included to add a tool for analysing large areas to the game.

Requires [ONI-Modloader](https://github.com/javisar/ONI-Modloader)

Forum: https://forums.kleientertainment.com/forums/topic/103110-mods-material-probe/

## What Does It Do?

The Material Probe looks at an area of cells (tiles) and analyzes them to determine certain properties about them as a group.

 - In Mass mode, the probe tells you what the total mass of the area is, and a break down of the materials in the area.
 - In Temperature mode, the probe calculates temperature over the entire area.
 - In Germs mode, the probe analyzes the populations of germs in the probed area.
 - In Biome mode, the probe just tells you what the biome for the area is. This is most useful for detecting the space biome.

In each mode (except Biome), the probe will give you information about the range of properties in the area: minimum, maximum and average. The area will also be colored according to that range. For instance in Temperature mode, the blue area represents the lowest temperature in the area, and the red area represents the hottest area.

This mod makes absolutely *no gameplay changes*. Technically, it doesn't even give you information you don't already have access to, only improving your tools to interpret it. You can add and remove the mod from your game as much as you like.

## What Are These Settings?

When you point somewhere, the probe remembers some properties of the cell you're pointing at. The "Match" settings restrict the probe to cells matching the properties of the first cell. So in Mass mode, with "Match Element" checked, if you point at Oxygen, the probe will only add other Oxygen cells to the probed area.

If checked:

 - Match Element - The probe only includes cells with the same element/germ type as the first cell.
 - Match Phase - The probe only includes cells with the same phase (gas, liquid, solid) as the first cell.
 - Match Constructed - The probe only includes cells with the same construction state as the first cell. (Sandstone tiles will be treated differently from natural sandstone.)
 - Match Biome - The probe only includes cells in the same biome as the first cell.
 - Ignore Abysallite/Neutronium - The probe ignores Abysallite and Neutronium, since they usually have weird mass/temperature.

The Range simply controls how far the probe is allowed to reach. The color palette determines which colors the probe uses to show elements. The default should be fine for most people. The - and + adjust the range by 1, or by 5 if you hold Shift.

Color Palettes:

 - Default (Recommended) - The same as UI, but some colors are overridden to be more unique from other colors.
 - Flat - All elements are colored white. This is useful if you care more about density than the element.
 - (Hold Shift) Substance - The substance color of the element, as specified by the game.
 - (Hold Shift) UI - The ui color of the element, as specified by the game.
 - (Hold Shift) Conduit - The conduit color of the element, as specified by the game.
 - (Hold Shift) Hash - A color generated based on the name of the element. These colors tend to be very random.

Shading controls what the range the probe uses for shading. Normally, the high value is the maximum value in the probe, and the low value is the minimum value in the probe. However, this can cause problems if there are extreme values. For instance, if you have 100 cells of about 1000g of Oxygen, but one cell has 10g instead, all the shading around 1000g will be lost, because of the single low value. The alternate range displays use some light statistics to get better range values. In any case, what values are represented by each shade are shown in the settings panel.

 - Min/Max - The high and low values are the maximum and minimum values, respectively. The most intuitive, but can have artifacts.
 - 2 Std. Dev. - The high and low values are 2 standard deviations above and below the average, respectively. This can be confusing if you don't know what that means, but generally results in much better shading.
 - 1 Std. Dev. - The same as above, but uses 1 standard deviation instead.

There is one hidden config in the settings file (generated on first run). "CamoflageNeutronium" causes Neutronium (the element under geysers) try to camoflage itself in the probe. It is still ignored for statistics if that option is selected. This *does* mean the probe will give you false information about neutronium when shading the world, but makes geysers much more difficult to find with the probe. If you would rather the probe be completely honest with you, or you're fine with finding geysers with the probe, you can disable this option.

## FAQ

 - What does NEGLIGIBLE RANGE mean?
   - There is a very small range of density/temperature over the area you're probing, so the probe won't show the greatest/least or hottest/coldest colors in the area.
 - The probe area doesn't show density shading, or it all shows as hot/cold.
   - There may be a very dense/light or hot/cold cell in the area you're probing. In min/max mode density/temperature shading has to cover the whole range, so an extreme value can make the shading very flat. In particular, gases can be tricky, because even if your whole base averages 1800 g, there might be a few tiles with only a few hundred grams. Try changing the Range Display setting.
 - Hey, this cell is colored blue, but it's boiling hot here!
   - Remember that the shading is *relative*. That blue spot may be 100 degrees, but it's still the coolest area in the area you're looking at.
 - Why is the UI bad?
   - Because the UI in Oxygen Not Included is built out of prefabs in the Unity Editor. I don't have access to the Editor for the game, or most of the prefabs, so building a proper looking UI is hard. I would have to steal prefabs from other parts of the UI and then put everything together with code. Instead, I used Unity's IMGUI to just get it working.
 