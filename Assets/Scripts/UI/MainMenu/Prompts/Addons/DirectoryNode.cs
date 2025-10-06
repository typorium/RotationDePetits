using System.Collections.Generic;

namespace NSMB.UI.MainMenu.Submenus.Prompts.Addons {
    public class DirectoryNode : Node {
        public Dictionary<string, Node> Children { get; } = new();
    }
}