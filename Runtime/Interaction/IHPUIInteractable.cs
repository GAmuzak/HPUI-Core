using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace ubco.ovilab.HPUI.Interaction 
{
    public interface IHPUIInteractable : IXRInteractable, IXRSelectInteractable
    {
        /// <summary>
        /// Lower z order will get higher priority.
        /// </summary>
        int zOrder { get; set; }

        /// <summary>
        /// Get the projection of the interactors position on the xz plane of this interactable, normalized.
        /// the returned Vector2 - (x, z) on the xz-plane, relative to the center of the interactable.
        /// </summary>
        Vector2 ComputeInteractorPostion(IXRInteractor interactor);

        /// <summary>
        /// This is called when a tap event occurs on the interactable.
        /// </summary>
        void OnTap(HPUITapEventArgs args);

        /// <summary>
        /// Indicates if this handles gesture. If not, if given gesture 
        /// happens while this interactable is selected, it'll be passed to
        /// the next selected interactable in the priority list.
        /// </summary>
        bool HandlesGesture(HPUIGesture gesture);

        /// <summary>
        /// This is called when a gesture event occurs on the interactable.
        /// </summary>
        void OnGesture(HPUIGestureEventArgs args);
    }

}
