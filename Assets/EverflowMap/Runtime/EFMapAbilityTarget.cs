using UnityEngine;

namespace EverflowMap
{
    // Explicit receiver for separated visual/collision hierarchies. A null receiver
    // deliberately marks a solid non-target (e.g. a slide pillar).
    [DisallowMultipleComponent]
    public sealed class EFMapAbilityTarget : MonoBehaviour
    {
        public EFMapNode receiver;
        public static EFMapNode Resolve(Collider body)
        {
            if (body == null) return null;
            var link = body.GetComponentInParent<EFMapAbilityTarget>();
            return link != null ? link.receiver : body.GetComponentInParent<EFMapNode>();
        }
    }
}
