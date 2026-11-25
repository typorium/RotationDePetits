using NSMB.UI.Translation;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class ManiaChangeableRule : NumberChangeableRule {
        protected override void UpdateLabel() {

            const int minuteDivider = 12;

            TranslationManager tm = GlobalController.Instance.translationManager;
            if (value is int intValue) {
                if (intValue == 0) {
                    label.text = tm.GetTranslation("ui.generic.random");
                    return;
                }

                string minutesString = (intValue / minuteDivider).ToString();

                int secondsMultiplier = intValue % minuteDivider;
                int secondsIncrement = (60 / minuteDivider);

                string secondsString = (secondsMultiplier * secondsIncrement).ToString("00");

                label.text = labelPrefix + minutesString + ":" + secondsString;
            }
        }
    }
}