using Avalonia.Controls;
using SaemDesk.ViewModels.Pages;

namespace SaemDesk.Views.Pages;

public partial class AddStudentsPage : UserControl
{
    public AddStudentsPage()
    {
        InitializeComponent();
        DataContext = new AddStudentsPageVM();
    }
}
