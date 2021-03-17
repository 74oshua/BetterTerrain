# BetterTerrain
A Valheim mod that attempts to fix lag due to terraforming.

Valheim's default terrain system leaves much to be desired in terms of performance. The main reasons for this are TerrainModifiers, which are spawned every time a player makes a change to the world. In most games, when you change a destructible heightmap, the heightmap's verticies are overwritten with the new change. In Valheim, every time you strike the earth with a pickaxe or hoe, a TerrainModifier object is spawned, a Unity GameObject which represents the change. Then, when the heightmap needs to be rebuilt, it regenerates the terrain to it's default state, then iterates through every TerrainModifier in range and applies the specified changes.

You could see where this gets laggy. After leveling a 10*10 area with a hoe, you already have 100 terrain modifiers that are processed every frame.

BetterTerrain fixes this by patching the terrain system to only apply every terrain modifier once, then delete them and save the resulting heightmap in a buffer. When the game is saved, a .hmap file is written in the world's save directory.

Releases can be found at https://www.nexusmods.com/valheim/mods/415?tab=files
