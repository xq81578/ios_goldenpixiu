using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class JsonExtensions
{
    public static bool IsValidJArray(this string json)
    {
        if (string.IsNullOrEmpty(json))
            return false;

        if (!json.IsValidJObject())
            return false;

        JToken token = JToken.Parse(json);
        return token.Type == JTokenType.Array;
    }

    public static bool IsValidJObject(this string json)
    {
        if (string.IsNullOrEmpty(json))
            return false;

        // JToken token = JToken.Parse(json);
        // return token.Type == JTokenType.Object;

        try
        {
            var obj = JsonConvert.DeserializeObject(json);
            return obj != null;
        }
        catch (JsonReaderException ex)
        {
            return false;
        }
    }

}
