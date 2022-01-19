using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;

[DisallowMultipleComponent]
public class DialogGroup : MonoBehaviour
{
    [SerializeField]
    Subcanvas subcanvas;

    [SerializeField]
    Animator dialogContentAnimator;

    [SerializeField]
    JasoTypewriter talkTypewriter;

    TaskCompletionSource<bool> tryNextTsc;
    
    static readonly int Appear = Animator.StringToHash("Appear");
    static readonly int Disappear = Animator.StringToHash("Disappear");

#if UNITY_EDITOR
    void OnValidate()
    {
        AutoBindUtil.BindAll(this);
    }
#endif

    async void Start()
    {
        if (BlackContext.instance.LastClearedStageId == 3 && BlackContext.instance.LastClearedStageIdEvent < 3)
        {
            await StartFairyDialogInternalAsync();
        }

        BlackContext.instance.LastClearedStageIdEvent = BlackContext.instance.LastClearedStageId;
    }

    public async void StartFairyDialogAsync() => await StartFairyDialogInternalAsync(); 

    async Task StartFairyDialogInternalAsync()
    {
        subcanvas.Open();
        talkTypewriter.ClearText();
        
        dialogContentAnimator.SetTrigger(Appear);
        Sound.instance.PlayPopup();
        
        await Task.Delay(1000);

        var talkList = new[]
        {
            "미술관 관장님,\n저는 체커의 요정이에요.",
            "어쩌면 아마도 앞으로는...\n광고가 나올지도 몰라요.",
            "대신 제가 게임을 좀 더\n재미있게 만들어 볼게요.",
            "감사한 관장님,\n앞으로도 즐겨주세요!"
        };
        
        foreach (var talk in talkList)
        {
            var tsc = new TaskCompletionSource<bool>();
            talkTypewriter.StartType(false, talk, () => { tsc.SetResult(true); });
            await tsc.Task;
            tryNextTsc = new TaskCompletionSource<bool>();
            await tryNextTsc.Task;
        }
        
        dialogContentAnimator.SetTrigger(Disappear);
        Sound.instance.PlayPopup();
        
        await Task.Delay(1000);
        
        Sound.instance.PlayJingleAchievement();

        ConfirmPopup.instance.Open("축하합니다!\n이제부터 색칠할 곳이 격자 무늬로 강조됩니다.", ConfirmPopup.instance.Close, "새 기능");
        
        subcanvas.Close();
    }

    public void TryNextTalk()
    {
        tryNextTsc?.TrySetResult(true);
    }

    [UsedImplicitly]
    void OpenPopup()
    {
    }

    [UsedImplicitly]
    void ClosePopup()
    {
    }
}