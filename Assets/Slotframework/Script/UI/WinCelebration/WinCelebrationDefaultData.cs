
using System.Collections.Generic;
using static WinCelebrationUIMediator;

public class WinCelebrationDefaultData
{
    public static List<WinAnimationData> DefaultWinAnimationData = new List<WinAnimationData>
    {
        new WinAnimationData()
        {
            winType = WinType.BigWin,
            ratio = 10,
            animationName = "bigWin",
            introSuffix = "_in",
            loopSuffix = "_loop",
            landscapeSuffix = "_L",
            portraitSuffix = "_P",
            animationDuration = 4f,
            audioName = "bw_bigwin",
            voiceAudioName = "vo_bigwin",
        },
        new WinAnimationData()
        {
            winType = WinType.MegaWin,
            ratio = 25,
            animationName = "megaWin",
            introSuffix = "_in",
            loopSuffix = "_loop",
            landscapeSuffix = "_L",
            portraitSuffix = "_P",
            animationDuration = 4f,
            audioName = "bw_megawin",
            voiceAudioName = "vo_megawin",
        },
        new WinAnimationData()
        {
            winType = WinType.SuperWin,
            ratio = 50,
            animationName = "superWin",
            introSuffix = "_in",
            loopSuffix = "_loop",
            landscapeSuffix = "_L",
            portraitSuffix = "_P",
            animationDuration = 4f,
            audioName = "bw_superwin",
            voiceAudioName = "vo_superwin",
        },
        new WinAnimationData()
        {
            winType = WinType.EpicWin,
            ratio = 100,
            animationName = "epicWin",
            introSuffix = "_in",
            loopSuffix = "_loop",
            landscapeSuffix = "_L",
            portraitSuffix = "_P",
            animationDuration = 4f,
            audioName = "bw_epicwin",
            voiceAudioName = "vo_epicwin",
        },
        new WinAnimationData()
        {
            winType = WinType.EpicWinLoop,
            ratio = 300,
            animationName = "epicWin",
            introSuffix = "_loop",
            loopSuffix = "_loop",
            landscapeSuffix = "_L",
            portraitSuffix = "_P",
            animationDuration = 4f,
            audioName =  "bw_epicwinloop",
            voiceAudioName = "vo_epicwin",
        },
    };

    public static List<WinAnimationData> DefaultEpicLoopAnimationData = new List<WinAnimationData>
    {
        new WinAnimationData()
        {
            winType = WinType.EpicWinLoop,
            ratio = 700,
            animationName = "epicWin",
            introSuffix = "_loop",
            loopSuffix = "_loop",
            landscapeSuffix = "_L",
            portraitSuffix = "_P",
            animationDuration = 4f,
            audioName = "bw_epicwinloop",
        },
        new WinAnimationData()
        {
            winType = WinType.EpicWinLoop,
            ratio = 2500,
            animationName = "epicWin",
            introSuffix = "_loop",
            loopSuffix = "_loop",
            landscapeSuffix = "_L",
            portraitSuffix = "_P",
            animationDuration = 4f,
            audioName =  "bw_epicwinloop",
        },
        new WinAnimationData()
        {
            winType = WinType.EpicWinLoop,
            ratio = 5000,
            animationName = "epicWin",
            introSuffix = "_loop",
            loopSuffix = "_loop",
            landscapeSuffix = "_L",
            portraitSuffix = "_P",
            animationDuration = 4f,
            audioName =  "bw_epicwinloop",
        },
        new WinAnimationData()
        {
            winType = WinType.EpicWinLoop,
            ratio = 10000,
            animationName = "epicWin",
            introSuffix = "_loop",
            loopSuffix = "_loop",
            landscapeSuffix = "_L",
            portraitSuffix = "_P",
            animationDuration = 4f,
            audioName =  "bw_epicwinloop",
        },
        new WinAnimationData()
        {
            winType = WinType.EpicWinLoop,
            ratio = 40000,
            animationName = "epicWin",
            introSuffix = "_loop",
            loopSuffix = "_loop",
            landscapeSuffix = "_L",
            portraitSuffix = "_P",
            animationDuration = 4f,
            audioName =  "bw_epicwinloop",
        },
        new WinAnimationData()
        {
            winType = WinType.EpicWinLoop,
            ratio = 100000,
            animationName = "epicWin",
            introSuffix = "_loop",
            loopSuffix = "_loop",
            landscapeSuffix = "_L",
            portraitSuffix = "_P",
            animationDuration = 4f,
            audioName =  "bw_epicwinloop",
        }
    };

    public static List<WinAnimationData> GetDefaultWinAnimationData(bool hasOrientationSuffix = true)
    {
        if (hasOrientationSuffix)
        {
            return DefaultWinAnimationData;
        }
        else
        {
            List<WinAnimationData> winAnimationData = new List<WinAnimationData>();
            foreach (var data in DefaultWinAnimationData)
            {
                var newData = new WinAnimationData()
                {
                    winType = data.winType,
                    ratio = data.ratio,
                    animationName = data.animationName,
                    introSuffix = data.introSuffix,
                    loopSuffix = data.loopSuffix,
                    animationDuration = data.animationDuration,
                    audioName = data.audioName,
                    voiceAudioName = data.voiceAudioName,
                };
                winAnimationData.Add(newData);
            }
            return winAnimationData;
        }
    }

    public static List<WinAnimationData> GetDefaultEpicLoopAnimationData(bool hasOrientationSuffix = true)
    {
        if (hasOrientationSuffix)
        {
            return DefaultEpicLoopAnimationData;
        }
        else
        {
            List<WinAnimationData> winAnimationData = new List<WinAnimationData>();
            foreach (var data in DefaultEpicLoopAnimationData)
            {
                var newData = new WinAnimationData()
                {
                    winType = data.winType,
                    ratio = data.ratio,
                    animationName = data.animationName,
                    introSuffix = data.introSuffix,
                    loopSuffix = data.loopSuffix,
                    animationDuration = data.animationDuration,
                    audioName = data.audioName,
                };
                winAnimationData.Add(newData);
            }
            return winAnimationData;
        }
    }
}
