namespace DeckBattle
{
    [System.Flags]
    public enum StatusPresentationMode
    {
        None = 0,
        Icon = 1,
        Vfx = 2,
        IconAndVfx = Icon | Vfx
    }
}
