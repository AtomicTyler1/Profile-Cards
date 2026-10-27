using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using Peak.Network;
using Peak.UI;
using Photon.Pun;
using Portningsbolaget;
using Portningsbolaget.Platforms;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using Family = Portningsbolaget.Platforms.PlatformUtility.PlatformFamily;

namespace ProfileViewer
{
    public class ButtonDefinition
    {
        public ButtonDefinition(
            string name,
            string icon,
            Func<ProfileController, bool> isVisible,
            Action<ProfileController> onClick,
            bool hideAfterClick = false)
        {
            Name = name;
            Icon = icon;
            IsVisible = isVisible;
            OnClick = onClick;
            HideAfterClick = hideAfterClick;
        }

        public string Name { get; }
        public string Icon { get; }
        public Func<ProfileController, bool> IsVisible { get; }
        public Action<ProfileController> OnClick { get; }
        public bool HideAfterClick { get; }
    }

    [BepInAutoPlugin]
    public partial class Plugin : BaseUnityPlugin
    {
        public static bool TransferButtonEnabled = false;

        internal static ManualLogSource Log { get; private set; } = null!;
        public static Harmony harmony = null!;

        public static readonly ButtonDefinition[] ButtonDefinitions =
        {
            new ButtonDefinition(
                "TransferButton",
                "ProfileViewer.Assets.Transfer-MasterClient.png",
                controller => TransferButtonEnabled && controller.IsHost && !controller.IsLocal && controller.SteamId.IsValid(),
                controller => controller.TransferHost()),

            new ButtonDefinition(
                "AddFriendButton",
                "ProfileViewer.Assets.Add-Friend.png",
                controller => !controller.IsLocal && controller.CanAddFriend && !controller.IsFriend,
                controller => controller.AddFriend(),
                hideAfterClick: true),

            new ButtonDefinition(
                "ProfileButton",
                "ProfileViewer.Assets.Open-Profile.png",
                controller => controller.CanOpenProfile,
                controller => controller.OpenProfile()),

            new ButtonDefinition(
                "MuteButton",
                "ProfileViewer.Assets.Mute.png",
                controller => controller.CanMute,
                controller => controller.Mute()),
        };

        private static readonly Dictionary<string, Sprite?> SpriteCache = new();

        private const string KickIconName = "KickIcon";
        private const string MuteButtonPath = "PlayerUI/Buttons/Mute";
        private const float ButtonGap = 5f;

        private void Awake()
        {
            Log = Logger;
            harmony = new Harmony(Id);
            harmony.PatchAll();
            Log.LogInfo($"Plugin {Name} is loaded!");
        }

        public static CSteamID GetSteamId(Photon.Realtime.Player? player)
        {
            if (player != null && ulong.TryParse(player.UserId, out ulong id))
            {
                return new CSteamID(id);
            }

            return CSteamID.Nil;
        }

        public static Sprite? LoadSprite(string resourceName)
        {
            if (SpriteCache.TryGetValue(resourceName, out Sprite? cached)) return cached;

            Sprite? sprite = null;
            using (Stream? stream = typeof(Plugin).Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    using var buffer = new MemoryStream();
                    stream.CopyTo(buffer);

                    var texture = new Texture2D(2, 2);
                    if (texture.LoadImage(buffer.ToArray()))
                    {
                        sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    }
                }
            }

            if (sprite == null) Log.LogWarning($"Could not load icon {resourceName}");
            SpriteCache[resourceName] = sprite;
            return sprite;
        }

        public static void CreateUI(AudioLevelSlider slider)
        {
            VerticalLayoutGroup? list = slider.GetComponentInParent<VerticalLayoutGroup>();
            if (list != null) list.spacing = 30f;

            Transform? local = slider.transform.Find("IsNotLocal");
            if (local == null) return;

            if (IsModInstalled("lammas123.PEAKER")) DisableViewportMask(slider);

            ProfileController controller = slider.GetComponent<ProfileController>()
                ?? slider.gameObject.AddComponent<ProfileController>();

            if (!controller.IsBuilt && !BuildUI(slider, local, controller)) return;

            controller.Refresh();
        }

