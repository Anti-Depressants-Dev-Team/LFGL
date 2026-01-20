using Windows.Storage.Pickers;
using WinRT.Interop;

namespace LFGL.Dialogs;

public sealed partial class AddGameDialog : ContentDialog
{
    public string GameName { get; private set; } = "";
    public string ExecutablePath { get; private set; } = "";
    public string Category { get; private set; } = "Manual";
    
    // The caller must set this before showing the dialog
    public IntPtr WindowHandle { get; set; }

    public AddGameDialog()
    {
        this.InitializeComponent();
    }

    private async void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".exe");
        picker.FileTypeFilter.Add(".lnk");
        picker.SuggestedStartLocation = PickerLocationId.Desktop;

        // Initialize with the provided window handle
        if (WindowHandle != IntPtr.Zero)
        {
            InitializeWithWindow.Initialize(picker, WindowHandle);
        }

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            ExePathBox.Text = file.Path;
            
            if (string.IsNullOrWhiteSpace(GameNameBox.Text))
            {
                GameNameBox.Text = System.IO.Path.GetFileNameWithoutExtension(file.Path);
            }
        }
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(GameNameBox.Text) || string.IsNullOrWhiteSpace(ExePathBox.Text))
        {
            args.Cancel = true;
            return;
        }

        GameName = GameNameBox.Text.Trim();
        ExecutablePath = ExePathBox.Text;
        Category = (CategoryBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Manual";
    }
}
