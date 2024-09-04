using ConditionalDebug;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ProgressMessage : MonoBehaviour, IPlatformProgressMessage
{
    public static IPlatformProgressMessage Instance;

    [SerializeField]
    GameObject closeButton;

    public Text messageText;

    [SerializeField]
    Subcanvas subcanvas;

    public bool IsOpen => subcanvas.IsOpen;

    public string MessageText
    {
        get => messageText.text;
        set => messageText.text = value;
    }

    public void Open(string msg)
    {
        messageText.text = msg;
        //gameObject.SetActive(true);

        subcanvas.Open();
    }

    public void Close()
    {
        //gameObject.SetActive(false);

        subcanvas.Close();
    }

    //必要时也可以在其他弹窗中调用和使用。
    public void PushCloseButton()
    {
        ConDebug.Log("ProgressMessage Force Close Push by user.");
        Close();
    }

    //忽略无法处理 back 的情况的方法，例如网络选项，请谨慎使用。
    public void ForceBackButtonActive()
    {
        subcanvas.ForceBackButtonHandler = true;
    }

    public void CloseButtonPopup()
    {
        ConDebug.Log("ProgressMessage Close button popup");
        BackButtonHandler.Instance.PushAction(Instance.PushCloseButton);
        closeButton.SetActive(true);
    }

    public void DisableCloseButton()
    {
        ConDebug.Log("ProgressMessage Close button Disabled");
        closeButton.SetActive(false);
    }

    void Awake()
    {
        if (transform.parent.childCount - 1 != transform.GetSiblingIndex())
            Debug.LogError("Progress Message should be last sibling!");

        closeButton.SetActive(false);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        subcanvas = GetComponent<Subcanvas>();
    }
#endif

    void OpenPopup()
    {
        // should receive message even if there is nothing to do
    }

    void ClosePopup()
    {
        // should receive message even if there is nothing to do
    }
}