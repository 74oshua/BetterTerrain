using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BetterTerrain
{
    static class TModManager
    {
        public static void UpdateTMods()
        {
            // check for all unused tmods
            List<TerrainModifier> remaining_tmods = new List<TerrainModifier>();
            foreach (BetterTerrain.ZoneInfo zone in HMAPManager.zone_info.Values)
            {
                remaining_tmods.AddRange(zone.tmods.Except(zone.deletable_tmods));
            }

            tmods_to_remove = TerrainModifier.m_instances.Except(remaining_tmods.Distinct().ToList()).ToList();
        }

        public static void DeleteTMod(TerrainModifier modifier)
        {
            if (!modifier.m_playerModifiction)
            {
                return;
            }

            ZNetView znview = modifier.gameObject.GetComponent<ZNetView>();
            if (znview && znview.GetZDO() != null)
            {
                // save TerrainModifier ZDO
                bool has_copy = false;
                foreach (ZDO zdo in zdos_to_save)
                {
                    if (zdo.m_uid == znview.GetZDO().m_uid)
                    {
                        has_copy = true;
                        break;
                    }
                }
                if (!has_copy)
                {
                    zdos_to_save.Add(znview.GetZDO().Clone());
                }

                // claim ownership of the TerrainModifier and destroy it
                modifier.enabled = false;
                znview.ClaimOwnership();
                ZNetScene.instance.Destroy(modifier.gameObject);
            }
        }

        public static void DeleteTMods()
        {
            // if this heightmap's zone has been loaded, save it's info in zone_info
            int destroyed = 0;
            for (int i = 0; i < tmods_to_remove.Count; i++)
            {
                if (tmods_to_remove[i] != null)
                {
                    //UnityEngine.Debug.Log("Deleting tmod in zone: (" + zone.x + ", " + zone.y + ")");
                    destroyed++;
                    DeleteTMod(tmods_to_remove[i]);
                }
                tmods_to_remove.Remove(tmods_to_remove[i]);
            }
        }

        public static void Reset()
        {
            tmods_to_remove.Clear();
            tmods_to_remove.Clear();
        }

        public static List<ZDO> zdos_to_save = new List<ZDO>();
        public static List<TerrainModifier> tmods_to_remove = new List<TerrainModifier>();
    }
}
