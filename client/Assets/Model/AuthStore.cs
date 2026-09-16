using System;
using System.IO;
using UnityEngine;

namespace Broodline.Model
{
    /// The access/refresh token pair. Plain data - JsonUtility needs fields,
    /// not properties, to serialize this.
    [Serializable]
    public sealed class Tokens
    {
        public string AccessToken;
        public string RefreshToken;
    }

    public interface IAuthStore
    {
        Tokens Load();
        void Save(Tokens tokens);
    }

    /// Tokens on disk under `Application.persistentDataPath`, not the iOS
    /// Keychain.
    ///
    /// `broodline_client_architecture.md` section 7 specifies the Keychain for
    /// the refresh token. Unity has no Keychain API without a native plugin,
    /// and this build is for internal testers only, so the token goes to a
    /// file instead - with the no-backup flag set on iOS, plus the platform's
    /// default `NSFileProtectionComplete`. THIS IS NOT KEYCHAIN, and the gap
    /// is recorded as owed in the phase 7 design's section 13 (Task 22)
    /// rather than silently substituted.
    ///
    /// Save/Load sit behind `IAuthStore` precisely so a Keychain-backed
    /// implementation can replace this one later without any caller (Session,
    /// BootController) changing.
    public sealed class AuthStore : IAuthStore
    {
        const string FileName = "tokens.json";

        readonly string _path;

        public AuthStore() : this(Application.persistentDataPath) { }

        public AuthStore(string directory)
        {
            _path = Path.Combine(directory, FileName);
        }

        public Tokens Load()
        {
            try
            {
                if (!File.Exists(_path)) return null;
                return JsonUtility.FromJson<Tokens>(File.ReadAllText(_path));
            }
            catch (Exception)
            {
                // A corrupt or unreadable token file reads the same as no
                // session at all - Session.ColdStartAsync's
                // `_auth.Load() ?? CreateGuestAsync()` treats that as a first
                // launch rather than the whole shell throwing on boot.
                return null;
            }
        }

        public void Save(Tokens tokens)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(_path, JsonUtility.ToJson(tokens));

#if UNITY_IOS
            // The no-backup flag - the closest this build gets to the
            // Keychain without a native plugin. See the class comment.
            UnityEngine.iOS.Device.SetNoBackupFlag(_path);
#endif
        }
    }
}
