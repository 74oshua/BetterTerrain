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
    [BepInPlugin("org.bepinex.plugins.betterterrain", "Better Terrain", "0.1.9.0")]
    public class BetterTerrain : BaseUnityPlugin
    {
        // major and minor version of BetterTerrain
        static byte major = 0;
        static byte minor = 9;

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

        static bool first_load = false;
        static bool loading = false;

        [HarmonyPatch(typeof(ZNet), "LoadWorld")]
        [HarmonyPrefix]
        static void LoadWorld_Prefix(World ___m_world)
        {
            tm_zdos.Clear();

            // get world save path
            db_path = ___m_world.GetDBPath();
            db_path = db_path.Substring(0, db_path.Length - 3);

            if (!File.Exists(db_path + ".hmap"))
            {
                first_load = true;
            }
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

        [HarmonyPatch(typeof(ZNet), "LoadWorld")]
        [HarmonyPrefix]
        static void LoadWorld_Prefix()
        {
            loading = true;
        }

        [HarmonyPatch(typeof(ZNet), "Start")]
        [HarmonyPostfix]
        static void Start_Postfix()
        {
            loading = false;
        }

        [HarmonyPatch(typeof(ZDOMan), "PrepareSave")]
        [HarmonyPrefix]
        static void PrepareSave_Prefix()
        {
            loading = true;
            // reinitialize all destroyed TerrainModifiers
            // if they aren't reinitialized before saving, the TerrainModifiers will be removed from the game's save file, and the world will be incompatible with the vanilla game
            foreach (ZDO zdo in tm_zdos)
            {
                ZDOMan.instance.AddToSector(zdo, zdo.GetSector());
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
            loading = false;
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
            first_load = false;
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

                int destroyed = 0;

                // go through every TerrainModifier scheduled to be destroyed
                for (int i = 0; i < tmods_to_remove.Count; i++)
                {
                    TerrainModifier t = tmods_to_remove[i];
                    if (t != null)
                    {
                        // get the TerrainModifier's ZNetView component, which contains the ZDO we need to save
                        ZNetView znview = t.gameObject.GetComponent<ZNetView>();
                        if (znview && znview.GetZDO() != null)
                        {
                            // save TerrainModifier ZDO
                            tm_zdos.Add(znview.GetZDO().Clone());

                            // claim ownership of the TerrainModifier and destroy it
                            znview.ClaimOwnership();
                            ZNetScene.instance.Destroy(t.gameObject);
                            destroyed++;
                            tmods_to_remove.Remove(t);
                        }
                    }
                }
                // log number of TerrainModifiers destroyed
                if (destroyed > 0)
                {
                    UnityEngine.Debug.Log("destroyed " + destroyed + " TerrainModifiers");
                }
            }
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
                ___m_buildData.m_baseHeights = zone_info[zone].heights;
                ___m_clearedMask.SetPixels(zone_info[zone].colors);
                ___m_clearedMask.Apply();

                //UnityEngine.Debug.Log("Loaded Zone (" + zone.x + ", " + zone.y + ")");
            }
        }

        [HarmonyPatch(typeof(Heightmap), "ApplyModifier")]
        [HarmonyPostfix]
        static void ApplyModifier_Postfix(Heightmap __instance, int ___m_width, List<float> ___m_heights, Texture2D ___m_clearedMask)
        {
            // don't run this code before the ZoneSystem is initialized
            if (ZoneSystem.instance == null)
            {
                return;
            }

            // if this heightmap's zone has been loaded, save it's info in zone_info
            Vector2i zone = ZoneSystem.instance.GetZone(__instance.transform.position);
            if (zone_info.ContainsKey(zone) && zone_info[zone].game_object != null && zone_info[zone].game_object == __instance.gameObject)
            {
                zone_info[zone].heights = ___m_heights;
                zone_info[zone].colors = ___m_clearedMask.GetPixels();
                zone_info[zone].width = ___m_clearedMask.width;
                zone_info[zone].saved = true;
                //UnityEngine.Debug.Log("Saved Zone (" + zone.x + ", " + zone.y + ")");
            }
        }

        [HarmonyPatch(typeof(TerrainModifier), "PokeHeightmaps")]
        [HarmonyPrefix]
        static bool PokeHeightmaps_Prefix(TerrainModifier __instance)
        {
            tmods_to_remove.Add(__instance);
            if (loading && !first_load)
            {
                //UnityEngine.Debug.Log("Skipped");
                __instance.enabled = false;
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
        [HarmonyPrefix]
        static void Zone_Update_Prefix()
        {
            loading = true;
        }
        [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
        [HarmonyPostfix]
        static void Zone_Update_Postfix()
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