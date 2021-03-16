using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;
using HarmonyLib;
using System.Text;

namespace BetterTerrain
{
    static class HMAPManager
    {
        static public bool ReadHMAP(BinaryReader reader)
        {
            // get and check the header
            // header format: 'BetterTerrain'<major version byte><minor version byte>
            String header = reader.ReadString();
            if (header != "BetterTerrain")
            {
                UnityEngine.Debug.LogWarning(".hmap file invalid, skipping load");
                return false;
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
                    zone_info.Add(id, new BetterTerrain.ZoneInfo(heights, colors));
                    zone_info[id].saved = true;
                }
            }

            return true;
        }

        static public bool WriteHMAP(BinaryWriter writer)
        {
            try
            {
                writer.Write("BetterTerrain");
                writer.Write(BetterTerrain.major);
                writer.Write(BetterTerrain.minor);

                int count = 0;
                foreach (BetterTerrain.ZoneInfo z in zone_info.Values)
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
            catch
            {
                UnityEngine.Debug.LogError("could not write .hmap file");
                return false;
            }

            return true;
        }

        static public void Reset()
        {
            zone_info.Clear();
        }

        static public bool IsZoneKnown(Vector2i id)
        {
            return zone_info.ContainsKey(id);
        }
        static public bool IsZoneKnown(Vector3 position)
        {
            Vector2i id = ZoneSystem.instance.GetZone(position);
            return zone_info.ContainsKey(id);
        }

        static public BetterTerrain.ZoneInfo GetZoneInfo(Vector3 position)
        {
            Vector2i id = ZoneSystem.instance.GetZone(position);
            if (IsZoneKnown(position))
            {
                return zone_info[id];
            }
            return null;
        }

        static public BetterTerrain.ZoneInfo GetZoneInfo(Vector2i id)
        {
            if (IsZoneKnown(id))
            {
                return zone_info[id];
            }
            return null;
        }

        static public void SetGameObject(Vector2i id, GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            if (!IsZoneKnown(id))
            {
                zone_info[id] = new BetterTerrain.ZoneInfo();
            }

            zone_info[id].game_object = gameObject;
        }

        static public void SetHeightmap(Vector2i id, Heightmap heightmap)
        {
            if (heightmap == null)
            {
                return;
            }

            if (!IsZoneKnown(id))
            {
                zone_info[id] = new BetterTerrain.ZoneInfo();
            }

            zone_info[id].hmap = heightmap;
        }

        static public void CalcTMods(Vector2i id)
        {
            if (!IsZoneKnown(id))
            {
                return;
            }

            foreach (TerrainModifier tmod in TerrainModifier.m_instances)
            {
                if (zone_info[id].hmap.TerrainVSModifier(tmod))
                {
                    zone_info[id].tmods.Add(tmod);
                }
            }
            return;
        }
        public static void RegenerateZone(Vector2i id)
        {
            if (!IsZoneKnown(id))
            {
                return;
            }

            RegenerateZone(GetZoneInfo(id));
        }

        public static void RegenerateZone(BetterTerrain.ZoneInfo zone)
        {
            if (zone == null)
            {
                return;
            }

            if (zone.saved)
            {
                for (int i = 0; i < zone.hmap.m_heights.Count; i++)
                {
                    zone.hmap.m_heights[i] = zone.heights[i];
                }
                zone.hmap.m_clearedMask.SetPixels(zone.colors);
                zone.hmap.m_clearedMask.Apply();
                //UnityEngine.Debug.Log("loaded zone");
            }
            CalcTMods(ZoneSystem.instance.GetZone(zone.hmap.transform.position));
            zone.hmap.ApplyModifiers();
            if (can_save)
            {
                if (zone.heights.Count != zone.hmap.m_heights.Count)
                {
                    zone.heights = new List<float>(new float[zone.hmap.m_heights.Count]);
                }
                for (int i = 0; i < zone.hmap.m_heights.Count; i++)
                {
                    zone.heights[i] = zone.hmap.m_heights[i];
                }
                zone.colors = (Color[])zone.hmap.m_clearedMask.GetPixels().Clone();
                zone.saved = true;
                TModManager.UpdateTMods();
                TModManager.DeleteTMods();
                //UnityEngine.Debug.Log("saved zone");
            }
        }

        static public Dictionary<Vector2i, BetterTerrain.ZoneInfo> zone_info = new Dictionary<Vector2i, BetterTerrain.ZoneInfo>();
        static public bool can_save = false;
        static public List<ZDO> all_tmod_zdos = new List<ZDO>();
        static public List<ZDO> checked_zdos = new List<ZDO>();
        static public float time_to_update = 1f;
        static public bool loading_object = false;
    }

    class Patches
    {
        // when a zone is loaded, get the corresponding heightmap's gameObject and store it in zone_info so it can be used to identify which heightmaps belong to which zones
        [HarmonyPatch(typeof(ZoneSystem), "SpawnZone")]
        [HarmonyPostfix]
        static void SpawnZone_Postfix(ZoneSystem __instance, Vector2i zoneID, GameObject root)
        {
            if (root == null)
            {
                return;
            }

            HMAPManager.SetGameObject(zoneID, root.GetComponentInChildren<Heightmap>().gameObject);
            HMAPManager.SetHeightmap(zoneID, root.GetComponentInChildren<Heightmap>());
            HMAPManager.CalcTMods(zoneID);

            HMAPManager.GetZoneInfo(zoneID).hmap.Regenerate();
        }

