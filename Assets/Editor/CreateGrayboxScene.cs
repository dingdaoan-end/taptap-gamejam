using System.IO;
using TapTapGameJam.Graybox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TapTapGameJam.Graybox.Editor
{
    public static class CreateGrayboxScene
    {
        private const string ScenePath = "Assets/Scenes/Graybox.unity";
        private const string SpritePath = "Assets/Art/WhiteSquare.png";

        [MenuItem("Tools/Game Jam/Create Graybox Scene")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
        }

        public static void CreateBatch()
        {
            BuildScene();
        }

        private static void BuildScene()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Art");
            Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (square == null)
            {
                Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                File.WriteAllBytes(SpritePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 1f;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
                square = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 5.2f;
            camera.backgroundColor = new Color(0.09f, 0.11f, 0.14f);
            CameraFollow2D follower = camera.gameObject.AddComponent<CameraFollow2D>();

            // Top-down: a closed room with fixed obstacles and a pushable crate.
            Block(square, "TopDown Floor", new Vector2(-12f, 0f), new Vector2(11f, 8f),
                new Color(0.17f, 0.2f, 0.23f), false, -2);
            Block(square, "Top Wall", new Vector2(-12f, 4.2f), new Vector2(11.6f, 0.4f), Color.gray, true);
            Block(square, "Bottom Wall", new Vector2(-12f, -4.2f), new Vector2(11.6f, 0.4f), Color.gray, true);
            Block(square, "Left Wall", new Vector2(-17.6f, 0f), new Vector2(0.4f, 8.8f), Color.gray, true);
            Block(square, "Right Wall", new Vector2(-6.4f, 0f), new Vector2(0.4f, 8.8f), Color.gray, true);
            Block(square, "TopDown Obstacle A", new Vector2(-14f, 1.6f), new Vector2(1.6f, 0.7f), Color.gray, true);
            Block(square, "TopDown Obstacle B", new Vector2(-9.5f, -1.3f), new Vector2(0.7f, 2f), Color.gray, true);
            Crate(square, "TopDown Crate", new Vector2(-11f, 1.5f), 0f);

            // Side view: ground, raised platforms, a wall and a falling crate.
            Block(square, "Side Background", new Vector2(10f, 0f), new Vector2(17f, 10f),
                new Color(0.16f, 0.18f, 0.22f), false, -2);
            Block(square, "Ground", new Vector2(10f, -4f), new Vector2(17f, 1f), Color.gray, true);
            Block(square, "Platform A", new Vector2(7f, -1.6f), new Vector2(2.4f, 0.35f), Color.gray, true);
            Block(square, "Platform B", new Vector2(12f, -0.2f), new Vector2(2.4f, 0.35f), Color.gray, true);
            Block(square, "Side Wall", new Vector2(15.5f, -2.3f), new Vector2(0.5f, 2.4f), Color.gray, true);
            Crate(square, "Falling Crate", new Vector2(12f, 2f), 1f);

            GameObject player = Block(square, "Player", new Vector2(-12f, 0f),
                new Vector2(0.8f, 1f), Color.cyan, true, 2);
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            player.AddComponent<GrayboxController2D>();
            follower.SetTarget(player.transform);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Graybox created at " + ScenePath + ". Press Play; Tab switches modes.");
        }

        private static GameObject Block(Sprite square, string name, Vector2 center, Vector2 size,
            Color color, bool solid, int sortingOrder = 0)
        {
            GameObject block = new GameObject(name);
            block.transform.position = center;
            block.transform.localScale = size;
            SpriteRenderer renderer = block.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            if (solid) block.AddComponent<BoxCollider2D>();
            return block;
        }

        private static void Crate(Sprite square, string name, Vector2 position, float gravity)
        {
            GameObject crate = Block(square, name, position, Vector2.one, new Color(0.85f, 0.35f, 0.3f), true, 1);
            Rigidbody2D body = crate.AddComponent<Rigidbody2D>();
            body.gravityScale = gravity;
#if UNITY_6000_0_OR_NEWER
            body.linearDamping = gravity == 0f ? 4f : 0f;
#else
            body.drag = gravity == 0f ? 4f : 0f;
#endif
            body.freezeRotation = true;
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            }
        }
    }
}
