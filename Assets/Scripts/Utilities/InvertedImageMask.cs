using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace NSMB.Utilities {
    public class InvertedImageMask : Image {
        public override Material materialForRendering {
            get {
                var mat = base.materialForRendering;
                mat.SetInt("_StencilComp", (int) CompareFunction.NotEqual);
                return mat;
            }
        }
    }
}