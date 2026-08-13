using System;
using System.Globalization;
using System.IO;
using CalmSpace.UI;
using CalmSpace.Workshop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CalmSpace.Editor
{
    /// <summary>
    /// QA-only capture of the generated home screen in the 1080x2400 portrait
    /// composition. It never saves the scene, so it cannot affect generated
    /// output or the scene fingerprint.
    /// </summary>
    public static class CalmSpaceRoomScreenshot
    {
        private const int Width = 1080;
        private const int Height = 2400;
        private const string OutputRoot = "docs/screenshots";

        public static void CaptureHomePortrait()
        {
            Directory.CreateDirectory(OutputRoot);
            Scene scene = EditorSceneManager.OpenScene(
                CalmSpaceProjectSetup.MainScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                throw new InvalidOperationException("Could not open Main.unity");
            }

            IsolateHomeScreen();
            Transform roomParent = FindRoomParent();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                CalmSpaceWorkshopAssetBuilder.RoomPrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException("Missing room prefab.");
            }

            Capture("workshop-home-dirty", roomParent, prefab, 0);
            Capture("workshop-home-mid", roomParent, prefab, 4);
            Capture("workshop-home-restored", roomParent, prefab, 8);
            Debug.Log("CALMSPACE_SCREENSHOTS_COMPLETE " + OutputRoot);
        }

        private static void Capture(
            string name,
            Transform roomParent,
            GameObject prefab,
            int completedBeats)
        {
            GameObject instance = Object.Instantiate(prefab, roomParent, false);
            var presenter = instance.GetComponent<WorkshopRoomPresenter>();
            presenter.ApplyState(BuildState(completedBeats));
            presenter.SetVisible(true);

            Canvas canvas = roomParent.GetComponentInParent<Canvas>();
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;

            var cameraObject = new GameObject("CalmSpace Capture Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.09f, 1f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            cameraObject.transform.position = new Vector3(0f, 0f, -100f);

            var texture = new RenderTexture(Width, Height, 24)
            {
                antiAliasing = 1
            };
            var readback = new Texture2D(
                Width, Height, TextureFormat.RGB24, false);

            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                Canvas.ForceUpdateCanvases();

                camera.targetTexture = texture;
                camera.Render();

                RenderTexture previousActive = RenderTexture.active;
                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                readback.Apply();
                RenderTexture.active = previousActive;

                string path = Path.Combine(OutputRoot, name + ".png");
                File.WriteAllBytes(path, readback.EncodeToPNG());
                Debug.Log(
                    "CALMSPACE_SCREENSHOT " + path + " " +
                    Width.ToString(CultureInfo.InvariantCulture) + "x" +
                    Height.ToString(CultureInfo.InvariantCulture));
            }
            finally
            {
                camera.targetTexture = null;
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(readback);
                texture.Release();
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// Edit mode never runs the screen router, so every authored screen is
        /// still active. Show only the home screen, exactly as the player sees
        /// it on cold launch.
        /// </summary>
        private static void IsolateHomeScreen()
        {
            var experience =
                Object.FindFirstObjectByType<DemoExperienceController>(
                    FindObjectsInactive.Include);
            if (experience == null)
            {
                throw new InvalidOperationException(
                    "Main.unity has no DemoExperienceController.");
            }

            string[] hidden =
            {
                "_levelSelectScreen", "_hudScreen", "_completionScreen",
                "_loadingOverlay"
            };
            foreach (string field in hidden)
            {
                SetScreenActive(experience, field, false);
            }

            SetScreenActive(experience, "_homeScreen", true);
        }

        private static void SetScreenActive(
            DemoExperienceController experience,
            string fieldName,
            bool active)
        {
            var serialized = new SerializedObject(experience);
            SerializedProperty property = serialized.FindProperty(fieldName);
            var group = property?.objectReferenceValue as CanvasGroup;
            if (group == null)
            {
                return;
            }

            group.alpha = active ? 1f : 0f;
            group.gameObject.SetActive(active);
        }

        private static WorkshopRoomVisualState BuildState(int completedBeats)
        {
            var mask = 0;
            for (var index = 0; index < completedBeats; index++)
            {
                if (WorkshopContentIds.TryGetCozyWorkshopBeat(
                        index, out WorkshopBeatContract beat))
                {
                    mask |= 1 << beat.ZoneIndex;
                }
            }

            string hotspot = string.Empty;
            if (completedBeats < WorkshopContentIds.CozyWorkshopBeatCount &&
                WorkshopContentIds.TryGetCozyWorkshopBeat(
                    completedBeats, out WorkshopBeatContract next))
            {
                hotspot = next.BeatId;
            }

            return new WorkshopRoomVisualState(
                WorkshopContentIds.CozyWorkshopChapterId,
                mask,
                hotspot,
                completedBeats == WorkshopContentIds.CozyWorkshopBeatCount);
        }

        private static Transform FindRoomParent()
        {
            foreach (GameObject root in
                     SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform found = FindChild(
                    root.transform, "Workshop Room Center");
                if (found != null)
                {
                    return found;
                }
            }

            throw new InvalidOperationException(
                "Main.unity has no Workshop Room Center.");
        }

        private static Transform FindChild(Transform parent, string name)
        {
            if (string.Equals(parent.name, name, StringComparison.Ordinal))
            {
                return parent;
            }

            for (var index = 0; index < parent.childCount; index++)
            {
                Transform found = FindChild(parent.GetChild(index), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
