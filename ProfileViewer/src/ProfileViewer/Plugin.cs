using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Peak.UI;
using Photon.Pun;
using Photon.Realtime;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProfileViewer
{
    [BepInAutoPlugin]
    public partial class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log { get; private set; } = null!;
        public static Harmony harmony = null!;

        private void Awake()
        {
            Log = Logger;
            harmony = new Harmony(Id);
            harmony.PatchAll();
            Log.LogInfo($"Plugin {Name} is loaded!");
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

        public static Texture2D GetSteamAvatar(CSteamID steamID)
        {
            int handler = SteamFriends.GetMediumFriendAvatar(steamID);
            if (handler == -1) return null;

            uint width, height;
            SteamUtils.GetImageSize(handler, out width, out height);

            byte[] imageBuffer = new byte[width * height * 4];
            if (SteamUtils.GetImageRGBA(handler, imageBuffer, (int)(width * height * 4)))
            {
                Texture2D texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
                texture.LoadRawTextureData(imageBuffer);
                texture.Apply();
                return texture;
            }
            return null;
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
            local.Find("Percent").localPosition += new Vector3(40f, 5f, 0f);
            local.Find("Icon").localPosition += new Vector3(40f, 5f, 0f);
            nameLayout.localPosition += new Vector3(0f, 40f, 0f);

            GameObject profileBtnObj = Instantiate(kickButtonTrans.gameObject, local);
            profileBtnObj.name = "ProfileButton";
            profileBtnObj.SetActive(true);

            GameObject addBtnObj = Instantiate(profileBtnObj, local);
            addBtnObj.name = "AddFriendButton";
            addBtnObj.SetActive(true);

            HandleVisibility(__instance, local, kickButtonTrans, sliderTrans);

            CleanButton(profileBtnObj, "ProfileViewer.Assets.Open-Profile.png");
            CleanButton(addBtnObj, "ProfileViewer.Assets.Add-Friend.png");

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
            frameObj.GetComponent<Image>().sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");

            GameObject avatarObj = new GameObject("ProfileAvatar", typeof(RectTransform), typeof(RawImage));
            avatarObj.transform.SetParent(frameObj.transform, false);
            var avatarRect = avatarObj.GetComponent<RectTransform>();
            avatarRect.anchorMin = Vector2.zero;
            avatarRect.anchorMax = Vector2.one;
            avatarRect.sizeDelta = Vector2.zero;
            avatarObj.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);

            controller.profileButton = profileBtnObj.GetComponent<Button>();
            controller.addButton = addBtnObj.GetComponent<Button>();
            controller.avatarDisplay = avatarObj.GetComponent<RawImage>();
            controller.parentSlider = __instance;

            controller.Refresh();
        }

        private static void HandleVisibility(AudioLevelSlider instance, Transform local, Transform kickBtn, Transform slider)
        {
            Transform profTrans = local.Find("ProfileButton");
            Transform addTrans = local.Find("AddFriendButton");
            if (profTrans == null || addTrans == null) return;

            if (instance.player.IsLocal)
            {
                if (kickBtn != null) kickBtn.gameObject.SetActive(false);
                if (slider != null) slider.gameObject.SetActive(false);
                profTrans.localPosition = kickBtn.localPosition;
            }
            else
            {
                bool isHost = PhotonNetwork.LocalPlayer.IsMasterClient;
                if (kickBtn != null) kickBtn.gameObject.SetActive(isHost);
                if (slider != null) slider.gameObject.SetActive(true);

                if (isHost)
                {
                    profTrans.localPosition = kickBtn.localPosition + new Vector3(-55f, 0f, 0f);
                }
                else
                {
                    profTrans.localPosition = kickBtn.localPosition;
                }
            }

            addTrans.localPosition = profTrans.localPosition + new Vector3(-55f, 0f, 0f);
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

    public class ProfileController : MonoBehaviour
    {
        public AudioLevelSlider parentSlider = null!;
        public Button profileButton = null!;
        public Button addButton = null!;
        public RawImage avatarDisplay = null!;
        private CSteamID currentSteamID = CSteamID.Nil;

        public void Refresh()
        {
            if (parentSlider?.player == null) return;

            currentSteamID = Plugin.GetSteamId(parentSlider.player);

            Texture2D tex = Plugin.GetSteamAvatar(currentSteamID);
            if (tex != null) avatarDisplay.texture = tex;

            profileButton.onClick.RemoveAllListeners();
            addButton.onClick.RemoveAllListeners();

            SetupButtonSFX(profileButton.gameObject);
            SetupButtonSFX(addButton.gameObject);

            EFriendRelationship rel = SteamFriends.GetFriendRelationship(currentSteamID);
            bool isMe = parentSlider.player.IsLocal;
            bool isFriend = (rel == EFriendRelationship.k_EFriendRelationshipFriend);

            addButton.gameObject.SetActive(!isFriend && !isMe);

            profileButton.onClick.AddListener(() => {
                PlaySFX(profileButton.gameObject, "SFX Click");
                SteamFriends.ActivateGameOverlayToUser("steamid", currentSteamID);
            });

            addButton.onClick.AddListener(() => {
                PlaySFX(addButton.gameObject, "SFX Click");
                SteamFriends.ActivateGameOverlayToUser("friendadd", currentSteamID);
                addButton.gameObject.SetActive(false);
            });
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

        private void OnDestroy()
        {
            profileButton?.onClick.RemoveAllListeners();
            addButton?.onClick.RemoveAllListeners();
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