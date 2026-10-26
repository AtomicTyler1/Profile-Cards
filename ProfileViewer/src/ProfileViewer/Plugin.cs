using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MonoMod.Utils;
using Peak.Network;
using Peak.UI;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice;
using Portningsbolaget.Platforms;
using Steamworks;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Unity.XGamingRuntime.SDK;

namespace ProfileViewer
{
    [BepInAutoPlugin]
    public partial class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log { get; private set; } = null!;
        public static Harmony harmony = null!;

        public static Texture2D SteamIcon;
        public static Texture2D XboxIcon;
        public static Texture2D SwitchIcon;
        public static Texture2D PlaystationIcon;

        private void Awake()
        {
            Log = Logger;
            harmony = new Harmony(Id);
            harmony.PatchAll();
            Log.LogInfo($"Plugin {Name} is loaded!");

            SteamIcon = IconFromEmbeddedResource("ProfileViewer.Assets.Steam.png");
            XboxIcon = IconFromEmbeddedResource("ProfileViewer.Assets.Xbox.png");
            SwitchIcon = IconFromEmbeddedResource("ProfileViewer.Assets.Nintendo.png");
            PlaystationIcon = IconFromEmbeddedResource("ProfileViewer.Assets.Playstation.png");
        }

        public static CSteamID GetSteamId(Photon.Realtime.Player player)
        {
            if (player != null && !string.IsNullOrEmpty(player.UserId))
            {
                if (ulong.TryParse(player.UserId, out ulong id))
                {
                    return new CSteamID(id);
                }
            }
            return CSteamID.Nil;
        }

