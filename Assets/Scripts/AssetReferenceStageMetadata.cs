﻿using System;
#if ADDRESSABLES
using UnityEngine.AddressableAssets;

[Serializable]
public class AssetReferenceStageMetadata : AssetReferenceT<StageMetadata>
{
    public AssetReferenceStageMetadata(string guid) : base(guid)
    {
    }
}
#endif
