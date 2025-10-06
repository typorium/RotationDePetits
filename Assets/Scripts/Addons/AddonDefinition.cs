using Newtonsoft.Json;
using System;

namespace NSMB.Addon {
    [Serializable]
    public class AddonDefinition : IEquatable<AddonDefinition> {
        public string Name { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }

        [JsonIgnore]
        public string FullName => Name + "-" + Version;

        public bool Equals(AddonDefinition other) {
            return Name == other.Name
                && Version == other.Version;
        }
    }
}