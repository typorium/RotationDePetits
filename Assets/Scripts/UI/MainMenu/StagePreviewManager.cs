using NSMB.Utilities;
using Quantum;
using System;
using UnityEngine;

namespace NSMB.UI.MainMenu {
    public class StagePreviewManager : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private Camera targetCamera;
        [SerializeField] private GameObject noPreviewPrefab;

        //---Private Variables
        private AssetRef<VersusStageData> currentActiveStageData;
        private GameObject currentActivePrefab;

        public void Start() {
            PreviewRandomStage();
            QuantumEvent.Subscribe<EventPlayerAdded>(this, OnPlayerAdded);
            QuantumEvent.Subscribe<EventRulesChanged>(this, OnRulesChanged);
        }

        public void PreviewRandomStage() {
            UnityEngine.Random.InitState((int) DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var maps = QuantumViewUtils.Maps;
            PreviewStage(maps[UnityEngine.Random.Range(0, maps.Length)]);
        }

        public void PreviewStage(VersusStageData stage) {
            if (currentActiveStageData == stage) {
                return;
            }
            currentActiveStageData = stage;

            if (currentActivePrefab) {
                Destroy(currentActivePrefab);
            }

            GameObject prefabToSpawn = noPreviewPrefab;
            if (stage && stage.MainMenuPreviewPrefab) {
                prefabToSpawn = stage.MainMenuPreviewPrefab;
            }

            currentActivePrefab = Instantiate(prefabToSpawn, transform);
            targetCamera.transform.position = currentActivePrefab.GetComponentInChildren<MainMenuCameraOrigin>().transform.position;
        }

        public void PreviewStage(AssetRef<Map> map) {
            PreviewStage(QuantumUnityDB.GetGlobalAsset<VersusStageData>(QuantumUnityDB.GetGlobalAsset(map).UserAsset.Id));
        }

        private unsafe void OnPlayerAdded(EventPlayerAdded e) {
            if (e.Game.PlayerIsLocal(e.Player)) {
                PreviewStage(e.Game.Frames.Predicted.Global->Rules.Stage);
            }
        }

        private unsafe void OnRulesChanged(EventRulesChanged e) {
            PreviewStage(e.Game.Frames.Predicted.Global->Rules.Stage);
        }
    }
}
