using UnityEngine;

namespace F_EasyUIMask
{
    // Runtime-only bookkeeping. The serialized reference survives a Play Mode domain reload.
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class FEasyUIMaskTargetArea : MonoBehaviour
    {
        [SerializeField, HideInInspector] private FEasyUIMask owner;

        internal FEasyUIMask Owner
        {
            get { return owner; }
            set { owner = value; }
        }
    }
}
