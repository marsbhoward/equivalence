using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Convergence.Chain
{
    /// <summary>
    /// BOOTH SHOTS - pictures a character took in the portrait booth (UI/BoothScreen), kept to hang in
    /// the hub's frames (the user's calls, 2026-11-13). The moment as taken, as a PNG at the art's own
    /// pixel size; kept with the character on the service (web/service/src/routes/shots.ts), or in the
    /// save folder on a local store; <see cref="Core.Tuning.Booth.ShotsKept"/> a character.
    ///
    /// Every character's shots are listed, not only the one being played: the room belongs to the
    /// player (AccountProfile.Room), so a frame keeps showing a shot whoever is playing. The frames see
    /// them through Hub.Showcase, as their own group ahead of the wallet's (Hub.BoothShowcase).
    /// </summary>
    public static class BoothShots
    {
        public class Shot
        {
            public string ProfileId, ShotId, Name;
            public byte[] Png;
            public DateTime Taken;
            Sprite _art;

            /// <summary>Its key in a room layout (RoomLayout.TrophyPlacement.Key).</summary>
            public string Key => KeyOf(ProfileId, ShotId);

            /// <summary>The picture, made on first ask: point-filtered, one world unit tall, as every
            /// showcase piece is (Hub.DevShowcaseSource).</summary>
            public Sprite Art
            {
                get
                {
                    if (_art != null || Png == null) return _art;
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                        { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                    if (!tex.LoadImage(Png)) { UnityEngine.Object.Destroy(tex); return null; }
                    tex.filterMode = FilterMode.Point;
                    _art = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.height);
                    return _art;
                }
            }
        }

        public const string KeyPrefix = "booth:";
        public static string KeyOf(string profileId, string shotId) => $"{KeyPrefix}{profileId}:{shotId}";

        // Not readonly - see the domain reload rules.
        static List<Shot> _all = new();
        public static IReadOnlyList<Shot> All => _all ??= new();

        /// <summary>A shot was kept, replaced or the list arrived - the frames repaint (Hub.Showcase).</summary>
        public static event Action Changed;

        public static int CountFor(string profileId)
        {
            int n = 0;
            foreach (var s in All) if (s.ProfileId == profileId) n++;
            return n;
        }

        public static List<Shot> Of(string profileId)
        {
            var list = new List<Shot>();
            foreach (var s in All) if (s.ProfileId == profileId) list.Add(s);
            return list;
        }

        public static bool TryGet(string key, out Shot shot)
        {
            foreach (var s in All) if (s.Key == key) { shot = s; return true; }
            shot = null;
            return false;
        }

        // ------------------------------------------------------------------ load

        /// <summary>Reads every character's shots. A failed read keeps what is held.</summary>
        public static async Task LoadAsync()
        {
            List<Shot> got = StoreFactory.UsingService ? await LoadServiceAsync() : LoadLocal();
            if (got == null) return;
            _all = got;
            Changed?.Invoke();
        }

        [Serializable] class Row { public string profileId, shotId, name, png, takenAt; }
        [Serializable] class Rows { public Row[] items; }

        static async Task<List<Shot>> LoadServiceAsync()
        {
            if (!await Service.ServiceIdentity.EnsureAsync()) return null;
            var r = await Service.ServiceClient.Get("/saves/shots", Service.ServiceIdentity.Token);
            if (!r.Ok) { Debug.LogWarning($"[Booth] shots unreadable: {r}"); return null; }
            try
            {
                var rows = JsonUtility.FromJson<Rows>(r.Body);
                var list = new List<Shot>();
                if (rows?.items != null)
                    foreach (var row in rows.items)
                        list.Add(new Shot
                        {
                            ProfileId = row.profileId, ShotId = row.shotId, Name = row.name ?? "",
                            Png = Convert.FromBase64String(row.png ?? ""),
                            Taken = DateTime.TryParse(row.takenAt, out var t) ? t : DateTime.UtcNow,
                        });
                return list;
            }
            catch (Exception e) { Debug.LogWarning($"[Booth] shots unparseable: {e.Message}"); return null; }
        }

        static string LocalDir => Path.Combine(Application.persistentDataPath, "BoothShots");

        /// <summary>The save folder's BoothShots/&lt;profile&gt;/&lt;shot&gt;.png, a name.txt beside them.</summary>
        static List<Shot> LoadLocal()
        {
            var list = new List<Shot>();
            if (!Directory.Exists(LocalDir)) return list;
            foreach (var dir in Directory.GetDirectories(LocalDir))
            {
                string profileId = Path.GetFileName(dir);
                string nameFile = Path.Combine(dir, "name.txt");
                string name = File.Exists(nameFile) ? File.ReadAllText(nameFile).Trim() : "";
                foreach (var file in Directory.GetFiles(dir, "*.png"))
                    list.Add(new Shot
                    {
                        ProfileId = profileId, ShotId = Path.GetFileNameWithoutExtension(file), Name = name,
                        Png = File.ReadAllBytes(file), Taken = File.GetLastWriteTimeUtc(file),
                    });
            }
            list.Sort((a, b) => a.Taken.CompareTo(b.Taken));
            return list;
        }

        // ------------------------------------------------------------------ keep

        /// <summary>
        /// Keeps <paramref name="png"/> as one of <paramref name="profile"/>'s shots, in the place of
        /// <paramref name="replaces"/> when given. Returns what to tell the player.
        /// </summary>
        public static async Task<string> KeepAsync(CharacterProfile profile, byte[] png, string replaces = null)
        {
            if (png == null || png.Length == 0) return "the picture could not be made";
            if (png.Length > Core.Tuning.Booth.ShotMaxBytes) return "this picture is too big to keep - frame it closer";
            string id = Guid.NewGuid().ToString("N");

            if (StoreFactory.UsingService)
            {
                if (!await Service.ServiceIdentity.EnsureAsync()) return "could not reach the service - not kept";
                string body = "{\"png\":" + Service.ServiceClient.Quote(Convert.ToBase64String(png))
                            + (replaces != null ? ",\"replaces\":" + Service.ServiceClient.Quote(replaces) : "") + "}";
                var r = await Service.ServiceClient.Put($"/saves/shots/{Uri.EscapeDataString(profile.ProfileId)}/{id}",
                                                        body, Service.ServiceIdentity.Token);
                if (!r.Ok)
                {
                    Debug.LogWarning($"[Booth] keep failed: {r}");
                    return r.Status == 409 ? "you keep twelve - choose one to replace" : "could not keep the picture - try again";
                }
            }
            else
            {
                var dir = Path.Combine(LocalDir, profile.ProfileId);
                Directory.CreateDirectory(dir);
                if (replaces != null) File.Delete(Path.Combine(dir, replaces + ".png"));
                File.WriteAllBytes(Path.Combine(dir, id + ".png"), png);
                File.WriteAllText(Path.Combine(dir, "name.txt"), profile.DisplayName);
            }

            _all ??= new();
            if (replaces != null) _all.RemoveAll(s => s.ProfileId == profile.ProfileId && s.ShotId == replaces);
            _all.Add(new Shot { ProfileId = profile.ProfileId, ShotId = id, Name = profile.DisplayName, Png = png, Taken = DateTime.UtcNow });
            Changed?.Invoke();
            return "kept - choose it for any frame in the hub";
        }
    }
}
