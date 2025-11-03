using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sh.Autofit.New.PartsMappingUI.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace Sh.Autofit.New.PartsMappingUI.ViewModels;

public partial class PartKitsViewModel : ObservableObject
{
    private readonly IPartKitService _partKitService;
    private readonly IDataService _dataService;

    [ObservableProperty]
    private ObservableCollection<PartKitDisplayModel> _kits = new();

    [ObservableProperty]
    private PartKitDisplayModel? _selectedKit;

    [ObservableProperty]
    private ObservableCollection<PartKitItemDisplayModel> _kitParts = new();

    [ObservableProperty]
    private ObservableCollection<PartDisplayModel> _availableParts = new();

    [ObservableProperty]
    private string _kitSearchText = string.Empty;

    [ObservableProperty]
    private string _partSearchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public PartKitsViewModel(IPartKitService partKitService, IDataService dataService)
    {
        _partKitService = partKitService;
        _dataService = dataService;
    }

    public async Task InitializeAsync()
    {
        await LoadKitsAsync();
        await LoadAvailablePartsAsync();
    }

    [RelayCommand]
    private async Task LoadKitsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "טוען ערכות...";

            var kits = await _partKitService.LoadAllKitsAsync();
            Kits.Clear();
            foreach (var kit in kits)
            {
                Kits.Add(kit);
            }

            StatusMessage = $"נטענו {Kits.Count} ערכות";
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה בטעינת ערכות: {ex.Message}";
            MessageBox.Show($"שגיאה בטעינת ערכות: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadAvailablePartsAsync()
    {
        try
        {
            var parts = await _dataService.LoadPartsAsync();
            AvailableParts.Clear();
            foreach (var part in parts)
            {
                AvailableParts.Add(part);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"שגיאה בטעינת חלקים: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    partial void OnSelectedKitChanged(PartKitDisplayModel? value)
    {
        if (value != null)
        {
            _ = LoadKitPartsAsync(value.PartKitId);
        }
        else
        {
            KitParts.Clear();
        }
    }

    private async Task LoadKitPartsAsync(int kitId)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "טוען חלקים בערכה...";

            var parts = await _partKitService.GetKitPartsAsync(kitId);
            KitParts.Clear();
            foreach (var part in parts)
            {
                KitParts.Add(part);
            }

            StatusMessage = $"הערכה מכילה {KitParts.Count} חלקים";
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה בטעינת חלקים: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreateKitAsync()
    {
        var dialog = new Views.CreateKitDialog();
        if (dialog.ShowDialog() == true)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "יוצר ערכה...";

                var kitId = await _partKitService.CreateKitAsync(
                    dialog.KitName,
                    dialog.KitDescription,
                    "current_user" // TODO: Get from auth service
                );

                StatusMessage = "ערכה נוצרה בהצלחה";
                await LoadKitsAsync();

                // Select the newly created kit
                SelectedKit = Kits.FirstOrDefault(k => k.PartKitId == kitId);
            }
            catch (Exception ex)
            {
                StatusMessage = $"שגיאה ביצירת ערכה: {ex.Message}";
                MessageBox.Show($"שגיאה ביצירת ערכה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task EditKitAsync()
    {
        if (SelectedKit == null)
        {
            MessageBox.Show("אנא בחר ערכה לעריכה", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new Views.CreateKitDialog(SelectedKit.KitName, SelectedKit.Description);
        if (dialog.ShowDialog() == true)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "מעדכן ערכה...";

                await _partKitService.UpdateKitAsync(
                    SelectedKit.PartKitId,
                    dialog.KitName,
                    dialog.KitDescription,
                    "current_user" // TODO: Get from auth service
                );

                StatusMessage = "ערכה עודכנה בהצלחה";
                await LoadKitsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"שגיאה בעדכון ערכה: {ex.Message}";
                MessageBox.Show($"שגיאה בעדכון ערכה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task DuplicateKitAsync()
    {
        if (SelectedKit == null)
        {
            MessageBox.Show("אנא בחר ערכה לשכפול", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new Views.CreateKitDialog($"{SelectedKit.KitName} - עותק", SelectedKit.Description);
        if (dialog.ShowDialog() == true)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "משכפל ערכה...";

                var kitId = await _partKitService.DuplicateKitAsync(
                    SelectedKit.PartKitId,
                    dialog.KitName,
                    "current_user" // TODO: Get from auth service
                );

                StatusMessage = "ערכה שוכפלה בהצלחה";
                await LoadKitsAsync();

                // Select the newly created kit
                SelectedKit = Kits.FirstOrDefault(k => k.PartKitId == kitId);
            }
            catch (Exception ex)
            {
                StatusMessage = $"שגיאה בשכפול ערכה: {ex.Message}";
                MessageBox.Show($"שגיאה בשכפול ערכה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task DeleteKitAsync()
    {
        if (SelectedKit == null)
        {
            MessageBox.Show("אנא בחר ערכה למחיקה", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"האם אתה בטוח שברצונך למחוק את הערכה '{SelectedKit.KitName}'?",
            "אישור מחיקה",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "מוחק ערכה...";

                await _partKitService.DeleteKitAsync(SelectedKit.PartKitId);

                StatusMessage = "ערכה נמחקה בהצלחה";
                SelectedKit = null;
                await LoadKitsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"שגיאה במחיקת ערכה: {ex.Message}";
                MessageBox.Show($"שגיאה במחיקת ערכה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task AddPartsToKitAsync()
    {
        if (SelectedKit == null)
        {
            MessageBox.Show("אנא בחר ערכה", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new Views.AddPartsToKitDialog(AvailableParts.ToList());
        if (dialog.ShowDialog() == true && dialog.SelectedParts.Any())
        {
            try
            {
                IsLoading = true;
                StatusMessage = "מוסיף חלקים לערכה...";

                foreach (var part in dialog.SelectedParts)
                {
                    try
                    {
                        await _partKitService.AddPartToKitAsync(
                            SelectedKit.PartKitId,
                            part.PartNumber,
                            null,
                            null,
                            "current_user" // TODO: Get from auth service
                        );
                    }
                    catch (InvalidOperationException)
                    {
                        // Part already in kit, skip
                    }
                }

                StatusMessage = "חלקים נוספו בהצלחה";
                await LoadKitPartsAsync(SelectedKit.PartKitId);
                await LoadKitsAsync(); // Refresh part counts
            }
            catch (Exception ex)
            {
                StatusMessage = $"שגיאה בהוספת חלקים: {ex.Message}";
                MessageBox.Show($"שגיאה בהוספת חלקים: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task RemovePartFromKitAsync(PartKitItemDisplayModel? part)
    {
        if (part == null || SelectedKit == null)
            return;

        var result = MessageBox.Show(
            $"האם להסיר את '{part.DisplayName}' מהערכה?",
            "אישור הסרה",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "מסיר חלק מהערכה...";

                await _partKitService.RemovePartFromKitAsync(part.PartKitItemId);

                StatusMessage = "חלק הוסר בהצלחה";
                await LoadKitPartsAsync(SelectedKit.PartKitId);
                await LoadKitsAsync(); // Refresh part counts
            }
            catch (Exception ex)
            {
                StatusMessage = $"שגיאה בהסרת חלק: {ex.Message}";
                MessageBox.Show($"שגיאה בהסרת חלק: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
