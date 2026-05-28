using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;

[Serializable]
public sealed class GameObjectUnityEvent : UnityEvent<GameObject>
{

}

[Serializable]
public sealed class IntegerUnityEvent : UnityEvent<int>
{

}

[Serializable]
public sealed class LongIntegerUnityEvent : UnityEvent<long>
{

}

[Serializable]
public sealed class StringUnityEvent : UnityEvent<string>
{

}

[Serializable]
public sealed class FloatUnityEvent : UnityEvent<float>
{

}

[Serializable]
public sealed class BoolUnityEvent : UnityEvent<bool>
{

}

[Serializable]
public sealed class StateMachineUnityEvent : UnityEvent<Animator, AnimatorStateInfo, int>
{

}

[Serializable]
public sealed class DoubleUnityEvent : UnityEvent<Double>
{

}

[Serializable]
public sealed class SpriteUnityEvent : UnityEvent<Sprite>
{

}

[Serializable]
public sealed class GameObjectNameUnityEvent : UnityEvent<string, GameObject>
{

}

[Serializable]
public sealed class GameObjectAssetReferenceUnityEvent : UnityEvent<AssetReference, GameObject>
{

}

[Serializable]
public sealed class BoolEventArgs : EventArgs
{
    public BoolEventArgs()
    {

    }

    public BoolEventArgs(bool bArg)
    {
        Value = bArg;
    }

    public bool Value = false;
}


[Serializable]
public sealed class IntEventArgs : EventArgs
{
    public IntEventArgs()
    {

    }

    public IntEventArgs(int iArg)
    {
        Value = iArg;
    }

    public int Value = 0;
}

[Serializable]
public sealed class FloatEventArgs : EventArgs
{
    public FloatEventArgs()
    {

    }

    public FloatEventArgs(float fArg)
    {
        Value = fArg;
    }

    public float Value = 0;
}

[Serializable]
public sealed class StringEventArgs : EventArgs
{
    public StringEventArgs()
    {

    }

    public StringEventArgs(string sArg)
    {
        Value = sArg;
    }

    public string Value = "";
}

[Serializable]
public sealed class GameObjectEventArgs : EventArgs
{
    public GameObjectEventArgs()
    {

    }

    public GameObjectEventArgs(GameObject arg)
    {
        Value = arg;
    }

    public GameObject Value;
}

[Serializable]
public sealed class ActionEventArgs : EventArgs
{
    public Action Go;

    public ActionEventArgs()
    {
    }

    public ActionEventArgs(Action arg)
    {
        Go = arg;
    }

    public void TriggerAction()
    {
        if (Go != null)
            Go.Invoke();
    }
}

[Serializable]
public sealed class UnityActionEventArgs : EventArgs
{
    public UnityAction action;

    public UnityActionEventArgs()
    {
    }

    public UnityActionEventArgs(UnityAction arg)
    {
        action = arg;
    }

    public void TriggerAction()
    {
        if (action != null)
            action.Invoke();
    }
}

[Serializable]
public sealed class GenericEventArgs<T> : EventArgs
{
    public T Value;

    public GenericEventArgs()
    {
    }

    public GenericEventArgs(T arg)
    {
        Value = arg;
    }
}

[Serializable]
public sealed class BoolActionEventArgs : EventArgs
{
    public Action<bool> Go;

    public void TriggerAction(bool Value)
    {
        if (Go != null)
            Go.Invoke(Value);
    }
}

[Serializable]
public sealed class TransformActionEventArgs : EventArgs
{
    public Action<Transform> Go;

    public void TriggerAction(Transform Value)
    {
        if (Go != null)
            Go.Invoke(Value);
    }
}

public sealed class EventArgs<T> : EventArgs
{
    public T Value { get; private set; }
    public EventArgs(T val) => Value = val;
}