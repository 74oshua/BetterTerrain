using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BetterTerrain
{
	internal static class HMAPManager
	{
		public static Dictionary<Vector2i, BetterTerrain.ZoneInfo> zone_info = new Dictionary<Vector2i, BetterTerrain.ZoneInfo>();

		public static bool can_save = false;

		public static List<ZDO> all_tmod_zdos = new List<ZDO>();

		public static List<ZDO> checked_zdos = new List<ZDO>();

		public static bool needs_to_update = true;

		public static float time_to_update = 1f;

		public static bool loading_object = false;

		public static bool ReadHMAP(BinaryReader reader)
		{
			if (reader.ReadString() != "BetterTerrain")
			{
				Debug.LogWarning(".hmap file invalid, skipping load");
				return false;
			}
			reader.ReadByte();
			reader.ReadByte();
			int num_hmaps = reader.ReadInt32();
			for (int i = 0; i < num_hmaps; i++)
			{
				int x = reader.ReadInt32();
				int y = reader.ReadInt32();
				Vector2i id = new Vector2i(x, y);
				List<float> heights = new List<float>();
				int num_heights = reader.ReadInt32();
				for (int k = 0; k < num_heights; k++)
				{
					heights.Add(reader.ReadSingle());
				}
				int tex_size = reader.ReadInt32();
				Color[] colors = new Color[tex_size];
				for (int j = 0; j < tex_size; j++)
				{
					colors[j].r = reader.ReadSingle();
					colors[j].g = reader.ReadSingle();
					colors[j].b = reader.ReadSingle();
				}
				if (!zone_info.ContainsKey(id))
				{
					zone_info.Add(id, new BetterTerrain.ZoneInfo(heights, colors));
					zone_info[id].saved = true;
				}
			}
			return true;
		}

		public static bool WriteHMAP(BinaryWriter writer)
		{
			try
			{
				writer.Write("BetterTerrain");
				writer.Write(BetterTerrain.major);
				writer.Write(BetterTerrain.minor);
				int count = 0;
				foreach (BetterTerrain.ZoneInfo value in zone_info.Values)
				{
					if (value.saved)
					{
						count++;
					}
				}
				writer.Write(count);
				foreach (KeyValuePair<Vector2i, BetterTerrain.ZoneInfo> item in zone_info)
				{
					if (!item.Value.saved)
					{
						continue;
					}
					writer.Write(item.Key.x);
					writer.Write(item.Key.y);
					writer.Write(item.Value.heights.Count);
					foreach (float height in item.Value.heights)
					{
						writer.Write(height);
					}
					writer.Write(item.Value.colors.Count());
					Color[] colors = item.Value.colors;
					for (int i = 0; i < colors.Length; i++)
					{
						Color color = colors[i];
						writer.Write(color.r);
						writer.Write(color.g);
						writer.Write(color.b);
					}
				}
			}
			catch
			{
				Debug.LogError("could not write .hmap file");
				return false;
			}
			return true;
		}

		public static void Reset()
		{
			zone_info.Clear();
		}

		public static bool IsZoneKnown(Vector2i id)
		{
			return zone_info.ContainsKey(id);
		}

		public static bool IsZoneKnown(Vector3 position)
		{
			Vector2i id = ZoneSystem.instance.GetZone(position);
			return zone_info.ContainsKey(id);
		}

		public static BetterTerrain.ZoneInfo GetZoneInfo(Vector3 position)
		{
			Vector2i id = ZoneSystem.instance.GetZone(position);
			if (IsZoneKnown(position))
			{
				return zone_info[id];
			}
			return null;
		}

		public static BetterTerrain.ZoneInfo GetZoneInfo(Vector2i id)
		{
			if (IsZoneKnown(id))
			{
				return zone_info[id];
			}
			return null;
		}

		public static void SetGameObject(Vector2i id, GameObject gameObject)
		{
			if (!(gameObject == null))
			{
				if (!IsZoneKnown(id))
				{
					zone_info[id] = new BetterTerrain.ZoneInfo();
				}
				zone_info[id].game_object = gameObject;
			}
		}

		public static void SetHeightmap(Vector2i id, Heightmap heightmap)
		{
			if (!(heightmap == null))
			{
				if (!IsZoneKnown(id))
				{
					zone_info[id] = new BetterTerrain.ZoneInfo();
				}
				zone_info[id].hmap = heightmap;
			}
		}

		public static void CalcTMods(Vector2i id)
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
		}

		public static void CalcTMods(BetterTerrain.ZoneInfo zone)
		{
			foreach (TerrainModifier tmod in TerrainModifier.m_instances)
			{
				if (zone.hmap.TerrainVSModifier(tmod))
				{
					zone.tmods.Add(tmod);
				}
			}
		}

		public static void RegenerateZone(Vector2i id)
		{
			if (IsZoneKnown(id))
			{
				RegenerateZone(GetZoneInfo(id));
			}
		}

		public static void RegenerateZone(BetterTerrain.ZoneInfo zone)
		{
			if (zone == null)
			{
				return;
			}
			CalcTMods(ZoneSystem.instance.GetZone(zone.hmap.transform.position));
			if (zone.saved)
			{
				for (int j = 0; j < zone.hmap.m_heights.Count; j++)
				{
					zone.hmap.m_heights[j] = zone.heights[j];
				}
				zone.hmap.m_clearedMask.SetPixels(zone.colors);
				zone.hmap.m_clearedMask.Apply();
			}
			zone.hmap.ApplyModifiers();
			if (can_save && (zone.tmods.Count > 0 || zone.has_location))
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
			}
		}
	}
}
