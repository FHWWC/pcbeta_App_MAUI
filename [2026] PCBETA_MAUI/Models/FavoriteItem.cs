namespace PCBetaMAUI.Models;

public class FavoriteItem
{
    public string FavId { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string AddedTime { get; set; } = string.Empty;
    public string HandleKey { get; set; } = string.Empty;
    public bool IsSelected { get; set; } = false;
}
