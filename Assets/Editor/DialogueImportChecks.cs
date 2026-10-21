using System;
using System.IO;
using System.Reflection;
using TapTapGameJam.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Runs against imported Unity assets without saving or changing the Map scene.
public static class DialogueImportChecks
{
    public static void Run()
    {
        GameObject root = null;
        try
        {
            root = new GameObject("Dialogue check");
            var player = root.AddComponent<DialoguePlayer>();
            player.enabled = false;
            player.panel = new GameObject("Panel", typeof(RectTransform));
            player.panel.transform.SetParent(root.transform);
            player.speakerName = Child<TextMeshProUGUI>(player.panel, "Speaker");
            player.content = Child<TextMeshProUGUI>(player.panel, "Content");
            player.portrait = Child<Image>(player.panel, "Portrait");
            player.nextButton = Child<Button>(player.panel, "Next");
            player.nextLabel = Child<TextMeshProUGUI>(player.nextButton.gameObject, "Label");
            player.skipButton = Child<Button>(player.panel, "Skip");
            var skipLabel = Child<TextMeshProUGUI>(player.skipButton.gameObject, "Label");
            skipLabel.text = "跳过";
            player.enabled = true;
            // Non-ExecuteAlways components need explicit lifecycle setup in edit mode.
            if (typeof(DialoguePlayer).GetField("skipLabel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player) == null)
                typeof(DialoguePlayer).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
            string[] names = { "L1_Intro", "L1_Clear", "L2_Clear", "L3_Clear", "L4_Clear", "Common_Fail" };
            int[] counts = { 7, 1, 1, 1, 2, 3 };
            for (int n = 0; n < names.Length; n++)
            {
                var sequence = AssetDatabase.LoadAssetAtPath<DialogueSequence>("Assets/Dialogue/" + names[n] + ".asset");
                Check(sequence != null && sequence.IsValid && sequence.lines.Length == counts[n], names[n]);
                int completed = 0;
                Check(player.Play(sequence, _ => completed++), "Play " + names[n]);
                for (int i = 0; i < sequence.lines.Length; i++)
                {
                    var line = sequence.lines[i];
                    Check(line.character != null && line.character.portrait != null, "Portrait asset");
                    Check(player.content.text == line.text && player.speakerName.text == line.character.displayName, "Line content");
                    Check(player.portrait.enabled && player.portrait.sprite == line.character.portrait, "Speaker portrait switch");
                    if (names[n] == "L1_Intro" && i == 5)
                        Check(player.content.text == "准备好了吗？" && player.nextLabel.text == "准备好了",
                            "Player response is shown on the question's advance button");
                    if (names[n] == "L1_Intro" && i == 6)
                        Check(player.content.text == "很好。愿乐灵永随诸身。" && player.nextLabel.text == "结束对话",
                            "Response advances directly to the next line and restores the label");
                    Ready(player);
                    player.Advance();
                }
                Check(completed == 1 && !player.IsPlaying && !player.panel.activeSelf, "Completion");
                Check(!player.portrait.enabled && player.portrait.sprite == null, "Portrait cleared");
            }
            var fail = AssetDatabase.LoadAssetAtPath<DialogueSequence>("Assets/Dialogue/Common_Fail.asset");
            int yes = 0, no = 0;
            player.Play(fail, _ => yes++, () => no++);
            Ready(player);
            player.Skip();
            Check(player.IsPlaying && player.LineIndex == 2 && yes == 0 && no == 0, "Skip stops at choice");
            Check(player.nextLabel.text == "是" && skipLabel.text == "否", "Choice labels");
            Ready(player);
            player.Skip();
            Check(no == 1 && yes == 0 && !player.IsPlaying, "Decline choice");
            player.Play(fail, _ => yes++, () => no++);
            Ready(player);
            player.Skip();
            Ready(player);
            player.Advance();
            Check(yes == 1 && no == 1 && skipLabel.text == "跳过", "Accept and restore labels");
            player.Play(fail, _ => yes++, () => no++, "重新开始", keepDialogueOnDecline: true);
            Ready(player);
            player.Skip();
            Check(player.nextLabel.text == "重新开始", "Failure restart label");
            Ready(player);
            player.Skip();
            Check(no == 2 && player.IsPlaying && player.LineIndex == 2, "Decline preserves retry prompt");
            Ready(player);
            player.Advance();
            Check(yes == 2 && !player.IsPlaying, "Restart completes exactly once");
            player.Play(AssetDatabase.LoadAssetAtPath<DialogueSequence>("Assets/Dialogue/L1_Clear.asset"), _ => { });
            Check(player.nextLabel.text == "结束对话", "Retry label does not leak to victory");
            player.Cancel();
            Directory.CreateDirectory("Artifacts/DialogueImport");
            File.WriteAllText("Artifacts/DialogueImport/check.txt", "PASS: 15 dialogue lines and 1 button response, imported portraits, speaker switching, completion cleanup, skip-to-choice, yes/no callbacks. Unity edit-mode checks; Map Play-mode layout not tested.");
            UnityEngine.Object.DestroyImmediate(root);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            EditorApplication.Exit(1);
        }
    }

    static T Child<T>(GameObject parent, string name) where T : Component
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent.transform, false);
        return child.AddComponent<T>();
    }

    static void Ready(DialoguePlayer player) => typeof(DialoguePlayer)
        .GetField("lastAdvanceFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, -1);
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
