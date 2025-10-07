using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Prompts.Addons {
    public class AddonFileSystemEntry : MonoBehaviour {

        //---Static Variables
        private static readonly Dictionary<AddonsSubmenu.ScannedPath.AddonType, string> SpriteNames = new() {
            { AddonsSubmenu.ScannedPath.AddonType.Folder,       "<sprite name=folder>" },
            { AddonsSubmenu.ScannedPath.AddonType.AddonFile,    "<sprite name=addon>" },
            { AddonsSubmenu.ScannedPath.AddonType.NonAddonFile, "<sprite name=unknown_file>" },
        };

        //---Public Variables
        public Button button;

        //---Serialized Variables
        [SerializeField] private TMP_Text text;

        //---Private Variables
        private AddonsSubmenu parent;
        private AddonsSubmenu.ScannedPath scannedPath;

        public void Initialize(AddonsSubmenu parent, AddonsSubmenu.ScannedPath scannedPath) {
            this.parent = parent;
            this.scannedPath = scannedPath;

            text.text = scannedPath.Name;
            if (SpriteNames.TryGetValue(scannedPath.Type, out string sprite)) {
                if (sprite == SpriteNames[AddonsSubmenu.ScannedPath.AddonType.Folder] && scannedPath.Name == "..") {
                    // Manual override for "up"
                    sprite = "<sprite name=up>";
                }
                text.text = sprite + text.text;
            }

            gameObject.SetActive(true);
        }

        public void OnClicked() {
            switch (scannedPath.Type) {
            case AddonsSubmenu.ScannedPath.AddonType.Folder:
                _ = parent.OpenFolder(scannedPath.Name);
                break;
            case AddonsSubmenu.ScannedPath.AddonType.AddonFile:
                var addonManager = GlobalController.Instance.addonManager;
                if (addonManager.IsAddonLoaded(scannedPath.Addon)) {
                    _ = addonManager.UnloadAddon(scannedPath.Addon.Definition.Guid);
                } else {
                    _ = addonManager.LoadAddon(scannedPath.Addon);
                }
                break;
            }
        }
    }
}