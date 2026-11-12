using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Kuafor.App;

[Activity(Name = "com.ondercode.kuafor.MainActivity", Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
    ScreenOrientation = ScreenOrientation.Portrait)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Ekran görüntüsü modu: adb shell am start ... --ez shots true
        if (Intent?.GetBooleanExtra("shots", false) == true)
            Shots.Requested = GetExternalFilesDir(null)?.AbsolutePath is { } dir ? Path.Combine(dir, "shots") : null;
        base.OnCreate(savedInstanceState);
    }
}
