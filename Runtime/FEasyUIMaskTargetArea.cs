using UnityEngine;

namespace F_EasyUIMask
{
    // Runtime-only bookkeeping. The serialized reference survives a Play Mode domain reload.
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class FEasyUIMaskTargetArea : MonoBehaviour
    {
        [SerializeField, HideInInspector] private FEasyUIRectMask owner;

        internal FEasyUIRectMask Owner
        {
            get { return owner; }
            set { owner = value; }
        }
    }
}
