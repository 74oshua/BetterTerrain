using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace BetterTerrain
{
	internal class Patches
	{
		[HarmonyPatch(typeof(ZNet), "LoadWorld")]
		[HarmonyPrefix]
		private static void LoadWorld_Prefix(World ___m_world)
		{
			BetterTerrain.db_path = ___m_world.GetDBPath();
			BetterTerrain.db_path = BetterTerrain.db_path.Substring(0, BetterTerrain.db_path.Length - 3);
		}

		[HarmonyPatch(typeof(ZoneSystem), "Load")]
		[HarmonyPrefix]
		private static void Load_Prefix()
		{
			if (File.Exists(BetterTerrain.db_path + ".hmap"))
			{
				using (BinaryReader reader = new BinaryReader(new FileStream(BetterTerrain.db_path + ".hmap", FileMode.Open)))
				{
				HMAPManager.ReadHMAP(reader);
			}
		}
		}

		[HarmonyPatch(typeof(ZDOMan), "PrepareSave")]
		[HarmonyPrefix]
		private static void PrepareSave_Prefix()
		{
			List<ZDO> save_clone = ZDOMan.instance.GetSaveClone();
			foreach (ZDO zdo in TModManager.zdos_to_save)
			{
				bool has_copy = false;
				foreach (ZDO item in save_clone)
				{
					if (item.m_uid == zdo.m_uid)
					{
						has_copy = true;
						break;
					}
				}
				if (!has_copy && !save_clone.Contains(zdo))
				{
					ZDOMan.instance.AddToSector(zdo, zdo.GetSector());
				}
			}
		}

		[HarmonyPatch(typeof(ZDOMan), "SaveAsync")]
		[HarmonyPostfix]
		private static void SaveAsync_Postfix()
		{
			foreach (ZDO zdo in TModManager.zdos_to_save)
			{
				if (ZNetScene.instance.HaveInstance(zdo))
				{
					ZDOMan.instance.RemoveFromSector(zdo, zdo.GetSector());
				}
			}
		}

		[HarmonyPatch(typeof(ZoneSystem), "SaveASync")]
		[HarmonyPrefix]
		private static void SaveASync_Prefix()
		{
			HMAPManager.WriteHMAP(writer);
		}
		}

		[HarmonyPatch(typeof(ZNet), "Shutdown")]
		[HarmonyPostfix]
		private static void Shutdown_Postfix()
		{
			HMAPManager.Reset();
			TModManager.Reset();
		}

		[HarmonyPatch(typeof(ZoneSystem), "SpawnZone")]
		[HarmonyPostfix]
		private static void SpawnZone_Postfix(ZoneSystem __instance, Vector2i zoneID, GameObject root)
		{
			if (!(root == null))
			{
				HMAPManager.SetGameObject(zoneID, root.GetComponentInChildren<Heightmap>().gameObject);
				HMAPManager.SetHeightmap(zoneID, root.GetComponentInChildren<Heightmap>());
				HMAPManager.CalcTMods(zoneID);
				HMAPManager.GetZoneInfo(zoneID).hmap.Regenerate();
			}
		}

		[HarmonyPatch(typeof(ZNetScene), "CreateDestroyObjects")]
		[HarmonyPostfix]
		private static void CreateDestroyObjects_Postfix(ZNetScene __instance)
		{
			bool all_loaded = true;
			int count = 0;
			foreach (ZDO zdo in __instance.m_tempCurrentObjects)
			{
				if (ZNetScene.instance.GetPrefab(zdo.GetPrefab()).GetComponent<TerrainModifier>() != null)
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
				bool can_save = HMAPManager.can_save;
				HMAPManager.can_save = true;
				TModManager.UpdateTMods();
				TModManager.DeleteTMods();
				if (can_save)
				{
					return;
				}
				foreach (Heightmap heightmap in Heightmap.m_heightmaps)
				{
					heightmap.Regenerate();
				}
			}
			else
			{
				HMAPManager.can_save = false;
			}
		}

		[HarmonyPatch(typeof(Heightmap), "Generate")]
		[HarmonyPrefix]
		private static bool Generate_Prefix(Heightmap __instance, int ___m_width, ref List<float> ___m_heights, ref Texture2D ___m_clearedMask, HeightmapBuilder.HMBuildData ___m_buildData)
		{
			if (ZoneSystem.instance == null)
			{
				return true;
			}
			Vector3 position = __instance.transform.position;
			__instance.Initialize();
			int num3 = __instance.m_width + 1;
			int num2 = num3 * num3;
			BetterTerrain.ZoneInfo zone = HMAPManager.GetZoneInfo(__instance.transform.position);
			if (zone == null || zone.game_object != __instance.gameObject)
			{
				return true;
			}
			if (!zone.generated)
			{
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
			}
			HMAPManager.RegenerateZone(zone);
			return false;
		}

		[HarmonyPatch(typeof(Heightmap), "ApplyModifier")]
		[HarmonyPrefix]
		private static bool ApplyModifier_Prefix(TerrainModifier modifier, Heightmap __instance, int ___m_width, ref List<float> ___m_heights, ref Texture2D ___m_clearedMask, HeightmapBuilder.HMBuildData ___m_buildData)
		{
			if (ZoneSystem.instance == null)
			{
				return true;
			}
			Vector2i zoneID = ZoneSystem.instance.GetZone(__instance.transform.position);
			BetterTerrain.ZoneInfo zone = HMAPManager.GetZoneInfo(zoneID);
			if (zone == null || modifier == null || __instance != zone.hmap)
			{
				return false;
			}
			bool was_applied = zone.applied_tmods.Contains(modifier);
			if (!was_applied)
			{
				zone.applied_tmods.Add(modifier);
			}
			bool can_delete = true;
			float modifier_radius = modifier.GetRadius();
			if (!zone.deletable_tmods.Contains(modifier))
			{
				for (int y = -1; y <= 1; y++)
				{
					for (int x = -1; x <= 1; x++)
					{
						BetterTerrain.ZoneInfo check_zone = HMAPManager.GetZoneInfo(new Vector2i(zoneID.x + x, zoneID.y + y));
						if (check_zone != null && check_zone.generated)
						{
							foreach (TerrainModifier tmod in check_zone.tmods.Except(check_zone.applied_tmods))
							{
								if (tmod != null)
								{
									float radius = Math.Max(tmod.GetRadius(), modifier_radius);
									if (tmod != modifier && Vector3.Distance(tmod.transform.position, modifier.transform.position) <= radius)
									{
										Debug.Log(Vector3.Distance(tmod.transform.position, modifier.transform.position));
										can_delete = false;
									}
								}
							}
						}
						else
						{
							can_delete = false;
						}
					}
				}
				if (can_delete)
				{
					Debug.Log("x");
					zone.deletable_tmods.Add(modifier);
				}
			}
			if (was_applied)
			{
				return false;
			}
			if (zone.saved && HMAPManager.loading_object)
			{
				return false;
			}
			return true;
		}

		[HarmonyPatch(typeof(ZNetScene), "CreateObject")]
		[HarmonyPrefix]
		private static void CreateObject_Prefix()
		{
			HMAPManager.loading_object = true;
		}

		[HarmonyPatch(typeof(ZNetScene), "CreateObject")]
		[HarmonyPostfix]
		private static void CreateObject_Postfix()
		{
			HMAPManager.loading_object = false;
		}

		[HarmonyPatch(typeof(ZoneSystem), "SpawnLocation")]
		[HarmonyPostfix]
		private static void LocationAwake_Postfix(ZoneSystem __instance, GameObject __result, Vector3 pos)
		{
			Vector2i zone2 = ZoneSystem.instance.GetZone(pos);
			BetterTerrain.ZoneInfo zone = HMAPManager.GetZoneInfo(zone2);
			if (HMAPManager.IsZoneKnown(zone2))
			{
				if (zone.has_location)
				{
					zone.hmap.Regenerate();
				}
				zone.has_location = true;
			}
		}
	}
}
