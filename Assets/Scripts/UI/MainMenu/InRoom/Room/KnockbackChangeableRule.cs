using NSMB.UI.Translation;
using Quantum;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class KnockbackChangeableRule : NumberChangeableRule {
        protected override void UpdateLabel() {
            TranslationManager tm = GlobalController.Instance.translationManager;
            if (value is int intValue) {
                decimal value = ((decimal) (intValue) / 10) + 1;
                label.text = labelPrefix + "x " + value.ToString();
            }
        }
    }
}