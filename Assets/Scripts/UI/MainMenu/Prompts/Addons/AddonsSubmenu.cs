using NSMB.Addons;
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.Prompts.Addons {
    public class AddonsSubmenu : PromptSubmenu {

        //---Static Variables
        private static readonly ScannedPath ParentFolder = new() {
            Name = "..",
            Type = ScannedPath.AddonType.Folder,
        };

        //---Serialized Variables
        [SerializeField] private GameObject loadingGraphic;
        [SerializeField] private AddonFileSystemEntry template;
        [SerializeField] private TMP_Text folderLabel;

        //---Private Variables
        private List<GameObject> entries = new();
        private string currentRelativePath = "";

        public override void Initialize() {
            base.Initialize();
        }

        public override void Show(bool first) {
            base.Show(first);
            _ = OpenFolder(".");
        }

        public async Awaitable OpenFolder(string newPath) {
            await Awaitable.MainThreadAsync();
            loadingGraphic.SetActive(true);
            string fullPath = Path.Combine(AddonManager.LocalFolderPath, currentRelativePath, newPath).Replace(@"\", "/");
            currentRelativePath = folderLabel.text = Path.GetRelativePath(AddonManager.LocalFolderPath, fullPath).Replace(@"\", "/");

            // Prepare paths in background thread
            await Awaitable.BackgroundThreadAsync();
            List<ScannedPath> results = new();

            foreach (string subdirectoryPath in Directory.EnumerateDirectories(fullPath)) {
                string subdirectoryName = Path.GetFileName(subdirectoryPath);
                results.Add(new ScannedPath {
                    Type = ScannedPath.AddonType.Folder,
                    Name = subdirectoryName,
                });
            }
            var addonManager = GlobalController.Instance.addonManager;
            foreach (string filePath in Directory.EnumerateFiles(fullPath)) {
                var addon = addonManager.FindAddon(filePath);
                string fileName = Path.GetFileName(filePath);

                if (addon != null) {
                    // This is an addon.
                    results.Add(new ScannedPath {
                        Type = ScannedPath.AddonType.AddonFile,
                        Addon = addon,
                        Name = fileName,
                    });
                } else {
                    results.Add(new ScannedPath {
                        Type = ScannedPath.AddonType.NonAddonFile,
                        Name = fileName,
                    });
                }
            }
            results.Sort();

            // Create gameobjects in main thread
            await Awaitable.MainThreadAsync();
            foreach (var entry in entries) {
                Destroy(entry);
            }
            entries.Clear();

            bool isRoot = currentRelativePath == ".";
            if (!isRoot) {
                // Create "up" entry.
                var newEntry = Instantiate(template, template.transform.parent);
                newEntry.Initialize(this, ParentFolder);
                entries.Add(newEntry.gameObject);
            }

            foreach (var result in results) {
                var newEntry = Instantiate(template, template.transform.parent);
                newEntry.Initialize(this, result);
                entries.Add(newEntry.gameObject);
            }

            loadingGraphic.SetActive(false);
        }

        public class ScannedPath : IComparable<ScannedPath> {
            public AddonType Type;
            public Addon Addon;
            public string Name;
            public bool IsFolder => Type == AddonType.Folder;

            public int CompareTo(ScannedPath other) {
                if (!IsFolder && other.IsFolder) {
                    return 1;
                } else if (IsFolder && !other.IsFolder) {
                    return -1;
                } else {
                    return string.Compare(Name, other.Name, true);
                }
            }

            public enum AddonType {
                AddonFile,
                Folder,
                NonAddonFile,
            }
        }
    }
}