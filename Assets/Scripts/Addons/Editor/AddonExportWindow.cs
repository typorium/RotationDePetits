using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace NSMB.Addon {

    public class AddonExportWindow : EditorWindow {

        private Vector2 scroll;
        private Dictionary<AddressableAssetGroup, bool> groupSelection = new();

        [MenuItem("Tools/MvLO/Addons/Build")]
        public static void BuildAddons() {
            GetWindow<MvLEditorUtils>();
        }

        public void OnEnable() {
            groupSelection.Clear();
            foreach (var group in AddressableAssetSettingsDefaultObject.Settings.groups) { if (group != null) { groupSelection[group] = false; } }
        }

        public void OnGUI() {
            EditorGUILayout.LabelField("Select groups for Addon", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var group in groupSelection.Keys.ToArray()) {
                groupSelection[group] = EditorGUILayout.ToggleLeft(group.Name, groupSelection[group]);
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Build")) {
                BuildForPlatform(BuildTarget.StandaloneWindows64);
                BuildForPlatform(BuildTarget.StandaloneOSX);
                BuildForPlatform(BuildTarget.StandaloneLinux64);
                BuildForPlatform(BuildTarget.WebGL);
                BuildForPlatform(BuildTarget.Android);
                BuildForPlatform(BuildTarget.iOS);
                Close();
            }
        }

        private static void BuildForPlatform(BuildTarget target) {
            Debug.Log("Building Addon Addressable Groups for " + target);

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target)) {
                Debug.LogError("Failed to switch build target to " + target);
                return;
            }

            AddressableAssetSettings.BuildPlayerContent();
            Debug.Log("Addon Addressables build completed for " + target);
        }
    }

    public class AddonCreateWindow : EditorWindow {

        private string addonName = "New Addon", author = "Unknown", version = "v1.0", description = "A brand-new MvLO addon.";

        [MenuItem("Tools/MvLO/Addons/Create New", priority = 1)]
        public static void CreateAddon() {
            GetWindow<AddonCreateWindow>().Show();
        }

        public void OnGUI() {
            EditorGUILayout.LabelField("Addon information", EditorStyles.boldLabel);
            addonName = EditorGUILayout.TextField("Name", addonName);
            author = EditorGUILayout.TextField("Author", author);
            version = EditorGUILayout.TextField("Version", version);
            description = EditorGUILayout.TextArea("Description", description);

            if (GUILayout.Button("Create")) {
                Debug.Log("Create :)");

                var newFolder = Path.Combine("Assets", "Addons", addonName);
                if (Directory.Exists(newFolder)) {
                    if (!EditorUtility.DisplayDialog("Warning", $"An addon with this name already exists at {newFolder}.\nContinue anyway?", "Yes", "No")) {
                        return;
                    }
                }

                // Create folder
                Directory.CreateDirectory(newFolder);
                File.WriteAllText(
                    Path.Combine(newFolder, "addon.json"), 
                    JsonUtility.ToJson(new AddonDefinition {
                        Name = addonName,
                        Author = author,
                        Version = version,
                        Description = description,
                    }, true));

                // Create asset group

                // Add asset group to catalog
                string catalogFolder = Path.Combine("Assets", "AddressableAssetsData", "ExtraCatalogs");
                var catalog = ScriptableObject.CreateInstance<ExternalCatalogSetup>();
                catalog.CatalogName = "catalog";
                catalog.AddAssetGroupToCatalog(null);


                
                
                Close();


            }
        }
    }
}