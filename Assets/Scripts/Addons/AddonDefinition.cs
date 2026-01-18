using Newtonsoft.Json;
using System;

namespace NSMB.Addons {
    [Serializable]
    public class AddonDefinition : IEquatable<AddonDefinition> {
        public Guid ReleaseGuid { get; set; }
        public string DisplayName { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }

        [JsonIgnore]
        public string FullName => $"{DisplayName} ({Version})";

        public bool Equals(AddonDefinition other) {
            return ReleaseGuid == other.ReleaseGuid;
        }
    }
}