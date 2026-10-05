namespace OneRugbyNavi2;

public static class DivisionSwipeGesture
{
    public static bool TryClassify(
        float startX,
        float startY,
        float endX,
        float endY,
        float minimumDistance,
        float horizontalDominanceRatio,
        out DivisionSwipeDirection direction)
    {
        direction = default;
        if (!float.IsFinite(startX) || !float.IsFinite(startY) ||
            !float.IsFinite(endX) || !float.IsFinite(endY) ||
            !float.IsFinite(minimumDistance) || minimumDistance <= 0 ||
            !float.IsFinite(horizontalDominanceRatio) || horizontalDominanceRatio < 1)
        {
            return false;
        }

        var deltaX = endX - startX;
        var deltaY = endY - startY;
        var horizontalDistance = Math.Abs(deltaX);
        if (horizontalDistance < minimumDistance ||
            horizontalDistance <= Math.Abs(deltaY) * horizontalDominanceRatio)
        {
            return false;
        }

        direction = deltaX < 0 ? DivisionSwipeDirection.Left : DivisionSwipeDirection.Right;
        return true;
    }
}
