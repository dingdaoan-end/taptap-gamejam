using System;
using UnityEngine;

namespace TapTapGameJam.MusicGarden
{
    [Serializable] public class GardenStage
    {
        public int regionId,budget,segments;
        public string title;
        [TextArea] public string instruction;
        public GardenSpawn[] waves;
    }
    [CreateAssetMenu(menuName="Music Garden/Game Configuration")]
    public sealed class GardenConfig : ScriptableObject
    {
        [Range(60,140)] public int bpm=100;
        public bool diamondChordRange;
        public RegionDefinition[] regions;
        public GardenStage[] stages;
        public Sprite[] actors;
        public Sprite[] terrain;
        public GameObject[] towerPrefabs,enemyPrefabs;
        public Sprite square,circle;
        public Font font;
        public AudioClip[] samples;
    }
}
