namespace CalmSpace.Haptics
{
    public interface IHapticService
    {
        bool IsSupported { get; }

        void PlayDragTick(float intensity);

        void PlaySnap();

        void Cancel();
    }
}
