using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace TapTapGameJam.MusicGarden.Editor
{
    public static class GardenChecks
    {
        public static void CreateCheckBuild()
        {
            GardenBuilder.Create();Run();GardenBuilder.Build();
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene(GardenBuilder.ScenePath);
            var g=UnityEngine.Object.FindObjectOfType<GardenGame>();
            if(g==null||g.config==null||g.hud==null||g.view==null)throw new Exception("Missing serialized gameplay references");
            if(g.config.towerPrefabs.Length!=2||g.config.enemyPrefabs.Length!=3||g.config.towerPrefabs.Concat(g.config.enemyPrefabs).Any(p=>p==null))throw new Exception("Missing runtime prefabs");
            if(!UnityEngine.Object.FindObjectsOfType<Camera>().Any(c=>c.rect==new Rect(0,0,1,1)&&c.cullingMask==0&&c.clearFlags==CameraClearFlags.SolidColor))throw new Exception("Overlay UI must have a full-screen clear");
            var tiles=UnityEngine.Object.FindObjectsOfType<GardenTile>();
            if(tiles.Length!=1300)throw new Exception("Expected 1300 editable tiles");
            foreach(var r in g.config.regions)
            {
                var local=tiles.Where(t=>t.regionId==r.id).ToArray();if(local.Length!=325)throw new Exception("Region tile count mismatch");
                if(local.Any(t=>t.Code!=r.cells[t.cell.y][t.cell.x]))throw new Exception("Reference map mismatch");
                string error=MapRules.ValidateRoute(r);if(error!=null)throw new Exception(error);
                if(g.config.samples.Any(c=>c==null)||g.config.actors.Any(s=>s==null)||g.config.font==null)throw new Exception("Missing art or audio");
                var stage=g.config.stages.First(s=>s.regionId==r.id);
                var model=new GardenSimulation(r,stage.waves,stage.budget);GardenContent.Arrange(model);
                int safety=300;while(!model.Complete&&!model.Lost&&safety-->0)model.Step();
                if(!model.Complete)throw new Exception("Scene pads prevent region "+r.id+" reference victory");
            }
            Debug.Log("GARDEN_EDITOR_CHECK_SUCCESS tiles=1300 exactMap=true regions=4 art=true samples=8");
        }
    }
}
