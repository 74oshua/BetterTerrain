using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BetterTerrain
{
	internal static class TModManager
	{
		public static List<ZDO> zdos_to_save = new List<ZDO>();

		public static List<TerrainModifier> tmods_to_remove = new List<TerrainModifier>();

		public static List<TerrainModifier> already_removed = new List<TerrainModifier>();

		public static void UpdateTMods()
		{
			foreach (BetterTerrain.ZoneInfo zone in HMAPManager.zone_info.Values)
			{
				tmods_to_remove.AddRange(zone.deletable_tmods);
			}
			tmods_to_remove = tmods_to_remove.Except(already_removed).ToList();
			tmods_to_remove = tmods_to_remove.Distinct().ToList();
		}

		public static void DeleteTMods()
		{
			int destroyed = 0;
			for (int i = 0; i < tmods_to_remove.Count; i++)
			{
				if (tmods_to_remove[i] != null)
				{
					destroyed++;
					DeleteTMod(tmods_to_remove[i]);
				}
				already_removed.Add(tmods_to_remove[i]);
			}
		}

		public static void DeleteTMod(TerrainModifier modifier)
		{
			ZNetView znview = modifier.gameObject.GetComponent<ZNetView>();
			if (!znview || znview.GetZDO() == null)
			{
				return;
			}
			bool has_copy = false;
			foreach (ZDO item in zdos_to_save)
			{
				if (item.m_uid == znview.GetZDO().m_uid)
				{
					has_copy = true;
					break;
				}
			}
			if (!has_copy)
			{
				zdos_to_save.Add(znview.GetZDO().Clone());
			}
			modifier.enabled = false;
			znview.ClaimOwnership();
			if (!new string[3] { "mud_road(Clone)", "digg(Clone)", "path(Clone)" }.Contains(modifier.name))
			{
				Debug.LogWarning("Deleting " + modifier.name);
			}
			Debug.Log("Deleting " + modifier.name);
			ZNetScene.instance.Destroy(modifier.gameObject);
		}

		public static void Reset()
		{
			tmods_to_remove.Clear();
			tmods_to_remove.Clear();
		}
	}
}
