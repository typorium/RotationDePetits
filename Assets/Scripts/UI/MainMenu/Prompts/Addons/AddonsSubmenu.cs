using NSMB.Addon;
using System.IO;
using TMPro;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.Prompts.Addons {
    public class AddonsSubmenu : PromptSubmenu {

        //---Properties
        private AddonManager AddonManager => GlobalController.Instance.addonManager;

        //---Serialized Variables
        [SerializeField] private GameObject loadingGraphic;
        [SerializeField] private FileNode fileTemplate;
        [SerializeField] private DirectoryNode folderTemplate;

        //---Private Variables
        private Awaitable availableAddonsTask;
        private DirectoryNode fileStructure;

        public override void Initialize() {
            base.Initialize();

            fileTemplate.gameObject.SetActive(false);
            folderTemplate.gameObject.SetActive(false);
        }

        public override void Show(bool first) {
            base.Show(first);

            loadingGraphic.SetActive(true);
            if (availableAddonsTask?.IsCompleted ?? true) {
                availableAddonsTask = CheckForNewAddons();
            }
        }

        public async Awaitable CheckForNewAddons() {
            DeleteNode(fileStructure);
            fileStructure = Instantiate(folderTemplate, folderTemplate.transform.parent);

            var foundAddons = await AddonManager.RefreshAvailableAddons();

            foreach ((var addonPath, var addon) in foundAddons) {
                string partialPath = Path.GetRelativePath(AddonManager.LocalFolderPath, addonPath);
                AddNode(fileStructure, partialPath, addon);
            }

            loadingGraphic.SetActive(false);
        }

        public void AddNode(DirectoryNode parent, string remainingPath, AddonDefinition addon) {
            Debug.Log("parsing " + remainingPath);
            int separatorIndex = remainingPath.IndexOf(Path.DirectorySeparatorChar);
            if (separatorIndex == -1) {
                separatorIndex = remainingPath.IndexOf(Path.AltDirectorySeparatorChar);
            }
            bool isFile = separatorIndex == -1;
            string name = isFile ? remainingPath : remainingPath[..separatorIndex];

            if (!parent.Children.TryGetValue(name, out Node newNode)) {
                if (isFile) {
                    newNode = Instantiate(fileTemplate, parent.transform.parent);
                    ((FileNode) newNode).Addon = addon;
                } else {
                    newNode = Instantiate(folderTemplate, parent.transform.parent);
                }
                newNode.name = name;
                newNode.gameObject.SetActive(true);
                newNode.transform.SetSiblingIndex(parent.transform.GetSiblingIndex() + 1);
                newNode.GetComponentInChildren<TMP_Text>().text = name;
                parent.Children[name] = newNode;
            }

            if (!isFile && newNode is DirectoryNode dNewNode) {
                AddNode(dNewNode, remainingPath[(separatorIndex + 1)..], addon);
            }
        }

        public void DeleteNode(Node node) {
            if (node == null) {
                return;
            }

            if (node is DirectoryNode dNode) {
                foreach ((_, var child) in dNode.Children) {
                    DeleteNode(child);
                }
            }
            Destroy(node.gameObject);
        }

    }
}