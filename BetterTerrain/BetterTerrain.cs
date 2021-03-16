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
    [BepInPlugin("org.bepinex.plugins.betterterrain", "Better Terrain", "0.1.11.0")]
    public class BetterTerrain : BaseUnityPlugin
    {
        // major and minor version of BetterTerrain
        public static byte major = 0;
        public static byte minor = 11;

        // apply patches
        void Awake()
        {
            UnityEngine.Debug.Log("Starting BetterTerrain");

            Harmony.CreateAndPatchAll(typeof(BetterTerrain));
            Harmony.CreateAndPatchAll(typeof(Patches));
        }

        // contains information about a zone
        public class ZoneInfo
        {
            public ZoneInfo()
            {
            }

            public ZoneInfo(List<float> h, Color[] c)
            {
                heights = h;
                colors = c;
            }

            public Heightmap hmap;

            public List<float> heights = new List<float>();
            public Color[] colors;

            public List<TerrainModifier> tmods = new List<TerrainModifier>();
            public List<TerrainModifier> applied_tmods = new List<TerrainModifier>();
            public List<TerrainModifier> deletable_tmods = new List<TerrainModifier>();
            public float ttu = 1;
            public int num_tmods = 0;
            public GameObject game_object;
            public bool saved = false;
            public bool generated = false;
        }
        
        // <world save directory>/<name of world>
        static String db_path = "";

        // timer for destruction of TerrainModifiers
        static float destroy_tm_timer = 0;

        [HarmonyPatch(typeof(ZNet), "LoadWorld")]
        [HarmonyPrefix]
        static void LoadWorld_Prefix(World ___m_world)
        {
            // get world save path
            db_path = ___m_world.GetDBPath();
            db_path = db_path.Substring(0, db_path.Length - 3);
        }

        [HarmonyPatch(typeof(ZoneSystem), "Load")]
        [HarmonyPrefix]
        static void Load_Prefix()
        {
            if (File.Exists(db_path + ".hmap"))
            {
                using (BinaryReader reader = new BinaryReader(new FileStream(db_path + ".hmap", FileMode.Open)))
                {
                    HMAPManager.ReadHMAP(reader);
                }
            }
        }

        [HarmonyPatch(typeof(ZDOMan), "PrepareSave")]
        [HarmonyPrefix]
        static void PrepareSave_Prefix()
        {
            // reinitialize all destroyed TerrainModifiers
            // if they aren't reinitialized before saving, the TerrainModifiers will be removed from the game's save file, and the world will be incompatible with the vanilla game
            List<ZDO> save_clone = ZDOMan.instance.GetSaveClone();
<<<<<<< HEAD
            foreach (ZDO zdo in tm_zdos)
=======
            foreach (ZDO zdo in TModManager.zdos_to_save)
>>>>>>> testing
            {
                bool has_copy = false;
                foreach (ZDO z in save_clone)
                {
                    if (z.m_uid == zdo.m_uid)
                    {
                        has_copy = true;
                        break;
                    }
                }

                if (!has_copy && !save_clone.Contains(zdo))
                {
                    ZDOMan.instance.AddToSector(zdo, zdo.GetSector());
                    UnityEngine.Debug.Log(zdo.m_uid);
                }
            }
        }

        [HarmonyPatch(typeof(ZDOMan), "SaveAsync")]
        [HarmonyPostfix]
        static void SaveAsync_Postfix()
        {
            // re-destroy TerrainModifiers
            foreach (ZDO zdo in TModManager.zdos_to_save)
            {
                if (ZNetScene.instance.HaveInstance(zdo))
                {
                    ZDOMan.instance.RemoveFromSector(zdo, zdo.GetSector());
                }
            }
        }

        // saves zone_info into a .hmap file, see Load_Prefix() for .hmap format details
        [HarmonyPatch(typeof(ZoneSystem), "SaveASync")]
        [HarmonyPrefix]
        static void SaveASync_Prefix()
        {
            using (BinaryWriter writer = new BinaryWriter(new FileStream(db_path + ".hmap", FileMode.Create)))
            {
                HMAPManager.WriteHMAP(writer);
            }
        }

        [HarmonyPatch(typeof(ZNet), "Shutdown")]
        [HarmonyPostfix]
        static void Shutdown_Postfix()
        {
            // clear any saved world data from memory
            HMAPManager.Reset();
            TModManager.Reset();
        }


        /*[HarmonyPatch(typeof(ZNetScene), "Update")]
        [HarmonyPrefix]
        static void Update_Prefix()
        {
            destroy_tm_timer += Time.deltaTime;

            if (destroy_tm_timer > 1f && HMAPManager.can_save)
            {
                destroy_tm_timer = 0f;
            }
        }*/
    }
}