        private static bool BuildUI(AudioLevelSlider slider, Transform local, ProfileController controller)
        {
            Transform? kick = local.Find("KickButton");
            Transform? nameLayout = slider.transform.Find("NameLayout");
            Transform? avatar = slider.transform.Find("PlayerUI/Avatar");

            if (kick is not RectTransform kickRect || nameLayout == null || avatar == null || kick.Find(KickIconName) == null)
            {
                Log.LogWarning("Profile UI skipped, the slider is missing children this mod expects.");
                return false;
            }

            Transform? mute = slider.transform.Find(MuteButtonPath);
            Button? muteButton = mute != null ? mute.GetComponentInChildren<Button>(true) : null;
            if (muteButton == null) Log.LogWarning($"'{MuteButtonPath}' not found, the mute button will not be shown.");

            kick.gameObject.SetActive(true);
            kick.localPosition += new Vector3(-60f, 15f, 0f);
            nameLayout.localPosition += new Vector3(0f, 40f, 0f);

            Image? platformIcon = CreatePlatformIcon(kick, local, slider.transform);

            RectTransform row = CreateButtonRow(slider.transform, kickRect);

            foreach (ButtonDefinition definition in ButtonDefinitions)
            {
                controller.AddButton(definition, kick, row);
            }

            kick.SetParent(row, false);
            kick.SetAsLastSibling();

            Transform frame = CreateFrame(slider.transform, nameLayout);
            avatar.SetParent(frame);

            controller.Setup(slider, kick, row, avatar, platformIcon, muteButton);
            return true;
        }

