using System;
using System.Reflection;
using CalmSpace.Demo;
using CalmSpace.UI;
using CalmSpace.Editor;
using CalmSpace.Levels;
using CalmSpace.Workshop;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

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

        [Test]
        public void GeneratedRecoverySurfaceHasVisiblePracticalContent()
        {
            EditorSceneManager.OpenScene(
                CalmSpaceProjectSetup.MainScenePath,
                OpenSceneMode.Single);
            WorkshopBottomSheet sheet =
                Object.FindFirstObjectByType<WorkshopBottomSheet>(
                    FindObjectsInactive.Include);

            Assert.That(sheet, Is.Not.Null);
            Transform recovery = sheet.transform.Find("Recovery Content");
            Assert.That(recovery, Is.Not.Null,
                "Load failure needs a distinct authored content group.");
            Text title = recovery?.Find("Failure Title")
                ?.GetComponent<Text>();
            Assert.That(title, Is.Not.Null);
            Assert.That(title.text, Is.Not.Empty);
            Assert.That(title.fontSize, Is.GreaterThanOrEqualTo(24));

            Button action = sheet.ActionButton;
            Assert.That(action, Is.Not.Null);
            Text label = action.GetComponentInChildren<Text>(true);
            Assert.That(label, Is.Not.Null);
            Assert.That(label.text, Is.Not.Empty);
            Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(24));
            Assert.That(action.GetComponent<RectTransform>().sizeDelta.x,
                Is.GreaterThanOrEqualTo(600f));
            Assert.That(action.GetComponent<RectTransform>().sizeDelta.y,
                Is.GreaterThanOrEqualTo(80f));
            Assert.That(action.targetGraphic.color.a,
                Is.GreaterThanOrEqualTo(0.8f));
        }

        [Test]
        public void ExperienceUsesCompanionRecoveryInterfaceWithoutConcreteCast()
        {
            Type recovery = Type.GetType(
                "CalmSpace.UI.IWorkshopHomeRecovery, CalmSpace.Runtime");
            Assert.That(recovery, Is.Not.Null,
                "A narrow recovery boundary must exist beside the exact home contract.");
            Assert.That(
                recovery?.IsAssignableFrom(typeof(WorkshopHomeController)),
                Is.True);

            MethodInfo construct = typeof(DemoExperienceController).GetMethod(
                "Construct",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(construct, Is.Not.Null);
            ParameterInfo[] parameters = construct.GetParameters();
            Assert.That(parameters,
                Has.Some.Matches<ParameterInfo>(parameter =>
                    parameter.ParameterType == recovery));
            Assert.That(parameters,
                Has.None.Matches<ParameterInfo>(parameter =>
                    parameter.ParameterType ==
                        typeof(WorkshopHomeController)));
        }

        [Test]
        public void RuntimeScopeAcceptsTransientInvalidWorkshopAuthoring()
        {
            var scopeRoot = new GameObject("Transient invalid scope");
            var levelRoot = new GameObject("Transient level root");
            LivingWorkshopCatalog invalidWorkshop =
                ScriptableObject.CreateInstance<LivingWorkshopCatalog>();
            WorkshopTextCatalog emptyText =
                ScriptableObject.CreateInstance<WorkshopTextCatalog>();
            try
            {
                Type scopeType = Type.GetType(
                    "CalmSpace.Core.CalmSpaceLifetimeScope, CalmSpace.Runtime");
                Assert.That(scopeType, Is.Not.Null);
                Component scope = scopeRoot.AddComponent(scopeType);
                SetPrivateField(scope, "_levelCatalog",
                    AssetDatabase.LoadAssetAtPath<LevelCatalog>(
                        "Assets/CalmSpace/Config/LevelCatalog.asset"));
                SetPrivateField(scope, "_levelRoot", levelRoot.transform);
                SetPrivateField(scope, "_demoThemeCatalog",
                    AssetDatabase.LoadAssetAtPath<DemoThemeCatalog>(
                        "Assets/CalmSpace/Config/DemoThemeCatalog.asset"));
                SetPrivateField(scope, "_demoDecorationCatalog",
                    AssetDatabase.LoadAssetAtPath<DemoDecorationCatalog>(
                        "Assets/CalmSpace/Config/DemoDecorationCatalog.asset"));
                SetPrivateField(scope, "_livingWorkshopCatalog",
                    invalidWorkshop);
                SetPrivateField(scope, "_workshopTextCatalog", emptyText);
                MethodInfo configure =
                    scopeType.GetMethod(
                        "Configure",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(configure, Is.Not.Null);
                Type builderType = Type.GetType(
                    "VContainer.ContainerBuilder, VContainer");
                Assert.That(builderType, Is.Not.Null);
                object builder = Activator.CreateInstance(builderType);
                Assert.DoesNotThrow(() => configure.Invoke(
                    scope,
                    new[] { builder }),
                    "Optional workshop authoring must fail closed without blocking core Catalog runtime.");
            }
            finally
            {
                Object.DestroyImmediate(scopeRoot);
                Object.DestroyImmediate(levelRoot);
                Object.DestroyImmediate(invalidWorkshop);
                Object.DestroyImmediate(emptyText);
            }
        }

        [Test]
        public void ConfigureRepairsTaskTitleAndRecoveryReferences()
        {
            EditorSceneManager.OpenScene(
                CalmSpaceProjectSetup.MainScenePath,
                OpenSceneMode.Single);
            WorkshopHomeController home =
                Object.FindFirstObjectByType<WorkshopHomeController>(
                    FindObjectsInactive.Include);
            Assert.That(home, Is.Not.Null);
            var view = new SerializedObject(home.View);
            SerializedProperty taskTitle = view.FindProperty("_taskTitle");
            Assert.That(taskTitle, Is.Not.Null,
                "The generated scene contract needs a dedicated task title.");
            WorkshopBottomSheet sheet = home.View.BottomSheet;
            Assert.That(sheet, Is.Not.Null);
            var sheetData = new SerializedObject(sheet);
            SerializedProperty recoveryTitle =
                sheetData.FindProperty("_recoveryTitleLabel");
            Assert.That(recoveryTitle, Is.Not.Null,
                "The generated scene contract needs recovery copy.");
            taskTitle.objectReferenceValue = null;
            recoveryTitle.objectReferenceValue = null;
            view.ApplyModifiedPropertiesWithoutUndo();
            sheetData.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(EditorSceneManager.SaveScene(
                home.gameObject.scene,
                CalmSpaceProjectSetup.MainScenePath), Is.True);

            CalmSpaceProjectSetup.ConfigureProject();

            home = Object.FindFirstObjectByType<WorkshopHomeController>(
                FindObjectsInactive.Include);
            view = new SerializedObject(home.View);
            sheetData = new SerializedObject(home.View.BottomSheet);
            Assert.That(view.FindProperty("_taskTitle").objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                sheetData.FindProperty("_recoveryTitleLabel")
                    .objectReferenceValue,
                Is.Not.Null);
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

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
