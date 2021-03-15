// Written by 74oshua

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace BetterTerrain
{
    [BepInPlugin("org.bepinex.plugins.betterterrain", "Better Terrain", "0.1.10.0")]
    public class BetterTerrain : BaseUnityPlugin
    {
        // major and minor version of BetterTerrain
        static byte major = 0;
        static byte minor = 10;

        // apply patches
        void Awake()
        {
            UnityEngine.Debug.Log("Starting BetterTerrain");

            Harmony.CreateAndPatchAll(typeof(BetterTerrain));
        }

        // contains information about a zone
        class ZoneInfo
        {
            public ZoneInfo()
            {
            }

            public ZoneInfo(int w, List<float> h, Color[] c)
            {
                width = w;
                heights = h;
                colors = c;
            }

            public int width;
            public List<float> heights;
            public List<float> base_heights;
            public Color[] colors;
            public GameObject game_object;
            public bool saved = false;
        }

        // <world save directory>/<name of world>
        static String db_path = "";

        // timer for destruction of TerrainModifiers
        static float destroy_tm_timer = 0;

        // list of TerrainModifier ZDOs to be temporarily reinitialized on world save
        static List<ZDO> tm_zdos = new List<ZDO>();

        // dictionary of ZoneInfo for saved zones, indexed by Vector2i's representing the location of the zone
        static Dictionary<Vector2i, ZoneInfo> zone_info = new Dictionary<Vector2i, ZoneInfo>();

        // TerrainModifiers that have not yet been destroyed, but are scheduled to
        static List<TerrainModifier> tmods_to_remove = new List<TerrainModifier>();
        
        // whether an object is being created through the ZNetScene
        static bool loading = false;

        // list of zones that should be saved
        static List<Vector2i> zones_to_save = new List<Vector2i>();

        static void DeleteTMod(TerrainModifier modifier)
        {
            ZNetView znview = modifier.gameObject.GetComponent<ZNetView>();
            if (znview && znview.GetZDO() != null)
            {
                // save TerrainModifier ZDO
                tm_zdos.Add(znview.GetZDO().Clone());

                // claim ownership of the TerrainModifier and destroy it
                znview.ClaimOwnership();
                ZNetScene.instance.Destroy(modifier.gameObject);

                //UnityEngine.Debug.Log("Destroyed");
            }
        }

        [HarmonyPatch(typeof(ZNet), "LoadWorld")]
        [HarmonyPrefix]
        static void LoadWorld_Prefix(World ___m_world)
        {
            tm_zdos.Clear();

            // get world save path
            db_path = ___m_world.GetDBPath();
            db_path = db_path.Substring(0, db_path.Length - 3);
        }

        [HarmonyPatch(typeof(ZoneSystem), "Load")]
        [HarmonyPrefix]
        static bool Load_Prefix()
        {
            BinaryReader reader;

            // return if file doesn't exist
            if (!File.Exists(db_path + ".hmap"))
            {
                return true;
            }

            reader = new BinaryReader(new FileStream(db_path + ".hmap", FileMode.Open));

            // get and check the header
            // header format: 'BetterTerrain'<major version byte><minor version byte>
            String header = reader.ReadString();
            if (header != "BetterTerrain")
            {
                UnityEngine.Debug.LogWarning(".hmap file invalid, skipping load");
                reader.Close();
                return true;
            }
            byte file_major = reader.ReadByte();
            byte file_minor = reader.ReadByte();

            // get number of heightmaps to load from .hmap file
            int num_hmaps = reader.ReadInt32();

            for (int i = 0; i < num_hmaps; i++)
            {
                // get ZoneID
                int x = reader.ReadInt32();
                int y = reader.ReadInt32();
                Vector2i id = new Vector2i(x, y);

                // load height values
                List<float> heights = new List<float>();
                int num_heights = reader.ReadInt32();
                for (int j = 0; j < num_heights; j++)
                {
                    heights.Add(reader.ReadSingle());
                }

                // load texture colors
                int tex_size = reader.ReadInt32();
                Color[] colors = new Color[tex_size];
                for (int j = 0; j < tex_size; j++)
                {
                    colors[j].r = reader.ReadSingle();
                    colors[j].g = reader.ReadSingle();
                    colors[j].b = reader.ReadSingle();
                }

                // add deserialized info into zone_info if the ZoneID it doesn't already exist
                if (!zone_info.ContainsKey(id))
                {
                    zone_info.Add(id, new ZoneInfo((int)Math.Sqrt(tex_size), heights, colors));
                    zone_info[id].saved = true;
                }
            }
            reader.Close();

            return true;
        }

        [HarmonyPatch(typeof(ZDOMan), "SaveAsync")]
        [HarmonyPrefix]
        static void SaveAsync_Prefix()
        {
            // reinitialize all destroyed TerrainModifiers
            // if they aren't reinitialized before saving, the TerrainModifiers will be removed from the game's save file, and the world will be incompatible with the vanilla game
            foreach (ZDO zdo in tm_zdos)
            {
                if (!ZNetScene.instance.HaveInstance(zdo))
                {
                    ZDOMan.instance.AddToSector(zdo, zdo.GetSector());
                }
            }
        }

        [HarmonyPatch(typeof(ZDOMan), "SaveAsync")]
        [HarmonyPostfix]
        static void SaveAsync_Postfix()
        {
            // re-destroy TerrainModifiers
            foreach (ZDO zdo in tm_zdos)
            {
                ZDOMan.instance.RemoveFromSector(zdo, zdo.GetSector());
            }
        }

        // saves zone_info into a .hmap file, see Load_Prefix() for .hmap format details
        [HarmonyPatch(typeof(ZoneSystem), "SaveASync")]
        [HarmonyPrefix]
        static void SaveASync_Prefix()
        {
            BinaryWriter writer = new BinaryWriter(new FileStream(db_path + ".hmap", FileMode.Create));

            writer.Write("BetterTerrain");
            writer.Write(major);
            writer.Write(minor);

            int count = 0;
            foreach (ZoneInfo z in zone_info.Values)
            {
                if (z.saved)
                {
                    count++;
                }
            }
            writer.Write(count);

            foreach (var item in zone_info)
            {
                if (item.Value.saved)
                {
                    writer.Write(item.Key.x);
                    writer.Write(item.Key.y);

                    writer.Write(item.Value.heights.Count);
                    foreach (float height in item.Value.heights)
                    {
                        writer.Write(height);
                    }

                    writer.Write(item.Value.colors.Count());
                    foreach (Color color in item.Value.colors)
                    {
                        writer.Write(color.r);
                        writer.Write(color.g);
                        writer.Write(color.b);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(ZNet), "Shutdown")]
        [HarmonyPostfix]
        static void Shutdown_Postfix()
        {
            // clear any saved world data from memory
            zone_info.Clear();
            tm_zdos.Clear();
        }

        [HarmonyPatch(typeof(ZNetScene), "Update")]
        [HarmonyPrefix]
        static void Update_Postfix()
        {
            // don't run this code before the ZoneSystem is initialized
            if (ZoneSystem.instance == null || ZNetScene.instance == null)
            {
                return;
            }

            // increment timer
            destroy_tm_timer += Time.deltaTime;

            // when timer reaches one second...
            if (destroy_tm_timer >= 1)
            {
                destroy_tm_timer = 0f;

                // if this heightmap's zone has been loaded, save it's info in zone_info
                for (int i = 0; i < tmods_to_remove.Count; i++)
                {
                    if (tmods_to_remove[i] != null)
                    {
                        TerrainModifier modifier = tmods_to_remove[i];
                        Vector2i zone = ZoneSystem.instance.GetZone(modifier.transform.position);

                        bool can_delete = true;
                        foreach (Heightmap hmap in Heightmap.GetAllHeightmaps())
                        {
                            Vector2i hmap_zone = ZoneSystem.instance.GetZone(hmap.transform.position);
                            if (hmap.TerrainVSModifier(modifier))
                            {
                                if (!zone_info.ContainsKey(hmap_zone))
                                {
                                    //UnityEngine.Debug.Log("Adding Zone: (" + hmap_zone.x + ", " + hmap_zone.y + ")");
                                    zone_info.Add(hmap_zone, new ZoneInfo());
                                    can_delete = false;
                                }
                                if (!zones_to_save.Contains(hmap_zone))
                                {
                                    //UnityEngine.Debug.Log("Initializing zone: (" + hmap_zone.x + ", " + hmap_zone.y + ")");
                                    zones_to_save.Add(hmap_zone);
                                    hmap.Regenerate();
                                    can_delete = false;
                                }
                            }
                        }
                        if (can_delete)
                        {
                            //UnityEngine.Debug.Log("Deleting tmod in zone: (" + zone.x + ", " + zone.y + ")");
                            DeleteTMod(modifier);
                            tmods_to_remove.Remove(modifier);
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Heightmap), "Generate")]
        [HarmonyPrefix]
        static bool Generate_Prefix(Heightmap __instance, int ___m_width, ref List<float> ___m_heights, ref Texture2D ___m_clearedMask, HeightmapBuilder.HMBuildData ___m_buildData)
        {
            if (ZoneSystem.instance == null)
            {
                return true;
            }
            Vector3 position = __instance.transform.position;
            __instance.Initialize();
            int num = __instance.m_width + 1;
            int num2 = num * num;

            if (__instance.m_buildData == null || __instance.m_buildData.m_baseHeights.Count != num2 || __instance.m_buildData.m_center != position || __instance.m_buildData.m_scale != __instance.m_scale || __instance.m_buildData.m_worldGen != WorldGenerator.instance)
            {
                __instance.m_buildData = HeightmapBuilder.instance.RequestTerrainSync(position, __instance.m_width, __instance.m_scale, __instance.m_isDistantLod, WorldGenerator.instance);
                __instance.m_cornerBiomes = __instance.m_buildData.m_cornerBiomes;
            }
            for (int i = 0; i < num2; i++)
            {
                __instance.m_heights[i] = __instance.m_buildData.m_baseHeights[i];
            }
            Color[] pixels = new Color[__instance.m_clearedMask.width * __instance.m_clearedMask.height];
            __instance.m_clearedMask.SetPixels(pixels);

            __instance.ApplyModifiers();
            for (int i = 0; i < num2; i++)
            {
                __instance.m_buildData.m_baseHeights[i] = __instance.m_heights[i];
            }

            return false;
        }

        [HarmonyPatch(typeof(Heightmap), "ApplyModifiers")]
        [HarmonyPrefix]
        static void ApplyModifiers_Prefix(Heightmap __instance, int ___m_width, ref List<float> ___m_heights, ref Texture2D ___m_clearedMask, HeightmapBuilder.HMBuildData ___m_buildData)
        {
            // don't run this code before the ZoneSystem is initialized
            if (ZoneSystem.instance == null)
            {
                return;
            }

            // get this heightmap's zone
            Vector2i zone = ZoneSystem.instance.GetZone(__instance.transform.position);

            // if this zone's heightmap data is saved, load it
            if (zone_info.ContainsKey(zone) && zone_info[zone].saved && zone_info[zone].game_object != null && zone_info[zone].game_object == __instance.gameObject)
            {
                ___m_heights = zone_info[zone].heights;
                ___m_clearedMask.SetPixels(zone_info[zone].colors);
                ___m_clearedMask.Apply();
                //UnityEngine.Debug.Log("Loaded Zone (" + zone.x + ", " + zone.y + ")");
            }
        }

        [HarmonyPatch(typeof(Heightmap), "ApplyModifiers")]
        [HarmonyPostfix]
        static void ApplyModifiers_Postfix(Heightmap __instance, int ___m_width, ref List<float> ___m_heights, ref Texture2D ___m_clearedMask, HeightmapBuilder.HMBuildData ___m_buildData)
        {
            // don't run this code before the ZoneSystem is initialized
            if (ZoneSystem.instance == null)
            {
                return;
            }

            Vector2i zone = ZoneSystem.instance.GetZone(__instance.transform.position);
            if (zones_to_save.Contains(zone) && zone_info.ContainsKey(zone) && zone_info[zone].game_object != null && zone_info[zone].game_object == __instance.gameObject)
            {
                zone_info[zone].heights = ___m_heights;
                zone_info[zone].colors = ___m_clearedMask.GetPixels();
                zone_info[zone].width = ___m_clearedMask.width;
                zone_info[zone].saved = true;
                // UnityEngine.Debug.Log("Saved zone: (" + zone.x + ", " + zone.y + ")");

                /*if (zone_info[zone].base_heights != null)
                {
                    ___m_buildData.m_baseHeights = zone_info[zone].base_heights;
                }*/
            }
        }

        [HarmonyPatch(typeof(TerrainModifier), "PokeHeightmaps")]
        [HarmonyPrefix]
        static bool PokeHeightmaps_Prefix(TerrainModifier __instance)
        {
            if (ZoneSystem.instance == null)
            {
                return true;
            }

            Vector2i zone = ZoneSystem.instance.GetZone(__instance.transform.position);
            if (loading && zone_info.ContainsKey(zone) && zone_info[zone].saved)
            {
                //UnityEngine.Debug.Log("Skipped");
                __instance.enabled = false;
                DeleteTMod(__instance);
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(TerrainModifier), "Awake")]
        [HarmonyPostfix]
        static void Awake_Postfix(TerrainModifier __instance)
        {
            tmods_to_remove.Add(__instance);
        }

        [HarmonyPatch(typeof(TerrainModifier), "OnDestroy")]
        [HarmonyPrefix]
        static bool OnDestroy_Prefix(TerrainModifier __instance, List<TerrainModifier> ___m_instances, ref bool ___m_needsSorting)
        {
            ___m_instances.Remove(__instance);
            ___m_needsSorting = true;
            return false;
        }

        [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
        [HarmonyPrefix]
        static void Create_Prefix()
        {
            loading = true;
        }
        [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
        [HarmonyPostfix]
        static void Create_Postfix()
        {
            loading = false;
        }

        // when a zone is loaded, get the corresponding heightmap's gameObject and store it in zone_info so it can be used to identify which heightmaps belong to which zones
        [HarmonyPatch(typeof(ZoneSystem), "SpawnZone")]
        [HarmonyPostfix]
        static void SpawnZone_Postfix(Vector2i zoneID, GameObject root)
        {
            if (root == null)
            {
                return;
            }

            if (!zone_info.ContainsKey(zoneID))
            {
                zone_info.Add(zoneID, new ZoneInfo());
            }
            zone_info[zoneID].game_object = root.GetComponentInChildren<Heightmap>().gameObject;
            root.GetComponentInChildren<Heightmap>().Regenerate();
        }
    }
}