        private static RectTransform CreateButtonRow(Transform parent, RectTransform kick)
        {
            Vector2 cell = kick.rect.size;
            if (cell.x <= 0f || cell.y <= 0f) cell = new Vector2(50f, 50f);

            var row = new GameObject("ButtonRow", typeof(RectTransform), typeof(GridLayoutGroup));
            var rect = (RectTransform)row.transform;
            rect.SetParent(parent, false);

            int slots = ButtonDefinitions.Length + 1;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2((cell.x + ButtonGap) * slots, cell.y);

            rect.localPosition = new Vector3(110f, 5f, 0f);

            GridLayoutGroup grid = row.GetComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = new Vector2(ButtonGap, 0f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.MiddleRight;
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = 1;

            return rect;
        }

        public static GameObject CloneKickButton(Transform kick, Transform parent, string name)
        {
            GameObject clone = Instantiate(kick.gameObject, parent);
            clone.name = name;
            clone.SetActive(true);

            if (clone.TryGetComponent(out KickButton kickButton)) Destroy(kickButton);
            if (clone.TryGetComponent(out Animator animator)) Destroy(animator);

            return clone;
        }

        private static Image? CreatePlatformIcon(Transform kick, Transform local, Transform slider)
        {
            GameObject badge = CloneKickButton(kick, local, "PlatformIcon");
            badge.transform.SetParent(slider, true);
            badge.transform.localPosition = new Vector3(155f, 15f, 0f);
            badge.transform.localScale = new Vector3(1.25f, 1.25f, 1.25f);

            Transform? icon = badge.transform.Find(KickIconName);
            if (icon == null || !icon.TryGetComponent(out Image image))
            {
                Destroy(badge);
                return null;
            }

            icon.gameObject.SetActive(true);
            return image;
        }

        private static Transform CreateFrame(Transform parent, Transform nameLayout)
        {
            TextMeshProUGUI? nameText = nameLayout.GetComponentInChildren<TextMeshProUGUI>();

            var frame = new GameObject("ProfileFrame", typeof(RectTransform));
            frame.transform.SetParent(parent, false);
            frame.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

            frame.AddComponent<Image>();
            frame.AddComponent<Mask>();

            Outline outline = frame.AddComponent<Outline>();
            outline.effectColor = nameText != null ? nameText.color : Color.white;
            outline.effectDistance = new Vector2(4f, 3f);

            var rect = (RectTransform)frame.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(100f, 100f);
            rect.localPosition = new Vector3(-273f, 13f, 0f);
            return rect;
        }

        private static void DisableViewportMask(AudioLevelSlider slider)
        {
            Transform? viewport = slider.transform.parent?.parent;
            if (viewport == null) return;

            if (viewport.TryGetComponent(out Image image)) image.enabled = false;
            if (viewport.TryGetComponent(out Mask mask)) mask.enabled = false;
        }

        private static bool IsModInstalled(string guid) => Chainloader.PluginInfos.ContainsKey(guid);

        internal static bool TryRequestHostTransfer(int targetActorNumber, CSteamID steamId)
        {
            var room = PhotonNetwork.CurrentRoom;
            var matchmaking = NetCode.Matchmaking;

            if (room == null || PhotonNetwork.OfflineMode || !PhotonNetwork.IsMasterClient || !matchmaking.InLobby)
            {
                return false;
            }

            if (!room.Players.TryGetValue(targetActorNumber, out Photon.Realtime.Player target)) return false;
            if (target.IsLocal || target.IsInactive || !matchmaking.PlayerIsInLobby(target.UserId)) return false;
            if (!ulong.TryParse(matchmaking.LobbyId, out ulong lobbyIdValue)) return false;

            var lobbyId = new CSteamID(lobbyIdValue);
            if (Steamworks.SteamMatchmaking.GetLobbyOwner(lobbyId) != SteamUser.GetSteamID()) return false;

            Steamworks.SteamMatchmaking.SetLobbyOwner(lobbyId, steamId);

            bool queued = PhotonNetwork.SetMasterClient(target);
            Log.LogInfo($"Photon host transfer queued={queued}; target={target.NickName} ({target.ActorNumber})");
            return queued;
        }

        internal static void SyncLobbyOwner()
        {
            var newMaster = PhotonNetwork.MasterClient;
            var matchmaking = NetCode.Matchmaking;

            if (newMaster == null || !matchmaking.InLobby || !matchmaking.PlayerIsInLobby(newMaster.UserId)) return;

            if (!ulong.TryParse(matchmaking.LobbyId, out ulong lobbyIdValue)
                || !ulong.TryParse(newMaster.UserId, out ulong newOwnerIdValue))
            {
                return;
            }

            var lobbyId = new CSteamID(lobbyIdValue);
            var newOwnerId = new CSteamID(newOwnerIdValue);
            CSteamID currentOwnerId = Steamworks.SteamMatchmaking.GetLobbyOwner(lobbyId);

            if (currentOwnerId == newOwnerId || currentOwnerId != SteamUser.GetSteamID()) return;

            if (!Steamworks.SteamMatchmaking.SetLobbyOwner(lobbyId, newOwnerId))
            {
                Log.LogError("Photon host changed, but Steam lobby ownership transfer failed.");
                return;
            }

            Log.LogInfo($"Steam lobby ownership transferred to {newMaster.NickName} ({newOwnerIdValue}).");
        }
    }

    public class ProfileController : MonoBehaviour, IPunOwnershipCallbacks
    {
        private const string XboxProfileUrl = "https://www.xbox.com/play/user/";
        public const string XboxAvatarUrl = "https://unavatar.io/xboxgamertag/";

        private static readonly Vector3 IconScale = new(0.7f, 0.7f, 0.7f);
        private static readonly Vector3 IconHoverScale = new(0.8f, 0.8f, 0.8f);
        private static readonly Vector3 AvatarScale = new(1.65f, -1.65f, 0f);
        private static readonly Vector3 PlatformIconAvatarScale = new(1.65f, 1.65f, 0f);

        private class Entry
        {
            public Entry(ButtonDefinition definition, GameObject gameObject)
            {
                Definition = definition;
                GameObject = gameObject;
            }

            public ButtonDefinition Definition { get; }
            public GameObject GameObject { get; }
        }

