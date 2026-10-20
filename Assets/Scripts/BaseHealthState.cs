namespace TapTapGameJam.Combat
{
    public sealed class BaseHealthState
    {
        public const int Maximum = 5;
        public int Current { get; private set; } = Maximum;
        public bool IsDefeated => Current == 0;

        public bool LoseLife()
        {
            if (IsDefeated) return false;
            Current--;
            return true;
        }

        public void Reset() => Current = Maximum;
    }
}
