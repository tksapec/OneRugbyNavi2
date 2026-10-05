namespace OneRugbyNavi2;

public enum DivisionSwipeDirection
{
    Left,
    Right
}

public static class DivisionSwipeNavigation
{
    public static int? GetAdjacentDivision(int currentDivision, DivisionSwipeDirection direction)
    {
        if (currentDivision is < 1 or > 3)
        {
            return null;
        }

        return direction switch
        {
            DivisionSwipeDirection.Left when currentDivision < 3 => currentDivision + 1,
            DivisionSwipeDirection.Right when currentDivision > 1 => currentDivision - 1,
            _ => null
        };
    }
}
