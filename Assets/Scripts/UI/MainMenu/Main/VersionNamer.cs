using UnityEngine;
using TMPro;

namespace NSMB.UI.MainMenu.Submenus.Main {
    public class VersionNamer : MonoBehaviour {

        public bool UseRDPVersion;
        public string RDPVersion;
        public void Start() {

            if (UseRDPVersion) {
                GetComponent<TMP_Text>().text = "RDP Version: v" + RDPVersion;
            } else {
                GetComponent<TMP_Text>().text = "Original Version: v" + Application.version;
            }
        }
    }
}
