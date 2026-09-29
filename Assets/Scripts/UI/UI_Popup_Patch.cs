using System;
using ThreeSouls.Manager;
using ThreeSouls.Utility;
using UnityEngine;
using UnityEngine.UI;

public class UI_Popup_Patch : MonoBehaviour
{
    private enum ButtonState
    {
        OK,
        Cancel,
    }

    [SerializeField] private GameObject PopupGroup = null;
    [SerializeField] private Text PatchText = null;
    [SerializeField] private Button[] PatchButton = null;
    [SerializeField] private GameObject ProgressGroup = null;
    [SerializeField] private Image ProgressBarImage = null;

    public Action ButtonAction { get; set; } = null;

    private void Awake()
    {
       
    }

    public void ShowPopup(long bundleSize)
    {
        ProgressBarImage.fillAmount = 0;

        SetPatchText(bundleSize);
        ButtonEvent();
    }

    private void ButtonEvent()
    {
        for (int i = 0; i < PatchButton.Length; i++)
        {
            ButtonState buttonState = (ButtonState)i;

            switch (buttonState)
            {
                case ButtonState.OK:
                    PatchButton[i].onClick.AddListener(AssetBundleDownload);
                    break;
                case ButtonState.Cancel:
                    PatchButton[i].onClick.AddListener(ApplicationQuit);
                    break;
            }
        }
    }

    private void SetPatchText(long bundleSize)
    {
        var formatPatchStr = ThreeSoulsUtility.FormatBytesToString(bundleSize);
        PatchText.text = string.Format("{0} 패치 파일이 존재합니다. 다운로드 받으시겠습니까?", formatPatchStr);
    }

    private void AssetBundleDownload()
    {
        ProgressGroup.gameObject.SetActive(true);
        PopupGroup.SetActive(false);

        ButtonAction?.Invoke();
    }

    public void OnBundleDownloadProgress(float progress)
    {
        ProgressBarImage.fillAmount = progress;
    }

    private void ApplicationQuit()
    {
        Application.Quit();
    }
}
