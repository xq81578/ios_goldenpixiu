
// Game Server response
using System;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

public class ProtobufHelper
{
    public static T ParseAny<T>(ByteString anyByteString) where T : IMessage<T>, new()
    {
#if !RELEASE_BUILD && UNITY_EDITOR
        // 處理假資料用
        try
        {
            T obj = ParseJson<T>(anyByteString.ToStringUtf8());
            return obj;
        }
        catch (Exception e)
        {
            // 不是假資料
            //LogUtils.LogWarning($"Error parsing ByteString to {typeof(T).Name}: {e.Message}");
        }
#endif
        Any anyObj = Any.Parser.ParseFrom(anyByteString);
        return anyObj.Unpack<T>();
    }

    public static T ParseJson<T>(string jsonData) where T : IMessage<T>, new()
    {
        T anyObj = JsonParser.Default.Parse<T>(jsonData);
        return anyObj;
    }

    public static Any AnyParse<T>(T protobufData) where T : IMessage<T>, new()
    {
        return Any.Pack(protobufData);
    }
}

public class MemberPreference
{
    public string HomeUrl;

    public MemberPreference(Protobuf.Gateway.MemberPreference data)
    {
        HomeUrl = data.HomeUrl;
    }
}

