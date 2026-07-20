using System;
using System.Threading.Tasks;
using Supabase.Gotrue;

namespace ClientApp.Services
{
    public static class AuthService
    {
        public static async Task<bool> SignUpAsync(string email, string password)
        {
            if (!SupabaseClientManager.IsConfigured) return false;
            
            try
            {
                var session = await SupabaseClientManager.Instance!.Auth.SignUp(email, password);
                return session != null;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> SignInAsync(string email, string password)
        {
            if (!SupabaseClientManager.IsConfigured) return false;

            try
            {
                var session = await SupabaseClientManager.Instance!.Auth.SignIn(email, password);
                if (session != null)
                {
                    SettingsManager.Default.CloudAuthToken = session.AccessToken ?? string.Empty;
                    SettingsManager.Save();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static async Task SignOutAsync()
        {
            if (!SupabaseClientManager.IsConfigured) return;

            try
            {
                await SupabaseClientManager.Instance!.Auth.SignOut();
                SettingsManager.Default.CloudAuthToken = string.Empty;
                SettingsManager.Save();
            }
            catch { }
        }

        public static bool IsLoggedIn => SupabaseClientManager.Instance?.Auth.CurrentUser != null;
    }
}