        private readonly List<Entry> entries = new();
        private static readonly Dictionary<string, Sprite> XboxAvatarCache = new();
        private static readonly HashSet<string> PendingXboxFetches = new();
        private static readonly HashSet<string> FailedXboxFetches = new();
        private static readonly Queue<string> XboxFetchQueue = new();
        private static bool isProcessingQueue = false;

        private AudioLevelSlider slider = null!;
        private Transform kick = null!;
        private RectTransform row = null!;
        private Transform avatar = null!;
        private Image? platformIcon;
        private Button? muteSource;
        private Image? muteButtonImage;
        private Image? avatarImage;
        private bool isMutedLocal;
        private bool built;

        public bool IsBuilt => built;
        public string PlayerName { get; private set; } = string.Empty;
        public Family Platform { get; private set; } = Family.Windows;
        public CSteamID SteamId { get; private set; } = CSteamID.Nil;
        public bool IsLocal { get; private set; }
        public bool IsHost { get; private set; }

        public bool IsMuted
        {
            get
            {
                if (muteSource != null && muteSource.TryGetComponent(out Toggle toggle))
                {
                    return toggle.isOn;
                }
                return isMutedLocal;
            }
        }

        public bool CanOpenProfile => Platform switch
        {
            Family.Xbox => !string.IsNullOrWhiteSpace(PlayerName),
            Family.Switch or Family.Playstation => false,
            _ => SteamId.IsValid()
        };

        public bool CanAddFriend => CanOpenProfile;

        public bool IsFriend
        {
            get
            {
                if (Platform != Family.Windows || !SteamId.IsValid()) return false;

                try
                {
                    return SteamFriends.GetFriendRelationship(SteamId) == EFriendRelationship.k_EFriendRelationshipFriend;
                }
                catch (Exception e) when (e is InvalidOperationException or DllNotFoundException)
                {
                    return false;
                }
            }
        }

        public bool CanMute => !IsLocal && muteSource != null;

        public void Setup(AudioLevelSlider owner, Transform kickButton, RectTransform buttonRow, Transform avatarObject, Image? icon, Button? mute)
        {
            slider = owner;
            kick = kickButton;
            row = buttonRow;
            avatar = avatarObject;
            platformIcon = icon;
            muteSource = mute;
            avatarImage = avatar.GetComponent<Image>() ?? avatar.GetComponentInChildren<Image>();
            built = true;
        }

        public void AddButton(ButtonDefinition definition, Transform template, Transform parent)
        {
            GameObject buttonObject = Plugin.CloneKickButton(template, parent, definition.Name);
            Transform? icon = buttonObject.transform.Find("KickIcon");

            if (icon == null || !buttonObject.TryGetComponent(out Button button))
            {
                Destroy(buttonObject);
                return;
            }

            Sprite? sprite = Plugin.LoadSprite(definition.Icon);
            if (sprite != null && icon.TryGetComponent(out Image image))
            {
                image.sprite = sprite;
                if (definition.Name == "MuteButton")
                {
                    muteButtonImage = image;
                }
            }

            icon.gameObject.SetActive(true);
            icon.localScale = IconScale;

            var entry = new Entry(definition, buttonObject);

            button.enabled = true;
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => HandleClick(entry));

            AddHoverEffects(buttonObject, icon);
            entries.Add(entry);
        }

