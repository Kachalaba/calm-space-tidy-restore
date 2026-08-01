using CalmSpace.UI;
using CalmSpace.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CalmSpace.Tests.EditMode
{
    public sealed class WorkshopHomeViewTests
    {
        private GameObject _root;
        private WorkshopHomeView _view;
        private WorkshopBottomSheet _sheet;
        private Button _primary;
        private Button _catalog;
        private Button _settings;
        private Button _hotspot;
        private GameObject _album;
        private GameObject _decor;
        private GameObject _daily;
        private GameObject _relaxPass;

        [SetUp]
        public void CreateView()
        {
            _root = new GameObject("Workshop home test");
            _view = _root.AddComponent<WorkshopHomeView>();
            _primary = CreateButton("Primary");
            _catalog = CreateButton("Catalog");
            _settings = CreateButton("Settings");
            _hotspot = CreateButton("Hotspot");
            _album = CreateRoot("Album");
            _decor = CreateRoot("Decor");
            _daily = CreateRoot("Daily");
            _relaxPass = CreateRoot("Relax Pass");

            GameObject sheetRoot = CreateRoot("Sheet");
            CanvasGroup sheetGroup = sheetRoot.AddComponent<CanvasGroup>();
            Button close = CreateButton("Close");
            Button action = CreateButton("Action");
            _sheet = sheetRoot.AddComponent<WorkshopBottomSheet>();
            _sheet.Configure(sheetGroup, close, action);

            _view.Configure(
                _primary,
                _catalog,
                _settings,
                _hotspot,
                _album,
                _decor,
                _daily,
                _relaxPass,
                null,
                null,
                _sheet);
        }

        [TearDown]
        public void DestroyView()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void RebindingDoesNotMultiplyListenersAndUnbindRemovesThem()
        {
            var primaryCount = 0;
            var catalogCount = 0;
            var settingsCount = 0;
            var hotspotCount = 0;

            void Bind() => _view.Bind(
                () => primaryCount++,
                () => catalogCount++,
                () => settingsCount++,
                () => hotspotCount++);

            Bind();
            Bind();
            _primary.onClick.Invoke();
            _catalog.onClick.Invoke();
            _settings.onClick.Invoke();
            _hotspot.onClick.Invoke();

            Assert.That(primaryCount, Is.EqualTo(1));
            Assert.That(catalogCount, Is.EqualTo(1));
            Assert.That(settingsCount, Is.EqualTo(1));
            Assert.That(hotspotCount, Is.EqualTo(1));

            _view.Unbind();
            _primary.onClick.Invoke();
            _catalog.onClick.Invoke();
            _settings.onClick.Invoke();
            _hotspot.onClick.Invoke();
            Assert.That(primaryCount, Is.EqualTo(1));
            Assert.That(catalogCount, Is.EqualTo(1));
            Assert.That(settingsCount, Is.EqualTo(1));
            Assert.That(hotspotCount, Is.EqualTo(1));
        }

        [Test]
        public void RenderKeepsOneHotspotAndBottomSheetInStableStates()
        {
            _view.Bind(() => { }, () => { }, () => { }, () => { });
            _view.Render(new WorkshopHomeViewState(
                "Start",
                "0 / 8",
                true,
                true));

            _sheet.Open();
            _sheet.Open();
            Assert.That(_view.ActiveHotspotCount, Is.EqualTo(1));
            Assert.That(_view.OpenBottomSheetCount, Is.EqualTo(1));

            _sheet.Close();
            _view.Render(new WorkshopHomeViewState(
                "The workshop is ready",
                "8 / 8",
                false,
                false));
            Assert.That(_view.ActiveHotspotCount, Is.Zero);
            Assert.That(_view.OpenBottomSheetCount, Is.Zero);
        }

        [Test]
        public void ReplacingSheetActionRemovesThePreviousListener()
        {
            var first = 0;
            var second = 0;
            _sheet.Open(() => first++);
            _sheet.Open(() => second++);

            _sheet.ActionButton.onClick.Invoke();

            Assert.That(first, Is.Zero);
            Assert.That(second, Is.EqualTo(1));
            _sheet.Close();
            _sheet.ActionButton.onClick.Invoke();
            Assert.That(second, Is.EqualTo(1));
        }

        [Test]
        public void UnavailableCapabilitiesStayHiddenAndVisibleControlsAreBound()
        {
            _view.Bind(() => { }, () => { }, () => { }, () => { });
            _view.Render(new WorkshopHomeViewState(
                "Start",
                "0 / 8",
                true,
                true));

            Assert.That(_album.activeSelf, Is.False);
            Assert.That(_decor.activeSelf, Is.False);
            Assert.That(_daily.activeSelf, Is.False);
            Assert.That(_relaxPass.activeSelf, Is.False);
            Assert.That(_view.VisibleActionControlsHaveBindings, Is.True);
        }

        [Test]
        public void ConfigureRepairsMissingRequiredHomeReference()
        {
            EditorSceneManager.OpenScene(
                CalmSpaceProjectSetup.MainScenePath,
                OpenSceneMode.Single);
            WorkshopHomeController home =
                Object.FindFirstObjectByType<WorkshopHomeController>(
                    FindObjectsInactive.Include);
            Assert.That(home, Is.Not.Null);
            var serialized = new SerializedObject(home.View);
            serialized.FindProperty("_primaryButton").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(
                EditorSceneManager.SaveScene(
                    home.gameObject.scene,
                    CalmSpaceProjectSetup.MainScenePath),
                Is.True);

            CalmSpaceProjectSetup.ConfigureProject();

            home = Object.FindFirstObjectByType<WorkshopHomeController>(
                FindObjectsInactive.Include);
            serialized = new SerializedObject(home.View);
            Assert.That(
                serialized.FindProperty("_primaryButton").objectReferenceValue,
                Is.Not.Null,
                "A matching marker must not block repair of a missing ref.");
        }

        private Button CreateButton(string name)
        {
            GameObject root = CreateRoot(name);
            root.AddComponent<Image>();
            return root.AddComponent<Button>();
        }

        private GameObject CreateRoot(string name)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(_root.transform, false);
            return root;
        }
    }
}
