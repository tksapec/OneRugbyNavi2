using OneRugbyNavi2;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class DivisionSwipeGestureTests
{
    [Theory]
    [InlineData(200, 100, 100, 112, 40, DivisionSwipeDirection.Left)]
    [InlineData(100, 100, 200, 112, 40, DivisionSwipeDirection.Right)]
    public void Classifies_a_long_horizontal_gesture(
        float startX,
        float startY,
        float endX,
        float endY,
        float minimumDistance,
        DivisionSwipeDirection expected)
    {
        Assert.True(DivisionSwipeGesture.TryClassify(
            startX, startY, endX, endY, minimumDistance, 1.2f, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(100, 100, 130, 105, 40, 1.2f)]
    [InlineData(100, 100, 160, 190, 40, 1.2f)]
    [InlineData(100, 100, 160, 145, 40, 1.4f)]
    public void Does_not_classify_short_vertical_or_diagonal_movement_as_a_page_swipe(
        float startX,
        float startY,
        float endX,
        float endY,
        float minimumDistance,
        float horizontalDominanceRatio)
    {
        Assert.False(DivisionSwipeGesture.TryClassify(
            startX, startY, endX, endY, minimumDistance, horizontalDominanceRatio, out _));
    }

    [Fact]
    public void Rejects_non_finite_coordinates_and_invalid_thresholds()
    {
        Assert.False(DivisionSwipeGesture.TryClassify(float.NaN, 0, 100, 0, 40, 1.2f, out _));
        Assert.False(DivisionSwipeGesture.TryClassify(0, 0, 100, 0, 0, 1.2f, out _));
        Assert.False(DivisionSwipeGesture.TryClassify(0, 0, 100, 0, 40, 0, out _));
    }
}
