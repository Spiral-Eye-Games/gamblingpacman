//bandos de la apuesta, no cambian cuando se invierte quien persigue
public enum MatchSide
{
    Pacman,
    Ghosts
}

internal static class MatchSideNames
{
    public static string DisplayName(this MatchSide side)
    {
        return side == MatchSide.Pacman ? "Pacman" : "Fantasmas";
    }
}
