using UnityEngine;

namespace CalmSpace.Audio
{
    public enum AsmrAudioCue
    {
        Placement = 0,
        ScrewTurn = 1,
        ScrewRelease = 2,
        CleaningCloth = 3,
        CleaningSponge = 4,
        CleaningSqueegee = 5,
        LevelComplete = 6,
        RoomReveal = 7,
        UiTap = 8
    }

    public interface IAsmrAudioService
    {
        bool IsAvailable { get; }

        void PlaySnap(Vector3 worldPosition);
    }

    public interface ICategorizedAsmrAudioService : IAsmrAudioService
    {
        void PlayCue(AsmrAudioCue cue, Vector3 worldPosition);
    }

    public static class AsmrAudioServiceExtensions
    {
        public static void PlayCue(
            this IAsmrAudioService service,
            AsmrAudioCue cue,
            Vector3 worldPosition)
        {
            if (service is ICategorizedAsmrAudioService categorized)
            {
                categorized.PlayCue(cue, worldPosition);
                return;
            }

            if (cue == AsmrAudioCue.Placement ||
                cue == AsmrAudioCue.ScrewRelease)
            {
                service?.PlaySnap(worldPosition);
            }
        }

        public static void PlayCue(
            this IAsmrAudioService service,
            AsmrAudioCue cue)
        {
            service.PlayCue(cue, Vector3.zero);
        }
    }
}
