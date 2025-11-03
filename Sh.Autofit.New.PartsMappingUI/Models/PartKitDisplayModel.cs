using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;

namespace Sh.Autofit.New.PartsMappingUI.Models;

public partial class PartKitDisplayModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public int PartKitId { get; set; }
    public string KitName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }

    [ObservableProperty]
    private int _partCount;

    public ObservableCollection<PartKitItemDisplayModel> Parts { get; set; } = new();

    public string DisplayName => $"{KitName} ({PartCount} חלקים)";

    public string StatusIcon => IsActive ? "✅" : "🚫";

    public string CreatedAtDisplay => CreatedAt.ToString("dd/MM/yyyy HH:mm");
}
