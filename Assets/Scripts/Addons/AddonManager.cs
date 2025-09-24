using Newtonsoft.Json;
using Quantum;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace NSMB.Addon {
    public class AddonManager : MonoBehaviour {

        //---Static Variables
        public static event Action<LoadedAddon> OnAddonLoaded, OnAddonUnloaded;

        private static readonly string RemoteRepoUrl = "https://raw.githubusercontent.com/ipodtouch0218/NSMB-MarioVsLuigi-AddonRepository/main/";
        private static readonly string RemoteAddonsFile = RemoteRepoUrl + "addons.json";
        private static string LocalFolderPath = Path.Combine(Application.dataPath, "addons");
        private static readonly string LocalFolderDownloadedPath = Path.Combine(LocalFolderPath, "download");

        //---Private Variables
        private List<LoadedAddon> loadedAddons = new();

        public async void Start() {
            // TEST
            var addon = await LoadAddonByName("mountain-v0.1");
        }

        public async Task<bool> LoadAddonList(List<string> requestedAddons) {
            // Unload *ALL* addons. this is important as the order MATTERS.
            foreach (var addon in loadedAddons.ToList()) {
                await UnloadAddon(addon);
            }

            // Load addons
            foreach (var addonKey in requestedAddons) {
                var loadedAddon = await LoadAddonByName(addonKey);
                if (loadedAddon == null) {
                    // Failed. Abort.
                    return false;
                }
            }

            // Good to go.
            return true;
        }

        public async Task<LoadedAddon> LoadAddonByName(string addonName) {
            // Primary: check through all addons.
            if (Directory.Exists(LocalFolderPath)) {
                foreach (var addonDef in Directory.GetFiles(LocalFolderPath, "addon.json", SearchOption.AllDirectories)) {
                    string rootFolderFullPath = Path.GetDirectoryName(addonDef);
                    string rootFolderName = Path.GetFileName(rootFolderFullPath);

                    if (rootFolderName.Equals(addonName, StringComparison.InvariantCultureIgnoreCase)) {
                        // We already have this addon locally. Cool beans.
                        Debug.Log($"Loading addon \"{addonName}\" from local source ({rootFolderFullPath})");
                        return await LoadAddonByPath(rootFolderFullPath);
                    }
                }
            }

            // Fallback: check the remote repository.
            string downloadedPath = await DownloadAddonByName(addonName);
            if (downloadedPath == null) {
                Debug.Log($"Failed to automatically download addon \"{addonName}\"");
                return null;
            }
            // Success!
            return await LoadAddonByPath(downloadedPath);
        }

        public async Task<LoadedAddon> LoadAddonByPath(string pathToFolder) {
            // Addon Definition
            string addonDefJson = await File.ReadAllTextAsync(Path.Combine(pathToFolder, "addon.json"));
            AddonDefinition addonDef = JsonConvert.DeserializeObject<AddonDefinition>(addonDefJson);

            // Catalog
            string platformFolder = GetFolderForPlatform();
            string catalogPath = Path.Combine(pathToFolder, platformFolder, "catalog.json");

            var catalogHandle = Addressables.LoadContentCatalogAsync(catalogPath);
            var resourceLocator = await catalogHandle.Task;

            if (catalogHandle.Status == AsyncOperationStatus.Succeeded) {
                var loadAssetObjectsHandle = Addressables.LoadAssetsAsync<AssetObject>(resourceLocator.Keys, _ => {}, Addressables.MergeMode.Union);
                var assetObjects = await loadAssetObjectsHandle.Task;

                if (loadAssetObjectsHandle.Status == AsyncOperationStatus.Succeeded) {
                    foreach (var assetObject in assetObjects) {
                        try {
                            QuantumUnityDB.Global.AddAsset(assetObject);
                        } catch { 
                            // Already added? Doesn't matter... ignore.
                        }
                    }
                }

                LoadedAddon newAddon = new LoadedAddon {
                    Definition = addonDef,
                    CatalogHandle = catalogHandle,
                    AllAssetObjectsHandle = loadAssetObjectsHandle,
                    FullName = Path.GetFileName(pathToFolder),
                };
                loadedAddons.Add(newAddon);
                OnAddonLoaded?.Invoke(newAddon);
                return newAddon;
            }

            return null;
        }

        public async Task<string> DownloadAddonByName(string addonName) {
            string targetFileUrl = CombineUrl(RemoteRepoUrl, addonName + ".zip");
            Debug.Log($"Attempting to download addon {addonName} from remote source ({targetFileUrl})");

            using UnityWebRequest zippedAddonRequest = UnityWebRequest.Get(targetFileUrl);
            zippedAddonRequest.SetRequestHeader("Accept", "*/*");
            zippedAddonRequest.SetRequestHeader("UserAgent", "ipodtouch0218/NSMB-MarioVsLuigi");
            await zippedAddonRequest.SendWebRequest();
            if (zippedAddonRequest.result != UnityWebRequest.Result.Success) {
                Debug.Log($"Download failed: {zippedAddonRequest.error} ({zippedAddonRequest.responseCode})");
                return null;
            }

            string finalPath = Path.Combine(LocalFolderDownloadedPath, addonName);
            string platformFolder = GetFolderForPlatform();
            using (MemoryStream memoryStream = new(zippedAddonRequest.downloadHandler.data)) {
                using ZipArchive zipFile = new(memoryStream);
                if (zipFile.GetEntry(platformFolder + "/catalog.json") == null) {
                    // Our platform doesn't support this mod... oh well
                    Debug.Log($"Download failed: {addonName} doesn't support our platform ({platformFolder})");
                    return null;
                }
                zipFile.ExtractToDirectory(finalPath);
                Debug.Log(finalPath);
            }

#if !UNITY_EDITOR || true
            // Remove other platforms
            foreach (var subdirectory in Directory.EnumerateDirectories(finalPath)) {
                if (!Path.GetFileName(subdirectory).Equals(platformFolder)) {
                    Directory.Delete(subdirectory, true);
                }
            }
#endif 

            return finalPath;
        }

        public async Task UnloadAddon(LoadedAddon addon) {
            if (!addon.AllAssetObjectsHandle.IsDone) {
                await addon.AllAssetObjectsHandle.Task;
            }

            foreach (var assetObject in addon.AllAssetObjectsHandle.Result) {
                QuantumUnityDB.Global.DisposeAsset(assetObject.Guid, true);
                QuantumUnityDB.Global.RemoveSource(assetObject.Guid);
                Debug.Log($"Unloaded {assetObject.name}");
            }

            OnAddonUnloaded?.Invoke(addon);
            addon.AllAssetObjectsHandle.Release();
            addon.CatalogHandle.Release();
            loadedAddons.Remove(addon);
        }

        private static string GetFolderForPlatform() {
#if UNITY_EDITOR
            return UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString();
#else
            switch (Application.platform) {
            case RuntimePlatform.WindowsPlayer:
                return IntPtr.Size == 8 ? "StandaloneWindows64" : "StandaloneWindows";
            case RuntimePlatform.OSXPlayer:
                return "StandaloneOSX";
            case RuntimePlatform.LinuxPlayer:
                return "StandaloneLinux64";
            case RuntimePlatform.Android:
                return "Android";
            case RuntimePlatform.IPhonePlayer:
                return "iOS";
            case RuntimePlatform.WebGLPlayer:
                return "WebGL";
            default:
                throw new Exception("Unsupported platform");
            }
#endif
        }

        private static string CombineUrl(string url1, string url2) {
            if (url1.Length == 0) {
                return url2;
            }

            if (url2.Length == 0) {
                return url1;
            }

            url1 = url1.TrimEnd('/', '\\');
            url2 = url2.TrimStart('/', '\\');

            return $"{url1}/{url2}";
        }
    }

    public class LoadedAddon {
        public AddonDefinition Definition;
        public AsyncOperationHandle<IResourceLocator> CatalogHandle;
        public AsyncOperationHandle<IList<AssetObject>> AllAssetObjectsHandle;
        public string FullName;
    }

    public enum AddonLoadResult {
        Failed,
        AlreadyLoaded,
        Success
    }
}