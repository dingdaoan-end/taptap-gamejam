using UnityEngine;
namespace TapTapGameJam.MusicGarden
{
    public enum GardenTerrain { Grass, Road, Blocker, Base }
    [ExecuteAlways, RequireComponent(typeof(SpriteRenderer))]
    public sealed class GardenTile : MonoBehaviour
    {
        public int regionId;public Cell cell;public GardenTerrain kind;
        public GardenConfig config;
        public char Code => "GRXB"[(int)kind];
        void OnValidate() { if(config!=null&&config.terrain!=null&&config.terrain.Length>=3) { var sr=GetComponent<SpriteRenderer>();sr.sprite=config.terrain[kind==GardenTerrain.Base?1:(int)kind];sr.color=kind==GardenTerrain.Base?new Color(.75f,.67f,.47f):Color.white; } }
    }
}