        public static Texture2D IconFromEmbeddedResource(string resourceName)
        {
            var assembly = typeof(Plugin).Assembly;
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) return null;
                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(data);
                return texture;
            }
        }

        public static void CreateUI(AudioLevelSlider __instance)
        {
            Transform local = __instance.transform.Find("IsNotLocal");
            __instance.GetComponentInParent<VerticalLayoutGroup>().spacing = 30f;
            if (local == null) return;
            local.gameObject.SetActive(true);

            if (isModInstalled("lammas123.PEAKER"))
            {
                var ViewPort = __instance.transform.parent.parent;
                ViewPort.GetComponent<Image>().enabled = false;
                ViewPort.GetComponent<Mask>().enabled = false;
            }

            Transform kickButtonTrans = local.Find("KickButton");
            Transform sliderTrans = local.Find("Slider");

            if (kickButtonTrans != null) kickButtonTrans.gameObject.SetActive(true);
            if (sliderTrans != null) sliderTrans.gameObject.SetActive(true);

            var controller = __instance.GetComponent<ProfileController>();
            if (controller == null)
            {
                controller = __instance.gameObject.AddComponent<ProfileController>();
            }

            if (__instance.transform.Find("IsNotLocal/ProfileButton") != null)
            {
                HandleVisibility(__instance, local, kickButtonTrans, sliderTrans);
                controller.Refresh();
                return;
            }

            Transform nameLayout = __instance.transform.Find("NameLayout");
            kickButtonTrans.localPosition += new Vector3(-60f, 15f, 0f);
            nameLayout.localPosition += new Vector3(0f, 40f, 0f);

            GameObject profileBtnObj = Instantiate(kickButtonTrans.gameObject, local);
            profileBtnObj.name = "ProfileButton";
            profileBtnObj.SetActive(true);

            GameObject addBtnObj = Instantiate(profileBtnObj, local);
            addBtnObj.name = "AddFriendButton";
            addBtnObj.SetActive(true);

            GameObject transferBtnObj = Instantiate(addBtnObj, local);
            transferBtnObj.name = "TransferButton";
            transferBtnObj.SetActive(true);

            GameObject platformIndicator = Instantiate(addBtnObj, local);
            platformIndicator.name = "PlatformIcon";
            platformIndicator.SetActive(true);
   

            HandleVisibility(__instance, local, kickButtonTrans, sliderTrans);

            CleanButton(profileBtnObj, "ProfileViewer.Assets.Open-Profile.png");
            CleanButton(addBtnObj, "ProfileViewer.Assets.Add-Friend.png");
            //CleanButton(transferBtnObj, "ProfileViewer.Assets.Transfer-MasterClient.png");

            GameObject frameObj = new GameObject("ProfileFrame", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(Outline));
            frameObj.transform.SetParent(__instance.transform, false);
            frameObj.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

            var outline = frameObj.GetComponent<Outline>();
            outline.effectColor = nameLayout.GetComponentInChildren<TextMeshProUGUI>().color;
            outline.effectDistance = new Vector2(4f, 3f);

            var frameRect = frameObj.GetComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0, 0.5f);
            frameRect.anchorMax = new Vector2(0, 0.5f);
            frameRect.sizeDelta = new Vector2(100f, 100f);
            frameRect.localPosition = new Vector3(-232f, 19f, 0f);

            controller.profileButton = profileBtnObj.GetComponent<Button>();
            controller.addButton = addBtnObj.GetComponent<Button>();
            controller.transferButton = transferBtnObj.GetComponent<Button>();
            controller.profilePicture = __instance.transform.Find("PlayerUI/Avatar").gameObject;
            controller.parentSlider = __instance;
            controller.platformIndicator = platformIndicator;
            controller.platformIndicator.transform.localPosition = new Vector3(141.0476f, -35.7085f, 41.3511f);
            controller.platformIndicator.transform.localScale = new Vector3(1.1f, 1.1f, 0);

            controller.profilePicture.transform.SetParent(frameObj.transform);
            controller.profilePicture.transform.localScale = new Vector3(1.65f, -1.65f, 0);
            controller.profilePicture.transform.localPosition = Vector3.zero;

            controller.Refresh();
        }

        private static void HandleVisibility(AudioLevelSlider instance, Transform local, Transform kickBtn, Transform slider)
        {
            if (instance == null || local == null) return;

            Transform profTrans = local.Find("ProfileButton");
            Transform addTrans = local.Find("AddFriendButton");
            Transform transferTrans = local.Find("TransferButton");

            Transform avatarTrans = local.parent != null ? local.parent.Find("ProfileFrame/Avatar") : null;

            if (profTrans == null || addTrans == null || transferTrans == null)
            {
                Plugin.Log.LogWarning("One or more UI buttons missing during HandleVisibility refresh.");
                return;
            }

            if (instance.player.IsLocal)
            {
                if (kickBtn != null)
                {
                    kickBtn.gameObject.SetActive(false);
                    profTrans.localPosition = kickBtn.localPosition;
                }

                if (slider != null)
                {
                    slider.gameObject.SetActive(false);
                }

                transferTrans.gameObject.SetActive(false);
            }
            else
            {
                bool isHost = PhotonNetwork.LocalPlayer.IsMasterClient;

                if (kickBtn != null)
                {
                    kickBtn.gameObject.SetActive(isHost);
                    //profTrans.localPosition = isHost
                        //? kickBtn.localPosition + new Vector3(-55f, 0f, 0f)
                        //: kickBtn.localPosition;
                }

                //transferTrans.gameObject.SetActive(isHost);

                if (slider != null)
                {
                    slider.gameObject.SetActive(true);
                }
            }

            addTrans.localPosition = profTrans.localPosition + new Vector3(-55f, 0f, 0f);
            //transferTrans.localPosition = addTrans.localPosition + new Vector3(-55f, 0f, 0f);

            if (avatarTrans != null)
            {
                avatarTrans.localPosition = Vector3.zero;
                avatarTrans.localScale = new Vector3(1.65f, -1.65f, 0f);
            }
        }

        private static void CleanButton(GameObject btn, string iconPath)
        {
            if (btn.GetComponent<KickButton>()) Destroy(btn.GetComponent<KickButton>());
            if (btn.GetComponent<Animator>()) Destroy(btn.GetComponent<Animator>());

            Transform icon = btn.transform.Find("KickIcon");
            Texture2D tex = IconFromEmbeddedResource(iconPath);
            icon.GetComponent<Image>().sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            icon.gameObject.SetActive(true);
            icon.localScale = new Vector3(0.7f, 0.7f, 0.7f);
        }
        private static bool isModInstalled(string guid) => BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(guid);
    }

    public class ProfileController : MonoBehaviour, IPunOwnershipCallbacks
    {
        public AudioLevelSlider parentSlider = null!;
        public Button profileButton = null!;
        public Button addButton = null!;
        public Button transferButton = null!;
        public GameObject profilePicture = null!;
        public GameObject platformIndicator = null!;
        private CSteamID currentSteamID = CSteamID.Nil;

        private static PlatformUtility.PlatformFamily playerPlatform;

        public void FetchProfileFromXuid(string xuid)
        {
            var xuidsToLookup = new List<string> { xuid };

            // Query the Xbox Live Profile Service
            XBL.XblProfileGetUserProfilesAsync(xuidsToLookup, (hresult, profiles) =>
            {
                if (HR.SUCCEEDED(hresult) && profiles != null && profiles.Count > 0)
                {
                    var profile = profiles[0];

                    // Read the profile metadata
                    string gamertag = profile.Gamertag;
                    string gamerpicUri = profile.GameDisplayPicRaw;
                    string realName = profile.RealName;

                    Debug.Log($"Successfully fetched profile! Gamertag: {gamertag}");
                    Debug.Log($"Gamerpic URL: {gamerpicUri}");
                }
                else
                {
                    Debug.LogError($"Failed to fetch Xbox profile. HResult: 0x{hresult:X}");
                }
            });
        }

        public void Refresh()
        {
            if (parentSlider?.player == null) return;

            Enum.TryParse<PlatformUtility.PlatformFamily>((string)parentSlider.player.CustomProperties["PlatformFamily"], out var platform);
            playerPlatform = platform;

            currentSteamID = Plugin.GetSteamId(parentSlider.player);

            profileButton.onClick.RemoveAllListeners();
            addButton.onClick.RemoveAllListeners();
            //transferButton.onClick.RemoveAllListeners();

            SetupButtonSFX(profileButton.gameObject);
            SetupButtonSFX(addButton.gameObject);
            //SetupButtonSFX(transferButton.gameObject);

            EFriendRelationship rel = SteamFriends.GetFriendRelationship(currentSteamID);
            bool isMe = parentSlider.player.IsLocal;
            bool isFriend = (rel == EFriendRelationship.k_EFriendRelationshipFriend);

            addButton.gameObject.SetActive(!isFriend && !isMe);
            bool disabled = isFriend || isMe;
            if (disabled)
            {
                transferButton.transform.localPosition += new Vector3(55f, 0f, 0f);
            }

            profileButton.enabled = true;
            profileButton.interactable = true;
            addButton.enabled = true;
            addButton.interactable = true;

            // Disable this because it doesnt work and I need to update the mod :plink:
            transferButton.enabled = false;
            transferButton.interactable = false;

            SetPlatformButton();

            profileButton.onClick.AddListener(() => {
                PlaySFX(profileButton.gameObject, "SFX Click");
                ProfileButtonAction();
            });

            addButton.onClick.AddListener(() => {
                PlaySFX(addButton.gameObject, "SFX Click");
                AddFriendAction();
                addButton.gameObject.SetActive(false);
            });

            /*
            transferButton.onClick.AddListener(() =>
            {
                PlaySFX(addButton.gameObject, "SFX Click");
                TryRequestHostTransfer(parentSlider.player.ActorNumber, currentSteamID);
                RefreshAll();
            });
            */
        }

        public void ProfileButtonAction()
        {
            switch (playerPlatform)
            {
                case PlatformUtility.PlatformFamily.Windows:
                    SteamFriends.ActivateGameOverlayToUser("steamid", currentSteamID);
                    break;
                //case PlatformUtility.PlatformFamily.Xbox:

            }

        }
        public void AddFriendAction()
        {
            switch (playerPlatform)
            {
                case PlatformUtility.PlatformFamily.Windows:
                    SteamFriends.ActivateGameOverlayToUser("friendadd", currentSteamID);
                    break;
            }
        }

        private void SetupButtonSFX(GameObject btnObj)
        {
            EventTrigger trigger = btnObj.GetComponent<EventTrigger>() ?? btnObj.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            Transform icon = btnObj.transform.Find("KickIcon");
            Vector3 normalScale = new Vector3(0.7f, 0.7f, 0.7f);
            Vector3 hoverScale = new Vector3(0.8f, 0.8f, 0.8f);

            EventTrigger.Entry hoverEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            hoverEntry.callback.AddListener((data) => {
                PlaySFX(btnObj, "SFX Hover");
                if (icon != null) icon.localScale = hoverScale;
            });
            trigger.triggers.Add(hoverEntry);

            EventTrigger.Entry exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((data) => {
                if (icon != null) icon.localScale = normalScale;
            });
            trigger.triggers.Add(exitEntry);
        }

        private void PlaySFX(GameObject parent, string childName)
        {
            Transform sfxTransform = parent.transform.Find(childName);
            if (sfxTransform != null)
            {
                sfxTransform.gameObject.SetActive(false);
                sfxTransform.gameObject.SetActive(true);
            }
        }

        public void SetPlatformButton()
        {
            if (platformIndicator.GetComponent<KickButton>()) Destroy(platformIndicator.GetComponent<KickButton>());
            if (platformIndicator.GetComponent<Animator>()) Destroy(platformIndicator.GetComponent<Animator>());

            Transform icon = platformIndicator.transform.Find("KickIcon");
            Texture2D tex = null;

            switch (playerPlatform)
            {
                case PlatformUtility.PlatformFamily.Windows:
                    tex = Plugin.SteamIcon;
                    break;
                case PlatformUtility.PlatformFamily.Xbox:
                    tex = Plugin.XboxIcon;
                    break;
                case PlatformUtility.PlatformFamily.Switch:
                    tex = Plugin.SwitchIcon;
                    break;
                case PlatformUtility.PlatformFamily.Playstation:
                    tex = Plugin.PlaystationIcon;
                    break;
                default:
                    tex = Plugin.SteamIcon;
                    break;
            }

            icon.GetComponent<Image>().sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            icon.gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            profileButton?.onClick.RemoveAllListeners();
            addButton?.onClick.RemoveAllListeners();
            transferButton?.onClick.RemoveAllListeners();
        }

        internal static bool TryRequestHostTransfer(int TargetActorNumber, CSteamID steamId)
        {
            var CurrentRoom = PhotonNetwork.CurrentRoom;
            var Matchmaking = NetCode.Matchmaking;

            if (CurrentRoom is null || PhotonNetwork.OfflineMode ||
                !PhotonNetwork.IsMasterClient || !Matchmaking.InLobby ||
                !CurrentRoom.Players.TryGetValue(TargetActorNumber, out Photon.Realtime.Player TargetPlayer) ||
                TargetPlayer.IsLocal || TargetPlayer.IsInactive ||
                !Matchmaking.PlayerIsInLobby(TargetPlayer.UserId) ||
                !ulong.TryParse(Matchmaking.LobbyId, out ulong LobbyIdValue))
            {
                return false;
            }
            CSteamID LobbyId = new(LobbyIdValue);
            if (Steamworks.SteamMatchmaking.GetLobbyOwner(LobbyId) != SteamUser.GetSteamID())
            {
                return false;
            }

            Steamworks.SteamMatchmaking.SetLobbyOwner(LobbyId, steamId);

            bool Queued = PhotonNetwork.SetMasterClient(TargetPlayer);
            Plugin.Log.LogInfo($"Photon host transfer queued={Queued}; " + $"target={TargetPlayer.NickName} " + $"({TargetPlayer.ActorNumber})");
            return Queued;
        }

        private void RefreshAll()
        {
            // Updates the client, not sure how to update the whole server (including vanilla clients)
            parentSlider.transform.parent.GetComponent<AudioLevels>().UpdateSliders();
        }

        public void OnOwnershipRequest(PhotonView targetView, Photon.Realtime.Player requestingPlayer)
        {
            throw new NotImplementedException();
        }

        public void OnOwnershipTransfered(PhotonView targetView, Photon.Realtime.Player previousOwner)
        {
            Photon.Realtime.Player NewMasterClient = PhotonNetwork.MasterClient;
            var Matchmaking = NetCode.Matchmaking;
            if (NewMasterClient is null || !Matchmaking.InLobby ||
                !Matchmaking.PlayerIsInLobby(NewMasterClient.UserId) ||
                !ulong.TryParse(Matchmaking.LobbyId, out ulong LobbyIdValue) ||
                !ulong.TryParse(NewMasterClient.UserId, out ulong NewOwnerIdValue))
            {
                return;
            }

            CSteamID LobbyId = new(LobbyIdValue);
            CSteamID NewOwnerId = new(NewOwnerIdValue);
            CSteamID CurrentOwnerId = Steamworks.SteamMatchmaking.GetLobbyOwner(LobbyId);
            if (CurrentOwnerId == NewOwnerId)
            {
                return;
            }
            if (CurrentOwnerId != SteamUser.GetSteamID())
            {
                return;
            }
            if (!Steamworks.SteamMatchmaking.SetLobbyOwner(LobbyId, NewOwnerId))
            {
                Plugin.Log.LogError("Photon host changed, but Steam lobby " + "ownership transfer failed.");
                return;
            }
            Plugin.Log.LogInfo($"Steam lobby ownership transferred to " + $"{NewMasterClient.NickName} " + $"({NewOwnerIdValue}).");
        }

        public void OnOwnershipTransferFailed(PhotonView targetView, Photon.Realtime.Player senderOfFailedRequest)
        {
            throw new System.NotImplementedException();
        }
    }

    [HarmonyPatch]
    public class Patches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(AudioLevelSlider), nameof(AudioLevelSlider.Init))]
        public static void AudioLevelSliderInit(AudioLevelSlider __instance)
        {
            if (__instance.player == null) return;
            Plugin.CreateUI(__instance);
        }
    }
}