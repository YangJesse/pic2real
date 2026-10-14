using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dashed.Editor
{
    // Only responds to the local launcher file; ordinary editor startup is unchanged.
    [InitializeOnLoad]
    internal static class QuickPlayLauncher
    {
        private const string StartScene = "Assets/Scenes/00_Workshop.unity";
        private const string PendingKey = "Dashed.QuickPlay.Pending";
        private static double nextCheck;
        private static string StateDirectory => Path.Combine(
            Directory.GetParent(Application.dataPath).FullName, "Library", "OneClickPlay");

        static QuickPlayLauncher()
        {
            EditorApplication.update += CheckRequest;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void CheckRequest()
        {
            if (Application.isBatchMode || EditorApplication.timeSinceStartup < nextCheck)
                return;
            nextCheck = EditorApplication.timeSinceStartup + 0.5;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            string request = Path.Combine(StateDirectory, "request.txt");
            if (!File.Exists(request))
                return;

            try
            {
                // Consume before changing scenes or prompting so cancellation never retries itself.
                File.Delete(request);
                if (EditorApplication.isPlaying)
                {
                    ShowGame();
                    Finish("ready");
                    return;
                }
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetBool(PendingKey, true);
                    return;
                }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Finish("已取消启动，保留当前未保存的场景。");
                    return;
                }
                if (SceneManager.sceneCount != 1 || SceneManager.GetActiveScene().path != StartScene)
                    EditorSceneManager.OpenScene(StartScene, OpenSceneMode.Single);
                SessionState.SetBool(PendingKey, true);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Finish("Unity 启动失败：" + exception.Message);
                Debug.LogException(exception);
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(PendingKey, false))
                return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                ShowGame();
                Finish("ready");
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
                Finish("Unity 未进入游戏，请查看 Console 中的错误后重试。");
        }

        private static void ShowGame()
        {
            EditorApplication.ExecuteMenuItem("Window/General/Game");
        }

        private static void Finish(string result)
        {
            SessionState.SetBool(PendingKey, false);
            Directory.CreateDirectory(StateDirectory);
            string temporary = Path.Combine(StateDirectory, "result.tmp");
            string destination = Path.Combine(StateDirectory, "result.txt");
            File.WriteAllText(temporary, result);
            if (File.Exists(destination))
                File.Delete(destination);
            File.Move(temporary, destination);
        }
    }
}
