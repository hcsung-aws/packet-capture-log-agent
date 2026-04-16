using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace PacketCaptureAgent;

/// <summary>상태 추적 목업 서버. C2S 파싱 → 상태 갱신 → S2C 응답 생성.</summary>
public class MockServer
{
    private readonly ProtocolDefinition _protocol;
    private readonly MockRuleSet _ruleSet;
    private readonly PacketParser _parser;
    private readonly PacketBuilder _builder;
    private readonly TextWriter _output;
    private readonly Dictionary<string, MockRule> _ruleMap;
    private readonly Dictionary<string, FieldRange>? _fieldRanges;
    private volatile bool _running = true;

    public MockServer(ProtocolDefinition protocol, MockRuleSet ruleSet, TextWriter? output = null)
    {
        _protocol = protocol;
        _ruleSet = ruleSet;
        _parser = new PacketParser(protocol);
        _builder = new PacketBuilder(protocol);
        _output = output ?? Console.Out;
        _ruleMap = ruleSet.Rules.ToDictionary(r => r.Trigger);
        _fieldRanges = ruleSet.FieldRanges;
    }

    public async Task RunAsync(int port, CancellationToken ct = default)
    {
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        _output.WriteLine($"[MockServer] Listening on port {port}");

        try
        {
            while (_running && !ct.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
        }
        catch (OperationCanceledException) { }
        finally { listener.Stop(); }
    }

    public void Stop() => _running = false;

    internal async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        var ep = client.Client.RemoteEndPoint;
        _output.WriteLine($"[MockServer] Client connected: {ep}");
        var session = new MockSession();

        try
        {
            using var stream = client.GetStream();
            var tcpStream = new TcpStream(new ConnectionKey(IPAddress.Any, 0, IPAddress.Any, 0));
            var buf = new byte[4096];

            while (_running && !ct.IsCancellationRequested && client.Connected)
            {
                int read;
                try { read = await stream.ReadAsync(buf, ct); }
                catch { break; }
                if (read == 0) break;

                tcpStream.Append(buf.AsSpan(0, read));

                while (true)
                {
                    var packet = _parser.TryParse(tcpStream);
                    if (packet == null) break;

                    _output.WriteLine($"[MockServer] ← {packet.Name}");
                    var responses = GenerateResponses(packet, session);
                    foreach (var resp in responses)
                    {
                        try
                        {
                            await stream.WriteAsync(resp.Data, ct);
                            _output.WriteLine($"[MockServer] → {resp.Name}");
                        }
                        catch { return; }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _output.WriteLine($"[MockServer] Error: {ex.Message}");
        }
        finally
        {
            client.Dispose();
            _output.WriteLine($"[MockServer] Client disconnected: {ep}");
        }
    }

    internal record ResponsePacket(string Name, byte[] Data);

    internal List<ResponsePacket> GenerateResponses(ParsedPacket request, MockSession session)
    {
        // 상태 추적 대상은 동적 응답
        if (_ruleMap.TryGetValue(request.Name, out var rule) && rule.Stateful)
            return GenerateStatefulResponse(request, session);

        // 비상태: 규칙 기본값으로 응답
        if (rule != null)
            return BuildFromRule(rule, session);

        return new();
    }

    private List<ResponsePacket> GenerateStatefulResponse(ParsedPacket request, MockSession session)
    {
        return request.Name switch
        {
            "CS_LOGIN" => HandleLogin(request, session),
            "CS_CHAR_CREATE" => HandleCharCreate(request, session),
            "CS_CHAR_SELECT" => HandleCharSelect(request, session),
            "CS_MOVE" => HandleMove(request, session),
            "CS_ATTACK" => HandleAttack(request, session),
            "CS_SHOP_OPEN" => HandleShopOpen(session),
            "CS_SHOP_BUY" => HandleShopBuy(request, session),
            "CS_SHOP_SELL" => HandleShopSell(request, session),
            "CS_ITEM_USE" => HandleItemUse(request, session),
            "CS_ITEM_EQUIP" => HandleItemEquip(request, session),
            "CS_HEARTBEAT" => new() { BuildPacket("SC_HEARTBEAT", new()) },
            _ => BuildFromRule(_ruleMap[request.Name], session)
        };
    }

    private List<ResponsePacket> HandleLogin(ParsedPacket req, MockSession session)
    {
        session.AccountUid = session.AllocAccountUid();
        session.LoggedIn = true;

        var result = BuildPacket("SC_LOGIN_RESULT", new()
        {
            ["success"] = 1,
            ["accountUid"] = session.AccountUid,
            ["message"] = "OK"
        });

        // 기존 캐릭터가 없으면 빈 목록
        var charList = BuildCharListPacket(session);
        return new() { result, charList };
    }

    private List<ResponsePacket> HandleCharCreate(ParsedPacket req, MockSession session)
    {
        var name = GetString(req, "name");
        var charType = GetByte(req, "charType");
        var uid = session.AllocCharUid();

        session.Characters.Add(new CharListEntry
        {
            CharUid = uid, Name = name, CharType = charType, Level = 1
        });

        var result = BuildPacket("SC_CHAR_CREATE_RESULT", new()
        {
            ["success"] = 1, ["charUid"] = uid, ["message"] = "Created"
        });
        var charList = BuildCharListPacket(session);
        return new() { result, charList };
    }

    private List<ResponsePacket> HandleCharSelect(ParsedPacket req, MockSession session)
    {
        var charUid = GetUlong(req, "charUid");
        var ch = session.Characters.FirstOrDefault(c => c.CharUid == charUid);

        session.CharUid = charUid;
        session.CharName = ch?.Name ?? "MockChar";
        session.Level = ch?.Level ?? 1;
        session.InGame = true;
        session.InitPosition(_fieldRanges);
        session.SpawnInitialNpcs(_fieldRanges);

        var responses = new List<ResponsePacket>();

        // SC_CHAR_INFO
        responses.Add(BuildCharInfoPacket(session));

        // SC_NPC_SPAWN × N
        foreach (var npc in session.Npcs.Values)
            responses.Add(BuildNpcSpawnPacket(npc));

        // SC_ATTENDANCE_INFO
        responses.Add(BuildPacket("SC_ATTENDANCE_INFO", new()
        {
            ["todayAttended"] = 0, ["rewardGold"] = 100
        }));

        // SC_INVENTORY_LIST (빈 인벤토리)
        responses.Add(BuildInventoryListPacket(session));

        return responses;
    }

    private List<ResponsePacket> HandleMove(ParsedPacket req, MockSession session)
    {
        var dirX = GetSbyte(req, "dirX");
        var dirY = GetSbyte(req, "dirY");
        var posRange = _fieldRanges != null && _fieldRanges.TryGetValue("SC_CHAR_INFO.posX", out var pr)
            ? pr : new FieldRange { Min = 0, Max = 19 };
        session.PosX = (short)Math.Clamp(session.PosX + dirX, posRange.Min, posRange.Max);
        session.PosY = (short)Math.Clamp(session.PosY + dirY, posRange.Min, posRange.Max);

        return new()
        {
            BuildPacket("SC_MOVE_RESULT", new()
            {
                ["success"] = 1,
                ["posX"] = (int)session.PosX,
                ["posY"] = (int)session.PosY
            })
        };
    }

    private List<ResponsePacket> HandleAttack(ParsedPacket req, MockSession session)
    {
        var targetUid = GetUlong(req, "targetUid");
        var responses = new List<ResponsePacket>();

        if (!session.Npcs.TryGetValue(targetUid, out var npc))
        {
            responses.Add(BuildPacket("SC_ATTACK_RESULT", new()
            {
                ["success"] = 0, ["targetUid"] = targetUid, ["damage"] = 0, ["targetHp"] = 0
            }));
            return responses;
        }

        ushort damage = 10;
        npc.Hp = (ushort)Math.Max(0, npc.Hp - damage);

        responses.Add(BuildPacket("SC_ATTACK_RESULT", new()
        {
            ["success"] = 1, ["targetUid"] = targetUid,
            ["damage"] = (int)damage, ["targetHp"] = (int)npc.Hp
        }));

        if (npc.Hp == 0)
        {
            // NPC 사망 → 리스폰
            responses.Add(BuildPacket("SC_NPC_DEATH", new()
            {
                ["npcUid"] = targetUid, ["expReward"] = 50, ["goldReward"] = 10
            }));

            session.Exp += 50;
            session.Gold += 10;

            // 아이템 드롭 (50% 확률)
            if (Random.Shared.Next(2) == 0)
            {
                var slot = (byte)session.Inventory.Count;
                var itemId = (ushort)Random.Shared.Next(1, 20);
                session.Inventory.Add(new InventorySlot
                {
                    Slot = slot, ItemId = itemId, ItemName = $"Item_{itemId}"
                });
                responses.Add(BuildPacket("SC_ITEM_DROP", new()
                {
                    ["slot"] = (int)slot, ["itemId"] = (int)itemId, ["itemName"] = $"Item_{itemId}"
                }));
            }

            // 리스폰
            session.Npcs.Remove(targetUid);
            var newUid = session.AllocNpcUid();
            var rng = Random.Shared;
            var posRange = _fieldRanges != null && _fieldRanges.TryGetValue("SC_NPC_SPAWN.posX", out var pr)
                ? pr : new FieldRange { Min = 0, Max = 19 };
            var hpRange = _fieldRanges != null && _fieldRanges.TryGetValue("SC_NPC_SPAWN.hp", out var hr)
                ? hr : new FieldRange { Min = 30, Max = 30 };
            var newNpc = new NpcState
            {
                Uid = newUid,
                PosX = (short)rng.Next((int)posRange.Min, (int)posRange.Max + 1),
                PosY = (short)rng.Next((int)posRange.Min, (int)posRange.Max + 1),
                Hp = (ushort)rng.Next((int)hpRange.Min, (int)hpRange.Max + 1),
                MaxHp = (ushort)hpRange.Max,
                NpcType = npc.NpcType
            };
            session.Npcs[newUid] = newNpc;
            responses.Add(BuildNpcSpawnPacket(newNpc));
        }

        return responses;
    }

    private List<ResponsePacket> HandleShopOpen(MockSession session)
    {
        // 고정 상점 목록
        var items = new List<object>();
        for (int i = 1; i <= 5; i++)
            items.Add(new Dictionary<string, object> { ["itemId"] = i, ["itemName"] = $"ShopItem_{i}", ["price"] = (uint)(i * 100) });

        return new()
        {
            BuildPacket("SC_SHOP_LIST", new()
            {
                ["count"] = items.Count,
                ["items"] = items
            })
        };
    }

    private List<ResponsePacket> HandleShopBuy(ParsedPacket req, MockSession session)
    {
        var itemId = GetUshort(req, "itemId");
        var price = (ulong)(itemId * 100);
        var success = session.Gold >= price;

        if (success)
        {
            session.Gold -= price;
            var slot = (byte)session.Inventory.Count;
            session.Inventory.Add(new InventorySlot
            {
                Slot = slot, ItemId = itemId, ItemName = $"ShopItem_{itemId}"
            });
        }

        return new()
        {
            BuildPacket("SC_SHOP_RESULT", new()
            {
                ["success"] = success ? 1 : 0,
                ["itemId"] = (int)itemId,
                ["remainGold"] = session.Gold,
                ["message"] = success ? "Purchased" : "Not enough gold"
            })
        };
    }

    private List<ResponsePacket> HandleShopSell(ParsedPacket req, MockSession session)
    {
        var slot = GetByte(req, "slot");
        var item = session.Inventory.FirstOrDefault(i => i.Slot == slot);
        var responses = new List<ResponsePacket>();

        if (item != null)
        {
            session.Inventory.Remove(item);
            session.Gold += 50;
            responses.Add(BuildPacket("SC_INVENTORY_UPDATE", new()
            {
                ["slot"] = (int)slot, ["itemId"] = 0, ["itemName"] = ""
            }));
        }

        responses.Add(BuildPacket("SC_SHOP_RESULT", new()
        {
            ["success"] = item != null ? 1 : 0,
            ["itemId"] = item != null ? (int)item.ItemId : 0,
            ["remainGold"] = session.Gold,
            ["message"] = item != null ? "Sold" : "Empty slot"
        }));

        return responses;
    }

    private List<ResponsePacket> HandleItemUse(ParsedPacket req, MockSession session)
    {
        var slot = GetByte(req, "slot");
        var item = session.Inventory.FirstOrDefault(i => i.Slot == slot);
        var responses = new List<ResponsePacket>();

        if (item != null)
        {
            session.Hp = (ushort)Math.Min(session.Hp + 20, session.MaxHp);
            session.Inventory.Remove(item);

            responses.Add(BuildPacket("SC_ITEM_USE_RESULT", new()
            {
                ["charUid"] = session.CharUid, ["charName"] = session.CharName,
                ["itemName"] = item.ItemName, ["effectType"] = 1, ["effectValue"] = 20
            }));
            responses.Add(BuildPacket("SC_INVENTORY_UPDATE", new()
            {
                ["slot"] = (int)slot, ["itemId"] = 0, ["itemName"] = ""
            }));
            responses.Add(BuildCharInfoPacket(session));
        }
        else
        {
            responses.Add(BuildPacket("SC_ITEM_USE_RESULT", new()
            {
                ["charUid"] = session.CharUid, ["charName"] = session.CharName,
                ["itemName"] = "", ["effectType"] = 0, ["effectValue"] = 0
            }));
        }

        return responses;
    }

    private List<ResponsePacket> HandleItemEquip(ParsedPacket req, MockSession session)
    {
        var slot = GetByte(req, "slot");
        var item = session.Inventory.FirstOrDefault(i => i.Slot == slot);

        var responses = new List<ResponsePacket>();
        responses.Add(BuildPacket("SC_EQUIP_RESULT", new()
        {
            ["success"] = item != null ? 1 : 0,
            ["slot"] = (int)slot, ["atk"] = 10, ["def"] = 5,
            ["weaponName"] = item?.ItemName ?? "", ["armorName"] = "", ["message"] = "OK"
        }));

        if (item != null)
        {
            responses.Add(BuildPacket("SC_INVENTORY_UPDATE", new()
            {
                ["slot"] = (int)slot, ["itemId"] = (int)item.ItemId, ["itemName"] = item.ItemName
            }));
            responses.Add(BuildCharInfoPacket(session));
        }

        return responses;
    }

    // ── 패킷 빌드 헬퍼 ──

    private ResponsePacket BuildPacket(string name, Dictionary<string, object> fields)
    {
        var data = _builder.Build(name, fields);
        return new ResponsePacket(name, data);
    }

    private ResponsePacket BuildCharInfoPacket(MockSession s) =>
        BuildPacket("SC_CHAR_INFO", new()
        {
            ["charUid"] = s.CharUid, ["name"] = s.CharName,
            ["level"] = (int)s.Level, ["exp"] = s.Exp,
            ["posX"] = (int)s.PosX, ["posY"] = (int)s.PosY,
            ["hp"] = (int)s.Hp, ["maxHp"] = (int)s.MaxHp, ["gold"] = s.Gold
        });

    private ResponsePacket BuildNpcSpawnPacket(NpcState npc) =>
        BuildPacket("SC_NPC_SPAWN", new()
        {
            ["npcUid"] = npc.Uid,
            ["posX"] = (int)npc.PosX, ["posY"] = (int)npc.PosY,
            ["hp"] = (int)npc.Hp, ["maxHp"] = (int)npc.MaxHp,
            ["npcType"] = (int)npc.NpcType
        });

    private ResponsePacket BuildCharListPacket(MockSession s)
    {
        var entries = s.Characters.Select(c => (object)new Dictionary<string, object>
        {
            ["charUid"] = c.CharUid, ["name"] = c.Name,
            ["charType"] = (int)c.CharType, ["level"] = (int)c.Level
        }).ToList();

        return BuildPacket("SC_CHAR_LIST", new()
        {
            ["count"] = entries.Count,
            ["chars"] = entries
        });
    }

    private ResponsePacket BuildInventoryListPacket(MockSession s)
    {
        var entries = s.Inventory.Select(i => (object)new Dictionary<string, object>
        {
            ["slot"] = (int)i.Slot, ["itemId"] = (int)i.ItemId, ["itemName"] = i.ItemName
        }).ToList();

        return BuildPacket("SC_INVENTORY_LIST", new()
        {
            ["count"] = entries.Count,
            ["items"] = entries
        });
    }

    private List<ResponsePacket> BuildFromRule(MockRule rule, MockSession session)
    {
        var responses = new List<ResponsePacket>();
        foreach (var resp in rule.Responses)
        {
            var fields = new Dictionary<string, object>();
            if (resp.Fields != null)
                foreach (var (k, v) in resp.Fields)
                    fields[k] = JsonElementToObject(v);
            responses.Add(BuildPacket(resp.Packet, fields));
        }
        return responses;
    }

    // ── 필드 추출 헬퍼 ──

    private static string GetString(ParsedPacket p, string field) =>
        p.Fields.TryGetValue(field, out var v) ? v?.ToString() ?? "" : "";

    private static byte GetByte(ParsedPacket p, string field) =>
        p.Fields.TryGetValue(field, out var v) ? Convert.ToByte(v) : (byte)0;

    private static sbyte GetSbyte(ParsedPacket p, string field) =>
        p.Fields.TryGetValue(field, out var v) ? Convert.ToSByte(v) : (sbyte)0;

    private static ushort GetUshort(ParsedPacket p, string field) =>
        p.Fields.TryGetValue(field, out var v) ? Convert.ToUInt16(v) : (ushort)0;

    private static ulong GetUlong(ParsedPacket p, string field) =>
        p.Fields.TryGetValue(field, out var v) ? Convert.ToUInt64(v) : 0UL;

    private static object JsonElementToObject(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number when e.TryGetInt64(out var l) => l,
        JsonValueKind.Number => e.GetDouble(),
        JsonValueKind.String => e.GetString() ?? "",
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => e.GetRawText()
    };
}
