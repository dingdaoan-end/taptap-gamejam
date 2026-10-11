using UnityEditor;
using UnityEngine;

namespace TapTapGameJam.PianoDefense.Editor
{
    [CustomEditor(typeof(PianoDefenseGame))]
    public sealed class PianoDefenseGameEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var composition = serializedObject.FindProperty("useCompositionLessons");
            EditorGUILayout.PropertyField(composition, new GUIContent("四乐章编曲模式"));
            if (composition.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("lessonIndex"), new GUIContent("开局乐章 0–3"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("lessons"), new GUIContent("乐章配置：双击修改波次与节奏"), true);
                EditorGUILayout.HelpBox("每关开局琴塔在 Editable Board → Starting Towers → Lesson 1–4 中。琴塔的 Lesson 要与关卡一致，0 表示第一关。界面在 Composition UI 的 Canvas 中，可直接修改文字、位置和图片。", MessageType.Info);
            }
            var board = serializedObject.FindProperty("board");
            EditorGUILayout.PropertyField(board, new GUIContent("场景棋盘"));
            if (board.objectReferenceValue != null)
                EditorGUILayout.HelpBox("在 Hierarchy 展开 Piano Defense → Editable Board。格子和三座琴塔是真正的场景对象；移动琴塔、改材质或增删对象后按 Ctrl+S 保存。", MessageType.Info);
            else
            {
                EditorGUILayout.HelpBox("尚未创建可编辑棋盘。运行 Tools → Game Jam → Create Editable Board In Scene。", MessageType.Warning);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("startingTowers"), new GUIContent("旧版开局琴塔"), true);
            }
            if (!composition.boolValue) {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bpm"), new GUIContent("节奏 BPM"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("startingCoins"), new GUIContent("开局金币"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("totalBeats"), new GUIContent("总拍数"));

            EditorGUILayout.Space();
            var custom = serializedObject.FindProperty("useCustomWaves");
            EditorGUILayout.PropertyField(custom, new GUIContent("自定义敌人波次"));
            if (custom.boolValue)
            {
                var waves = serializedObject.FindProperty("waveSpawns");
                if (GUILayout.Button("导入默认波次后编辑"))
                {
                    var defaults = DefenseSimulation.CreateWaves();
                    waves.arraySize = defaults.Count;
                    for (int i = 0; i < defaults.Count; i++)
                    {
                        var entry = waves.GetArrayElementAtIndex(i);
                        entry.FindPropertyRelative("Beat").intValue = defaults[i].Beat;
                        entry.FindPropertyRelative("Lane").intValue = defaults[i].Lane;
                        entry.FindPropertyRelative("Pitch").intValue = defaults[i].Pitch;
                        entry.FindPropertyRelative("Speed").intValue = defaults[i].Speed;
                    }
                }
                EditorGUILayout.PropertyField(waves, new GUIContent("敌人：拍／路线／音高／速度"), true);
                if (waves.arraySize == 0) EditorGUILayout.HelpBox("当前没有敌人。路线使用 1、3、5；音高 0–7；速度 1 或 2。", MessageType.Warning);
            }
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
