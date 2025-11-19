using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class GlobalSoundEffectOverrides : ScriptableObject, ISoundEffectOverrideProvider {

    [FormerlySerializedAs("Overrides")] public SoundEffectOverride[] SfxOverrides;

    [NonSerialized] private Dictionary<SoundEffect, SoundEffectOverride> overridesDict;
    public void OnEnable() {
        overridesDict = new();
        if (SfxOverrides != null) {
            foreach (var @override in SfxOverrides) {
                overridesDict[@override.SoundEffect] = @override;
            }
        }
    }

    public void OnValidate() {
        OnEnable();
    }

    public SoundEffectOverride GetOverrideForSfx(SoundEffect sfx) {
        overridesDict.TryGetValue(sfx, out var result);
        return result;
    }
}