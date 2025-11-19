namespace Quantum {
  using Photon.Deterministic;
  using System;
  using UnityEngine;

  /// <summary>
  /// The asset wraps an instance of <see cref="DeterministicSessionConfig"/> and makes it globally (in Unity) accessible as a default config.
  /// </summary>
  [CreateAssetMenu(menuName = "Quantum/Configurations/SessionConfig", order = EditorDefines.AssetMenuPriorityConfigurations + 2)]
  [QuantumGlobalScriptableObject(DefaultPath)]
  public class QuantumDeterministicSessionConfigAsset : QuantumGlobalScriptableObject<QuantumDeterministicSessionConfigAsset>, ISerializationCallbackReceiver {
    /// <summary>
    /// The default location of the global QuantumDeterministicSessionConfigAsset asset.
    /// </summary>
    public const string DefaultPath = "Assets/QuantumUser/Resources/SessionConfig.asset";
    /// <summary>
    /// A rule of thumb time added to compensate to additional network transport and condition.
    /// </summary>
    public const int AliasingSlackTimeMs = 30;

    /// <summary>
    /// The config instance.
    /// </summary>
    [DrawInline]
    public DeterministicSessionConfig Config;
    /// <summary>
    /// Override the hard tolerance calculation.
    /// </summary>
    [Header("Override")]
    [InlineHelp]
    public bool OverrideHardTolerance;
    /// <summary>
    /// Override the calculated hard tolerance with this value.
    /// </summary>
    [DrawIf("OverrideHardTolerance", true)]
    [InlineHelp]
    public int HardTolerance = 8;

    /// <summary>
    /// Return the default global config instance.
    /// </summary>
    public static DeterministicSessionConfig DefaultConfig => Global.Config;

    /// <summary>
    /// Calculate the hard tolerance from the ping value that starts local input delay.
    /// ceil((rtt / 2 + delta + slack) / delta) + 1 - min 
    /// </summary>
    /// <param name="offsetPingStart">Ping to start input delay</param>
    /// <param name="updateFps">Simulation update rate</param>
    /// <param name="minInputDelay">Min input delay</param>
    /// <returns>Computed hard tolerance value</returns>
    public static int CalculateHardToleranceFrames(int offsetPingStart, int updateFps, int minInputDelay) =>
      Math.Max(0, Mathf.CeilToInt((offsetPingStart * 0.5f + AliasingSlackTimeMs) / (1000.0f / updateFps)) + 2 - minInputDelay);

    /// <summary>
    /// Make sure limitations, calculations or overrides are applied.
    /// </summary>
    public void OnAfterDeserialize() {
      Config.InputDelayMin = Math.Min(Config.InputDelayMin, Config.UpdateFPS);
      Config.TimeScalePingMax = Math.Max(Config.TimeScalePingMax, Config.TimeScalePingMin + 1);
      Config.InputDelayMax = Config.UpdateFPS;

      if (OverrideHardTolerance) {
        Config.InputHardTolerance = HardTolerance;
      } else {
        Config.InputHardTolerance = CalculateHardToleranceFrames(Config.InputDelayPingStart, Config.UpdateFPS, Config.InputDelayMin);
      }
    }

    /// <summary>
    /// Not needed.
    /// </summary>
    public void OnBeforeSerialize() {
    }
  }
}