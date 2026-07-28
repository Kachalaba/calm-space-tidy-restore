using UnityEngine;

namespace CalmSpace.Audio
{
    public interface IAsmrAudioService
    {
        bool IsAvailable { get; }

        void PlaySnap(Vector3 worldPosition);
    }
}
