using Microsoft.Extensions.DependencyInjection;

namespace App_OCR
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            DongBoNen.Bat();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var cuaSo = new Window(new AppShell());
            cuaSo.Activated += (_, _) => DongBoNen.ThuDongBoNgay();
            return cuaSo;
        }
    }
}