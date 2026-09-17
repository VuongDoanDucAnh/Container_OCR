namespace App_OCR;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var token = await SecureStorage.Default.GetAsync("jwt_token");

        if (string.IsNullOrEmpty(token))
        {
            await Navigation.PushModalAsync(new NavigationPage(new MainPage()));
        }
    }
}