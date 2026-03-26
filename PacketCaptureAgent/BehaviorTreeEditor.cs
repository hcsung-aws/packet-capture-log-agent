namespace PacketCaptureAgent;

/// <summary>Behavior Tree 인터랙티브 편집기.
/// 트리 표시 → 노드 제거/조건 편집 → 저장.</summary>
public class BehaviorTreeEditor
{
    public static BehaviorTreeDefinition Edit(BehaviorTreeDefinition tree)
    {
        Console.WriteLine($"\n=== BT Editor: {tree.Name} ===\n");

        while (true)
        {
            var nodes = new List<(BtNode node, string path)>();
            IndexNodes(tree.Root, "", nodes);

            // 트리 표시
            for (int i = 0; i < nodes.Count; i++)
            {
                var (node, path) = nodes[i];
                var cond = node.Condition != null ? $" [{node.Condition}]" : "";
                var desc = node switch
                {
                    BtAction a => $"Action: {a.Id}",
                    BtSequence => "Sequence",
                    BtSelector => "Selector",
                    BtRepeat r => $"Repeat x{r.Count}",
                    _ => node.Type
                };
                Console.WriteLine($"  {i,3}. {path}{desc}{cond}");
            }

            Console.WriteLine($"\n명령: (r)emove N, (c)ondition N expr, (s)ave, (q)uit");
            Console.Write("> ");
            var input = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(input)) continue;

            var parts = input.Split(' ', 2);
            var cmd = parts[0].ToLower();

            switch (cmd)
            {
                case "s" or "save":
                    return tree;
                case "q" or "quit":
                    return tree;
                case "r" or "remove" when parts.Length > 1 && int.TryParse(parts[1].Trim(), out var ri):
                    if (ri >= 0 && ri < nodes.Count)
                    {
                        RemoveNode(tree, nodes[ri].node);
                        Console.WriteLine($"  ✓ 노드 {ri} 제거됨\n");
                    }
                    break;
                case "c" or "condition" when parts.Length > 1:
                    var cParts = parts[1].Trim().Split(' ', 2);
                    if (int.TryParse(cParts[0], out var ci) && ci >= 0 && ci < nodes.Count)
                    {
                        var expr = cParts.Length > 1 ? cParts[1].Trim() : null;
                        if (string.IsNullOrEmpty(expr)) expr = null;
                        nodes[ci].node.Condition = expr;
                        Console.WriteLine($"  ✓ 노드 {ci} 조건: {expr ?? "(제거)"}\n");
                    }
                    break;
            }
        }
    }

    private static void IndexNodes(BtNode node, string indent, List<(BtNode, string)> list)
    {
        list.Add((node, indent));
        switch (node)
        {
            case BtSequence s:
                foreach (var c in s.Children) IndexNodes(c, indent + "  ", list);
                break;
            case BtSelector s:
                foreach (var c in s.Children) IndexNodes(c, indent + "  ", list);
                break;
            case BtRepeat r:
                IndexNodes(r.Child, indent + "  ", list);
                break;
        }
    }

    private static void RemoveNode(BehaviorTreeDefinition tree, BtNode target)
    {
        tree.Root = RemoveFromTree(tree.Root, target) ?? new BtSequence();
    }

    private static BtNode? RemoveFromTree(BtNode node, BtNode target)
    {
        if (node == target) return null;
        switch (node)
        {
            case BtSequence s:
                s.Children = s.Children.Select(c => RemoveFromTree(c, target)).Where(c => c != null).ToList()!;
                return s.Children.Count == 0 ? null : s;
            case BtSelector s:
                s.Children = s.Children.Select(c => RemoveFromTree(c, target)).Where(c => c != null).ToList()!;
                return s.Children.Count == 0 ? null : s;
            case BtRepeat r:
                var child = RemoveFromTree(r.Child, target);
                if (child == null) return null;
                r.Child = child;
                return r;
            default:
                return node;
        }
    }
}
