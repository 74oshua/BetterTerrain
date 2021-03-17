using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace BetterTerrain
{
	[BepInPlugin("org.bepinex.plugins.betterterrain", "Better Terrain", "0.1.12.0")]
	public class BetterTerrain : BaseUnityPlugin
	{
		public class ZoneInfo
		{
			public Heightmap hmap;

			public List<float> heights = new List<float>();

			public Color[] colors;

			public List<TerrainModifier> tmods = new List<TerrainModifier>();

			public List<TerrainModifier> applied_tmods = new List<TerrainModifier>();

			public List<TerrainModifier> deletable_tmods = new List<TerrainModifier>();

			public float ttu = 1f;

			public int num_tmods;

			public GameObject game_object;

			public bool saved;

			public bool generated;

			public bool has_location;

			public ZoneInfo()
			{
			}

			public ZoneInfo(List<float> h, Color[] c)
			{
				heights = h;
				colors = c;
			}
		}

		public static byte major = 0;

		public static byte minor = 12;

		public static string db_path = "";

		private void Awake()
		{
			Debug.Log("Starting BetterTerrain");
			Harmony.CreateAndPatchAll(typeof(Patches));
		}
	}
}
