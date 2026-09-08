using Unity.Netcode.Components;

namespace Sprint0.Multiplayer
{
    /// <summary>
    /// Prototype movement is owned by the player that controls this object.
    /// Server-side validation can replace this component when gameplay rules mature.
    /// </summary>
    public sealed class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
