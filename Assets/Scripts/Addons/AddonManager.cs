using Newtonsoft.Json;
using Quantum;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace NSMB.Addons {
    public class AddonManager : MonoBehaviour {

        //---Static Variables
        public static event Action<LoadedAddon> OnAddonLoaded, OnAddonUnloaded;
        public static event Action OnAvailableAddonListLoaded;

        private static readonly string RemoteRepoUrl = "https://raw.githubusercontent.com/ipodtouch0218/NSMB-MarioVsLuigi-AddonRepository/main/";
        private static readonly string RemoteAddonsFile = RemoteRepoUrl + "addons.json";
        public static string LocalFolderPath = Path.Combine(Application.dataPath, "addons");
        private static readonly string LocalFolderDownloadedPath = Path.Combine(LocalFolderPath, "download");
        public static readonly string AddonExtension = ".mvladdon";
        private static string AddonCachePath => Path.Combine(Application.persistentDataPath, "addoncache");

        //---Properties
        public List<LoadedAddon> LoadedAddons { get; private set; } = new();
        public ReadOnlyCollection<Addon> AvailableAddons => _availableAddons.AsReadOnly();

        //---Private Variables
        private List<Addon> _availableAddons = new();

        public void Start() {
            _ = FindAvailableAddons();
        }

        public async Awaitable FindAvailableAddons() {
            // Background thread this sh*t
            await Awaitable.BackgroundThreadAsync();
            List<Addon> results = new();

            foreach (var filepath in Directory.EnumerateFiles(LocalFolderPath, "*" + AddonExtension, new EnumerationOptions { RecurseSubdirectories = true })) {
                // Find all `.mvladdon` files.
                await RegisterAddon(filepath, results);
            }

            // Main thread the events
            await Awaitable.MainThreadAsync();
            _availableAddons = results;
            OnAvailableAddonListLoaded?.Invoke();
        }

        public async Task<bool> LoadAllAddons(List<Guid> requestedAddons) {
            // Unload *ALL* addons. this is important as the order MATTERS.
            foreach (var addon in LoadedAddons.ToList()) {
                await UnloadAddon(addon);
            }

            // Load addons
            foreach (var addonKey in requestedAddons) {
                var loadedAddon = await LoadOrDownloadAddon(addonKey);
                if (loadedAddon == null) {
                    // Failed. Abort.
                    return false;
                }
            }
            
            // Good to go.
            return true;
        }

        public async Task<LoadedAddon> LoadOrDownloadAddon(Guid addonGuid) {
            var availableAddon = _availableAddons.FirstOrDefault(addon => addon.Definition.Guid == addonGuid);
            if (availableAddon != null) {
                // Great! We already have this one.
                try {
                    return await LoadAddon(availableAddon);
                } catch (Exception e) {
                    Debug.Log($"[Addon] Failed to load addon {availableAddon.Definition.FullName} ({addonGuid}) from file \"{availableAddon.Filepath}\": {e.Message}");
                }
            }

            // Fallback: check the remote repository.
            Addon downloadedAddon = await DownloadAddon(addonGuid);
            if (downloadedAddon != null) {
                // Success!
                return await LoadAddon(downloadedAddon);
            }

            // Failed.
            return null;
        }

        public async Awaitable<LoadedAddon> LoadAddon(Addon addon) {
            var addonDef = addon.Definition;
            Debug.Log($"[Addon] Loading addon {addonDef.FullName} ({addonDef.Guid}) from file \"{addon.Filepath}\"");

            await Awaitable.BackgroundThreadAsync();

            // Extract to temp folder
            string pathToFolder = Path.Combine(AddonCachePath, addonDef.Guid.ToString());
            if (!Directory.Exists(pathToFolder)) {
                using ZipArchive zipped = ZipFile.OpenRead(addon.Filepath);
                zipped.ExtractToDirectory(pathToFolder);
            }

            // Catalog
            string platformFolderName = GetFolderForPlatform();
            string platformFolderPath = Path.Combine(pathToFolder, platformFolderName);
            if (!Directory.Exists(platformFolderPath)) {
                Debug.LogError($"[Addon] Failed to load addon {addonDef.FullName} ({addonDef.Guid}), it does not seem to support our platform ({platformFolderName})!");
                return null;
            }
            string catalogPath = Directory.GetFiles(platformFolderPath, "*.json").FirstOrDefault();
            if (catalogPath == null) {
                // No catalog? No bitches?
                Debug.LogError($"[Addon] Failed to load addon {addonDef.FullName} ({addonDef.Guid}), it does not seem to support our platform ({platformFolderName})!");
                return null;
            }

            // Resolve paths + create a copy of the catalog.
            string catalogAsString = await File.ReadAllTextAsync(catalogPath);
            catalogAsString = catalogAsString.Replace("{MOD_PATH}", platformFolderPath.Replace(@"\", @"\\")); // JSON expects double escaped backslashes

            string tempCatalogPath = Path.Combine(Application.temporaryCachePath, $"catalog_{addonDef.FullName}.json");
            await File.WriteAllTextAsync(tempCatalogPath, catalogAsString);


            // Read temp catalog
            await Awaitable.MainThreadAsync();
            var catalogHandle = Addressables.LoadContentCatalogAsync(tempCatalogPath);
            var resourceLocator = await catalogHandle.Task;

            if (catalogHandle.Status != AsyncOperationStatus.Succeeded) {
                Debug.LogError($"[Addon] Failed to load addon {addonDef.FullName} ({addonDef.Guid}): {catalogHandle.Status} - {catalogHandle.OperationException.Message}");
                return null;
            }

            var loadAssetObjectsHandle = Addressables.LoadAssetsAsync<AssetObject>(resourceLocator.Keys, _ => {}, Addressables.MergeMode.Union);
            var assetObjects = await loadAssetObjectsHandle.Task;

            if (loadAssetObjectsHandle.Status == AsyncOperationStatus.Succeeded) {
                foreach (var assetObject in assetObjects) {
                    try {
                        QuantumUnityDB.Global.AddAsset(assetObject);
                        Debug.Log($"[Addon] Successfully registered asset {assetObject.name} ({assetObject.Guid})");
                    } catch {
                        // Already added? Doesn't matter... ignore.
                        Debug.Log($"[Addon] Failed to register asset {assetObject.name} ({assetObject.Guid})");
                    }
                }
            }

            var newAddon = new LoadedAddon {
                Definition = addonDef,
                CatalogHandle = catalogHandle,
                AllAssetObjectsHandle = loadAssetObjectsHandle,
            };
            LoadedAddons.Add(newAddon);
            OnAddonLoaded?.Invoke(newAddon);
            return newAddon;
        }

        public async Task<Addon> DownloadAddon(Guid addonGuid) {
            string targetFileUrl = CombineUrl(RemoteRepoUrl, addonGuid + AddonExtension);
            Debug.Log($"[Addon] Attempting to download addon {addonGuid} from remote source ({targetFileUrl})");

            using UnityWebRequest zippedAddonRequest = UnityWebRequest.Get(targetFileUrl);
            zippedAddonRequest.SetRequestHeader("Accept", "*/*");
            zippedAddonRequest.SetRequestHeader("UserAgent", "ipodtouch0218/NSMB-MarioVsLuigi");
            await zippedAddonRequest.SendWebRequest();
            if (zippedAddonRequest.result != UnityWebRequest.Result.Success) {
                Debug.Log($"[Addon] Download failed: {zippedAddonRequest.error} ({zippedAddonRequest.responseCode})");
                return null;
            }

            string finalPath;
            AddonDefinition addonDef;
            using (MemoryStream memoryStream = new(zippedAddonRequest.downloadHandler.data)) {
                using (ZipArchive zipFile = new(memoryStream)) {
                    var addonDefEntry = zipFile.GetEntry("addon.json");
                    if (addonDefEntry == null) {
                        Debug.Log($"[Addon] Download failed: the downloaded file doesn't appear to be an addon?");
                        return null;
                    }

                    using (var addonDefStream = addonDefEntry.Open()) {
                        using var addonDefStreamReader = new StreamReader(addonDefStream);
                        try {
                            addonDef = JsonConvert.DeserializeObject<AddonDefinition>(await addonDefStreamReader.ReadToEndAsync());
                        } catch (Exception e) {
                            Debug.Log($"[Addon] Download failed: the addon.json failed to deserialize {e.Message}");
                            return null;
                        }
                    }

                    string platformFolderName = GetFolderForPlatform();
                    if (zipFile.GetEntry(platformFolderName + "/catalog.json") == null) {
                        Debug.Log($"[Addon] Download failed: the addon {addonDef.FullName} ({addonGuid}) doesn't appear to support our platform! ({platformFolderName})");
                        return null;
                    }
                }

                finalPath = Path.Combine(LocalFolderDownloadedPath, addonDef.FullName + AddonExtension);
                using (var fileStream = new FileStream(finalPath, FileMode.Create)) {
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    memoryStream.CopyTo(fileStream);
                }
                Debug.Log($"[Addon] Successfully downloaded addon {addonDef.FullName} ({addonGuid}) to \"{finalPath}\"");
            }

            var result = new Addon {
                Definition = addonDef,
                Filepath = finalPath,
            };
            _availableAddons.Add(result);
            return result;
        }

        public async Task UnloadAddon(Guid addonGuid) {
            await UnloadAddon(LoadedAddons.FirstOrDefault(la => la.Definition.Guid == addonGuid));
        }

        public async Task UnloadAddon(LoadedAddon addon) {
            if (addon == null) {
                throw new ArgumentNullException("Tried to unload a null addon!");
            }

            if (!addon.AllAssetObjectsHandle.IsDone) {
                await addon.AllAssetObjectsHandle.Task;
            }

            foreach (var assetObject in addon.AllAssetObjectsHandle.Result) {
                QuantumUnityDB.Global.DisposeAsset(assetObject.Guid, true);
                QuantumUnityDB.Global.RemoveSource(assetObject.Guid);
                Debug.Log($"[Addon] Unloaded asset {assetObject.name} ({assetObject.Guid})");
            }

            OnAddonUnloaded?.Invoke(addon);
            addon.AllAssetObjectsHandle.Release();
            addon.CatalogHandle.Release();
            LoadedAddons.Remove(addon);
        }

        public bool IsAddonLoaded(Addon addon) {
            return LoadedAddons.Any(la => la.Definition.Guid == addon.Definition.Guid);
        }

        public Addon FindAddon(string fullPath) {
            // Check if we already know about this addon.
            fullPath = new FileInfo(fullPath).FullName; // Clean up file paths.
            Addon addon = _availableAddons.FirstOrDefault(aa => aa.Filepath == fullPath);
            if (addon != null) {
                return addon;
            }

            return null;
        }

        public async Awaitable<Addon> RegisterAddon(string fullPath, List<Addon> results) {
            // Parse file to see if we need to add this.
            await Awaitable.BackgroundThreadAsync();
            try {
                fullPath = new FileInfo(fullPath).FullName; // Clean up file paths.
                using var zipFile = ZipFile.OpenRead(fullPath);
                var addonEntry = zipFile.GetEntry("addon.json");
                if (addonEntry == null) {
                    return null;
                }

                AddonDefinition addonDef;
                using (var addonStream = addonEntry.Open()) {
                    using var addonStreamReader = new StreamReader(addonStream);
                    addonDef = JsonConvert.DeserializeObject<AddonDefinition>(await addonStreamReader.ReadToEndAsync());
                }

                Addon addon = new() {
                    Definition = addonDef,
                    Filepath = fullPath
                };

                await Awaitable.MainThreadAsync();
                results.Add(addon);
                Debug.Log($"[Addon] Registered addon {addonDef.FullName} ({addonDef.Guid}) at \"{fullPath}\"");
                return addon;
            } catch (Exception e) {
                Debug.LogWarning($"[Addon] Failed to read addon file {fullPath}: {e.Message}");
            }
            return null;
        }

        public static string GetFolderForPlatform() {
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
    }

    public class Addon {
        public AddonDefinition Definition;
        public string Filepath;
    }

    public enum AddonLoadResult {
        Failed,
        AlreadyLoaded,
        Success
    }
}