namespace PacketCaptureAgent;

/// <summary>중첩 필드를 flat key로 펼치는 공통 유틸리티.</summary>
public static class FieldFlattener
{
    public static void Flatten(Dictionary<string, object> state, string key, object value)
    {
        if (value is List<object> list)
        {
            state[key] = list;
            for (int i = 0; i < list.Count; i++)
                if (list[i] is Dictionary<string, object> s)
                    foreach (var (fn, fv) in s)
                        Flatten(state, $"{key}[{i}].{fn}", fv);
                else
                    state[$"{key}[{i}]"] = list[i];
        }
        else if (value is Dictionary<string, object> fields)
        {
            foreach (var (fn, fv) in fields)
                Flatten(state, $"{key}.{fn}", fv);
        }
        else
            state[key] = value;
    }
}
