using UnityEngine;
namespace TapTapGameJam.MusicGarden
{
    public sealed class GardenTowerSetup : MonoBehaviour
    {
        public int regionId=1;
        public bool chord;
        public GardenRhythm rhythm;
        public PitchFilter filter;
        [Range(0,7)]public int pitch;
    }
}
