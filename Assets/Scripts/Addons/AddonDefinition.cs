using Newtonsoft.Json;
using System;

namespace NSMB.Addons {
    [Serializable]
    public class AddonDefinition : IEquatable<AddonDefinition> {
        public Guid Guid { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }

        [JsonIgnore]
        public string FullName => Name + "-" + Version;

        public bool Equals(AddonDefinition other) {
            return Guid == other.Guid;
        }
    }
}