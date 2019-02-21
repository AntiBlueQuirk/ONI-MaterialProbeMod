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

## What Are These Settings?

When you point somewhere, the probe remembers some properties of the cell you're pointing at. The "Match" settings restrict the probe to cells matching the properties of the first cell. So in Mass mode, with "Match Element" checked, if you point at Oxygen, the probe will only add other Oxygen cells to the probed area.

If checked:

 - Match Element - The probe only includes cells with the same element/germ type as the first cell.
 - Match Phase - The probe only includes cells with the same phase (gas, liquid, solid) as the first cell.
 - Match Constructed - The probe only includes cells with the same construction state as the first cell. (Sandstone tiles will be treated differently from natural sandstone.)
 - Match Biome - The probe only includes cells in the same biome as the first cell.
 - Ignore Abysallite/Neutronium - The probe ignores Abysallite and Neutronium, since they usually have weird mass/temperature.

The Range simply controls how far the probe is allowed to reach. The color palette determines which colors the probe uses to show elements. The default should be fine for most people.

## FAQ

 - Why is the UI bad?
   - Because the UI in Oxygen Not Included is built out of prefabs in the Unity Editor. I don't have access to the Editor for the game, or most of the prefabs, so building a proper looking UI is hard. I would have to steal prefabs from other parts of the UI and then put everything together with code. Instead, I used Unity's IMGUI to just get it working.
 - What does NEGLIGIBLE RANGE mean?
   - There is a very small range of density/temperature over the area you're probing, so the probe won't show the greatest/least or hottest/coldest colors in the area.
 - The probe area doesn't show density shading, or it all shows as hot/cold.
   - There may be a very dense/light or hot/cold cell in the area you're probing. The density/temperature shading has to cover the whole range, so an extreme value can make the shading very flat. Look at the minimum/maximum of the probe area, then try adjusting the probe settings or try probing a smaller area. In particular, gases can be tricky, because even if your whole base averages 1800 g, there might be a few tiles with only a few hundred grams.
