using NSMB.UI.Translation;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class ManiaChangeableRule : NumberChangeableRule {
        protected override void UpdateLabel() {
            TranslationManager tm = GlobalController.Instance.translationManager;
            if (value is int intValue) {
                label.text = (intValue / 6).ToString() + ":" + (intValue % 6).ToString() + "0";
            }
        }
    }
}
