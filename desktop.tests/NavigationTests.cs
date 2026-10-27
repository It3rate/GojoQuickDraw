using GojoQuickDraw.Desktop;

public class NavigationTests
{
    [Theory]
    [InlineData(0, 11, 12, 26, 1, false, 12)]
    [InlineData(12, 0, 12, 26, -1, false, 11)]
    [InlineData(0, 0, 12, 26, -1, false, 0)]
    [InlineData(24, 1, 12, 26, 1, false, 25)]
    [InlineData(0, 4, 12, 26, 1, true, 16)]
    [InlineData(12, 10, 12, 26, 1, true, 25)]
    [InlineData(24, 1, 12, 26, -1, true, 13)]
    [InlineData(0, 4, 12, 26, -1, true, 4)]
    [InlineData(24, 1, 12, 26, 1, true, 25)]
    [InlineData(0, 0, 12, 0, 1, true, 0)]
    public void Navigation_respects_dataset_and_short_page_boundaries(int offset, int slot, int size, int total, int direction, bool page, int expected)
        => Assert.Equal(expected, ViewerNavigation.Target(offset, slot, size, total, direction, page));
}
