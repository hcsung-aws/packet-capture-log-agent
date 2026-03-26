using System.Text.RegularExpressions;

namespace PacketCaptureAgent;

/// <summary>SessionState 기반 조건 표현식 평가.
/// 단순: "SC_CHAR_LIST.count == 0"
/// 복합: "SC_CHAR_LIST.count > 0 AND SC_LOGIN_RESULT.success == 1"</summary>
public static class ConditionEvaluator
{
    public static bool Evaluate(string expression, Dictionary<string, object> sessionState)
    {
        if (string.IsNullOrWhiteSpace(expression)) return true;

        // OR 분할 (우선순위 낮음)
        var orParts = Regex.Split(expression, @"\s+OR\s+");
        if (orParts.Length > 1)
            return orParts.Any(p => Evaluate(p.Trim(), sessionState));

        // AND 분할
        var andParts = Regex.Split(expression, @"\s+AND\s+");
        if (andParts.Length > 1)
            return andParts.All(p => Evaluate(p.Trim(), sessionState));

        // 단일 조건: field op value
        var m = Regex.Match(expression, @"^(.+?)\s*(==|!=|>=|<=|>|<)\s*(.+)$");
        if (!m.Success) return false;

        var key = m.Groups[1].Value.Trim();
        var op = m.Groups[2].Value;
        var right = m.Groups[3].Value.Trim();

        if (!sessionState.TryGetValue(key, out var leftObj)) return false;

        return Compare(leftObj, op, right);
    }

    private static bool Compare(object left, string op, string right)
    {
        // 숫자 비교
        if (ToDouble(left, out var ld) && double.TryParse(right, out var rd))
        {
            return op switch
            {
                "==" => ld == rd,
                "!=" => ld != rd,
                ">" => ld > rd,
                "<" => ld < rd,
                ">=" => ld >= rd,
                "<=" => ld <= rd,
                _ => false
            };
        }

        // 문자열 비교
        var ls = left?.ToString() ?? "";
        var rs = right.Trim('"');
        return op switch
        {
            "==" => ls == rs,
            "!=" => ls != rs,
            _ => false
        };
    }

    private static bool ToDouble(object value, out double result)
    {
        result = 0;
        if (value is int i) { result = i; return true; }
        if (value is long l) { result = l; return true; }
        if (value is double d) { result = d; return true; }
        if (value is float f) { result = f; return true; }
        return double.TryParse(value?.ToString(), out result);
    }
}