        public void Refresh()
        {
            if (!built || slider == null || !ReadPlayer()) return;

            kick.gameObject.SetActive(!IsLocal && IsHost);

            if (muteSource != null)
            {
                muteSource.gameObject.SetActive(false);
                if (muteSource.TryGetComponent(out Toggle toggle))
                {
                    isMutedLocal = toggle.isOn;
                }
            }

            if (muteButtonImage != null)
            {
                string assetName = IsMuted ? "ProfileViewer.Assets.Unmute.png" : "ProfileViewer.Assets.Mute.png";
                Sprite? sprite = Plugin.LoadSprite(assetName);
                if (sprite == null && IsMuted)
                {
                    sprite = Plugin.LoadSprite("ProfileViewer.Assets.Mute.png");
                }
                if (sprite != null)
                {
                    muteButtonImage.sprite = sprite;
                }
            }

            foreach (Entry entry in entries)
            {
                entry.GameObject.SetActive(entry.Definition.IsVisible(this));
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
            ShowPlatformIcon();

            if (avatarImage != null)
            {
                if (Platform == Family.Xbox && !string.IsNullOrWhiteSpace(PlayerName))
                {
                    FetchXboxAvatar(PlayerName);
                }
                else
                {
                    var localFamily = PlatformManager.Platform.PlatformFamily;
                    if (Platform != localFamily)
                    {
                        string resource = Platform switch
                        {
                            Family.Switch => "ProfileViewer.Assets.Nintendo.png",
                            Family.Playstation => "ProfileViewer.Assets.Playstation.png",
                            _ => "ProfileViewer.Assets.Steam.png",
                        };

                        Sprite? sprite = Plugin.LoadSprite(resource);
                        if (sprite != null)
                        {
                            avatarImage.sprite = sprite;
                        }

                        avatar.localScale = PlatformIconAvatarScale;
                    }
                    else
                    {
                        avatar.localScale = AvatarScale;
                    }
                }
            }

            avatar.parent.GetComponent<Outline>().effectColor = slider.playerName.color;
            avatar.localPosition = Vector3.zero;
        }

        private void FetchXboxAvatar(string gamertag)
        {
            string cleanTag = gamertag.Replace(" ", "");

            if (XboxAvatarCache.TryGetValue(cleanTag, out Sprite? cachedSprite))
            {
                avatarImage!.sprite = cachedSprite;
                avatar.localScale = PlatformIconAvatarScale;
                return;
            }

            Sprite? fallback = Plugin.LoadSprite("ProfileViewer.Assets.Xbox.png");
            if (fallback != null) avatarImage!.sprite = fallback;
            avatar.localScale = PlatformIconAvatarScale;

            if (FailedXboxFetches.Contains(cleanTag) || PendingXboxFetches.Contains(cleanTag) || XboxFetchQueue.Contains(cleanTag))
            {
                return;
            }

            PendingXboxFetches.Add(cleanTag);
            XboxFetchQueue.Enqueue(cleanTag);

            if (!isProcessingQueue)
            {
                StartCoroutine(ProcessXboxQueueRoutine());
            }
        }

        private IEnumerator ProcessXboxQueueRoutine()
        {
            isProcessingQueue = true;

            while (XboxFetchQueue.Count > 0)
            {
                string cleanTag = XboxFetchQueue.Dequeue();
                yield return StartCoroutine(DownloadXboxAvatarRoutine(cleanTag));

                yield return new WaitForSeconds(1f);
            }

            isProcessingQueue = false;
        }

        [Serializable]
        private class UnavatarResponse
        {
            public string url = string.Empty;
        }

        private IEnumerator DownloadXboxAvatarRoutine(string cleanTag)
        {
            string jsonUrl = XboxAvatarUrl + Uri.EscapeDataString(cleanTag) + "?json";
            using UnityWebRequest jsonRequest = UnityWebRequest.Get(jsonUrl);
            jsonRequest.timeout = 10;

            try
            {
                yield return jsonRequest.SendWebRequest();

                if (jsonRequest.responseCode == 429 || jsonRequest.result != UnityWebRequest.Result.Success)
                {
                    FailedXboxFetches.Add(cleanTag);
                    yield break;
                }

                string jsonText = jsonRequest.downloadHandler.text;
                UnavatarResponse? response = null;

                try
                {
                    response = JsonUtility.FromJson<UnavatarResponse>(jsonText);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Failed to parse Xbox avatar JSON for '{cleanTag}': {e.Message}");
                }

                if (response == null || string.IsNullOrEmpty(response.url))
                {
                    FailedXboxFetches.Add(cleanTag);
                    yield break;
                }

                string imageUrl = response.url;
                int formatIndex = imageUrl.IndexOf("&format=");
                if (formatIndex > 0)
                {
                    imageUrl = imageUrl.Substring(0, formatIndex);
                }

                using UnityWebRequest imageRequest = UnityWebRequest.Get(imageUrl);
                imageRequest.timeout = 10;
                yield return imageRequest.SendWebRequest();

                if (imageRequest.responseCode == 429 || imageRequest.result != UnityWebRequest.Result.Success)
                {
                    FailedXboxFetches.Add(cleanTag);
                    yield break;
                }

                byte[] bytes = imageRequest.downloadHandler.data;
                if (bytes == null || bytes.Length == 0)
                {
                    FailedXboxFetches.Add(cleanTag);
                    yield break;
                }

                Texture2D texture = new Texture2D(2, 2);
                if (!texture.LoadImage(bytes))
                {
                    FailedXboxFetches.Add(cleanTag);
                    yield break;
                }

                Sprite newSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                XboxAvatarCache[cleanTag] = newSprite;

                if (built && ReadPlayer() && PlayerName.Replace(" ", "") == cleanTag && avatarImage != null)
                {
                    avatarImage.sprite = newSprite;
                    avatar.localScale = PlatformIconAvatarScale;
                }
            }
            finally
            {
                PendingXboxFetches.Remove(cleanTag);
            }
        }

        public void OpenProfile()
        {
            switch (Platform)
            {
                case Family.Xbox:
                    OpenWebPage(XboxProfileUrl + Uri.EscapeDataString(PlayerName));
                    break;
                case Family.Playstation:
                    break;
                case Family.Switch:
                    break;
                default:
                    SteamFriends.ActivateGameOverlayToUser("steamid", SteamId);
                    break;
            }
        }

        public void AddFriend()
        {
            switch (Platform)
            {
                case Family.Xbox:
                case Family.Playstation:
                case Family.Switch:
                    OpenProfile();
                    break;
                default:
                    SteamFriends.ActivateGameOverlayToUser("friendadd", SteamId);
                    break;
            }
        }

        public void TransferHost()
        {
            Plugin.TryRequestHostTransfer(slider.player.ActorNumber, SteamId);

            Transform? list = slider.transform.parent;
            if (list != null && list.TryGetComponent(out AudioLevels levels)) levels.UpdateSliders();
        }

        public void Mute()
        {
            if (muteSource != null)
            {
                var voiceSlider = slider.m_PlayerUI.voiceSlider;
                voiceSlider.ToggleMute();
                isMutedLocal = voiceSlider.m_muteToggled;
                Refresh();
            }
        }

        private bool ReadPlayer()
        {
            Photon.Realtime.Player? player = slider.player;
            if (player == null) return false;

            PlayerName = player.NickName ?? string.Empty;
            SteamId = Plugin.GetSteamId(player);
            IsLocal = player.IsLocal;
            IsHost = PhotonNetwork.LocalPlayer != null && PhotonNetwork.LocalPlayer.IsMasterClient;

            Platform = Family.Windows;
            if (player.CustomProperties != null)
            {
                if (player.CustomProperties.TryGetValue("PlatformFamily", out object raw))
                {
                    if (raw is string text && Enum.TryParse<Family>(text, true, out Family parsed))
                    {
                        Platform = parsed;
                    }
                    else if (raw is int intVal)
                    {
                        Platform = (Family)intVal;
                    }
                    else if (raw is byte byteVal)
                    {
                        Platform = (Family)byteVal;
                    }
                }
            }

            return true;
        }

        private void ShowPlatformIcon()
        {
            if (platformIcon == null) return;

            string resource = Platform switch
            {
                Family.Xbox => "ProfileViewer.Assets.Xbox.png",
                Family.Switch => "ProfileViewer.Assets.Nintendo.png",
                Family.Playstation => "ProfileViewer.Assets.Playstation.png",
                _ => "ProfileViewer.Assets.Steam.png",
            };

            Sprite? sprite = Plugin.LoadSprite(resource);
            if (sprite != null) platformIcon.sprite = sprite;
        }

        private void HandleClick(Entry entry)
        {
            if (!built || slider == null || !ReadPlayer()) return;

            PlaySound(entry.GameObject, "SFX Click");

            try
            {
                entry.Definition.OnClick(this);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"{entry.Definition.Name} failed: {e.Message}");
                return;
            }

            if (!entry.Definition.HideAfterClick) return;

            entry.GameObject.SetActive(false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
        }

        private static void OpenWebPage(string url)
        {
            try
            {
                SteamFriends.ActivateGameOverlayToWebPage(url);
            }
            catch (Exception e) when (e is InvalidOperationException or DllNotFoundException)
            {
                Application.OpenURL(url);
            }
        }

        private static void AddHoverEffects(GameObject buttonObject, Transform icon)
        {
            EventTrigger trigger = buttonObject.GetComponent<EventTrigger>() ?? buttonObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ =>
            {
                PlaySound(buttonObject, "SFX Hover");
                icon.localScale = IconHoverScale;
            });
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => icon.localScale = IconScale);
            trigger.triggers.Add(exit);
        }

        private static void PlaySound(GameObject parent, string childName)
        {
            Transform? sound = parent.transform.Find(childName);
            if (sound == null) return;

            sound.gameObject.SetActive(false);
            sound.gameObject.SetActive(true);
        }

        public void OnOwnershipRequest(PhotonView targetView, Photon.Realtime.Player requestingPlayer)
        {
        }

        public void OnOwnershipTransfered(PhotonView targetView, Photon.Realtime.Player previousOwner)
        {
            Plugin.SyncLobbyOwner();
        }

        public void OnOwnershipTransferFailed(PhotonView targetView, Photon.Realtime.Player senderOfFailedRequest)
        {
        }
    }

    [HarmonyPatch]
    public class InitPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(AudioLevelSlider), nameof(AudioLevelSlider.Init))]
        public static void AudioLevelSliderInit(AudioLevelSlider __instance)
        {
            if (__instance.player == null) return;

            try
            {
                Plugin.CreateUI(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Profile UI setup failed: {e}");
            }
        }
    }

    [HarmonyPatch]
    public class SteamAvatarFixPatch
    {
        private static Callback<AvatarImageLoaded_t>? avatarLoadedCallback;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SteamRuntimeManager), nameof(SteamRuntimeManager.InitializeAfterSceneLoad))]
        public static void InitializeAvatarListener()
        {
            if (avatarLoadedCallback == null && SteamManager.Initialized)
            {
                avatarLoadedCallback = Callback<AvatarImageLoaded_t>.Create(OnAvatarImageLoaded);
                Plugin.Log.LogInfo("Initialized Steam Avatar callback listener.");
            }
        }

        private static void OnAvatarImageLoaded(AvatarImageLoaded_t param)
        {
            try
            {
                foreach (PlayerUI playerUi in UnityEngine.Object.FindObjectsOfType<PlayerUI>())
                {
                    if (playerUi.m_player != null && ulong.TryParse(playerUi.m_player.UserId, out ulong id))
                    {
                        if (new CSteamID(id) == param.m_steamID)
                        {
                            var traverse = Traverse.Create(playerUi);
                            traverse.Field("loadedSprite").SetValue(false);
                            traverse.Field("loadingSprite").SetValue(false);

                            playerUi.UpdateCell();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Failed to handle AvatarImageLoaded callback: {e.Message}");
            }
        }
    }

    [HarmonyPatch]
    public class SteamAvatarRequestPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SteamRuntimeManager), nameof(SteamRuntimeManager.GetAvatarAsync), typeof(ulong), typeof(Action<Texture2D>))]
        public static bool GetAvatar(ulong accountId, Action<Texture2D> callback)
        {
            var steamId = new CSteamID(accountId);
            int handle = SteamFriends.GetMediumFriendAvatar(steamId);

            if (handle == 0 && steamId.IsValid())
            {
                bool requested = SteamFriends.RequestUserInformation(steamId, true);
                if (requested)
                {
                    Plugin.Log.LogInfo($"Requested missing Steam avatar for account {accountId}.");
                }
            }

            return true;
        }
    }
}