        [HarmonyPatch(typeof(ZNetScene), "CreateDestroyObjects")]
        [HarmonyPostfix]
        static void CreateDestroyObjects_Postfix(ZNetScene __instance)
        {
            HMAPManager.time_to_update -= Time.deltaTime;
            if (HMAPManager.time_to_update > 0)
            {
                return;
            }

            HMAPManager.time_to_update = 0f;

            bool all_loaded = true;
            Vector2i id = ZoneSystem.instance.GetZone(ZNet.instance.GetReferencePosition());
            int count = 0;

            foreach (ZDO zdo in __instance.m_tempCurrentObjects)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
                if (prefab.GetComponent<TerrainModifier>() != null)
                {
                    count++;
                    if (!__instance.m_instances.Keys.Contains(zdo))
                    {
                        all_loaded = false;
                    }
                }
            }

            if (all_loaded && !ZNetScene.instance.InLoadingScreen())
            {
                if (!HMAPManager.can_save)
                {
                    foreach (BetterTerrain.ZoneInfo zone in HMAPManager.zone_info.Values)
                    {
                        if (zone.hmap != null)
                        {
                            HMAPManager.can_save = true;
                            zone.hmap.Regenerate();
                        }
                    }
                }
                HMAPManager.can_save = true;

                return;
            }
            HMAPManager.can_save = false;
        }

        [HarmonyPatch(typeof(ZoneSystem), "UpdateTTL")]
        [HarmonyPrefix]
        static void UpdateTTL_Prefix(float dt, ZoneSystem __instance)
        {
            foreach (KeyValuePair<Vector2i, ZoneSystem.ZoneData> zone in __instance.m_zones)
            {
                zone.Value.m_ttl += dt;
            }
            foreach (KeyValuePair<Vector2i, ZoneSystem.ZoneData> zone2 in __instance.m_zones)
            {
                if (zone2.Value.m_ttl > __instance.m_zoneTTL && !ZNetScene.instance.HaveInstanceInSector(zone2.Key))
                {
                    UnityEngine.Object.Destroy(zone2.Value.m_root);
                    __instance.m_zones.Remove(zone2.Key);
                    break;
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

            // set heightmap zone and verify that it belongs to actual terrain data
            BetterTerrain.ZoneInfo zone = HMAPManager.GetZoneInfo(__instance.transform.position);
            if (zone == null || zone.game_object != __instance.gameObject)
            {
                return true;
            }

            // if this zone isn't saved...
            if (!zone.generated)
            {
                // use the normal terrain system
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

                zone.generated = true;
                zone.hmap = __instance;
                // UnityEngine.Debug.Log("Reset zone");
            }

            HMAPManager.RegenerateZone(zone);

            return false;
        }
        

        [HarmonyPatch(typeof(Heightmap), "ApplyModifier")]
        [HarmonyPrefix]
        static bool ApplyModifier_Prefix(TerrainModifier modifier, Heightmap __instance, int ___m_width, ref List<float> ___m_heights, ref Texture2D ___m_clearedMask, HeightmapBuilder.HMBuildData ___m_buildData)
        {
            if (ZoneSystem.instance == null)
            {
                return true;
            }

            // set heightmap zone and verify that it belongs to actual terrain data
            Vector2i zoneID = ZoneSystem.instance.GetZone(__instance.transform.position);
            BetterTerrain.ZoneInfo zone = HMAPManager.GetZoneInfo(zoneID);
            if (zone != null && zone.game_object != null && zone.game_object == __instance.gameObject)
            {
                // if the tmod has already been applied, skip
                if (zone.applied_tmods.Contains(modifier))
                {
                    bool can_delete = true;
                    float radius = modifier.GetRadius();

                    for (int y = -1; y <= 1; y++)
                    {
                        for (int x = -1; x <= 1; x++)
                        {
                            BetterTerrain.ZoneInfo check_zone = HMAPManager.GetZoneInfo(new Vector2i(zoneID.x + x, zoneID.y + y));
                            if (check_zone != null)
                            {
                                foreach (TerrainModifier tmod in check_zone.tmods.Except(check_zone.applied_tmods))
                                {
                                    if (tmod != null)
                                    {
                                        if (Vector3.Distance(tmod.transform.position, modifier.transform.position) <= radius)
                                        {
                                            can_delete = false;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (can_delete)
                    {
                        zone.deletable_tmods.Add(modifier);
                    }
                    return false;
                }

                // mark modifier as applied
                zone.applied_tmods.Add(modifier);

                if (zone.saved && HMAPManager.loading_object)
                {
                    return false;
                }
            }

            return true;
        }

        [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
        [HarmonyPrefix]
        static void CreateObject_Prefix()
        {
            HMAPManager.loading_object = true;
        }

        [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
        [HarmonyPostfix]
        static void CreateObject_Postfix()
        {
            HMAPManager.loading_object = false;
        }
    }
}
